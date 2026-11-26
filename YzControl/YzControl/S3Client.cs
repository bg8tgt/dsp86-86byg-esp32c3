using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using YzControl.Net;

namespace YzControl;

/// <summary>
/// 云台 HTTP 客户端。
/// v1.6.0：内部换成 <see cref="HttpTransport" /> + <see cref="LinkStateMachine" /> + <see cref="ReconnectPolicy" />，
/// **对外方法名/字段一个没改**（152 个调用点原样可用），只把「内部实现 + 超时分级 + 错误分类」换掉：
/// - 云台命令（/cmd /goto /set …）快档 300ms，状态轮询（/st 心跳）快档 500ms，上限仍是 HttpTimeoutMs；
/// - 实测 RTT 大时自动放宽到 RTT×3（慢设备不误判），纯读接口还允许快档超时后按上限再试一次；
/// - 连续 3 次失败 → Failed + 指数退避（1s→30s），退避窗口内快速失败、不发 IO；
/// - LastError 变成「超时 / 连接被拒 / 不可达 / 协议错 / 设备忙」分类文案（界面文案自动跟着变好）。
/// </summary>
public sealed class S3Client
{
	public string Host = "172.16.1.122";

	public int HttpTimeoutMs = 1500;

	public int TcpTimeoutMs = 2000;

	public string LastError = "";

	public static Action<string, string> RequestLog;

	/// <summary>v1.6.0：云台命令快档超时（链路健康时按它快速失败）。</summary>
	public int CommandTimeoutMs = 300;

	/// <summary>v1.6.0：状态轮询（/st 心跳）快档超时。</summary>
	public int PollTimeoutMs = 500;

	private readonly HttpTransport _http = new HttpTransport();

	private TcpClient _tcp;

	private NetworkStream _stream;

	private readonly object _tcpLock = new object();

	private readonly List<byte> _rx = new List<byte>();

	private static bool _tlsReady;

	public string RigTransport = "HTTPS";

	private string _rigOkScheme = "";

	public int RigTimeoutMs = 1500;

	public int RigProbeTimeoutMs = 700;

	public int LastHttpCode;

	/// <summary>最近一次失败的错误分类（Timeout / Refused / Unreachable / Protocol / Busy）。</summary>
	public YzLinkErrorKind LastErrorKind { get; private set; }

	/// <summary>链路状态变化（后台线程抛出；MainForm 只在状态灯/提示处接它）。</summary>
	public event Action<LinkState> LinkStateChanged
	{
		add
		{
			_http.StateChanged += value;
		}
		remove
		{
			_http.StateChanged -= value;
		}
	}

	/// <summary>当前链路状态（UI 状态灯直接映射这个值）。</summary>
	public LinkState LinkState
	{
		get
		{
			return _http.State;
		}
	}

	/// <summary>状态原因（完整长句，放 tooltip）。</summary>
	public string LinkStateReason
	{
		get
		{
			return _http.Reason ?? "";
		}
	}

	/// <summary>是否在退避窗口内（此时命令会快速失败）。</summary>
	public bool LinkInBackoff
	{
		get
		{
			return _http.InBackoff;
		}
	}

	public int LinkFailures
	{
		get
		{
			return _http.ConsecutiveFailures;
		}
	}

	public int LinkRttMs
	{
		get
		{
			return _http.RttMs;
		}
	}

	/// <summary>手动重试：清退避（用户在地址栏点「连接」时用）。</summary>
	public void ResetLink()
	{
		_http.ResetLink("手动重试");
	}

	public bool TcpConnected
	{
		get
		{
			if (_tcp != null)
			{
				return _tcp.Connected;
			}
			return false;
		}
	}

	/// <summary>Host 变化时同步给传输层（传输层自己会清 DNS 缓存 + 复位退避）。</summary>
	private void SyncEndpoint()
	{
		_http.Host = Host ?? "";
	}

	private static void FireLog(string method, string path)
	{
		if (RequestLog == null)
		{
			return;
		}
		try
		{
			RequestLog(method, path);
		}
		catch
		{
		}
	}

