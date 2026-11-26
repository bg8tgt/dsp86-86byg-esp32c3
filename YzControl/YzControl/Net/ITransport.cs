using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YzControl.Net;

/// <summary>
/// v1.6.0 传输抽象（参照 OrbitLink 的 BaseTransport / TcpTransport 设计）。
/// 上层只认「能力」：Connect / Request / Close + 状态事件；socket 与 HTTP 调用全部在后台线程完成，
/// UI 线程只通过 <see cref="StateChanged" /> 收事件（参照调用方自行 BeginInvoke）。
/// </summary>
public interface ITransport : IDisposable
{
	/// <summary>当前链路状态（Disconnected/Connecting/Ready/Backoff/Failed）。</summary>
	LinkState State { get; }

	/// <summary>给人看的传输名，例如「直连 TCP 172.16.1.123:4001」。</summary>
	string Name { get; }

	/// <summary>状态变化事件（后台线程抛出，UI 自行 marshal）。</summary>
	event Action<LinkState> StateChanged;

	Task ConnectAsync(CancellationToken ct);

	Task CloseAsync();

	/// <summary>一问一答：发 req，等应答；失败抛 <see cref="YzLinkException" />。</summary>
	Task<byte[]> RequestAsync(byte[] req, int timeoutMs, CancellationToken ct);
}

/// <summary>一次 HTTP GET 的结果（不用异常传递失败，方便 S3Client 保留原来「返回 null + LastError」的 API）。</summary>
public sealed class HttpResult
{
	public string Body = "";

	public int StatusCode;

	public bool Ok;

	public int ElapsedMs;

	public YzLinkError Error = YzLinkError.None();

	public bool IsTimeout
	{
		get
		{
			return Error != null && Error.Kind == YzLinkErrorKind.Timeout;
		}
	}
}

/// <summary>
/// 一次 GET 的策略：快档超时 / 上限 / 是否走 TLS / 是否允许「快档超时后按上限重试一次」/ 是否计入链路健康。
/// 变成结构体是为了避免六个 bool 参数的调用点可读性掉到零。
/// </summary>
public struct HttpGetOptions
{
	/// <summary>快档超时（命令 300ms / 状态轮询 500ms）。</summary>
	public int FastMs;

	/// <summary>上限（HttpTimeoutMs；实测 RTT 大时会自适应放大到这个值以内）。</summary>
	public int CeilingMs;

	/// <summary>true = https。</summary>
	public bool Tls;

	/// <summary>只对**纯读**接口开：快档超时且链路本来 Ready 时按上限重试一次（避免对 /wifi、/cmd 这类改状态的接口重复下发）。</summary>
	public bool AllowEscalate;

	/// <summary>是否计入链路健康（1s /st 心跳算；经板子读电台 /rst2 不算，它有自己的通道灯）。</summary>
	public bool TrackHealth;

	public static HttpGetOptions Make(int fastMs, int ceilingMs, bool tls, bool allowEscalate, bool trackHealth)
	{
		HttpGetOptions o = default(HttpGetOptions);
		o.FastMs = fastMs;
		o.CeilingMs = ceilingMs;
		o.Tls = tls;
		o.AllowEscalate = allowEscalate;
		o.TrackHealth = trackHealth;
		return o;
	}
}

/// <summary>
/// 云台 HTTP 传输（S3Client 内部用）。
/// - 每次请求按「快档超时（命令 300ms / 状态 500ms）+ 上限（HttpTimeoutMs）」执行；
/// - 实测 RTT 大时自动放宽到 RTT×3（慢设备不会因为快档被误判），但不超过上限；
/// - 快档超时且链路本来 Ready，自动按上限重试一次（设备慢而不死，不误报）；
/// - 连续 3 次失败 → Failed + 指数退避，退避窗口内**快速失败**不发 IO。
/// </summary>
public sealed class HttpTransport : ITransport
{
	private static bool _tlsReady;

	private readonly object _epLock = new object();

