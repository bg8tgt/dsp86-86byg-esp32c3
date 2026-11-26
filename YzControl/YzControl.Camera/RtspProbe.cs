using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace YzControl.Camera;

public static class RtspProbe
{
	internal sealed class RtspResponse
	{
		public int Code;

		public string Reason = "";

		public string Raw = "";

		public string Body = "";

		private readonly Dictionary<string, string> _headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		public string Header(string name)
		{
			if (!_headers.TryGetValue(name, out var value))
			{
				return null;
			}
			return value;
		}

		public void AddHeader(string line)
		{
			int num = line.IndexOf(':');
			if (num > 0)
			{
				_headers[line.Substring(0, num).Trim()] = line.Substring(num + 1).Trim();
			}
		}
	}

	public static List<string> CommonPaths()
	{
		return new List<string>
		{
			"/media/video1", "/media/video2", "/Streaming/Channels/101", "/Streaming/Channels/102", "/cam/realmonitor?channel=1&subtype=0", "/cam/realmonitor?channel=1&subtype=1", "/onvif1", "/onvif2", "/av0_0", "/h264",
			"/live", "/live/ch0", "/ch0_0.h264", "/video1", "/1"
		};
	}

	public static string BuildUrl(string host, int port, string user, string password, string path)
	{
		string text = (path.StartsWith("/") ? path : ("/" + path));
		if (string.IsNullOrEmpty(user))
		{
			return string.Format(CultureInfo.InvariantCulture, "rtsp://{0}:{1}{2}", host, port, text);
		}
		return string.Format(CultureInfo.InvariantCulture, "rtsp://{0}:{1}@{2}:{3}{4}", Uri.EscapeDataString(user), Uri.EscapeDataString(password ?? ""), host, port, text);
	}

	public static RtspProbeResult Probe(string host, int port, string path, string user, string password, int timeoutMs)
	{
		RtspProbeResult rtspProbeResult = new RtspProbeResult();
		rtspProbeResult.Path = path;
		rtspProbeResult.Url = BuildUrl(host, port, user, password, path);
		Socket socket = null;
		try
		{
			socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			IAsyncResult asyncResult = socket.BeginConnect(host, port, null, null);
			if (!asyncResult.AsyncWaitHandle.WaitOne(timeoutMs))
			{
				rtspProbeResult.Error = "连接超时";
				return rtspProbeResult;
			}
			socket.EndConnect(asyncResult);
			socket.ReceiveTimeout = timeoutMs;
			socket.SendTimeout = timeoutMs;
			rtspProbeResult.Reachable = true;
			string text = string.Format(CultureInfo.InvariantCulture, "rtsp://{0}:{1}{2}", host, port, path);
			RtspResponse rtspResponse = Exchange(socket, "OPTIONS", text, 1, null, timeoutMs);
			if (rtspResponse != null)
			{
				rtspProbeResult.Server = rtspResponse.Header("Server");
			}
			RtspResponse rtspResponse2 = Exchange(socket, "DESCRIBE", text, 2, null, timeoutMs);
			if (rtspResponse2 == null)
			{
				rtspProbeResult.Error = "无响应";
				return rtspProbeResult;
			}
			if (rtspResponse2.Code == 200)
			{
				rtspProbeResult.Authorized = true;
				rtspProbeResult.Sdp = rtspResponse2.Body;
				rtspProbeResult.Tracks = ParseTracks(rtspResponse2.Body);
				return rtspProbeResult;
			}
			if (rtspResponse2.Code != 401)
			{
				rtspProbeResult.Error = "DESCRIBE " + rtspResponse2.Code + " " + rtspResponse2.Reason;
				return rtspProbeResult;
			}
			rtspProbeResult.AuthRequired = true;
			string text2 = rtspResponse2.Header("WWW-Authenticate");
			if (text2 == null || text2.IndexOf("Digest", StringComparison.OrdinalIgnoreCase) < 0)
			{
				rtspProbeResult.Error = "不支持的认证方式：" + (text2 ?? "(无挑战头)");
				return rtspProbeResult;
			}
			string text3 = BuildDigest(text2, "DESCRIBE", text, user, password);
			if (text3 == null)
			{
				rtspProbeResult.Error = "无法解析 Digest 挑战";
				return rtspProbeResult;
			}
			RtspResponse rtspResponse3 = Exchange(socket, "DESCRIBE", text, 3, text3, timeoutMs);
			if (rtspResponse3 == null)
			{
				rtspProbeResult.Error = "认证请求无响应（连接被关闭）";
				return rtspProbeResult;
			}
			if (rtspResponse3.Code == 200)
			{
				rtspProbeResult.Authorized = true;
				rtspProbeResult.Sdp = rtspResponse3.Body;
				rtspProbeResult.Tracks = ParseTracks(rtspResponse3.Body);
			}
			else
			{
				rtspProbeResult.Error = "认证后 DESCRIBE " + rtspResponse3.Code + " " + rtspResponse3.Reason;
			}
			return rtspProbeResult;
		}
		catch (Exception ex)
		{
			rtspProbeResult.Error = ex.Message;
			return rtspProbeResult;
		}
		finally
		{
			try
			{
				socket?.Close();
			}
			catch
			{
			}
		}
	}

