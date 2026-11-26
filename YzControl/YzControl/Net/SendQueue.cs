using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace YzControl.Net;

/// <summary>一次「发送 → 等应答」的结果（含序号与统计，便于诊断半双工冲突）。</summary>
public sealed class SendOutcome
{
	/// <summary>本次请求的链路序号（单调递增，1 起）。</summary>
	public long Seq;

	/// <summary>匹配到的应答；null = 没匹配上（超时 / 断链 / 发送失败）。</summary>
	public CivReply Reply;

	/// <summary>等待期间是否收到过总线上别的帧。</summary>
	public bool SawAny;

	/// <summary>链路在等待期间断开。</summary>
	public bool LinkLost;

	/// <summary>写失败（socket 抛异常）。</summary>
	public bool SendFailed;

	public string SendError = "";

	/// <summary>实际尝试次数（1 = 一次就成）。</summary>
	public int Attempts;

	/// <summary>累计超时次数（每次等待失败 +1）。</summary>
	public int Timeouts;

	/// <summary>收到的、不属于本次请求的帧数。</summary>
	public int Foreign;

	public int RttMs;
}

/// <summary>
/// v1.6.0 发送队列（电台 CI-V 必需，参照 OrbitLink 的串行下发思路）：
/// - 单链路串行：所有请求在本队列上排队，同一时刻只有一条在飞（消除 CI-V 半双工冲突）；
/// - 请求序号：每次下发带单调递增序号，应答按「命令字节 + 子命令」匹配（CI-V 本身无序号字段）；
/// - 断线快速失败：链路不在时立刻返回，不排队、不堆积；
/// - 统计：Sent / Timeouts / Foreign / Dropped，供界面「原始调试」显示。
/// 收帧由链路自己的收帧线程 PushRx 推进来（含电台主动上报帧），本类只管等待与匹配。
/// </summary>
public sealed class SendQueue
{
	private readonly object _sendLock = new object();

	private readonly object _rxGate = new object();

	private readonly Queue<byte[]> _rx = new Queue<byte[]>();

	private readonly Func<bool> _isOpen;

	private readonly Action<byte[]> _write;

	private readonly Action<int> _sleep;

	private long _seq;

	private long _sent;

	private long _timeouts;

	private long _foreign;

	private long _dropped;

	private long _retries;

	private int _queueLimit = 256;

	public SendQueue(Func<bool> isOpen, Action<byte[]> write, Action<int> sleep)
	{
		_isOpen = isOpen;
		_write = write;
		_sleep = sleep ?? delegate(int _)
		{
		};
	}

	/// <summary>接收队列上限（与旧 CivLink.QueueLimit 同义：超了就丢最旧的）。</summary>
	public int QueueLimit
	{
		get
		{
			return _queueLimit;
		}
		set
		{
			_queueLimit = ((value < 1) ? 1 : value);
		}
	}

	public long Seq
	{
		get
		{
			return Interlocked.Read(ref _seq);
		}
	}

	public long Sent
	{
		get
		{
			return Interlocked.Read(ref _sent);
		}
	}

	public long Timeouts
	{
		get
		{
			return Interlocked.Read(ref _timeouts);
		}
	}

	public long Foreign
	{
		get
		{
			return Interlocked.Read(ref _foreign);
		}
	}

	public long Dropped
	{
		get
		{
			return Interlocked.Read(ref _dropped);
		}
	}

	public long Retries
	{
		get
		{
			return Interlocked.Read(ref _retries);
		}
	}

	public int Pending
	{
		get
		{
			lock (_rxGate)
			{
				return _rx.Count;
			}
		}
	}

	/// <summary>收帧线程调用：把一帧推进等待队列。</summary>
	public void PushRx(byte[] frame)
	{
		if (frame == null || frame.Length == 0)
		{
			return;
		}
		lock (_rxGate)
		{
			if (_rx.Count >= _queueLimit)
			{
				_rx.Dequeue();
				Interlocked.Increment(ref _dropped);
			}
			_rx.Enqueue(frame);
			Monitor.PulseAll(_rxGate);
		}
	}

	/// <summary>丢弃残留帧（等价旧的 DrainFrames：每次下发前清一次，避免把上一轮的回帧认成本轮）。</summary>
	public void Clear()
	{
		lock (_rxGate)
		{
			_rx.Clear();
		}
	}

	/// <summary>
	/// 只发不等（原始发帧用）。返回是否写成功。
	/// </summary>
	public bool SendOnly(byte[] frame, out string error)
	{
		error = "";
		lock (_sendLock)
		{
			if (!_isOpen())
			{
				error = "直连链路未建立";
				return false;
			}
			try
			{
				_write(frame);
				Interlocked.Increment(ref _sent);
				Interlocked.Increment(ref _seq);
				return true;
			}
			catch (Exception ex)
			{
				error = ex.GetType().Name + "：" + ex.Message;
				return false;
			}
		}
	}

	/// <summary>
	/// 一问一答（带重试）。<paramref name="classify" /> 负责把一帧回数据认成本次请求的应答，认不出返回 null。
	/// </summary>
	public SendOutcome Request(byte[] txFrame, Func<byte[], CivReply> classify, int timeoutMs, int retries, int gapMs)
	{
		SendOutcome o = new SendOutcome();
		lock (_sendLock)
		{
			o.Seq = Interlocked.Increment(ref _seq);
			if (timeoutMs <= 0)
			{
				timeoutMs = 900;
			}
			if (retries < 0)
			{
				retries = 0;
			}
			for (int attempt = 0; ; attempt++)
			{
				if (attempt > 0 && !_isOpen())
				{
					o.LinkLost = true;
					o.Attempts = attempt;
					return o;
				}
				Clear();
				Stopwatch sw = Stopwatch.StartNew();
				try
				{
					_write(txFrame);
					Interlocked.Increment(ref _sent);
				}
				catch (Exception ex)
				{
					o.SendFailed = true;
					o.SendError = ex.GetType().Name + "：" + ex.Message;
					o.Attempts = attempt + 1;
					return o;
				}
				CivReply reply = WaitFor(classify, timeoutMs, o);
				if (reply != null)
				{
					o.Reply = reply;
					o.RttMs = (int)sw.ElapsedMilliseconds;
					o.Attempts = attempt + 1;
					return o;
				}
				o.Timeouts++;
				Interlocked.Increment(ref _timeouts);
				o.Attempts = attempt + 1;
				if (o.LinkLost || attempt >= retries)
				{
					return o;
				}
				Interlocked.Increment(ref _retries);
				try
				{
					_sleep(gapMs);
				}
				catch
				{
				}
			}
		}
	}

	private CivReply WaitFor(Func<byte[], CivReply> classify, int timeoutMs, SendOutcome o)
	{
		Stopwatch sw = Stopwatch.StartNew();
		lock (_rxGate)
		{
			while (true)
			{
				if (_rx.Count > 0)
				{
					byte[] f = _rx.Dequeue();
					o.SawAny = true;
					CivReply r = classify(f);
					if (r != null)
					{
						return r;
					}
					Interlocked.Increment(ref _foreign);
					o.Foreign++;
					continue;
				}
				int left = timeoutMs - (int)sw.ElapsedMilliseconds;
				if (left <= 0)
				{
					return null;
				}
				Monitor.Wait(_rxGate, Math.Min(left, 100));
				if (!_isOpen())
				{
					o.LinkLost = true;
					return null;
				}
			}
		}
	}
}