	private readonly LinkStateMachine _status = new LinkStateMachine("http");

	private readonly ReconnectPolicy _policy = new ReconnectPolicy();

	private string _host = "";

	private string _epKey;

	private string _epValue;

	private int _rttMs;

	/// <summary>最近一段时间的 RTT 峰值（衰减保留）：设备 HTTP 是单线程的，页面同时发 3~4 个请求时会排队，
	/// 只看「上一次 RTT」会把排队时间误判成死链。用峰值×3 做自适应下限。</summary>
	private int _rttPeakMs;

	private int _requests;

	private int _failures;

	private YzLinkError _lastError = YzLinkError.None();

	static HttpTransport()
	{
		EnsureTls();
	}

	public HttpTransport()
	{
	}

	public HttpTransport(string host)
	{
		_host = (host ?? "");
	}

	/// <summary>目标（可以是 IP、主机名，或带 :port 的形式）。</summary>
	public string Host
	{
		get
		{
			return _host;
		}
		set
		{
			string next = (value ?? "");
			if (!string.Equals(_host, next, StringComparison.Ordinal))
			{
				_host = next;
				lock (_epLock)
				{
					_epKey = null;
					_epValue = null;
				}
				ResetLink("端点已更换为 " + next);
			}
		}
	}

	public LinkState State
	{
		get
		{
			return _status.State;
		}
	}

	public string Name
	{
		get
		{
			return "http://" + _host;
		}
	}

	public string Reason
	{
		get
		{
			return _status.Reason;
		}
	}

	public int ConsecutiveFailures
	{
		get
		{
			return _status.ConsecutiveFailures;
		}
	}

	public int RttMs
	{
		get
		{
			return _rttMs;
		}
	}

	public long Requests
	{
		get
		{
			return _requests;
		}
	}

	public long Failures
	{
		get
		{
			return _failures;
		}
	}

	public YzLinkError LastError
	{
		get
		{
			return _lastError;
		}
	}

	public bool InBackoff
	{
		get
		{
			return _policy.Waiting;
		}
	}

	public string BackoffText
	{
		get
		{
			return _policy.WaitingText();
		}
	}

	public event Action<LinkState> StateChanged
	{
		add
		{
			_status.StateChanged += value;
		}
		remove
		{
			_status.StateChanged -= value;
		}
	}

	/// <summary>TLS 一次性初始化（自签证书也放行：设备就是本地盒子）。</summary>
	public static void EnsureTls()
	{
		if (_tlsReady)
		{
			return;
		}
		_tlsReady = true;
		try
		{
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
		}
		catch
		{
		}
		try
		{
			ServicePointManager.ServerCertificateValidationCallback = delegate(object sender, X509Certificate cert, X509Chain chain, System.Net.Security.SslPolicyErrors errors)
			{
				return true;
			};
		}
		catch
		{
		}
	}

	/// <summary>清退避 + 失败计数（端点变更 / OTA 后 / 手动重试用）。</summary>
	public void ResetLink(string reason)
	{
		_policy.Reset();
		_status.ResetFailures();
		_status.Set(LinkState.Disconnected, reason ?? "已复位");
	}