	public static List<RtspProbeResult> DiscoverStreams(string host, int port, string user, string password, int perTryTimeoutMs, Action<int, int, string> progress)
	{
		List<RtspProbeResult> list = new List<RtspProbeResult>();
		List<string> list2 = CommonPaths();
		for (int i = 0; i < list2.Count; i++)
		{
			RtspProbeResult rtspProbeResult = Probe(host, port, list2[i], user, password, perTryTimeoutMs);
			progress?.Invoke(i + 1, list2.Count, list2[i] + "  →  " + rtspProbeResult.Summary);
			if (rtspProbeResult.Usable)
			{
				list.Add(rtspProbeResult);
			}
			if (list.Count >= 2)
			{
				break;
			}
		}
		return list;
	}

	internal static RtspResponse Exchange(Socket sock, string method, string url, int cseq, string authHeader, int timeoutMs)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(method).Append(' ').Append(url)
			.Append(" RTSP/1.0\r\n");
		stringBuilder.Append("CSeq: ").Append(cseq).Append("\r\n");
		stringBuilder.Append("User-Agent: YzControl\r\n");
		if (authHeader != null)
		{
			stringBuilder.Append(authHeader).Append("\r\n");
		}
		if (method == "DESCRIBE")
		{
			stringBuilder.Append("Accept: application/sdp\r\n");
		}
		stringBuilder.Append("\r\n");
		try
		{
			sock.Send(Encoding.ASCII.GetBytes(stringBuilder.ToString()));
		}
		catch
		{
			return null;
		}
		byte[] array = new byte[4096];
		StringBuilder stringBuilder2 = new StringBuilder();
		int num;
		while ((num = stringBuilder2.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal)) < 0)
		{
			int num2;
			try
			{
				num2 = sock.Receive(array);
			}
			catch
			{
				return null;
			}
			if (num2 <= 0)
			{
				return null;
			}
			stringBuilder2.Append(Encoding.ASCII.GetString(array, 0, num2));
			if (stringBuilder2.Length > 65536)
			{
				break;
			}
		}
		RtspResponse rtspResponse = new RtspResponse();
		string text = stringBuilder2.ToString();
		num = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
		string text2 = ((num >= 0) ? text.Substring(0, num) : text);
		string value = ((num >= 0) ? text.Substring(num + 4) : "");
		string[] array2 = text2.Split(new string[1] { "\r\n" }, StringSplitOptions.None);
		if (array2.Length != 0)
		{
			Match match = Regex.Match(array2[0], "RTSP/\\d\\.\\d\\s+(\\d+)\\s*(.*)");
			if (match.Success)
			{
				rtspResponse.Code = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
				rtspResponse.Reason = match.Groups[2].Value.Trim();
			}
		}
		for (int i = 1; i < array2.Length; i++)
		{
			rtspResponse.AddHeader(array2[i]);
		}
		int result = 0;
		string text3 = rtspResponse.Header("Content-Length");
		if (text3 != null)
		{
			int.TryParse(text3, out result);
		}
		StringBuilder stringBuilder3 = new StringBuilder(value);
		while (stringBuilder3.Length < result)
		{
			int num3;
			try
			{
				num3 = sock.Receive(array);
			}
			catch
			{
				break;
			}
			if (num3 <= 0)
			{
				break;
			}
			stringBuilder3.Append(Encoding.ASCII.GetString(array, 0, num3));
		}
		rtspResponse.Body = stringBuilder3.ToString();
		rtspResponse.Raw = text2 + "\r\n\r\n" + rtspResponse.Body;
		return rtspResponse;
	}

	internal static List<string> ParseTracks(string sdp)
	{
		List<string> list = new List<string>();
		if (string.IsNullOrEmpty(sdp))
		{
			return list;
		}
		string[] array = sdp.Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			if (text.StartsWith("a=rtpmap:", StringComparison.OrdinalIgnoreCase))
			{
				string text2 = text.Substring(9).Trim();
				if (text2.IndexOf("H265", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					list.Add("H265");
				}
				else if (text2.IndexOf("H264", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					list.Add("H264");
				}
				else if (text2.IndexOf("MPEG4", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					list.Add("MPEG4");
				}
				else if (text2.IndexOf("MJPEG", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					list.Add("MJPEG");
				}
				else if (text2.IndexOf("metadata", StringComparison.OrdinalIgnoreCase) < 0)
				{
					list.Add(text2.Split(' ')[0]);
				}
			}
		}
		if (list.Count == 0 && sdp.IndexOf("m=video", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			list.Add("video");
		}
		return list;
	}

	internal static string BuildDigest(string challenge, string method, string uri, string user, string password)
	{
		string text = ExtractParam(challenge, "realm");
		string text2 = ExtractParam(challenge, "nonce");
		if (text == null || text2 == null)
		{
			return null;
		}
		string value = ExtractParam(challenge, "qop");
		string text3 = Md5Hex(user + ":" + text + ":" + password);
		string text4 = Md5Hex(method + ":" + uri);
		StringBuilder stringBuilder = new StringBuilder();
		if (!string.IsNullOrEmpty(value))
		{
			string text5 = "00000001";
			string text6 = Guid.NewGuid().ToString("N").Substring(0, 16);
			string text7 = Md5Hex(text3 + ":" + text2 + ":" + text5 + ":" + text6 + ":auth:" + text4);
			stringBuilder.AppendFormat(CultureInfo.InvariantCulture, "Authorization: Digest username=\"{0}\", realm=\"{1}\", nonce=\"{2}\", uri=\"{3}\", response=\"{4}\", qop=auth, nc={5}, cnonce=\"{6}\"", user, text, text2, uri, text7, text5, text6);
		}
		else
		{
			string text7 = Md5Hex(text3 + ":" + text2 + ":" + text4);
			stringBuilder.AppendFormat(CultureInfo.InvariantCulture, "Authorization: Digest username=\"{0}\", realm=\"{1}\", nonce=\"{2}\", uri=\"{3}\", response=\"{4}\"", user, text, text2, uri, text7);
		}
		return stringBuilder.ToString();
	}

	private static string ExtractParam(string challenge, string key)
	{
		Match match = Regex.Match(challenge, key + "\\s*=\\s*\"?([^\",\\s]+)\"?", RegexOptions.IgnoreCase);
		if (!match.Success)
		{
			return null;
		}
		return match.Groups[1].Value;
	}

	internal static string Md5Hex(string s)
	{
		using MD5 mD = MD5.Create();
		byte[] array = mD.ComputeHash(Encoding.UTF8.GetBytes(s));
		StringBuilder stringBuilder = new StringBuilder(array.Length * 2);
		for (int i = 0; i < array.Length; i++)
		{
			stringBuilder.Append(array[i].ToString("x2"));
		}
		return stringBuilder.ToString();
	}
}
