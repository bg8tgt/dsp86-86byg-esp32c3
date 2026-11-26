using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace YzControl;

public static class S3Discovery
{
	public const int UdpPort = 60059;

	public const string HotspotBoardIp = "192.168.4.1";

	public const string DefaultBoardIp = "172.16.1.122";

	public static readonly string[] HistoricalBoardTails = new string[2] { "172.16.1.122", "172.16.1.128" };

	private static readonly List<string> sLearned = new List<string>();

	public const string DefaultRigIp = "172.16.1.123";

	public const int DefaultRigPort = 4001;

	public const string KnownIdPrefix = "1051DB3D";

	public const string KnownFullId = "1051DB3D2124";

	public static void RememberBoardIp(string ip)
	{
		if (!IsUsableIp(ip))
		{
			return;
		}
		string text = ip.Trim();
		if (text.StartsWith("127.", StringComparison.Ordinal))
		{
			return;
		}
		lock (sLearned)
		{
			if (sLearned.IndexOf(text) < 0)
			{
				sLearned.Add(text);
			}
		}
	}

	public static string[] LearnedBoardIps()
	{
		lock (sLearned)
		{
			return sLearned.ToArray();
		}
	}

	public static void ClearLearnedBoardIps()
	{
		lock (sLearned)
		{
			sLearned.Clear();
		}
	}

	public static bool IsUsableIp(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return false;
		}
		string text = s.Trim();
		string[] array = text.Split('.');
		if (array.Length != 4)
		{
			return false;
		}
		for (int i = 0; i < 4; i++)
		{
			string text2 = array[i];
			if (text2.Length == 0 || text2.Length > 3)
			{
				return false;
			}
			if (text2.Length > 1 && text2[0] == '0')
			{
				return false;
			}
			for (int j = 0; j < text2.Length; j++)
			{
				if (text2[j] < '0' || text2[j] > '9')
				{
					return false;
				}
			}
			if (!int.TryParse(text2, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				return false;
			}
			if (result < 0 || result > 255)
			{
				return false;
			}
		}
		if (!IPAddress.TryParse(text, out var address))
		{
			return false;
		}
		if (address.AddressFamily != AddressFamily.InterNetwork)
		{
			return false;
		}
		byte[] addressBytes = address.GetAddressBytes();
		if (addressBytes[0] == 0 && addressBytes[1] == 0 && addressBytes[2] == 0 && addressBytes[3] == 0)
		{
			return false;
		}
		if (addressBytes[0] == byte.MaxValue && addressBytes[1] == byte.MaxValue && addressBytes[2] == byte.MaxValue && addressBytes[3] == byte.MaxValue)
		{
			return false;
		}
		return true;
	}