	/// <summary>
	/// 同步 GET（现有 S3Client 的调用点全是同步的；这里是唯一真正发 IO 的地方）。
	/// <paramref name="fastMs" /> 是这一类请求的快档超时，<paramref name="ceilingMs" /> 是上限。
	/// </summary>
	public HttpResult Get(string path, HttpGetOptions opt)
	{
		int fastMs = opt.FastMs;
		int ceilingMs = opt.CeilingMs;
		if (fastMs < 50)
		{
			fastMs = 50;
		}
		if (ceilingMs < fastMs)
		{
			ceilingMs = fastMs;
		}
		Interlocked.Increment(ref _requests);
		// 退避窗口内：快速失败，不发任何 IO（不排队、不堆积）
		if (_policy.Waiting)
		{
			YzLinkError gate = YzLinkError.Of(YzLinkErrorKind.Unreachable, _policy.WaitingText(), _status.Reason, 0);
			_lastError = gate;
			return new HttpResult
			{
				Ok = false,
				Error = gate
			};
		}
		bool wasReady = LinkStateText.IsUp(_status.State);
		if (!wasReady && opt.TrackHealth)
		{
			_status.Set(LinkState.Connecting, "GET " + path);
		}
		int timeout = EffectiveTimeout(fastMs, ceilingMs);
		HttpResult res = DoGet(path, timeout, opt.Tls);
		// 快档超时 ⇒ 可能只是设备被并发请求排队（固件 HTTP 单线程），按上限再试一次。
		// 只对**纯读**接口这么做：/cmd /set /wifi 这类改状态的重复下发有风险，宁可报「可能已执行」也不重发。
		if (!res.Ok && res.Error != null && res.Error.Kind == YzLinkErrorKind.Timeout && timeout < ceilingMs && opt.AllowEscalate)
		{
			res = DoGet(path, ceilingMs, opt.Tls);
		}
		else if (!res.Ok && res.Error != null && res.Error.Kind == YzLinkErrorKind.Timeout && !opt.AllowEscalate)
		{
			// 命令类：不重发，但要把「可能已经执行」讲清楚，避免用户以为没生效又点一次
			res.Error = YzLinkError.Of(YzLinkErrorKind.Timeout, res.Error.Text + "（命令可能已被设备执行，请先看状态再决定是否重发）", res.Error.Detail, 0);
		}
		UpdateHealth(res, path, opt.TrackHealth);
		return res;
	}

	private int EffectiveTimeout(int fastMs, int ceilingMs)
	{
		int rtt = Volatile.Read(ref _rttMs);
		int peak = Volatile.Read(ref _rttPeakMs);
		int reference = ((peak > rtt) ? peak : rtt);
		if (reference <= 0)
		{
			return fastMs;
		}
		int adaptive = reference * 3;
		if (adaptive < fastMs)
		{
			adaptive = fastMs;
		}
		return Math.Min(adaptive, ceilingMs);
	}

	private void UpdateHealth(HttpResult res, string path, bool trackHealth)
	{
		if (!trackHealth)
		{
			// 不计健康的请求（经板子读电台 /rst2）：成功与否都不改链路状态与退避，
			// 否则「电台不通但板子在」会让顶栏 S3 状态每秒 Failed↔Ready 抖一次。
			if (res.Ok)
			{
				_lastError = YzLinkError.None();
			}
			else
			{
				_lastError = (res.Error ?? YzLinkError.None());
				Interlocked.Increment(ref _failures);
			}
			return;
		}
		if (res.Ok)
		{
			Volatile.Write(ref _rttMs, res.ElapsedMs);
			// 峰值缓慢衰减（×7/8）：突发排队把预算顶上去，安静下来几秒后自动回落到快档
			Volatile.Write(ref _rttPeakMs, Math.Max(res.ElapsedMs, (Volatile.Read(ref _rttPeakMs) * 7) / 8));
			_policy.Reset();
			_lastError = YzLinkError.None();
			_status.NotifySuccess("GET " + path + " " + res.ElapsedMs + "ms");
			return;
		}
		_lastError = (res.Error ?? YzLinkError.None());
		Interlocked.Increment(ref _failures);
		YzLinkError e = res.Error;
		if (e != null && (e.Kind == YzLinkErrorKind.Protocol || e.Kind == YzLinkErrorKind.Busy))
		{
			// 设备答了（只是答得不对 / 忙）⇒ 链路是通的，不算链路故障
			Volatile.Write(ref _rttMs, res.ElapsedMs);
			_policy.Reset();
			_status.NotifySuccess("GET " + path + " → " + e.Text);
			return;
		}
		_policy.OnFailure();
		_status.NotifyFailure(LinkState.Backoff, e == null ? ("GET " + path + " 失败") : e.Text);
	}

