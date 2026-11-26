using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace YzControl.Net;

/// <summary>
/// v1.6.0 链路错误分类（参照 OrbitLink：失败不是一种，要分得清才能给对提示）。
/// 旧代码所有失败都是「读取失败 + ex.Message」，用户看不出是网线、端口还是设备忙。
/// </summary>
public enum YzLinkErrorKind
{
	None = 0,

	/// <summary>超时：网络通、对端没在预算内回。</summary>
	Timeout = 1,

	/// <summary>连接被拒：端口没监听 / 对端主动关闭（RST）。</summary>
	Refused = 2,

	/// <summary>不可达：路由不通 / IP 不在同网段 / 设备掉电。</summary>
	Unreachable = 3,

	/// <summary>协议错：应答不是期望的格式（HTTP 4xx/5xx、CI-V 回帧残缺）。</summary>
	Protocol = 4,

	/// <summary>设备忙：HTTP 409 / ERR:busy，正在执行上一动作。</summary>
	Busy = 5
}

/// <summary>带分类的链路错误。不可变，可安全跨线程传递。</summary>
public sealed class YzLinkError
{
	public readonly YzLinkErrorKind Kind;

	/// <summary>给用户看的短句（已经内含目标地址与关键数字）。</summary>
	public readonly string Text;

	/// <summary>技术细节（异常类型 + 原始 message），只用于 tooltip / 日志。</summary>
	public readonly string Detail;

	public readonly int HttpCode;

	private YzLinkError(YzLinkErrorKind kind, string text, string detail, int httpCode)
	{
		Kind = kind;
		Text = (text ?? "");
		Detail = (detail ?? "");
		HttpCode = httpCode;
	}

	public bool IsNone
	{
		get
		{
			return Kind == YzLinkErrorKind.None;
		}
	}

	/// <summary>没有错误。</summary>
	public static YzLinkError None()
	{
		return new YzLinkError(YzLinkErrorKind.None, "", "", 0);
	}

	public static YzLinkError Of(YzLinkErrorKind kind, string text, string detail, int httpCode)
	{
		return new YzLinkError(kind, text, detail, httpCode);
	}

