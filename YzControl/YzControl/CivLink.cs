using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using YzControl.Net;

namespace YzControl;

/// <summary>
/// v1.6.0：CI-V 直连链路（TCP 4001 / 串口）。
/// 本次只换连接层内核，对外方法名与字段全部保持：
/// - 传输：TCP 走 <see cref="TcpTransport" />（原来的 TcpClient 直连代码退休）；
/// - 发送：走 <see cref="SendQueue" />（单链路串行 + 请求序号 + 应答匹配，消除半双工冲突）；
/// - 状态：<see cref="LinkStateMachine" /> + <see cref="ReconnectPolicy" />（1s→2s→…→30s 退避，成功后复位）；
/// - 收帧线程与帧分类逻辑保持原样（电台会主动上报帧，不能被当成一问一答）。
/// </summary>
public sealed class CivLink : IDisposable
{
	public byte ControllerAddr = 224;

	public byte RadioAddr = 136;

	public int DefaultTimeoutMs = 900;

	public int DefaultRetries = 1;

	public int InterFrameGapMs = 40;

	public int QueueLimit = 256;

	public int RecentLimit = 40;

	private readonly object _rxGate = new object();

	private readonly List<string> _recent = new List<string>();

	private readonly CivFramer _framer = new CivFramer();

	private readonly LinkStateMachine _status = new LinkStateMachine("civ");

	private readonly ReconnectPolicy _policy = new ReconnectPolicy();

	private readonly SendQueue _queue;

	private TcpTransport _tcp;

	private NetworkStream _ns;

	private SerialPort _com;

	private Thread _rx;

	private volatile bool _rxRun;

	private volatile bool _peerClosed;

	private int _rxIoErrors;

	private string _transport = "";

	private string _lastError = "";

	private string _lastRxHex = "";

	private string _lastTxHex = "";

	private long _seq;

	public long TxFrames;

	public long RxFrames;

	public long Timeouts;

	public long ForeignFrames;

	public long NgReplies;

	public long Retries;

	public CivLink()
	{
		_queue = new SendQueue(() => IsOpen, WriteAll, Thread.Sleep);
	}

	/// <summary>链路状态变化（后台线程抛出，UI 自行 BeginInvoke）。</summary>
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

	/// <summary>当前链路状态（UI 状态灯直接映射）。</summary>
	public LinkState State
	{
		get
		{
			return _status.State;
		}
	}

	/// <summary>状态原因（完整长句，放 tooltip）。</summary>
	public string StateReason
	{
		get
		{
			return _status.Reason;
		}
	}

	public string StateText
	{
		get
		{
			return LinkStateText.Zh(_status.State);
		}
	}

	public int ConsecutiveFailures
	{
		get
		{
			return _status.ConsecutiveFailures;
		}
	}