	/// <summary>这一类请求的快档超时：/st 走轮询档，其余走命令档。</summary>
	private int FastTimeoutFor(string path)
	{
		if (!string.IsNullOrEmpty(path) && path.StartsWith("/st", StringComparison.Ordinal))
		{
			return ((PollTimeoutMs > 0) ? PollTimeoutMs : 500);
		}
		return ((CommandTimeoutMs > 0) ? CommandTimeoutMs : 300);
	}

	/// <summary>
	/// 只允许「纯读」接口在快档超时后按上限重试一次。
	/// /cmd、/goto、/set、/wifi、/update 这些会改设备状态，重复下发有风险，一律只发一次。
	/// </summary>
	private static bool IsIdempotentRead(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return false;
		}
		if (path == "/st" || path == "/sys" || path == "/otas" || path == "/eth" || path == "/az"
			|| path == "/prio" || path == "/tcal" || path == "/trackcli")
		{
			return true;
		}
		return path.StartsWith("/rst", StringComparison.Ordinal);
	}

	/// <summary>真正发一次 GET（唯一入口：记日志 → 同步端点 → 传输层 → 分类 LastError）。</summary>
	private HttpResult Request(string path, int fastMs, bool tls, bool allowEscalate, bool trackHealth)
	{
		LastError = "";
		LastErrorKind = YzLinkErrorKind.None;
		LastHttpCode = 0;
		FireLog("GET", path);
		SyncEndpoint();
		int ceiling = ((HttpTimeoutMs > fastMs) ? HttpTimeoutMs : fastMs);
		HttpResult r = _http.Get(path, HttpGetOptions.Make(fastMs, ceiling, tls, allowEscalate, trackHealth));
		LastHttpCode = r.StatusCode;
		if (!r.Ok)
		{
			YzLinkError e = r.Error ?? YzLinkError.None();
			LastErrorKind = e.Kind;
			string text = e.TextWithDetail();
			LastError = ((text.Length > 0) ? text : "请求失败");
		}
		return r;
	}

	public string Get(string path)
	{
		HttpResult r = Request(path, FastTimeoutFor(path), tls: false, allowEscalate: IsIdempotentRead(path), trackHealth: true);
		return (r.Ok ? r.Body : null);
	}

	public YzStatus GetStatus()
	{
		string text = Get("/st");
		if (text == null)
		{
			return null;
		}
		return YzStatus.Parse(text);
	}

	public string GetSys()
	{
		return Get("/sys");
	}

	private static void EnsureTls()
	{
		if (!_tlsReady)
		{
			_tlsReady = true;
		}
		HttpTransport.EnsureTls();
	}

	public string GetHttps(string path)
	{
		EnsureTls();
		HttpResult r = Request(path, FastTimeoutFor(path), tls: true, allowEscalate: IsIdempotentRead(path), trackHealth: true);
		return (r.Ok ? r.Body : null);
	}

	/// <summary>
	/// 经云台板转发到电台的 HTTP（/rf /rm /rptt /rst2 …）。
	/// 这里**不计链路健康**：电台不通不代表云台板不通，否则顶栏 S3 灯会每秒 Failed↔Ready 抖。
	/// </summary>
	public string GetRig(string path)
	{
		int fast = ((RigTimeoutMs > 0) ? RigTimeoutMs : 1500);
		if (_rigOkScheme == "HTTPS")
		{
			HttpResult https = Request(path, fast, tls: true, allowEscalate: false, trackHealth: false);
			if (https.Ok)
			{
				RigTransport = "HTTPS";
				return https.Body;
			}
			string lastError = LastError;
			HttpResult plain = Request(path, fast, tls: false, allowEscalate: false, trackHealth: false);
			if (plain.Ok)
			{
				_rigOkScheme = "HTTP";
				RigTransport = "HTTP";
				return plain.Body;
			}
			RigTransport = "不可用";
			LastError = lastError + " / " + LastError;
			return null;
		}
		HttpResult http = Request(path, fast, tls: false, allowEscalate: false, trackHealth: false);
		if (http.Ok)
		{
			_rigOkScheme = "HTTP";
			RigTransport = "HTTP";
			return http.Body;
		}
		string lastError2 = LastError;
		int probe = ((_rigOkScheme.Length == 0) ? RigProbeTimeoutMs : fast);
		HttpResult https2 = Request(path, probe, tls: true, allowEscalate: false, trackHealth: false);
		if (https2.Ok)
		{
			_rigOkScheme = "HTTPS";
			RigTransport = "HTTPS";
			return https2.Body;
		}
		RigTransport = "不可用";
		LastError = lastError2 + " / " + LastError;
		return null;
	}

	public bool ProbeHttps(out string info)
	{
		string https = GetHttps("/st");
		if (https == null)
		{
			info = "HTTPS 不可用：" + LastError;
			return false;
		}
		string arg = "";
		string arg2 = "";
		string[] array = https.Split(';');
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].StartsWith("ssl="))
			{
				arg = array[i].Substring(4);
			}
			if (array[i].StartsWith("ver="))
			{
				arg2 = array[i].Substring(4);
			}
		}
		info = $"HTTPS 可用 · https://{Host}/radio · 固件 {arg2} · ssl={arg}";
		return true;
	}

	private string GetChecked(string path)
	{
		return Get(path)?.Trim();
	}

	private bool GetOk(string path)
	{
		return GetChecked(path)?.StartsWith("OK", StringComparison.OrdinalIgnoreCase) ?? false;
	}

	public bool Cmd(string g)
	{
		return GetOk("/cmd?g=" + Uri.EscapeDataString(g));
	}

	public bool Goto(double az, double el)
	{
		string @checked = GetChecked("/goto?h=" + az.ToString("0.###", CultureInfo.InvariantCulture) + "&v=" + el.ToString("0.###", CultureInfo.InvariantCulture));
		if (@checked != null)
		{
			return @checked.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return false;
	}

	public bool SetSpeed(int msPerDeg)
	{
		return GetOk("/set?k=" + msPerDeg);
	}

	public bool SetSpeedV(int msPerDeg)
	{
		return GetOk("/set?k2=" + msPerDeg);
	}

	public string GetRaw(string path)
	{
		HttpResult r = Request(path, FastTimeoutFor(path), tls: false, allowEscalate: IsIdempotentRead(path), trackHealth: true);
		if (r.Ok)
		{
			return r.Body;
		}
		// 4xx/5xx 时固件把 ERR:xxx 写在 body 里，旧的 GetRaw 也是把 body 返回去的，这里保持一致
		string body = r.Body ?? "";
		return ((body.Length > 0) ? body : null);
	}

	public bool SelfTestRun(out string note)
	{
		int httpTimeoutMs = HttpTimeoutMs;
		try
		{
			HttpTimeoutMs = Math.Max(HttpTimeoutMs, 5000);
			string raw = GetRaw("/selftest?run=1");
			int lastHttpCode = LastHttpCode;
			string text = ((raw == null) ? "" : raw.Trim());
			if (lastHttpCode == 409 || text.IndexOf("busy", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				note = "设备忙（HTTP 409 ERR:busy）：云台正在运动/自检中，请等它停下再试";
				return false;
			}
			if (lastHttpCode == 200 && text.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
			{
				note = "已开始“跑一圈”：水平 0→360→回 0，约 80 秒（9 °/s）";
				return true;
			}
			note = "启动失败：" + ((lastHttpCode > 0) ? ("HTTP " + lastHttpCode + " ") : "") + ((text.Length > 0) ? text : LastError);
			return false;
		}
		finally
		{
			HttpTimeoutMs = httpTimeoutMs;
		}
	}

	public YzTrackSnapshot GetTrackCli()
	{
		string @checked = GetChecked("/trackcli");
		if (@checked == null)
		{
			return null;
		}
		return YzTrackSnapshot.Parse(@checked);
	}

	public YzResetStat GetResetStat()
	{
		string @checked = GetChecked("/rst?n=8");
		if (@checked == null)
		{
			return null;
		}
		return YzResetStat.Parse(@checked);
	}

	public string GetSysField(string key)
	{
		string sys = GetSys();
		if (sys == null)
		{
			return null;
		}
		string[] array = sys.Split(';');
		for (int i = 0; i < array.Length; i++)
		{
			int num = array[i].IndexOf('=');
			if (num > 0 && array[i].Substring(0, num).Trim() == key)
			{
				return array[i].Substring(num + 1).Trim();
			}
		}
		return null;
	}

	public bool CmdEx(string g, out int code, out string body, out string note)
	{
		TrackerLauncher.StopIfRunning("云台命令");
		string raw = GetRaw("/cmd?g=" + Uri.EscapeDataString(g));
		code = LastHttpCode;
		body = ((raw == null) ? "" : raw.Trim());
		if (YzHttpMsg.LooksOk(code, body))
		{
			note = "OK";
			return true;
		}
		note = YzHttpMsg.Explain(code, (body.Length > 0) ? body : LastError);
		return false;
	}

	public bool GotoEx(double az, double el, out int code, out string body, out string note)
	{
		TrackerLauncher.StopIfRunning("云台定位");
		string raw = GetRaw("/goto?h=" + az.ToString("0.###", CultureInfo.InvariantCulture) + "&v=" + el.ToString("0.###", CultureInfo.InvariantCulture));
		code = LastHttpCode;
		body = ((raw == null) ? "" : raw.Trim());
		if (code == 200 && body.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			note = "OK";
			return true;
		}
		note = YzHttpMsg.Explain(code, (body.Length > 0) ? body : LastError);
		return false;
	}

	public bool PitchZero(out string note)
	{
		if (CmdEx("VZ", out var code, out var body, out note))
		{
			note = "已受理「俯仰当前姿态归零」（/cmd?g=VZ）：当圈 va/elint 回 0，写 NVS";
			return true;
		}
		string text = note;
		if (CmdEx("TZ", out code, out body, out note))
		{
			note = "已受理「俯仰姿态归零」（/cmd?g=TZ，老固件路径）：当圈 va 回 0";
			return true;
		}
		string text2 = note;
		string raw = GetRaw("/set?va=0");
		string text3 = ((raw == null) ? "" : raw.Trim());
		if (YzHttpMsg.LooksOk(LastHttpCode, text3))
		{
			note = "已受理「俯仰姿态归零」（/set?va=0）：当圈 va 回 0，写 NVS";
			return true;
		}
		note = "俯仰姿态归零失败：VZ → " + text + "；TZ → " + text2 + "；va=0 → " + YzHttpMsg.Explain(LastHttpCode, (text3.Length > 0) ? text3 : LastError);
		return false;
	}

	public bool SetPtrace(bool on, out string note)
	{
		string raw = GetRaw("/set?ptrace=" + (on ? "1" : "0"));
		string text = ((raw == null) ? "" : raw.Trim());
		int lastHttpCode = LastHttpCode;
		if (lastHttpCode == 200 && text.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
		{
			note = "引脚时序日志已" + (on ? "开启（ptrace=1，串口每次 IO4/IO5 变化打一行；不写 flash）" : "关闭（ptrace=0，零开销）");
			return true;
		}
		note = "设置 ptrace 失败：" + YzHttpMsg.Explain(lastHttpCode, (text.Length > 0) ? text : LastError);
		return false;
	}

	public YzOtasInfo GetOtas()
	{
		string @checked = GetChecked("/otas");
		if (@checked == null)
		{
			return null;
		}
		return YzOtasInfo.Parse(@checked);
	}

	public YzSysSnapshot GetSysSnapshot()
	{
		string sys = GetSys();
		if (sys == null)
		{
			return null;
		}
		return YzSysSnapshot.Parse(sys);
	}

	public YzRstHistory GetRstHistory(int n)
	{
		if (n < 1)
		{
			n = 1;
		}
		if (n > 64)
		{
			n = 64;
		}
		string @checked = GetChecked("/rst?n=" + n);
		if (@checked == null)
		{
			return null;
		}
		return YzRstHistory.Parse(@checked);
	}

	public string GetAzRaw()
	{
		return GetChecked("/az");
	}

	public YzSysSnapshot GetWdtReadback()
	{
		string raw = GetRaw("/set?wdt=");
		int lastHttpCode = LastHttpCode;
		string text = ((raw == null) ? "" : raw.Trim());
		if (lastHttpCode != 200 || !text.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
		{
			LastError = "读 /set?wdt= 失败：" + YzHttpMsg.Explain(lastHttpCode, (text.Length > 0) ? text : LastError);
			return null;
		}
		int num = text.IndexOf(':');
		return YzSysSnapshot.Parse((num >= 0) ? text.Substring(num + 1) : text);
	}

	public YzEthInfo GetEthInfo()
	{
		string @checked = GetChecked("/eth");
		if (@checked == null)
		{
			return null;
		}
		return YzEthInfo.Parse(@checked);
	}

	public bool HangInject(int seconds, bool noInt, out int code, out string body, out string note)
	{
		code = -1;
		body = "";
		if (seconds < 0 || seconds > 120)
		{
			note = $"拒绝下发：注入 {seconds} s 超出固件上限 0~{120} s（固件会回 400 ERR:hang-range）";
			return false;
		}
		string raw = GetRaw("/set?hang=" + seconds.ToString(CultureInfo.InvariantCulture) + "&noint=" + (noInt ? "1" : "0"));
		code = LastHttpCode;
		body = ((raw == null) ? "" : raw.Trim());
		if (code == 200 && body.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
		{
			note = string.Format("已受理卡死注入：hang={0}s、noint={1}（{2}）—— 设备将在**下一圈 loop** 开始忙等", seconds, noInt ? 1 : 0, noInt ? "关中断，只剩第 2 层 RTC_WDT(30 s) 能救" : "不喂第 1 层，超 15 s 由 TWDT 复位");
			return true;
		}
		note = "卡死注入被拒：" + YzHttpMsg.Explain(code, (body.Length > 0) ? body : LastError);
		return false;
	}

	public bool WifiConnect(string ssid, string pass, out string note)
	{
		if (string.IsNullOrEmpty(ssid) || ssid.Trim().Length == 0)
		{
			note = "SSID 不能为空（固件要求 s 必填且 ≤32 字符）";
			return false;
		}
		if (ssid.Trim().Length > 32)
		{
			note = "SSID 超过 32 字符，固件会拒绝（400 ERR:args）";
			return false;
		}
		if (pass != null && pass.Length != 0 && (pass.Length < 8 || pass.Length > 63))
		{
			note = "WiFi 密码要么留空，要么 8~63 字符（固件要求；否则 400 ERR:args）";
			return false;
		}
		int httpTimeoutMs = HttpTimeoutMs;
		try
		{
			HttpTimeoutMs = Math.Max(HttpTimeoutMs, 25000);
			string raw = GetRaw("/wifi?s=" + Uri.EscapeDataString(ssid.Trim()) + "&p=" + Uri.EscapeDataString(pass ?? ""));
			int lastHttpCode = LastHttpCode;
			string text = ((raw == null) ? "" : raw.Trim());
			if (lastHttpCode == 200 && text.StartsWith("OK", StringComparison.OrdinalIgnoreCase))
			{
				note = "配网成功：" + text;
				return true;
			}
			if (text.IndexOf("rollback-ok", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				note = "新凭据连不上，固件已回滚旧凭据（502 ERR:connect:rollback-ok）——SSID/密码可能不对";
				return false;
			}
			if (raw == null && lastHttpCode == 0)
			{
				note = "配网请求失败（" + LastError + "）——板子可能正在切换 WiFi，等 10 秒后读 /otas 看结果";
				return false;
			}
			note = "配网被拒：" + YzHttpMsg.Explain(lastHttpCode, (text.Length > 0) ? text : LastError) + "（固件参数要求：s 必填 ≤32 字符；p 为空或 8~63 字符）";
			return false;
		}
		finally
		{
			HttpTimeoutMs = httpTimeoutMs;
		}
	}

	public bool OtaPush(string path, Action<int, string> progress, out string err, out string bldBefore, out string bldAfter)
	{
		err = null;
		bldBefore = null;
		bldAfter = null;
		string host = Host;
		if (RequestLog != null)
		{
			try
			{
				RequestLog("POST", "/update");
			}
			catch
			{
			}
		}
		try
		{
			bldBefore = GetSysField("bld");
		}
		catch
		{
		}
		progress?.Invoke(0, "当前固件 bld=" + (bldBefore ?? "?") + "，开始上传 " + Path.GetFileName(path) + "…");
		FileInfo fileInfo;
		try
		{
			fileInfo = new FileInfo(path);
		}
		catch (Exception ex)
		{
			err = "固件文件无法读取：" + ex.Message;
			return false;
		}
		if (!fileInfo.Exists)
		{
			err = "固件文件不存在";
			return false;
		}
		try
		{
			string text = "----YzOta" + Guid.NewGuid().ToString("N");
			HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create("http://" + host + "/update");
			httpWebRequest.Method = "POST";
			httpWebRequest.ContentType = "multipart/form-data; boundary=" + text;
			httpWebRequest.Timeout = 600000;
			httpWebRequest.ReadWriteTimeout = 600000;
			httpWebRequest.AllowWriteStreamBuffering = false;
			httpWebRequest.Proxy = null;
			byte[] bytes = Encoding.UTF8.GetBytes("--" + text + "\r\nContent-Disposition: form-data; name=\"update\"; filename=\"" + fileInfo.Name + "\"\r\nContent-Type: application/octet-stream\r\n\r\n");
			byte[] bytes2 = Encoding.UTF8.GetBytes("\r\n--" + text + "--\r\n");
			httpWebRequest.ContentLength = bytes.Length + fileInfo.Length + bytes2.Length;
			using (Stream stream = httpWebRequest.GetRequestStream())
			{
				stream.Write(bytes, 0, bytes.Length);
				using (FileStream fileStream = File.OpenRead(path))
				{
					byte[] array = new byte[16384];
					long num = 0L;
					int num2;
					while ((num2 = fileStream.Read(array, 0, array.Length)) > 0)
					{
						stream.Write(array, 0, num2);
						num += num2;
						int arg = (int)(num * 100 / fileInfo.Length);
						progress?.Invoke(arg, "已上传 " + num / 1024 + " / " + fileInfo.Length / 1024 + " KB（" + arg + "%）");
					}
				}
				stream.Write(bytes2, 0, bytes2.Length);
			}
			using HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
			int statusCode = (int)httpWebResponse.StatusCode;
			if (statusCode != 200)
			{
				err = "上传被拒（HTTP " + statusCode + "）";
			}
		}
		catch (WebException ex2)
		{
			if (ex2.Response is HttpWebResponse httpWebResponse2)
			{
				string body = "";
				try
				{
					using StreamReader streamReader = new StreamReader(httpWebResponse2.GetResponseStream(), Encoding.UTF8);
					body = streamReader.ReadToEnd();
				}
				catch
				{
				}
				err = YzHttpMsg.Explain((int)httpWebResponse2.StatusCode, body);
			}
			else
			{
				err = "上传失败：" + ex2.Message;
			}
			return false;
		}
		catch (Exception ex3)
		{
			err = "上传失败：" + ex3.Message;
			return false;
		}
		progress?.Invoke(100, "上传完成，等待设备重启…（顺带读 /sys 的 bld 判成功）");
		for (int i = 0; i < 20; i++)
		{
			Thread.Sleep(2000);
			try
			{
				string sysField = GetSysField("bld");
				if (sysField != null && sysField.Length > 0)
				{
					bldAfter = sysField;
					if (bldBefore == null || sysField != bldBefore)
					{
						break;
					}
				}
			}
			catch
			{
			}
		}
		return true;
	}

	public bool SetLimits(int up, int down)
	{
		return GetOk("/set?u=" + up + "&d=" + down);
	}

	public bool SetTitle(string title)
	{
		return GetOk("/set?title=" + Uri.EscapeDataString(title ?? ""));
	}

	public bool SetSelfTest(bool on)
	{
		string text = (on ? "1" : "0");
		return GetOk("/set?selftest=" + text + "&sf=" + text);
	}

	public string GetPriority()
	{
		return GetChecked("/prio");
	}

	public bool ReleasePriority()
	{
		string @checked = GetChecked("/prio?off=1");
		if (@checked == null)
		{
			return false;
		}
		if (!@checked.StartsWith("ip=", StringComparison.Ordinal))
		{
			return @checked.StartsWith("OK", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	public bool SetProtocol(int p)
	{
		return GetOk("/proto?a=" + p);
	}

	public bool SetPriority(string ip)
	{
		return GetChecked("/prio?ip=" + Uri.EscapeDataString(ip ?? "0.0.0.0"))?.StartsWith("ip=", StringComparison.Ordinal) ?? false;
	}

	public string EthGet()
	{
		return GetChecked("/eth");
	}

	public bool EthSetStatic(string ip, string gw, string nm, string ds)
	{
		return GetOk("/eth?ip=" + ip + "&gw=" + gw + "&nm=" + nm + "&ds=" + ds);
	}

	public bool EthSetDhcp()
	{
		return GetOk("/eth?dhcp=1");
	}

	public string GetTcal()
	{
		return GetChecked("/tcal");
	}

	public static string LocalIpFor(string host)
	{
		int[] array = new int[2] { 80, 9 };
		for (int i = 0; i < array.Length; i++)
		{
			try
			{
				using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
				socket.Connect(host, array[i]);
				if (socket.LocalEndPoint is IPEndPoint iPEndPoint && iPEndPoint.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(iPEndPoint.Address))
				{
					return iPEndPoint.Address.ToString();
				}
			}
			catch
			{
			}
		}
		try
		{
			if (IPAddress.TryParse(host ?? "", out var address) && address.AddressFamily == AddressFamily.InterNetwork)
			{
				byte[] addressBytes = address.GetAddressBytes();
				string text = null;
				NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
				foreach (NetworkInterface networkInterface in allNetworkInterfaces)
				{
					if (networkInterface.OperationalStatus != OperationalStatus.Up || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
					{
						continue;
					}
					foreach (UnicastIPAddressInformation unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
					{
						IPAddress address2 = unicastAddress.Address;
						if (address2.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address2))
						{
							byte[] addressBytes2 = address2.GetAddressBytes();
							if (addressBytes2[0] == addressBytes[0] && addressBytes2[1] == addressBytes[1] && addressBytes2[2] == addressBytes[2])
							{
								return address2.ToString();
							}
							if (text == null)
							{
								text = address2.ToString();
							}
						}
					}
				}
				if (text != null)
				{
					return text;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public bool RotatorConnect()
	{
		lock (_tcpLock)
		{
			CloseTcpLocked();
			try
			{
				_tcp = new TcpClient();
				_tcp.NoDelay = true;
				IAsyncResult asyncResult = _tcp.BeginConnect(Host, 4533, null, null);
				if (!asyncResult.AsyncWaitHandle.WaitOne(TcpTimeoutMs))
				{
					LastError = "连接 4533 超时";
					CloseTcpLocked();
					return false;
				}
				_tcp.EndConnect(asyncResult);
				_stream = _tcp.GetStream();
				_stream.ReadTimeout = TcpTimeoutMs;
				_stream.WriteTimeout = TcpTimeoutMs;
				_rx.Clear();
				LastError = "";
				return true;
			}
			catch (Exception ex)
			{
				LastError = "连接 4533 失败：" + ex.Message;
				CloseTcpLocked();
				return false;
			}
		}
	}

	public void RotatorClose()
	{
		lock (_tcpLock)
		{
			CloseTcpLocked();
		}
	}

	private void CloseTcpLocked()
	{
		try
		{
			if (_stream != null)
			{
				_stream.Close();
			}
		}
		catch
		{
		}
		try
		{
			if (_tcp != null)
			{
				_tcp.Close();
			}
		}
		catch
		{
		}
		_stream = null;
		_tcp = null;
		_rx.Clear();
	}

	public bool RotatorSend(string line)
	{
		lock (_tcpLock)
		{
			if (_stream == null)
			{
				LastError = "未连接 4533";
				return false;
			}
			try
			{
				byte[] bytes = Encoding.ASCII.GetBytes(line + "\n");
				_stream.Write(bytes, 0, bytes.Length);
				_stream.Flush();
				return true;
			}
			catch (Exception ex)
			{
				LastError = "发送失败：" + ex.Message;
				CloseTcpLocked();
				return false;
			}
		}
	}

	public string RotatorQuery(string line, int timeoutMs)
	{
		lock (_tcpLock)
		{
			if (_stream == null && !RotatorConnectNoLock())
			{
				return null;
			}
			try
			{
				byte[] bytes = Encoding.ASCII.GetBytes(line + "\n");
				_stream.Write(bytes, 0, bytes.Length);
				_stream.Flush();
				DateTime dateTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);
				while (DateTime.UtcNow < dateTime)
				{
					for (int i = 0; i < _rx.Count; i++)
					{
						if (_rx[i] == 10)
						{
							string result = Encoding.ASCII.GetString(_rx.GetRange(0, i).ToArray()).TrimEnd('\r');
							_rx.RemoveRange(0, i + 1);
							return result;
						}
					}
					if (!_stream.DataAvailable)
					{
						Thread.Sleep(15);
						continue;
					}
					byte[] array = new byte[512];
					int num = _stream.Read(array, 0, array.Length);
					if (num <= 0)
					{
						throw new IOException("对端关闭");
					}
					for (int j = 0; j < num; j++)
					{
						_rx.Add(array[j]);
					}
				}
				LastError = "读取应答超时";
				return null;
			}
			catch (Exception ex)
			{
				LastError = "查询失败：" + ex.Message;
				CloseTcpLocked();
				return null;
			}
		}
	}

	private bool RotatorConnectNoLock()
	{
		try
		{
			_tcp = new TcpClient();
			_tcp.NoDelay = true;
			IAsyncResult asyncResult = _tcp.BeginConnect(Host, 4533, null, null);
			if (!asyncResult.AsyncWaitHandle.WaitOne(TcpTimeoutMs))
			{
				CloseTcpLocked();
				return false;
			}
			_tcp.EndConnect(asyncResult);
			_stream = _tcp.GetStream();
			_stream.ReadTimeout = TcpTimeoutMs;
			_stream.WriteTimeout = TcpTimeoutMs;
			_rx.Clear();
			return true;
		}
		catch (Exception ex)
		{
			LastError = "连接 4533 失败：" + ex.Message;
			CloseTcpLocked();
			return false;
		}
	}

	public double QueryAzimuth()
	{
		string text = RotatorQuery("p", 1500);
		if (string.IsNullOrEmpty(text))
		{
			return double.NaN;
		}
		if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return double.NaN;
	}

	public string ReadOnlyQuery4533(int proto, out string note)
	{
		string text = proto switch
		{
			3 => "C", 
			0 => "AZ", 
			_ => "p", 
		};
		lock (_tcpLock)
		{
			CloseTcpLocked();
			try
			{
				using TcpClient tcpClient = new TcpClient();
				tcpClient.NoDelay = true;
				IAsyncResult asyncResult = tcpClient.BeginConnect(Host, 4533, null, null);
				if (!asyncResult.AsyncWaitHandle.WaitOne(TcpTimeoutMs))
				{
					note = "连接 4533 超时（设备未就绪或被防火墙拦截）";
					return null;
				}
				tcpClient.EndConnect(asyncResult);
				using NetworkStream networkStream = tcpClient.GetStream();
				networkStream.ReadTimeout = 1500;
				networkStream.WriteTimeout = 1500;
				byte[] bytes = Encoding.ASCII.GetBytes(text + "\n");
				networkStream.Write(bytes, 0, bytes.Length);
				networkStream.Flush();
				StringBuilder stringBuilder = new StringBuilder();
				byte[] array = new byte[256];
				DateTime dateTime = DateTime.UtcNow.AddMilliseconds(1500.0);
				while (DateTime.UtcNow < dateTime && stringBuilder.Length < 200)
				{
					if (!networkStream.DataAvailable)
					{
						Thread.Sleep(15);
						continue;
					}
					int num = networkStream.Read(array, 0, array.Length);
					if (num <= 0)
					{
						break;
					}
					stringBuilder.Append(Encoding.ASCII.GetString(array, 0, num));
					if (stringBuilder.ToString().IndexOf('\n') >= 0)
					{
						break;
					}
				}
				string text2 = stringBuilder.ToString().Replace("\r", "").Replace("\n", " ")
					.Trim();
				note = ((text2.Length == 0) ? ("已连上并发出只读查询「" + text + "」，但 1.5 秒内无应答（该协议可能不认这个查询，或本机不是控制者）") : ("只读查询「" + text + "」应答：" + text2));
				return text2;
			}
			catch (Exception ex)
			{
				note = "只读查询失败：" + ex.Message;
				return null;
			}
			finally
			{
				CloseTcpLocked();
			}
		}
	}
}