	/// <summary>Host（可能是主机名）→ 实际连接的端点，带缓存（DNS 不重复解析）。</summary>
	public string ResolveEndpoint()
	{
		string hostText = _host ?? "";
		string key;
		string portPart = "";
		int colon = hostText.IndexOf(':');
		if (colon > 0)
		{
			key = hostText.Substring(0, colon);
			portPart = hostText.Substring(colon);
		}
		else
		{
			key = hostText;
		}
		if (IPAddress.TryParse(key, out IPAddress _))
		{
			return hostText;
		}
		lock (_epLock)
		{
			if (_epKey == key && _epValue != null)
			{
				return _epValue;
			}
			string ip = null;
			try
			{
				IPAddress[] list = Dns.GetHostAddresses(key);
				for (int i = 0; i < list.Length; i++)
				{
					if (list[i].AddressFamily == AddressFamily.InterNetwork)
					{
						ip = list[i].ToString();
						break;
					}
				}
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(ip))
			{
				return hostText;
			}
			string resolved = ip + portPart;
			_epKey = key;
			_epValue = resolved;
			return resolved;
		}
	}

	private HttpResult DoGet(string path, int timeoutMs, bool tls)
	{
		HttpResult res = new HttpResult();
		Stopwatch sw = Stopwatch.StartNew();
		HttpWebRequest req = null;
		try
		{
			string url = (tls ? "https://" : "http://") + ResolveEndpoint() + path;
			req = (HttpWebRequest)WebRequest.Create(url);
			req.Method = "GET";
			req.Timeout = timeoutMs;
			req.ReadWriteTimeout = timeoutMs;
			req.KeepAlive = false;
			req.Proxy = null;
			using (HttpWebResponse rsp = (HttpWebResponse)req.GetResponse())
			{
				res.StatusCode = (int)rsp.StatusCode;
				res.Body = ReadBody(rsp);
				res.Ok = res.StatusCode >= 200 && res.StatusCode < 300;
				res.Error = (res.Ok ? YzLinkError.None() : YzLinkError.FromHttpStatus(res.StatusCode, res.Body, ""));
			}
		}
		catch (WebException we)
		{
			HttpWebResponse errRsp = we.Response as HttpWebResponse;
			if (errRsp != null)
			{
				try
				{
					res.StatusCode = (int)errRsp.StatusCode;
					res.Body = ReadBody(errRsp);
				}
				catch
				{
				}
				res.Ok = false;
				res.Error = YzLinkError.FromHttpStatus(res.StatusCode, res.Body, YzLinkError.Describe(we));
			}
			else
			{
				res.Ok = false;
				res.Error = YzLinkError.Classify(we, Name, timeoutMs);
			}
		}
		catch (Exception ex)
		{
			res.Ok = false;
			res.Error = YzLinkError.Classify(ex, Name, timeoutMs);
		}
		finally
		{
			if (req != null)
			{
				try
				{
					req.Abort();
				}
				catch
				{
				}
			}
			sw.Stop();
		}
		res.ElapsedMs = (int)sw.ElapsedMilliseconds;
		return res;
	}

	private static string ReadBody(HttpWebResponse rsp)
	{
		if (rsp == null)
		{
			return "";
		}
		using (System.IO.StreamReader sr = new System.IO.StreamReader(rsp.GetResponseStream(), Encoding.UTF8))
		{
			return sr.ReadToEnd();
		}
	}

	/// <summary>HTTP 是无连接的，ConnectAsync 就是「探活一次」（/st）。</summary>
	public Task ConnectAsync(CancellationToken ct)
	{
		return Task.Run(delegate
		{
			ct.ThrowIfCancellationRequested();
			HttpResult r = Get("/st", HttpGetOptions.Make(500, 1500, tls: false, allowEscalate: true, trackHealth: true));
			if (!r.Ok)
			{
				throw new YzLinkException(r.Error);
			}
		}, ct);
	}

