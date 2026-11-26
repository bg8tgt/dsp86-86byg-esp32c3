using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace YzControl.Camera;

public static class RtspScanner
{
	public static List<string> LocalSubnetPrefixes()
	{
		List<string> list = new List<string>();
		try
		{
			NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
			foreach (NetworkInterface networkInterface in allNetworkInterfaces)
			{
				if (networkInterface.OperationalStatus != OperationalStatus.Up || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
				{
					continue;
				}
				foreach (UnicastIPAddressInformation unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
				{
					IPAddress address = unicastAddress.Address;
					if (address.AddressFamily != AddressFamily.InterNetwork)
					{
						continue;
					}
					string text = address.ToString();
					if (text.StartsWith("169.254.") || text.StartsWith("127."))
					{
						continue;
					}
					string[] array = text.Split('.');
					if (array.Length == 4)
					{
						string item = array[0] + "." + array[1] + "." + array[2];
						if (!list.Contains(item))
						{
							list.Add(item);
						}
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	public static string PrimarySubnetPrefix()
	{
		List<string> list = LocalSubnetPrefixes();
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].StartsWith("192.168."))
			{
				return list[i];
			}
		}
		if (list.Count <= 0)
		{
			return "";
		}
		return list[0];
	}

	public static List<string> ScanSubnet(string prefix, int port, int timeoutMs, int parallelism, Action<int, int, string> progress)
	{
		List<string> list = new List<string>();
		for (int i = 1; i <= 254; i++)
		{
			list.Add(prefix + "." + i.ToString(CultureInfo.InvariantCulture));
		}
		return ScanHosts(list, port, timeoutMs, parallelism, progress);
	}

	public static List<string> ScanHosts(List<string> hosts, int port, int timeoutMs, int parallelism, Action<int, int, string> progress)
	{
		List<string> found = new List<string>();
		object gate = new object();
		int done = 0;
		int total = hosts.Count;
		if (parallelism < 1)
		{
			parallelism = 32;
		}
		try
		{
			Parallel.ForEach(hosts, new ParallelOptions
			{
				MaxDegreeOfParallelism = parallelism
			}, delegate(string host)
			{
				bool flag = IsPortOpen(host, port, timeoutMs);
				int num = Interlocked.Increment(ref done);
				if (flag)
				{
					lock (gate)
					{
						found.Add(host);
					}
				}
				if (progress != null && (num % 8 == 0 || num == total))
				{
					progress(num, total, host + (flag ? ("  ✔ 开放 " + port) : ""));
				}
			});
		}
		catch
		{
		}
		lock (gate)
		{
			found.Sort(CompareIp);
			return new List<string>(found);
		}
	}

	public static bool IsPortOpen(string host, int port, int timeoutMs)
	{
		Socket socket = null;
		try
		{
			socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			IAsyncResult asyncResult = socket.BeginConnect(host, port, null, null);
			if (!asyncResult.AsyncWaitHandle.WaitOne(timeoutMs))
			{
				return false;
			}
			socket.EndConnect(asyncResult);
			return true;
		}
		catch
		{
			return false;
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

	private static int CompareIp(string a, string b)
	{
		string[] array = a.Split('.');
		string[] array2 = b.Split('.');
		for (int i = 0; i < 4 && i < array.Length && i < array2.Length; i++)
		{
			int.TryParse(array[i], out var result);
			int.TryParse(array2[i], out var result2);
			if (result != result2)
			{
				return result.CompareTo(result2);
			}
		}
		return 0;
	}
}