	public static bool IsUsableHostName(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return false;
		}
		string text = s.Trim();
		if (text != s)
		{
			return false;
		}
		if (text.Length == 0 || text.Length > 253)
		{
			return false;
		}
		foreach (char c in text)
		{
			if ((c < '0' || c > '9') && (c < 'a' || c > 'z') && (c < 'A' || c > 'Z') && c != '.' && c != '-' && c != '_')
			{
				return false;
			}
		}
		if (text[0] != '.')
		{
			return text[text.Length - 1] != '.';
		}
		return false;
	}

	public static string ReverseIdBytes(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return "";
		}
		string text = id.Trim();
		if (((uint)text.Length & (true ? 1u : 0u)) != 0)
		{
			return text;
		}
		char[] array = new char[text.Length];
		int num = text.Length / 2;
		for (int i = 0; i < num; i++)
		{
			array[i * 2] = text[(num - 1 - i) * 2];
			array[i * 2 + 1] = text[(num - 1 - i) * 2 + 1];
		}
		return new string(array);
	}

	public static bool IdLooksLikeBoard(string id, string prefix, string fullId)
	{
		if (string.IsNullOrEmpty(id))
		{
			return false;
		}
		string text = id.Trim().ToUpperInvariant();
		string s = ReverseIdBytes(text).ToUpperInvariant();
		if (!IdHit(text, prefix, fullId))
		{
			return IdHit(s, prefix, fullId);
		}
		return true;
	}

	private static bool IdHit(string s, string prefix, string fullId)
	{
		if (!string.IsNullOrEmpty(fullId) && s.IndexOf(fullId.ToUpperInvariant(), StringComparison.Ordinal) >= 0)
		{
			return true;
		}
		if (!string.IsNullOrEmpty(prefix) && s.IndexOf(prefix.ToUpperInvariant(), StringComparison.Ordinal) >= 0)
		{
			return true;
		}
		return false;
	}

	public static string[] BroadcastTargets()
	{
		List<string> list = new List<string>();
		AddUnique(list, "255.255.255.255");
		try
		{
			NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
			foreach (NetworkInterface networkInterface in allNetworkInterfaces)
			{
				try
				{
					if (networkInterface.OperationalStatus != OperationalStatus.Up || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
					{
						continue;
					}
					IPInterfaceProperties iPProperties = networkInterface.GetIPProperties();
					if (iPProperties == null)
					{
						continue;
					}
					foreach (UnicastIPAddressInformation unicastAddress in iPProperties.UnicastAddresses)
					{
						if (unicastAddress == null || unicastAddress.Address == null || unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork)
						{
							continue;
						}
						IPAddress iPv4Mask = unicastAddress.IPv4Mask;
						if (iPv4Mask == null)
						{
							continue;
						}
						byte[] addressBytes = unicastAddress.Address.GetAddressBytes();
						byte[] addressBytes2 = iPv4Mask.GetAddressBytes();
						if (addressBytes.Length == 4 && addressBytes2.Length == 4 && (addressBytes2[0] != 0 || addressBytes2[1] != 0 || addressBytes2[2] != 0 || addressBytes2[3] != 0))
						{
							byte[] array = new byte[4];
							for (int j = 0; j < 4; j++)
							{
								array[j] = (byte)(addressBytes[j] | (byte)(~addressBytes2[j]));
							}
							string text = new IPAddress(array).ToString();
							if (text != unicastAddress.Address.ToString())
							{
								AddUnique(list, text);
							}
						}
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		return list.ToArray();
	}

	private static void AddUnique(List<string> l, string s)
	{
		if (!string.IsNullOrEmpty(s) && l.IndexOf(s) < 0)
		{
			l.Add(s);
		}
	}

	public static bool TryParsePong(string raw, string srcIp, string knownPrefix, string knownFullId, out S3Pong p)
	{
		p = null;
		if (string.IsNullOrEmpty(raw))
		{
			return false;
		}
		if (raw.IndexOf("cmd=pong", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return false;
		}
		S3Pong s3Pong = new S3Pong();
		s3Pong.SourceIp = srcIp ?? "";
		s3Pong.Raw = ((raw.Length > 200) ? raw.Substring(0, 200) : raw);
		string[] array = raw.Split('&');
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			int num = text.IndexOf('=');
			if (num <= 0)
			{
				continue;
			}
			string text2 = text.Substring(0, num).Trim().ToLowerInvariant();
			string text3 = text.Substring(num + 1).Trim();
			switch (text2)
			{
			case "ip":
			{
				if (IPAddress.TryParse(text3, out var _) && IsUsableIp(text3))
				{
					s3Pong.ReportedIp = text3;
				}
				break;
			}
			case "id":
				s3Pong.Id = text3;
				break;
			case "port":
				s3Pong.Port = text3;
				break;
			}
		}
		s3Pong.IdMatched = IdLooksLikeBoard(s3Pong.Id, knownPrefix, knownFullId);
		p = s3Pong;
		return true;
	}

	private static int SendPing(UdpClient u, byte[] ping, string[] targets, int port)
	{
		int num = 0;
		for (int i = 0; i < targets.Length; i++)
		{
			if (IPAddress.TryParse((targets[i] ?? "").Trim(), out var address) && address.AddressFamily == AddressFamily.InterNetwork)
			{
				try
				{
					u.Send(ping, ping.Length, new IPEndPoint(address, port));
					num++;
				}
				catch
				{
				}
			}
		}
		return num;
	}

	public static S3DiscoverOutcome UdpDiscover(S3DiscoverOptions o)
	{
		if (o == null)
		{
			o = new S3DiscoverOptions();
		}
		S3DiscoverOutcome s3DiscoverOutcome = new S3DiscoverOutcome();
		string[] array = ((o.Targets != null && o.Targets.Length != 0) ? o.Targets : BroadcastTargets());
		o.SentTo = string.Join(",", array);
		UdpClient udpClient = null;
		try
		{
			udpClient = new UdpClient();
			udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);
			udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
			udpClient.EnableBroadcast = true;
			byte[] bytes = Encoding.ASCII.GetBytes("cmd=ping");
			if (SendPing(udpClient, bytes, array, o.UdpPort) == 0)
			{
				s3DiscoverOutcome.Detail = "没有可用的广播目标（本机没有活动的 IPv4 网卡？）";
				return s3DiscoverOutcome;
			}
			int num = Math.Max(50, o.TimeoutMs);
			Stopwatch stopwatch = Stopwatch.StartNew();
			bool flag = false;
			Dictionary<string, S3Pong> dictionary = new Dictionary<string, S3Pong>(StringComparer.OrdinalIgnoreCase);
			while (true)
			{
				int num2 = num - (int)stopwatch.ElapsedMilliseconds;
				if (num2 <= 0)
				{
					break;
				}
				if (!flag && dictionary.Count == 0 && stopwatch.ElapsedMilliseconds >= num / 2)
				{
					flag = true;
					SendPing(udpClient, bytes, array, o.UdpPort);
				}
				int num3 = num2;
				if (!flag && dictionary.Count == 0)
				{
					int num4 = num / 2 - (int)stopwatch.ElapsedMilliseconds;
					if (num4 > 0 && num4 < num3)
					{
						num3 = num4;
					}
				}
				if (num3 < 1)
				{
					num3 = 1;
				}
				try
				{
					udpClient.Client.ReceiveTimeout = num3;
				}
				catch
				{
				}
				IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
				byte[] array2;
				try
				{
					array2 = udpClient.Receive(ref remoteEP);
				}
				catch (SocketException ex)
				{
					if (ex.SocketErrorCode == SocketError.TimedOut)
					{
						continue;
					}
					break;
				}
				catch
				{
					break;
				}
				if (array2 == null || array2.Length == 0)
				{
					continue;
				}
				string @string = Encoding.ASCII.GetString(array2);
				string srcIp = ((remoteEP != null && remoteEP.Address != null) ? remoteEP.Address.ToString() : "");
				if (!TryParsePong(@string, srcIp, o.KnownIdPrefix, o.KnownFullId, out var p))
				{
					continue;
				}
				string key = ((p.SourceIp.Length > 0) ? p.SourceIp : ("?" + s3DiscoverOutcome.All.Count));
				if (dictionary.ContainsKey(key))
				{
					continue;
				}
				dictionary[key] = p;
				if (p.IdMatched)
				{
					int num5 = (int)stopwatch.ElapsedMilliseconds + 150;
					if (num5 < num)
					{
						num = num5;
					}
				}
			}
			foreach (KeyValuePair<string, S3Pong> item in dictionary)
			{
				s3DiscoverOutcome.All.Add(item.Value);
			}
			s3DiscoverOutcome.All.Sort((S3Pong x, S3Pong y) => (x.IdMatched != y.IdMatched) ? ((!x.IdMatched) ? 1 : (-1)) : string.CompareOrdinal(x.SourceIp, y.SourceIp));
			if (s3DiscoverOutcome.All.Count == 0)
			{
				s3DiscoverOutcome.Detail = "广播已发出（" + o.SentTo + "），" + Math.Max(50, o.TimeoutMs) + "ms 内无任何应答";
				return s3DiscoverOutcome;
			}
			S3Pong s3Pong = null;
			for (int i = 0; i < s3DiscoverOutcome.All.Count; i++)
			{
				if (s3DiscoverOutcome.All[i].IdMatched)
				{
					s3Pong = s3DiscoverOutcome.All[i];
					break;
				}
			}
			bool flag2 = s3Pong != null;
			if (s3Pong == null)
			{
				if (s3DiscoverOutcome.All.Count != 1)
				{
					s3DiscoverOutcome.Detail = "收到 " + s3DiscoverOutcome.All.Count + " 个应答但都不匹配本机板子的 efuse MAC → 全部忽略（避免连错设备）";
					return s3DiscoverOutcome;
				}
				s3Pong = s3DiscoverOutcome.All[0];
			}
			string bestIp = s3Pong.BestIp;
			if (!IsUsableIp(bestIp))
			{
				s3DiscoverOutcome.Detail = "应答里既没有可用源地址也没有可用 ip= 字段（原始：" + s3Pong.Raw + "）";
				return s3DiscoverOutcome;
			}
			s3DiscoverOutcome.Ip = bestIp;
			s3DiscoverOutcome.Id = s3Pong.Id;
			s3DiscoverOutcome.Port = s3Pong.Port;
			s3DiscoverOutcome.IdMatched = s3Pong.IdMatched;
			s3DiscoverOutcome.Detail = (flag2 ? ("UDP 发现命中（id=" + s3Pong.Id + " 匹配本机板子 MAC" + ((s3Pong.ReportedIp.Length > 0 && s3Pong.ReportedIp != s3Pong.SourceIp) ? ("，报文 ip=" + s3Pong.ReportedIp) : "") + "）") : ("UDP 发现：全场唯一应答（id=" + s3Pong.Id + " 未匹配已知 MAC 前缀，已按唯一应答采用）"));
			return s3DiscoverOutcome;
		}
		catch (Exception ex2)
		{
			s3DiscoverOutcome.Detail = "UDP 发现异常：" + ex2.Message;
			return s3DiscoverOutcome;
		}
		finally
		{
			try
			{
				udpClient?.Close();
			}
			catch
			{
			}
		}
	}

	public static bool HttpProbe(string ip, int port, int timeoutMs, out string detail)
	{
		detail = "";
		if (!IsUsableIp(ip))
		{
			detail = "地址不合法";
			return false;
		}
		if (timeoutMs < 50)
		{
			timeoutMs = 50;
		}
		TcpClient tcpClient = new TcpClient();
		try
		{
			tcpClient.NoDelay = true;
			IAsyncResult asyncResult = tcpClient.BeginConnect(ip, port, null, null);
			if (!asyncResult.AsyncWaitHandle.WaitOne(timeoutMs, exitContext: false))
			{
				detail = "连接超时(" + timeoutMs + "ms)";
				return false;
			}
			tcpClient.EndConnect(asyncResult);
			NetworkStream stream = tcpClient.GetStream();
			stream.ReadTimeout = timeoutMs;
			stream.WriteTimeout = timeoutMs;
			byte[] bytes = Encoding.ASCII.GetBytes("GET /sys HTTP/1.0\r\nHost: " + ip + "\r\nUser-Agent: YzControl/discover\r\nConnection: close\r\n\r\n");
			stream.Write(bytes, 0, bytes.Length);
			byte[] array = new byte[512];
			int num = 0;
			while (num < array.Length)
			{
				int num2 = 0;
				try
				{
					num2 = stream.Read(array, num, array.Length - num);
				}
				catch
				{
					num2 = 0;
				}
				if (num2 <= 0)
				{
					break;
				}
				num += num2;
				if (Array.IndexOf(array, (byte)10, 0, num) >= 0)
				{
					break;
				}
			}
			if (num <= 0)
			{
				detail = "已连上但无应答";
				return false;
			}
			string @string = Encoding.ASCII.GetString(array, 0, num);
			int num3 = @string.IndexOf('\n');
			string text = ((num3 > 0) ? @string.Substring(0, num3) : @string).Trim();
			if (!text.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
			{
				detail = "非 HTTP 应答：" + text;
				return false;
			}
			detail = text;
			return text.IndexOf(" 200", StringComparison.Ordinal) > 0;
		}
		catch (Exception ex)
		{
			detail = ex.Message;
			return false;
		}
		finally
		{
			try
			{
				tcpClient.Close();
			}
			catch
			{
			}
		}
	}

	public static List<string> CandidateIps(string memory, string manual, string boardIp)
	{
		List<string> list = new List<string>();
		AddCandidate(list, memory);
		AddCandidate(list, manual);
		string[] array = LearnedBoardIps();
		foreach (string ip in array)
		{
			AddCandidate(list, ip);
		}
		AddCandidate(list, boardIp);
		array = HistoricalBoardTails;
		foreach (string peer in array)
		{
			AddSubnetPeers(list, boardIp, peer);
		}
		try
		{
			NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
			for (int j = 0; j < allNetworkInterfaces.Length; j++)
			{
				try
				{
					if (allNetworkInterfaces[j].OperationalStatus != OperationalStatus.Up || allNetworkInterfaces[j].NetworkInterfaceType == NetworkInterfaceType.Loopback)
					{
						continue;
					}
					foreach (UnicastIPAddressInformation unicastAddress in allNetworkInterfaces[j].GetIPProperties().UnicastAddresses)
					{
						if (unicastAddress != null && unicastAddress.Address != null && unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
						{
							array = HistoricalBoardTails;
							foreach (string peer2 in array)
							{
								AddSubnetPeers(list, unicastAddress.Address.ToString(), peer2);
							}
						}
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		array = HistoricalBoardTails;
		foreach (string ip2 in array)
		{
			AddCandidate(list, ip2);
		}
		AddCandidate(list, "192.168.4.1");
		return list;
	}

	private static void AddSubnetPeers(List<string> l, string ip, string peer)
	{
		if (TryBytes(ip, out var b) && TryBytes(peer, out var b2))
		{
			AddCandidate(l, b[0] + "." + b[1] + "." + b[2] + "." + b2[3]);
		}
	}

	private static bool TryBytes(string s, out byte[] b)
	{
		b = null;
		if (!IPAddress.TryParse((s ?? "").Trim(), out var address))
		{
			return false;
		}
		if (address.AddressFamily != AddressFamily.InterNetwork)
		{
			return false;
		}
		b = address.GetAddressBytes();
		return b.Length == 4;
	}

	private static void AddCandidate(List<string> l, string ip)
	{
		if (IsUsableIp(ip))
		{
			string item = ip.Trim();
			if (l.IndexOf(item) < 0)
			{
				l.Add(item);
			}
		}
	}

	public static string ProbeFirst(List<string> candidates, int port, int timeoutMs, out string detail)
	{
		detail = "";
		if (candidates == null)
		{
			return "";
		}
		for (int i = 0; i < candidates.Count; i++)
		{
			if (HttpProbe(candidates[i], port, timeoutMs, out var detail2))
			{
				detail = candidates[i] + " " + detail2;
				return candidates[i];
			}
		}
		detail = "候选 " + candidates.Count + " 个地址均无 HTTP 200 /sys 应答";
		return "";
	}

	public static S3ResolveResult Resolve(S3ResolveInputs inp)
	{
		if (inp == null)
		{
			inp = new S3ResolveInputs();
		}
		S3ResolveResult s3ResolveResult = new S3ResolveResult();
		string text = (inp.Memory ?? "").Trim();
		if (IsUsableIp(text))
		{
			bool flag = true;
			if (inp.Probe != null)
			{
				flag = inp.Probe(text);
				if (!flag)
				{
					s3ResolveResult.MemoryProbeFailed = true;
				}
			}
			if (flag)
			{
				s3ResolveResult.Ip = text;
				s3ResolveResult.Source = S3AddressSource.Memory;
				s3ResolveResult.Detail = "沿用上次成功地址";
				return s3ResolveResult;
			}
		}
		else if (IsUsableHostName(text))
		{
			s3ResolveResult.Ip = text;
			s3ResolveResult.Source = S3AddressSource.Memory;
			s3ResolveResult.Detail = "沿用上次成功地址（主机名）";
			return s3ResolveResult;
		}
		if (inp.Discover != null)
		{
			s3ResolveResult.UdpAttempted = true;
			S3DiscoverOutcome s3DiscoverOutcome = null;
			try
			{
				s3DiscoverOutcome = inp.Discover();
			}
			catch (Exception ex)
			{
				s3DiscoverOutcome = null;
				s3ResolveResult.Detail = "发现异常：" + ex.Message;
			}
			if (s3DiscoverOutcome != null)
			{
				s3ResolveResult.Responders = s3DiscoverOutcome.All.Count;
				if (s3DiscoverOutcome.Found)
				{
					s3ResolveResult.Ip = s3DiscoverOutcome.Ip;
					s3ResolveResult.Source = S3AddressSource.Udp;
					s3ResolveResult.Detail = s3DiscoverOutcome.Detail;
					s3ResolveResult.ChangedFromMemory = IsUsableIp(text) && !string.Equals(text, s3DiscoverOutcome.Ip, StringComparison.OrdinalIgnoreCase);
					return s3ResolveResult;
				}
				if (s3DiscoverOutcome.Detail.Length > 0)
				{
					s3ResolveResult.Detail = s3DiscoverOutcome.Detail;
				}
			}
		}
		string text2 = (inp.Manual ?? "").Trim();
		if (IsUsableIp(text2) || IsUsableHostName(text2))
		{
			s3ResolveResult.Ip = text2;
			s3ResolveResult.Source = S3AddressSource.Manual;
			s3ResolveResult.Detail = "按用户手填地址";
			s3ResolveResult.ChangedFromMemory = IsUsableIp(text) && !string.Equals(text, text2, StringComparison.OrdinalIgnoreCase);
			return s3ResolveResult;
		}
		string b = (s3ResolveResult.Ip = (IsUsableIp(inp.HotspotIp) ? inp.HotspotIp.Trim() : "192.168.4.1"));
		s3ResolveResult.Source = S3AddressSource.Hotspot;
		s3ResolveResult.ChangedFromMemory = IsUsableIp(text) && !string.Equals(text, b, StringComparison.OrdinalIgnoreCase);
		s3ResolveResult.Detail = "未发现板子，已回退热点直连地址";
		s3ResolveResult.Hint = FailureHint();
		return s3ResolveResult;
	}

	public static string FailureHint()
	{
		return "未发现板子：① 确认与板子同网；② 或连板子热点后用 192.168.4.1；③ 或在下面手填地址";
	}

	public static List<string> RigCandidateIps(string memory, string boardIp)
	{
		List<string> list = new List<string>();
		AddCandidate(list, memory);
		AddSubnetPeers(list, boardIp, "172.16.1.123");
		try
		{
			NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
			for (int i = 0; i < allNetworkInterfaces.Length; i++)
			{
				try
				{
					if (allNetworkInterfaces[i].OperationalStatus != OperationalStatus.Up || allNetworkInterfaces[i].NetworkInterfaceType == NetworkInterfaceType.Loopback)
					{
						continue;
					}
					foreach (UnicastIPAddressInformation unicastAddress in allNetworkInterfaces[i].GetIPProperties().UnicastAddresses)
					{
						if (unicastAddress != null && unicastAddress.Address != null && unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
						{
							AddSubnetPeers(list, unicastAddress.Address.ToString(), "172.16.1.123");
						}
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		AddCandidate(list, "172.16.1.123");
		return list;
	}

	public static string DiscoverRig(string memory, string boardIp, int port, int timeoutMs, out string detail)
	{
		detail = "";
		List<string> list = RigCandidateIps(memory, boardIp);
		for (int i = 0; i < list.Count; i++)
		{
			if (TcpOpen(list[i], port, timeoutMs))
			{
				detail = list[i] + ":" + port + " 可连接";
				return list[i];
			}
		}
		detail = "候选 " + list.Count + " 个地址的 " + port + " 端口都连不上";
		return "";
	}

	public static bool TcpOpen(string ip, int port, int timeoutMs)
	{
		if (!IsUsableIp(ip))
		{
			return false;
		}
		if (timeoutMs < 50)
		{
			timeoutMs = 50;
		}
		TcpClient tcpClient = new TcpClient();
		try
		{
			IAsyncResult asyncResult = tcpClient.BeginConnect(ip, port, null, null);
			if (!asyncResult.AsyncWaitHandle.WaitOne(timeoutMs, exitContext: false))
			{
				return false;
			}
			tcpClient.EndConnect(asyncResult);
			return tcpClient.Connected;
		}
		catch
		{
			return false;
		}
		finally
		{
			try
			{
				tcpClient.Close();
			}
			catch
			{
			}
		}
	}

	public static string SplitHost(string ipPort, out int port)
	{
		port = 0;
		string text = (ipPort ?? "").Trim();
		int num = text.IndexOf(':');
		if (num <= 0)
		{
			return text;
		}
		if (int.TryParse(text.Substring(num + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			port = result;
		}
		return text.Substring(0, num);
	}
}