	public Task CloseAsync()
	{
		_policy.Reset();
		_status.Set(LinkState.Disconnected, "已关闭");
		return Task.CompletedTask;
	}

	public Task<byte[]> RequestAsync(byte[] req, int timeoutMs, CancellationToken ct)
	{
		string path = ((req == null) ? "" : Encoding.UTF8.GetString(req));
		return Task.Run(delegate
		{
			ct.ThrowIfCancellationRequested();
			HttpResult r = Get(path, HttpGetOptions.Make(timeoutMs, timeoutMs, tls: false, allowEscalate: false, trackHealth: true));
			if (!r.Ok)
			{
				throw new YzLinkException(r.Error);
			}
			return Encoding.UTF8.GetBytes(r.Body ?? "");
		}, ct);
	}

	public void Dispose()
	{
		ResetLink("释放");
	}
}

/// <summary>
/// 电台 CI-V over TCP 的传输（172.16.1.123:4001 这类 ZQWL 转接盒）。
/// 只负责「连接 / 原始收发 / 断线判定」；一问一答与半双工串行化在 <see cref="SendQueue" /> 里，
/// 这样 CI-V 的收帧循环（含主动上报帧）保持原样不动。
/// </summary>
public sealed class TcpTransport : ITransport
{
	private readonly object _connLock = new object();

	private readonly object _sendLock = new object();

	private readonly LinkStateMachine _status = new LinkStateMachine("tcp");

	private readonly ReconnectPolicy _policy = new ReconnectPolicy();

	private Socket _sock;

	private NetworkStream _ns;

	private string _host = "";

	private int _port;

	private bool _peerClosed;

	private volatile bool _closing;

	private YzLinkError _lastError = YzLinkError.None();

	public TcpTransport()
	{
	}

	public TcpTransport(string host, int port)
	{
		_host = (host ?? "");
		_port = port;
	}

	public string Host
	{
		get
		{
			return _host;
		}
	}

	public int Port
	{
		get
		{
			return _port;
		}
	}

	public int ConnectTimeoutMs = 2500;

	public LinkState State
	{
		get
		{
			return _status.State;
		}
	}

	public string Name
	{
		get
		{
			return "直连 TCP " + _host + ":" + _port;
		}
	}

	public string Reason
	{
		get
		{
			return _status.Reason;
		}
	}

	public YzLinkError LastError
	{
		get
		{
			return _lastError;
		}
	}

	public bool PeerClosed
	{
		get
		{
			return _peerClosed;
		}
	}

	/// <summary>底层流（CivLink 的收帧线程直接读它，保持原有阻塞读语义）。</summary>
	public NetworkStream RawStream
	{
		get
		{
			return _ns;
		}
	}

	public bool IsLinked
	{
		get
		{
			if (_ns == null || _sock == null || _peerClosed || _closing)
			{
				return false;
			}
			try
			{
				return _sock.Connected;
			}
			catch
			{
				return false;
			}
		}
	}

	public bool InBackoff
	{
		get
		{
			return _policy.Waiting;
		}
	}

	public string BackoffText
	{
		get
		{
			return _policy.WaitingText();
		}
	}

	public event Action<LinkState> StateChanged
	{
		add
		{
			_status.StateChanged += value;
		}
		remove
		{
			_status.StateChanged -= value;
		}
	}

