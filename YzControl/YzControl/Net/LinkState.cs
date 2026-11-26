using System;
using System.Threading;

namespace YzControl.Net;

/// <summary>
/// v1.6.0 链路状态（参照 OrbitLink 的显式状态机设计）。
/// UI 状态灯直接映射这五个值，不再靠「上次请求是否成功」去猜链路是否还在。
/// </summary>
public enum LinkState
{
	/// <summary>未连接（用户还没连 / 主动断开）。</summary>
	Disconnected = 0,

	/// <summary>正在连接（TCP 握手中 / 首次 HTTP 探活 / 退避窗口到点后的重试中）。</summary>
	Connecting = 1,

	/// <summary>就绪：最近一次请求成功。</summary>
	Ready = 2,

	/// <summary>退避等待中（连续失败但未判死，等 ReconnectPolicy 的窗口）。</summary>
	Backoff = 3,

	/// <summary>判死：连续失败达到阈值，链路视为不可用。</summary>
	Failed = 4
}

/// <summary>状态 → 中文短标签（UI 与日志共用，保证同一套文案）。</summary>
public static class LinkStateText
{
	public static string Zh(LinkState s)
	{
		switch (s)
		{
		case LinkState.Connecting:
			return "连接中";
		case LinkState.Ready:
			return "已连接";
		case LinkState.Backoff:
			return "重连中";
		case LinkState.Failed:
			return "已断开";
		default:
			return "未连接";
		}
	}

	/// <summary>状态 → 现有 DotState（StatusDot 用的枚举，避免 UI 侧再写一遍 switch）。</summary>
	public static int DotValue(LinkState s)
	{
		// 0=Unknown 1=Ok 2=Fail 3=Busy 4=Off（与 DotState 的顺序一致）
		switch (s)
		{
		case LinkState.Ready:
			return 1;
		case LinkState.Connecting:
		case LinkState.Backoff:
			return 3;
		case LinkState.Failed:
			return 2;
		default:
			return 4;
		}
	}

	public static bool IsUp(LinkState s)
	{
		return s == LinkState.Ready;
	}

	public static bool IsDown(LinkState s)
	{
		return s == LinkState.Disconnected || s == LinkState.Failed;
	}
}

/// <summary>
/// 线程安全的状态机：持有当前状态、失败原因、连续失败计数，并在**状态真正改变**时抛 StateChanged。
/// 事件在调用方线程（通常是后台线程）同步抛出，UI 侧必须自己 BeginInvoke 回主线程。
/// 只在状态变化时抛事件（同一状态只更新原因），避免每秒一次的轮询把 UI 消息泵刷满。
/// </summary>
public sealed class LinkStateMachine
{
	private readonly object _gate = new object();

	private readonly string _name;

	private LinkState _state;

	private string _reason = "";

	private int _fails;

	private int _failThreshold = 3;

	private DateTime _changedAt = DateTime.UtcNow;

	public LinkStateMachine(string name)
	{
		_name = ((name == null || name.Length == 0) ? "link" : name);
	}

	public event Action<LinkState> StateChanged;

	public string Name
	{
		get
		{
			return _name;
		}
	}

	public LinkState State
	{
		get
		{
			lock (_gate)
			{
				return _state;
			}
		}
	}

	/// <summary>最近一次状态变化的原因（给 tooltip / 日志用，可以是完整长句）。</summary>
	public string Reason
	{
		get
		{
			lock (_gate)
			{
				return _reason;
			}
		}
	}

	public int ConsecutiveFailures
	{
		get
		{
			lock (_gate)
			{
				return _fails;
			}
		}
	}

	/// <summary>判死阈值：连续失败达到这么多次就进 Failed（默认 3）。</summary>
	public int FailThreshold
	{
		get
		{
			lock (_gate)
			{
				return _failThreshold;
			}
		}
		set
		{
			lock (_gate)
			{
				_failThreshold = ((value < 1) ? 1 : value);
			}
		}
	}

	public DateTime ChangedAt
	{
		get
		{
			lock (_gate)
			{
				return _changedAt;
			}
		}
	}

	/// <summary>设置状态；只有状态值真的变了才返回 true 并抛事件。</summary>
	public bool Set(LinkState next, string reason)
	{
		Action<LinkState> handler;
		bool changed;
		lock (_gate)
		{
			_reason = reason ?? "";
			changed = _state != next;
			if (changed)
			{
				_state = next;
				_changedAt = DateTime.UtcNow;
			}
			handler = StateChanged;
		}
		if (changed && handler != null)
		{
			try
			{
				handler(next);
			}
			catch
			{
			}
		}
		return changed;
	}

	/// <summary>请求成功：失败计数清零 + Ready。</summary>
	public bool NotifySuccess(string reason)
	{
		lock (_gate)
		{
			_fails = 0;
		}
		return Set(LinkState.Ready, reason);
	}

	/// <summary>
	/// 请求失败：连续失败 +1；未到阈值时停在 <paramref name="transient" />（一般是 Backoff），
	/// 到阈值后判死进 Failed。返回是否真的判死。
	/// </summary>
	public bool NotifyFailure(LinkState transient, string reason)
	{
		int fails;
		bool dead;
		lock (_gate)
		{
			_fails++;
			fails = _fails;
			dead = fails >= _failThreshold;
		}
		string why = reason ?? "";
		if (dead)
		{
			Set(LinkState.Failed, why + "（连续失败 " + fails + " 次，判死）");
		}
		else
		{
			Set(transient, why + "（连续失败 " + fails + "/" + _failThreshold + "）");
		}
		return dead;
	}

	/// <summary>清零失败计数（不改状态），用于「手动重试」。</summary>
	public void ResetFailures()
	{
		lock (_gate)
		{
			_fails = 0;
		}
	}

	public override string ToString()
	{
		return _name + "=" + LinkStateText.Zh(State) + (Reason.Length > 0 ? ("（" + Reason + "）") : "");
	}
}
