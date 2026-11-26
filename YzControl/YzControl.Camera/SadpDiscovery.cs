using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace YzControl.Camera;

public static class SadpDiscovery
{
	private const string MulticastGroup = "239.255.255.250";

	private const int SadpPort = 37020;

	private const int SioUdpConnReset = -1744830452;

	public static List<DiscoveredCamera> Discover(int timeoutMs)
	{
		if (timeoutMs <= 0)
		{
			timeoutMs = 2000;
		}
		Dictionary<string, DiscoveredCamera> dictionary = new Dictionary<string, DiscoveredCamera>(StringComparer.OrdinalIgnoreCase);
		UdpClient udpClient = null;
		try
		{
			byte[] bytes = Encoding.UTF8.GetBytes(BuildProbeXml());
			IPAddress iPAddress = IPAddress.Parse("239.255.255.250");
			IPEndPoint target = new IPEndPoint(iPAddress, 37020);
			udpClient = new UdpClient();
			udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);
			udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
			udpClient.Client.ReceiveTimeout = 300;
			Prepare(udpClient, iPAddress);
			TrySend(udpClient, bytes, target);
			try
			{
				udpClient.EnableBroadcast = true;
				TrySend(udpClient, bytes, new IPEndPoint(IPAddress.Broadcast, 37020));
			}
			catch
			{
			}
			DateTime utcNow = DateTime.UtcNow;
			DateTime dateTime = utcNow;
			while ((DateTime.UtcNow - utcNow).TotalMilliseconds < (double)timeoutMs)
			{
				IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
				byte[] data;
				try
				{
					data = udpClient.Receive(ref remoteEP);
				}
				catch (SocketException ex)
				{
					if (ex.SocketErrorCode != SocketError.TimedOut && ex.SocketErrorCode != SocketError.WouldBlock)
					{
						Thread.Sleep(20);
					}
					if ((DateTime.UtcNow - dateTime).TotalMilliseconds >= 500.0)
					{
						TrySend(udpClient, bytes, target);
						dateTime = DateTime.UtcNow;
					}
					continue;
				}
				catch (ObjectDisposedException)
				{
					break;
				}
				ParseResponses(SafeUtf8(data), remoteEP, dictionary);
			}
		}
		catch
		{
		}
		finally
		{
			if (udpClient != null)
			{
				try
				{
					udpClient.Close();
				}
				catch
				{
				}
			}
		}
		return new List<DiscoveredCamera>(dictionary.Values);
	}

	private static string BuildProbeXml()
	{
		return "<?xml version=\"1.0\" encoding=\"utf-8\"?><Probe><Uuid>" + Guid.NewGuid().ToString("N") + "</Uuid><Types>inquiry</Types></Probe>";
	}

	private static void ParseResponses(string xml, IPEndPoint remote, Dictionary<string, DiscoveredCamera> map)
	{
		if (string.IsNullOrEmpty(xml))
		{
			return;
		}
		XmlDocument xmlDocument = null;
		try
		{
			xmlDocument = new XmlDocument();
			xmlDocument.LoadXml(xml);
		}
		catch
		{
			xmlDocument = null;
		}
		if (xmlDocument != null)
		{
			List<XmlElement> list = new List<XmlElement>();
			CollectByLocalName(xmlDocument, "ProbeMatch", list);
			if (list.Count == 0 && xmlDocument.DocumentElement != null)
			{
				if (FirstElement(xmlDocument, "IPv4Address") == null && FirstElement(xmlDocument, "MAC") == null)
				{
					return;
				}
				list.Add(xmlDocument.DocumentElement);
			}
			bool flag = false;
			foreach (XmlElement item in list)
			{
				DiscoveredCamera discoveredCamera = FromElement(item, remote);
				if (discoveredCamera != null)
				{
					Merge(map, discoveredCamera);
					flag = true;
				}
			}
			if (flag)
			{
				return;
			}
		}
		DiscoveredCamera discoveredCamera2 = FromRawText(xml, remote);
		if (discoveredCamera2 != null)
		{
			Merge(map, discoveredCamera2);
		}
	}

	private static DiscoveredCamera FromElement(XmlElement el, IPEndPoint remote)
	{
		string text = FirstText(el, "IPv4Address");
		if (string.IsNullOrEmpty(text))
		{
			text = FirstText(el, "IPAddress");
		}
		if (string.IsNullOrEmpty(text))
		{
			text = FirstText(el, "IPv6Address");
		}
		if (string.IsNullOrEmpty(text) && remote != null)
		{
			text = remote.Address.ToString();
		}
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		string text2 = FirstText(el, "DeviceType");
		if (string.IsNullOrEmpty(text2))
		{
			text2 = FirstText(el, "Model");
		}
		string text3 = FirstText(el, "DeviceSN");
		if (string.IsNullOrEmpty(text3))
		{
			text3 = FirstText(el, "SerialNo");
		}
		if (string.IsNullOrEmpty(text3))
		{
			text3 = FirstText(el, "SerialNumber");
		}
		string mac = FirstText(el, "MAC");
		string firmwareVersion = FirstText(el, "FirmwareVersion");
		string text4 = FirstText(el, "DeviceDescription");
		DiscoveredCamera discoveredCamera = new DiscoveredCamera();
		discoveredCamera.Source = "SADP";
		discoveredCamera.Ip = text;
		discoveredCamera.Mac = mac;
		discoveredCamera.Model = ((!string.IsNullOrEmpty(text2)) ? text2 : text4);
		discoveredCamera.SerialNo = text3;
		discoveredCamera.FirmwareVersion = firmwareVersion;
		discoveredCamera.XAddr = "";
		discoveredCamera.Name = BuildSadpName(discoveredCamera.Ip, discoveredCamera.Model, discoveredCamera.SerialNo);
		return discoveredCamera;
	}

	private static string BuildSadpName(string ip, string model, string serial)
	{
		string text = ((!string.IsNullOrEmpty(model)) ? model : "HIK");
		if (!string.IsNullOrEmpty(serial))
		{
			text = text + " (" + serial + ")";
		}
		return text + " " + ip;
	}

	private static DiscoveredCamera FromRawText(string xml, IPEndPoint remote)
	{
		if (string.IsNullOrEmpty(xml))
		{
			return null;
		}
		if (xml.IndexOf("IPv4Address", StringComparison.OrdinalIgnoreCase) < 0 && xml.IndexOf("MAC", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return null;
		}
		string text = RxField(xml, "IPv4Address");
		if (string.IsNullOrEmpty(text))
		{
			text = RxField(xml, "IPAddress");
		}
		if (string.IsNullOrEmpty(text) && remote != null)
		{
			text = remote.Address.ToString();
		}
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		string text2 = RxField(xml, "DeviceType");
		if (string.IsNullOrEmpty(text2))
		{
			text2 = RxField(xml, "Model");
		}
		string text3 = RxField(xml, "DeviceSN");
		if (string.IsNullOrEmpty(text3))
		{
			text3 = RxField(xml, "SerialNo");
		}
		DiscoveredCamera discoveredCamera = new DiscoveredCamera();
		discoveredCamera.Source = "SADP";
		discoveredCamera.Ip = text;
		discoveredCamera.Mac = RxField(xml, "MAC");
		discoveredCamera.Model = text2;
		discoveredCamera.SerialNo = text3;
		discoveredCamera.FirmwareVersion = RxField(xml, "FirmwareVersion");
		discoveredCamera.XAddr = "";
		discoveredCamera.Name = BuildSadpName(discoveredCamera.Ip, discoveredCamera.Model, discoveredCamera.SerialNo);
		return discoveredCamera;
	}

	private static string RxField(string xml, string name)
	{
		if (string.IsNullOrEmpty(xml))
		{
			return "";
		}
		Match match = Regex.Match(xml, "<(?:[A-Za-z0-9_.\\-]+:)?" + name + "\\s*>(.*?)</(?:[A-Za-z0-9_.\\-]+:)?" + name + "\\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
		if (!match.Success)
		{
			return "";
		}
		return match.Groups[1].Value.Trim();
	}

	private static void Merge(Dictionary<string, DiscoveredCamera> map, DiscoveredCamera cam)
	{
		if (cam == null)
		{
			return;
		}
		string text = ((!string.IsNullOrEmpty(cam.Ip)) ? cam.Ip : cam.Mac);
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		if (map.TryGetValue(text, out var value))
		{
			if (string.IsNullOrEmpty(value.Mac))
			{
				value.Mac = cam.Mac;
			}
			if (string.IsNullOrEmpty(value.Model))
			{
				value.Model = cam.Model;
			}
			if (string.IsNullOrEmpty(value.SerialNo))
			{
				value.SerialNo = cam.SerialNo;
			}
			if (string.IsNullOrEmpty(value.FirmwareVersion))
			{
				value.FirmwareVersion = cam.FirmwareVersion;
			}
			if (string.IsNullOrEmpty(value.Name))
			{
				value.Name = cam.Name;
			}
		}
		else
		{
			map[text] = cam;
		}
	}

	private static void Prepare(UdpClient client, IPAddress group)
	{
		try
		{
			client.Client.IOControl(-1744830452, new byte[4], null);
		}
		catch
		{
		}
		bool flag = false;
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
					foreach (UnicastIPAddressInformation unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
					{
						if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
						{
							try
							{
								client.JoinMulticastGroup(group, unicastAddress.Address);
								flag = true;
							}
							catch
							{
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
		if (!flag)
		{
			try
			{
				client.JoinMulticastGroup(group);
			}
			catch
			{
			}
		}
		try
		{
			client.MulticastLoopback = true;
		}
		catch
		{
		}
	}

	private static void TrySend(UdpClient client, byte[] data, IPEndPoint target)
	{
		try
		{
			client.Send(data, data.Length, target);
		}
		catch
		{
		}
	}

	private static string SafeUtf8(byte[] data)
	{
		if (data == null || data.Length == 0)
		{
			return "";
		}
		try
		{
			return Encoding.UTF8.GetString(data);
		}
		catch
		{
			return "";
		}
	}

	private static void CollectByLocalName(XmlNode root, string localName, List<XmlElement> sink)
	{
		if (root == null)
		{
			return;
		}
		if (root is XmlElement xmlElement && xmlElement.LocalName == localName)
		{
			sink.Add(xmlElement);
		}
		foreach (XmlNode childNode in root.ChildNodes)
		{
			CollectByLocalName(childNode, localName, sink);
		}
	}

	private static XmlElement FirstElement(XmlNode root, string localName)
	{
		if (root == null)
		{
			return null;
		}
		if (root is XmlElement xmlElement && xmlElement.LocalName == localName)
		{
			return xmlElement;
		}
		foreach (XmlNode childNode in root.ChildNodes)
		{
			XmlElement xmlElement2 = FirstElement(childNode, localName);
			if (xmlElement2 != null)
			{
				return xmlElement2;
			}
		}
		return null;
	}

	private static string FirstText(XmlNode root, string localName)
	{
		XmlElement xmlElement = FirstElement(root, localName);
		if (xmlElement == null)
		{
			return "";
		}
		return (xmlElement.InnerText ?? "").Trim();
	}
}