	/// <summary>
	/// 同步连接（BeginConnect + WaitOne 超时，等价原 CivLink.ConnectTcp 的做法）。
	/// 失败时会记一次退避。
	/// </summary>
	public bool Connect(string host, int port, int timeoutMs, out YzLinkError err)
	{
		err = YzLinkError.None();
		if (timeoutMs <= 0)
		{
			timeoutMs = ConnectTimeoutMs;
		}
		Close(false, "重连");
		if (string.IsNullOrEmpty(host))
		{
			err = YzLinkError.Of(YzLinkErrorKind.Unreachable, "目标 IP 为空", "", 0);
			_lastError = err;
			_status.NotifyFailure(LinkState.Backoff, err.Text);
			return false;
		}
		if (port <= 0 || port > 65535)
		{
			err = YzLinkError.Of(YzLinkErrorKind.Refused, "端口非法：" + port, "", 0);
			_lastError = err;
			_status.NotifyFailure(LinkState.Backoff, err.Text);
			return false;
		}
		_host = host;
		_port = port;
		_closing = false;
		_status.Set(LinkState.Connecting, "连接 " + host + ":" + port);
		string target = host + ":" + port;
		Socket sock = null;
		try
		{
			sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			sock.NoDelay = true;
			IAsyncResult ar = sock.BeginConnect(host, port, null, null);
			if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
			{
				try
				{
					ar.AsyncWaitHandle.Close();
				}
				catch
				{
				}
				try
				{
					sock.Close();
				}
				catch
				{
				}
				err = YzLinkError.Of(YzLinkErrorKind.Timeout, "超时（" + target + " " + timeoutMs + "ms 内没连上：设备未开机 / 端口未监听 / 不在同网段）", "", 0);
				_lastError = err;
				_policy.OnFailure();
				_status.NotifyFailure(LinkState.Backoff, err.Text);
				return false;
			}
			try
			{
				ar.AsyncWaitHandle.Close();
			}
			catch
			{
			}
			sock.EndConnect(ar);
			NetworkStream ns = new NetworkStream(sock, ownsSocket: false);
			lock (_connLock)
			{
				_sock = sock;
				_ns = ns;
			}
			_peerClosed = false;
			_policy.Reset();
			_lastError = YzLinkError.None();
			_status.NotifySuccess("已连接 " + target);
			return true;
		}
		catch (Exception ex)
		{
			try
			{
				sock?.Close();
			}
			catch
			{
			}
			err = YzLinkError.Classify(ex, target, timeoutMs);
			_lastError = err;
			_policy.OnFailure();
			_status.NotifyFailure(LinkState.Backoff, err.TextWithDetail());
			return false;
		}
	}

	/// <summary>写一帧（半双工：同一时刻只有一条在写）。</summary>
	public bool WriteRaw(byte[] data, out YzLinkError err)
	{
		err = YzLinkError.None();
		if (data == null || data.Length == 0)
		{
			err = YzLinkError.Of(YzLinkErrorKind.Protocol, "内部错误：空帧", "", 0);
			return false;
		}
		lock (_sendLock)
		{
			NetworkStream ns = _ns;
			if (ns == null)
			{
				err = YzLinkError.Of(YzLinkErrorKind.Refused, "直连链路未建立", "", 0);
				return false;
			}
			try
			{
				ns.Write(data, 0, data.Length);
				ns.Flush();
				return true;
			}
			catch (Exception ex)
			{
				err = YzLinkError.Classify(ex, Name, 0);
				_lastError = err;
				_peerClosed = true;
				_policy.OnFailure();
				_status.NotifyFailure(LinkState.Failed, err.TextWithDetail());
				return false;
			}
		}
	}

	/// <summary>
	/// 阻塞读（保持原 CivLink 的行为：无限 timeout，靠 Close 打断）。
	/// 读到 0 字节 = 对端关闭；其它异常原样抛给收帧循环（它有自己的连续 IO 错误计数）。
	/// </summary>
	public int ReadRaw(byte[] buf, int offset, int count)
	{
		NetworkStream ns = _ns;
		if (ns == null)
		{
			return 0;
		}
		int n = ns.Read(buf, offset, count);
		if (n <= 0)
		{
			MarkPeerClosed("对端关闭（读到 0 字节）");
		}
		return n;
	}