	/// <summary>按 HTTP 状态码分类：200 一族算成功，409/busy 算忙，其余 4xx/5xx 算协议错。</summary>
	public static YzLinkError FromHttpStatus(int code, string body, string detail)
	{
		if (code >= 200 && code < 300)
		{
			return None();
		}
		string b = body ?? "";
		if (code == 409 || b.IndexOf("busy", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return new YzLinkError(YzLinkErrorKind.Busy, "设备忙（HTTP " + code + " ERR:busy，正在执行上一动作）", detail, code);
		}
		if (code > 0)
		{
			return new YzLinkError(YzLinkErrorKind.Protocol, "协议错（HTTP " + code + " 应答异常）", detail, code);
		}
		return new YzLinkError(YzLinkErrorKind.Protocol, "协议错（应答无法解析）", detail, code);
	}

	/// <summary>
	/// 把传输层异常归类。target 是「host:port」这类目标字符串，timeoutMs&gt;0 时写进提示。
	/// </summary>
	public static YzLinkError Classify(Exception ex, string target, int timeoutMs)
	{
		string t = ((target == null || target.Length == 0) ? "设备" : target);
		string detail = Describe(ex);
		if (ex == null)
		{
			return new YzLinkError(YzLinkErrorKind.Unreachable, "不可达（" + t + " 无响应）", detail, 0);
		}
		// 1) WebException：先看 Response（HTTP 状态码），再看 Status，最后看内层 SocketException
		WebException web = ex as WebException;
		if (web != null)
		{
			HttpWebResponse resp = web.Response as HttpWebResponse;
			if (resp != null)
			{
				return FromHttpStatus((int)resp.StatusCode, null, detail);
			}
			switch (web.Status)
			{
			case WebExceptionStatus.Timeout:
				return Timeout(t, timeoutMs, detail);
			case WebExceptionStatus.NameResolutionFailure:
			case WebExceptionStatus.ProxyNameResolutionFailure:
				return new YzLinkError(YzLinkErrorKind.Unreachable, "不可达（" + t + " 域名解析失败：主机名写错或不在本网段）", detail, 0);
			case WebExceptionStatus.ConnectFailure:
			{
				SocketException se = web.InnerException as SocketException;
				if (se != null)
				{
					return FromSocket(se, t, timeoutMs, detail);
				}
				return new YzLinkError(YzLinkErrorKind.Refused, "连接被拒（" + t + " 端口未监听或设备拒绝连接）", detail, 0);
			}
			case WebExceptionStatus.ConnectionClosed:
			case WebExceptionStatus.KeepAliveFailure:
			case WebExceptionStatus.ReceiveFailure:
			case WebExceptionStatus.SendFailure:
			case WebExceptionStatus.PipelineFailure:
				return new YzLinkError(YzLinkErrorKind.Refused, "连接被拒（" + t + " 对端中途关闭）", detail, 0);
			case WebExceptionStatus.TrustFailure:
				return new YzLinkError(YzLinkErrorKind.Protocol, "协议错（" + t + " TLS 证书校验失败）", detail, 0);
			default:
				return new YzLinkError(YzLinkErrorKind.Unreachable, "不可达（" + t + " 请求失败）", detail, 0);
			}
		}
		// 2) SocketException / IOException（CivLink 的 TCP 链路走这里）
		SocketException sock = ex as SocketException;
		if (sock != null)
		{
			return FromSocket(sock, t, timeoutMs, detail);
		}
		IOException io = ex as IOException;
		if (io != null)
		{
			SocketException inner = io.InnerException as SocketException;
			if (inner != null)
			{
				return FromSocket(inner, t, timeoutMs, detail);
			}
			return new YzLinkError(YzLinkErrorKind.Refused, "连接被拒（" + t + " 对端关闭）", detail, 0);
		}
		return new YzLinkError(YzLinkErrorKind.Protocol, "协议错（" + t + " 应答异常）", detail, 0);
	}

	private static YzLinkError FromSocket(SocketException se, string t, int timeoutMs, string detail)
	{
		switch (se.SocketErrorCode)
		{
		case SocketError.TimedOut:
			return Timeout(t, timeoutMs, detail);
		case SocketError.ConnectionRefused:
			return new YzLinkError(YzLinkErrorKind.Refused, "连接被拒（" + t + " 端口未监听：设备没开服务或端口填错）", detail, 0);
		case SocketError.HostUnreachable:
		case SocketError.NetworkUnreachable:
		case SocketError.HostDown:
		case SocketError.NetworkDown:
			return new YzLinkError(YzLinkErrorKind.Unreachable, "不可达（" + t + " 路由不通：IP 填错 / 不在同网段 / 设备掉电）", detail, 0);
		case SocketError.ConnectionReset:
		case SocketError.ConnectionAborted:
		case SocketError.Shutdown:
			return new YzLinkError(YzLinkErrorKind.Refused, "连接被拒（" + t + " 对端重置了连接）", detail, 0);
		default:
			return new YzLinkError(YzLinkErrorKind.Unreachable, "不可达（" + t + " " + se.SocketErrorCode + "）", detail, 0);
		}
	}

	private static YzLinkError Timeout(string t, int timeoutMs, string detail)
	{
		string budget = ((timeoutMs > 0) ? (" " + timeoutMs + "ms 内") : " ");
		return new YzLinkError(YzLinkErrorKind.Timeout, "超时（" + t + budget + "无应答：设备在线但没回）", detail, 0);
	}

	/// <summary>异常 → 一行技术细节（截断，避免撑爆 tooltip / 状态栏）。</summary>
	public static string Describe(Exception ex)
	{
		if (ex == null)
		{
			return "";
		}
		StringBuilder sb = new StringBuilder();
		sb.Append(ex.GetType().Name);
		if (!string.IsNullOrEmpty(ex.Message))
		{
			sb.Append(": ").Append(ex.Message);
		}
		Exception inner = ex.InnerException;
		int guard = 0;
		while (inner != null && guard++ < 3)
		{
			if (!string.IsNullOrEmpty(inner.Message))
			{
				sb.Append(" ← ").Append(inner.GetType().Name).Append(": ").Append(inner.Message);
			}
			inner = inner.InnerException;
		}
		string s = sb.ToString();
		return ((s.Length > 160) ? (s.Substring(0, 157) + "…") : s);
	}

	/// <summary>把 Detail 拼进用户可见文本（够短才拼，太长只留分类句）。</summary>
	public string TextWithDetail()
	{
		if (IsNone)
		{
			return "";
		}
		if (Detail.Length == 0 || Detail.Length > 90)
		{
			return Text;
		}
		return Text + " · " + Detail;
	}

	public override string ToString()
	{
		return Kind + ": " + Text;
	}
}

/// <summary>传输层失败时抛的异常（ITransport 的 Task API 用）。</summary>
public sealed class YzLinkException : Exception
{
	public readonly YzLinkError Error;

	public YzLinkException(YzLinkError error)
		: base((error == null) ? "link error" : error.TextWithDetail())
	{
		Error = (error ?? YzLinkError.None());
	}
}
