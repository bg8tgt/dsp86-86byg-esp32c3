using System;

namespace YzControl.Net;

/// <summary>
/// v1.6.0 重连退避（参照 OrbitLink）：1s → 2s → 4s → 8s → 16s → 30s 封顶，成功后复位。
/// 断线期间命令走「快速失败」：窗口内不发任何 IO，也不排队（避免按钮点十下就堆十条超时请求）。
/// </summary>
public sealed class ReconnectPolicy
{
	private static readonly int[] Steps = new int[6] { 1000, 2000, 4000, 8000, 16000, 30000 };

	private readonly object _gate = new object();

	private int _failures;

	private DateTime _nextTryUtc = DateTime.MinValue;

	private int _maxDelayMs = 30000;

	public ReconnectPolicy()
	{
	}

	public ReconnectPolicy(int maxDelayMs)
	{
		if (maxDelayMs > 0)
		{
			_maxDelayMs = maxDelayMs;
		}
	}

	/// <summary>退避上限（毫秒）。设更小只会收紧上限，不会放大到 30s 以上。</summary>
	public int MaxDelayMs
	{
		get
		{
			lock (_gate)
			{
				return _maxDelayMs;
			}
		}
		set
		{
			lock (_gate)
			{
				_maxDelayMs = ((value < 200) ? 200 : value);
			}
		}
	}

	/// <summary>连续失败次数。</summary>
	public int Failures
	{
		get
		{
			lock (_gate)
			{
				return _failures;
			}
		}
	}

	/// <summary>当前这一级的退避时长（毫秒）；没失败过是 0。</summary>
	public int DelayMs
	{
		get
		{
			lock (_gate)
			{
				return DelayLocked();
			}
		}
	}

	private int DelayLocked()
	{
		if (_failures <= 0)
		{
			return 0;
		}
		int step = Steps[Math.Min(_failures - 1, Steps.Length - 1)];
		return Math.Min(step, _maxDelayMs);
	}

	/// <summary>记一次失败并把下一次尝试时间推后。</summary>
	public int OnFailure()
	{
		lock (_gate)
		{
			_failures++;
			int delay = DelayLocked();
			_nextTryUtc = DateTime.UtcNow.AddMilliseconds(delay);
			return delay;
		}
	}

	/// <summary>是否还在退避窗口内（true = 现在不该发请求）。</summary>
	public bool Waiting
	{
		get
		{
			lock (_gate)
			{
				return DateTime.UtcNow < _nextTryUtc;
			}
		}
	}

	/// <summary>距离下次允许尝试还有多少毫秒。</summary>
	public int RemainingMs
	{
		get
		{
			lock (_gate)
			{
				double ms = (_nextTryUtc - DateTime.UtcNow).TotalMilliseconds;
				return ((ms <= 0.0) ? 0 : ((int)ms));
			}
		}
	}

	/// <summary>成功后复位（恢复 1s 起步）。</summary>
	public void Reset()
	{
		lock (_gate)
		{
			_failures = 0;
			_nextTryUtc = DateTime.MinValue;
		}
	}

	/// <summary>一行描述，给 UI tooltip / 报错文案用。</summary>
	public string Describe()
	{
		lock (_gate)
		{
			if (_failures <= 0)
			{
				return "链路正常";
			}
			double sec = DelayLocked() / 1000.0;
			return "退避 " + sec.ToString("0.#") + "s（连续失败 " + _failures + " 次）";
		}
	}

	/// <summary>给用户的快速失败文案（窗口内调用时用）。</summary>
	public string WaitingText()
	{
		lock (_gate)
		{
			if (_failures <= 0)
			{
				return "";
			}
			double left = Math.Max(0.0, (_nextTryUtc - DateTime.UtcNow).TotalMilliseconds) / 1000.0;
			// 顶栏状态位只显示前 9 个字，所以把「还要等多久」放最前面
			return "退避中 " + left.ToString("0.#") + "s 后重试（" + Describe() + "）";
		}
	}
}