	/// <summary>对端关闭 / IO 连续失败时由收帧循环调用。</summary>
	public void MarkPeerClosed(string reason)
	{
		if (_peerClosed)
		{
			return;
		}
		_peerClosed = true;
		YzLinkError e = YzLinkError.Of(YzLinkErrorKind.Refused, reason ?? "对端关闭", _status.Reason, 0);
		_lastError = e;
		_policy.OnFailure();
		_status.NotifyFailure(LinkState.Failed, e.Text);
	}

	/// <summary>退避窗口到点后重连（供上层在空闲时机调用）。</summary>
	public bool TryReconnect(int timeoutMs, out YzLinkError err)
	{
		err = YzLinkError.None();
		if (_policy.Waiting)
		{
			err = YzLinkError.Of(YzLinkErrorKind.Unreachable, _policy.WaitingText(), _status.Reason, 0);
			_lastError = err;
			return false;
		}
		return Connect(_host, _port, timeoutMs, out err);
	}

	/// <summary>关闭连接。resetPolicy=true 表示「这是主动断开」，退避也复位。</summary>
	public void Close(bool resetPolicy, string reason)
	{
		_closing = true;
		NetworkStream ns = _ns;
		Socket sock = _sock;
		lock (_connLock)
		{
			_ns = null;
			_sock = null;
		}
		try
		{
			ns?.Close();
		}
		catch
		{
		}
		try
		{
			sock?.Close();
		}
		catch
		{
		}
		if (resetPolicy)
		{
			_policy.Reset();
			_status.ResetFailures();
			_status.Set(LinkState.Disconnected, reason ?? "已断开");
		}
	}

	public Task ConnectAsync(CancellationToken ct)
	{
		return Task.Run(delegate
		{
			ct.ThrowIfCancellationRequested();
			if (!Connect(_host, _port, ConnectTimeoutMs, out YzLinkError err))
			{
				throw new YzLinkException(err);
			}
		}, ct);
	}

	public Task CloseAsync()
	{
		Close(true, "已关闭");
		return Task.CompletedTask;
	}

	/// <summary>一问一答：写 req，然后按行读到终止符（\r \n ; 任一）或超时。</summary>
	public Task<byte[]> RequestAsync(byte[] req, int timeoutMs, CancellationToken ct)
	{
		return Task.Run(delegate
		{
			ct.ThrowIfCancellationRequested();
			if (timeoutMs <= 0)
			{
				timeoutMs = 1500;
			}
			if (!WriteRaw(req, out YzLinkError werr))
			{
				throw new YzLinkException(werr);
			}
			NetworkStream ns = _ns;
			if (ns == null)
			{
				throw new YzLinkException(YzLinkError.Of(YzLinkErrorKind.Refused, "直连链路未建立", "", 0));
			}
			byte[] buf = new byte[1024];
			System.IO.MemoryStream ms = new System.IO.MemoryStream();
			Stopwatch sw = Stopwatch.StartNew();
			while (sw.ElapsedMilliseconds < timeoutMs && ms.Length < 8192)
			{
				ct.ThrowIfCancellationRequested();
				try
				{
					if (!ns.DataAvailable)
					{
						Thread.Sleep(15);
						continue;
					}
					int n = ns.Read(buf, 0, buf.Length);
					if (n <= 0)
					{
						break;
					}
					ms.Write(buf, 0, n);
					byte[] sofar = ms.ToArray();
					if (Array.IndexOf(sofar, (byte)10) >= 0 || Array.IndexOf(sofar, (byte)13) >= 0 || Array.IndexOf(sofar, (byte)59) >= 0)
					{
						break;
					}
				}
				catch (Exception ex)
				{
					throw new YzLinkException(YzLinkError.Classify(ex, Name, timeoutMs));
				}
			}
			byte[] data = ms.ToArray();
			if (data.Length == 0)
			{
				throw new YzLinkException(YzLinkError.Of(YzLinkErrorKind.Timeout, "超时（" + Name + " " + timeoutMs + "ms 内无应答）", "", 0));
			}
			return data;
		}, ct);
	}

	public void Dispose()
	{
		Close(true, "释放");
	}
}
