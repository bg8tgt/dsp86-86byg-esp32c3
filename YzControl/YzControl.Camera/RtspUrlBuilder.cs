using System;
using System.Collections.Generic;

namespace YzControl.Camera;

public static class RtspUrlBuilder
{
	public static List<string> Candidates(string vendor, string ip, int port, string user, string password, int channel, bool main)
	{
		List<string> list = new List<string>();
		if (string.IsNullOrEmpty(ip))
		{
			return list;
		}
		if (port <= 0)
		{
			port = 554;
		}
		if (channel <= 0)
		{
			channel = 1;
		}
		string text = (vendor ?? "").Trim().ToLowerInvariant();
		bool num = text.Length == 0 || text == "auto";
		List<string> list2 = new List<string>();
		if (!num)
		{
			switch (text)
			{
			case "hik":
			case "hikvision":
			case "海康":
				break;
			default:
				goto IL_008d;
			}
		}
		list2.AddRange(HikPaths(channel, main));
		goto IL_008d;
		IL_00d3:
		if (!num)
		{
			switch (text)
			{
			case "dahua":
			case "dh":
			case "大华":
				break;
			default:
				goto IL_010c;
			}
		}
		list2.AddRange(DahuaPaths(channel, main));
		goto IL_010c;
		IL_010c:
		if (!num)
		{
			switch (text)
			{
			case "generic":
			case "other":
			case "通用":
				break;
			default:
				goto IL_0144;
			}
		}
		list2.AddRange(GenericPaths(channel, main));
		goto IL_0144;
		IL_0144:
		if (list2.Count == 0)
		{
			list2.AddRange(GenericPaths(channel, main));
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		for (int i = 0; i < list2.Count; i++)
		{
			string text2 = BuildRtsp(ip, port, user, password, list2[i]);
			if (text2.Length != 0 && hashSet.Add(text2))
			{
				list.Add(text2);
			}
		}
		return list;
		IL_008d:
		if (!num)
		{
			switch (text)
			{
			case "uniview":
			case "unv":
			case "yz":
			case "宇视":
				break;
			default:
				goto IL_00d3;
			}
		}
		list2.AddRange(UniviewPaths(channel, main));
		goto IL_00d3;
	}

	public static string BuildRtsp(string ip, int port, string user, string password, string path)
	{
		if (string.IsNullOrEmpty(ip))
		{
			return "";
		}
		if (port <= 0)
		{
			port = 554;
		}
		if (string.IsNullOrEmpty(path))
		{
			path = "/";
		}
		if (!path.StartsWith("/", StringComparison.Ordinal))
		{
			path = "/" + path;
		}
		string text = "";
		if (!string.IsNullOrEmpty(user))
		{
			text = Uri.EscapeDataString(user) + ":" + Uri.EscapeDataString(password ?? "") + "@";
		}
		return "rtsp://" + text + ip.Trim() + ":" + port + path;
	}

	public static bool IsUrl(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return false;
		}
		string text = s.Trim();
		if (!text.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("rtsps://", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
		{
			return text.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static List<string> HikPaths(int channel, bool main)
	{
		return new List<string> { "/Streaming/Channels/" + channel + (main ? "01" : "02") };
	}

	private static List<string> UniviewPaths(int channel, bool main)
	{
		List<string> list = new List<string>();
		if (channel <= 1)
		{
			list.Add(main ? "/media/video1" : "/media/video2");
		}
		else
		{
			list.Add("/media/video" + channel);
		}
		return list;
	}

	private static List<string> DahuaPaths(int channel, bool main)
	{
		return new List<string> { "/cam/realmonitor?channel=" + channel + "&subtype=" + (main ? "0" : "1") };
	}

	private static List<string> GenericPaths(int channel, bool main)
	{
		List<string> list = new List<string>();
		list.Add(main ? "/onvif1" : "/onvif2");
		list.Add(main ? "/stream1" : "/stream2");
		list.Add(main ? "/live/ch0" : "/live/ch1");
		list.Add("/h264/ch" + channel + (main ? "/main" : "/sub") + "/av_stream");
		list.AddRange(HikPaths(channel, main));
		list.AddRange(DahuaPaths(channel, main));
		list.AddRange(UniviewPaths(channel, main));
		return list;
	}
}