	/// <summary>最近一次请求的链路序号（诊断半双工冲突用）。</summary>
	public long LastSeq
	{
		get
		{
			return Interlocked.Read(ref _seq);
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

	public string LastError
	{
		get
		{
			lock (_rxGate)
			{
				return _lastError;
			}
		}
	}

	public string LastRxHex
	{
		get
		{
			lock (_rxGate)
			{
				return _lastRxHex;
			}
		}
	}

	public string Transport
	{
		get
		{
			return _transport;
		}
	}

	public bool IsTcp
	{
		get
		{
			return _tcp != null;
		}
	}

	public bool IsCom
	{
		get
		{
			return _com != null;
		}
	}

	public bool IsOpen
	{
		get
		{
			try
			{
				if (_peerClosed)
				{
					return false;
				}
				if (_tcp != null)
				{
					return _tcp.IsLinked && _ns != null;
				}
				if (_com != null)
				{
					return _com.IsOpen;
				}
			}
			catch
			{
			}
			return false;
		}
	}

	public string LastTxHex
	{
		get
		{
			lock (_rxGate)
			{
				return _lastTxHex;
			}
		}
	}

	public string[] RecentFrames
	{
		get
		{
			lock (_rxGate)
			{
				return _recent.ToArray();
			}
		}
	}

	private void SetError(string s)
	{
		lock (_rxGate)
		{
			_lastError = s ?? "";
		}
	}

	/// <summary>手动重试：清退避与失败计数（用户点「连接直连」时调用）。</summary>
	public void ResetLink()
	{
		_policy.Reset();
		_status.ResetFailures();
		if (!IsOpen)
		{
			_status.Set(LinkState.Disconnected, "手动重试");
		}
	}

	/// <summary>
	/// 建立 TCP 直连。退避窗口内**快速失败**（不连、不占线程），窗口到点后自动再试。
	/// 宿主（MainForm）在断开后每次轮询都会调到这里，因此断线重连节奏 = 轮询节奏 + 指数退避。
	/// </summary>
	public bool ConnectTcp(string host, int port, int connectTimeoutMs, out string err)
	{
		err = null;
		if (string.IsNullOrEmpty(host))
		{
			err = "目标 IP 为空";
			_status.Set(LinkState.Disconnected, err);
			return false;
		}
		if (port <= 0 || port > 65535)
		{
			err = "端口非法：" + port;
			_status.Set(LinkState.Disconnected, err);
			return false;
		}
		if (_policy.Waiting)
		{
			err = _policy.WaitingText();
			_status.Set(LinkState.Backoff, err);
			SetError(err);
			return false;
		}
		CloseCore(resetPolicy: false);
		_status.Set(LinkState.Connecting, "连接 " + host + ":" + port);
		TcpTransport t = new TcpTransport();
		if (!t.Connect(host, port, connectTimeoutMs, out YzLinkError e))
		{
			err = e.TextWithDetail();
			SetError(err);
			_policy.OnFailure();
			_status.NotifyFailure(LinkState.Backoff, err);
			return false;
		}
		_tcp = t;
		_ns = t.RawStream;
		_transport = t.Name;
		_peerClosed = false;
		_policy.Reset();
		_status.NotifySuccess("直连 " + host + ":" + port);
		SetError("");
		StartRx();
		return true;
	}

	public bool ConnectCom(string portName, int baud, out string err)
	{
		err = null;
		if (string.IsNullOrEmpty(portName))
		{
			err = "COM 口为空";
			_status.Set(LinkState.Disconnected, err);
			return false;
		}
		if (baud <= 0)
		{
			baud = 19200;
		}
		CloseCore(resetPolicy: false);
		_status.Set(LinkState.Connecting, "打开 " + portName + " @" + baud);
		try
		{
			SerialPort serialPort = new SerialPort(portName, baud, Parity.None, 8, StopBits.One);
			serialPort.ReadTimeout = 300;
			serialPort.WriteTimeout = 1500;
			serialPort.DtrEnable = true;
			serialPort.RtsEnable = true;
			serialPort.Open();
			_com = serialPort;
			_transport = "直连串口 " + portName + " @" + baud;
			_peerClosed = false;
			_policy.Reset();
			_status.NotifySuccess(_transport);
			StartRx();
			return true;
		}
		catch (UnauthorizedAccessException ex)
		{
			err = portName + " 被占用或无权限：" + ex.Message;
			_policy.OnFailure();
			_status.NotifyFailure(LinkState.Failed, err);
			return false;
		}
		catch (Exception ex2)
		{
			err = portName + " 打开失败：" + ex2.GetType().Name + "：" + ex2.Message;
			_policy.OnFailure();
			_status.NotifyFailure(LinkState.Failed, err);
			return false;
		}
	}

	private void StartRx()
	{
		_framer.Clear();
		_queue.Clear();
		lock (_rxGate)
		{
			_recent.Clear();
			_lastError = "";
		}
		_peerClosed = false;
		_rxIoErrors = 0;
		_rxRun = true;
		_rx = new Thread(RxLoop);
		_rx.IsBackground = true;
		_rx.Name = "CivLinkRx";
		_rx.Start();
	}

	public void Close()
	{
		CloseCore(resetPolicy: true);
	}

	private void CloseCore(bool resetPolicy)
	{
		_rxRun = false;
		try
		{
			if (_ns != null)
			{
				_ns.Close();
			}
		}
		catch
		{
		}
		try
		{
			_tcp?.Close(resetPolicy: true, "已断开");
		}
		catch
		{
		}
		try
		{
			if (_com != null)
			{
				if (_com.IsOpen)
				{
					_com.Close();
				}
				_com.Dispose();
			}
		}
		catch
		{
		}
		_ns = null;
		_tcp = null;
		_com = null;
		try
		{
			if (_rx != null && _rx.IsAlive)
			{
				_rx.Join(500);
			}
		}
		catch
		{
		}
		_rx = null;
		_queue.Clear();
		lock (_rxGate)
		{
			_recent.Clear();
			Monitor.PulseAll(_rxGate);
		}
		_framer.Clear();
		_transport = "";
		if (resetPolicy)
		{
			_policy.Reset();
			_status.ResetFailures();
			_status.Set(LinkState.Disconnected, "已断开");
		}
	}

	public void Dispose()
	{
		Close();
	}

	public string Describe()
	{
		if (!string.IsNullOrEmpty(_transport))
		{
			return _transport;
		}
		return "未连接";
	}

	private void RxLoop()
	{
		byte[] array = new byte[1024];
		while (_rxRun)
		{
			int num = 0;
			try
			{
				if (_tcp != null)
				{
					num = _tcp.ReadRaw(array, 0, array.Length);
				}
				else if (_com != null)
				{
					num = _com.Read(array, 0, array.Length);
				}
				else
				{
					break;
				}
			}
			catch (TimeoutException)
			{
				continue;
			}
			catch (Exception)
			{
				if (!_rxRun)
				{
					break;
				}
				if (++_rxIoErrors >= 5)
				{
					_peerClosed = true;
					try
					{
						_tcp?.MarkPeerClosed("IO 连续失败 5 次");
					}
					catch
					{
					}
					_status.NotifyFailure(LinkState.Failed, "收帧连续 IO 失败 5 次");
					break;
				}
				try
				{
					Thread.Sleep(50);
				}
				catch
				{
				}
				continue;
			}
			if (num <= 0)
			{
				if (_tcp != null)
				{
					_peerClosed = true;
					break;
				}
				continue;
			}
			_rxIoErrors = 0;
			_framer.Append(array, 0, num);
			byte[] frame;
			while (_framer.TryTake(out frame))
			{
				string text = Civ.Hex(frame);
				lock (_rxGate)
				{
					RxFrames++;
					_lastRxHex = text;
					_recent.Insert(0, text);
					if (_recent.Count > RecentLimit)
					{
						_recent.RemoveAt(_recent.Count - 1);
					}
				}
				_queue.PushRx(frame);
			}
		}
	}

	public CivReply Request(byte[] payload, int timeoutMs, int retries)
	{
		return RequestCore(payload, timeoutMs, retries, acceptAck: false);
	}

	public CivReply RequestAck(byte[] payload, int timeoutMs, int retries)
	{
		return RequestCore(payload, timeoutMs, retries, acceptAck: true);
	}

	private CivReply RequestCore(byte[] payload, int timeoutMs, int retries, bool acceptAck)
	{
		if (payload == null || payload.Length == 0)
		{
			SetError("内部错误：空命令");
			return null;
		}
		if (timeoutMs <= 0)
		{
			timeoutMs = DefaultTimeoutMs;
		}
		if (retries < 0)
		{
			retries = 0;
		}
		// 断线期间：直接快速失败，不排队、不堆积
		if (!IsOpen)
		{
			SetError(NotOpenText());
			return null;
		}
		_queue.QueueLimit = QueueLimit;
		byte[] frame = Civ.Frame(RadioAddr, ControllerAddr, payload);
		SendOutcome o = _queue.Request(frame, delegate(byte[] f)
		{
			return Classify(f, payload, acceptAck);
		}, timeoutMs, retries, InterFrameGapMs);
		if (o.Seq > 0)
		{
			Interlocked.Exchange(ref _seq, o.Seq);
		}
		TxFrames += o.Attempts;
		Timeouts += o.Timeouts;
		ForeignFrames += o.Foreign;
		if (o.Attempts > 1)
		{
			Retries += o.Attempts - 1;
		}
		if (o.SendFailed)
		{
			SetError("发送失败：" + o.SendError);
			CloseCore(resetPolicy: false);
			_peerClosed = true;
			_policy.OnFailure();
			_status.NotifyFailure(LinkState.Failed, "写入失败：" + o.SendError);
			return null;
		}
		if (o.LinkLost)
		{
			SetError("链路已断开（对端关闭 / IO 连续失败）");
			_peerClosed = true;
			_policy.OnFailure();
			_status.NotifyFailure(LinkState.Failed, "链路已断开");
			return null;
		}
		CivReply reply = o.Reply;
		if (reply != null)
		{
			reply.RttMs = o.RttMs;
			reply.Attempts = o.Attempts;
			if (reply.IsNg)
			{
				NgReplies++;
			}
			SetError("");
			_policy.Reset();
			_status.NotifySuccess("请求 " + o.Seq + " OK " + o.RttMs + "ms");
			return reply;
		}
		SetError("无应答（" + Civ.Hex(payload) + "，超时 " + timeoutMs + "ms" + (o.SawAny ? "；期间收到过总线上别的帧" : "") + "）");
		// 单次无应答只把状态降为 Backoff（不在这里累计失败次数）：
		// 「连续 3 次失败 → 判死」由 NoteHeartbeat（轮询心跳）唯一负责，避免一次轮询被算两次。
		_status.Set(LinkState.Backoff, "无应答 " + Civ.Hex(payload) + "（" + timeoutMs + "ms）");
		return null;
	}

	private string NotOpenText()
	{
		switch (_status.State)
		{
		case LinkState.Backoff:
			return "直连链路未建立：" + _policy.WaitingText();
		case LinkState.Failed:
			return "直连链路未建立（已判死：" + _status.Reason + "）";
		default:
			return "直连链路未建立";
		}
	}

	/// <summary>轮询心跳：CivPoller 每轮结束后调用，连续 3 次失败判死（对应规范里的 2s 心跳）。</summary>
	public void NoteHeartbeat(bool ok)
	{
		if (ok)
		{
			_policy.Reset();
			_status.NotifySuccess("轮询正常");
			return;
		}
		if (!IsOpen)
		{
			// 链路本身已经断了，不重复计失败（Connect/Close 那边已经记过）
			return;
		}
		_status.NotifyFailure(LinkState.Backoff, "读电台失败：" + LastError);
	}

	public bool SendRaw(byte[] frame, out string err)
	{
		err = null;
		if (frame == null || frame.Length < 5)
		{
			err = "帧格式非法（至少 FE FE to from cmd FD）";
			return false;
		}
		if (!IsOpen)
		{
			err = NotOpenText();
			return false;
		}
		if (_queue.SendOnly(frame, out string werr))
		{
			SetError("");
			return true;
		}
		err = "发送失败：" + werr;
		SetError(err);
		CloseCore(resetPolicy: false);
		_peerClosed = true;
		return false;
	}

	public bool SendHex(string hex, out string err)
	{
		err = null;
		byte[] array = Civ.ParseHex(hex);
		if (array == null)
		{
			err = "十六进制格式不对（只能是 0-9/A-F 且长度为偶数）";
			return false;
		}
		byte[] array2;
		if (array.Length >= 2 && array[0] == 254 && array[1] == 254)
		{
			array2 = array;
			if (array2[array2.Length - 1] != 253)
			{
				byte[] array3 = new byte[array2.Length + 1];
				Array.Copy(array2, array3, array2.Length);
				array3[array2.Length] = 253;
				array2 = array3;
			}
		}
		else
		{
			array2 = Civ.Frame(RadioAddr, ControllerAddr, array);
		}
		return SendRaw(array2, out err);
	}

	private void WriteAll(byte[] b)
	{
		lock (_rxGate)
		{
			_lastTxHex = Civ.Hex(b);
		}
		NetworkStream ns = _ns;
		if (ns != null)
		{
			ns.Write(b, 0, b.Length);
			ns.Flush();
			return;
		}
		SerialPort com = _com;
		if (com != null)
		{
			com.Write(b, 0, b.Length);
			return;
		}
		throw new InvalidOperationException("直连链路未建立");
	}

	private CivReply Classify(byte[] f, byte[] payload, bool acceptAck)
	{
		if (f == null || f.Length < 6)
		{
			return null;
		}
		if (f[0] != 254 || f[1] != 254)
		{
			return null;
		}
		if (f[f.Length - 1] != 253)
		{
			return null;
		}
		if (f[2] != ControllerAddr || f[3] != RadioAddr)
		{
			return null;
		}
		byte b = f[4];
		byte[] array = new byte[f.Length - 6];
		if (array.Length != 0)
		{
			Array.Copy(f, 5, array, 0, array.Length);
		}
		switch (b)
		{
		case 250:
			return new CivReply
			{
				Raw = f,
				Data = new byte[0],
				Cmd = 250
			};
		case 251:
			if (!acceptAck)
			{
				return null;
			}
			return new CivReply
			{
				Raw = f,
				Data = array,
				Cmd = 251
			};
		default:
		{
			if (b != payload[0])
			{
				return null;
			}
			if (payload.Length > 1 && array.Length != 0 && array[0] != payload[1])
			{
				return null;
			}
			if (!ShapeOk(b, array, out var _))
			{
				return null;
			}
			return new CivReply
			{
				Raw = f,
				Data = array,
				Cmd = b
			};
		}
		}
	}

	private static bool ShapeOk(byte cmd, byte[] data, out string why)
	{
		why = null;
		int num = ((data != null) ? data.Length : 0);
		switch (cmd)
		{
		case 3:
			if (num != 5)
			{
				why = "03 数据段应为 5 字节，实为 " + num;
				return false;
			}
			return true;
		case 4:
			if (num < 2)
			{
				why = "04 数据段应 ≥2 字节，实为 " + num;
				return false;
			}
			return true;
		case 15:
			if (num != 1)
			{
				why = "0F 数据段应为 1 字节，实为 " + num;
				return false;
			}
			return true;
		case 37:
			if (num != 6)
			{
				why = "25 数据段应为 6 字节，实为 " + num;
				return false;
			}
			return true;
		case 28:
			if (num < 2)
			{
				why = "1C 数据段残缺（<2 字节），实为 " + num;
				return false;
			}
			return true;
		case 21:
			if (num < 3)
			{
				why = "15 数据段应 ≥3 字节，实为 " + num;
				return false;
			}
			return true;
		default:
			return true;
		}
	}
}
