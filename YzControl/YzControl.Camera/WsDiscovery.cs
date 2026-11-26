using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Xml;

namespace YzControl.Camera;

public static class WsDiscovery
{
	private const string MulticastGroup = "239.255.255.250";

	private const int WsDiscoveryPort = 3702;

	private const string NsSoap = "http://www.w3.org/2003/05/soap-envelope";

	private const string NsAddressing = "http://schemas.xmlsoap.org/ws/2004/08/addressing";

	private const string NsDiscovery = "http://schemas.xmlsoap.org/ws/2005/04/discovery";

	private const string NsNetworkVideo = "http://www.onvif.org/ver10/network/wsdl";

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
			IPEndPoint target = new IPEndPoint(iPAddress, 3702);
			udpClient = new UdpClient();
			udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);
			udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
			udpClient.Client.ReceiveTimeout = 300;
			Prepare(udpClient, iPAddress);
			TrySend(udpClient, bytes, target);
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
					if ((DateTime.UtcNow - dateTime).TotalMilliseconds >= 800.0)
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
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
		stringBuilder.Append("<e:Envelope xmlns:e=\"").Append("http://www.w3.org/2003/05/soap-envelope").Append("\"");
		stringBuilder.Append(" xmlns:w=\"").Append("http://schemas.xmlsoap.org/ws/2004/08/addressing").Append("\"");
		stringBuilder.Append(" xmlns:d=\"").Append("http://schemas.xmlsoap.org/ws/2005/04/discovery").Append("\"");
		stringBuilder.Append(" xmlns:dn=\"").Append("http://www.onvif.org/ver10/network/wsdl").Append("\">");
		stringBuilder.Append("<e:Header>");
		stringBuilder.Append("<w:MessageID>urn:uuid:").Append(Guid.NewGuid().ToString()).Append("</w:MessageID>");
		stringBuilder.Append("<w:To e:mustUnderstand=\"true\">urn:schemas-xmlsoap-org:ws:2005:04:discovery</w:To>");
		stringBuilder.Append("<w:Action e:mustUnderstand=\"true\">http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe</w:Action>");
		stringBuilder.Append("</e:Header>");
		stringBuilder.Append("<e:Body><d:Probe><d:Types>dn:NetworkVideoTransmitter</d:Types></d:Probe></e:Body>");
		stringBuilder.Append("</e:Envelope>");
		return stringBuilder.ToString();
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
		if (xmlDocument == null)
		{
			return;
		}
		List<XmlElement> list = new List<XmlElement>();
		CollectByLocalName(xmlDocument, "ProbeMatch", list);
		if (list.Count == 0)
		{
			if (FirstElement(xmlDocument, "XAddrs") != null)
			{
				DiscoveredCamera discoveredCamera = FromElement(xmlDocument, remote);
				if (discoveredCamera != null)
				{
					Merge(map, discoveredCamera);
				}
			}
			return;
		}
		foreach (XmlElement item in list)
		{
			DiscoveredCamera discoveredCamera2 = FromElement(item, remote);
			if (discoveredCamera2 != null)
			{
				Merge(map, discoveredCamera2);
			}
		}
	}

	private static DiscoveredCamera FromElement(XmlNode node, IPEndPoint remote)
	{
		string text = ((remote != null) ? remote.Address.ToString() : "");
		string text2 = PickXAddr(FirstText(node, "XAddrs"));
		if (string.IsNullOrEmpty(text2))
		{
			text2 = PickXAddr(FirstText(node, "Address"));
		}
		string text3 = "";
		string text4 = HostOf(text2);
		if (!string.IsNullOrEmpty(text4) && IPAddress.TryParse(text4, out var address))
		{
			text3 = address.ToString();
		}
		if (string.IsNullOrEmpty(text3))
		{
			text3 = text;
		}
		if (string.IsNullOrEmpty(text3))
		{
			return null;
		}
		if (string.IsNullOrEmpty(text2))
		{
			text2 = "http://" + text3 + "/onvif/device_service";
		}
		string name = "";
		string model = "";
		ParseScopes(FirstText(node, "Scopes"), ref name, ref model);
		if (string.IsNullOrEmpty(name))
		{
			name = ((!string.IsNullOrEmpty(text4)) ? text4 : text3);
		}
		return new DiscoveredCamera
		{
			Source = "ONVIF",
			Ip = text3,
			Mac = "",
			Model = model,
			SerialNo = "",
			FirmwareVersion = "",
			XAddr = text2,
			Name = name + " " + text3
		};
	}

	private static string PickXAddr(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return "";
		}
		string[] array = value.Split(new char[4] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				return text;
			}
		}
		return "";
	}

	private static string HostOf(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return "";
		}
		try
		{
			if (Uri.TryCreate(url, UriKind.Absolute, out var result))
			{
				return result.Host;
			}
		}
		catch
		{
		}
		return "";
	}

	private static void ParseScopes(string scopes, ref string name, ref string model)
	{
		if (string.IsNullOrEmpty(scopes))
		{
			return;
		}
		string[] array = scopes.Split(new char[4] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		foreach (string text in array)
		{
			if (string.IsNullOrEmpty(name) && text.IndexOf("/name/", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				name = TailAfter(text, "/name/");
			}
			else if (string.IsNullOrEmpty(model) && text.IndexOf("/hardware/", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				model = TailAfter(text, "/hardware/");
			}
		}
	}

	private static string TailAfter(string s, string marker)
	{
		int num = s.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
		if (num < 0)
		{
			return "";
		}
		string text = s.Substring(num + marker.Length);
		try
		{
			return Uri.UnescapeDataString(text);
		}
		catch
		{
			return text;
		}
	}

	private static void Merge(Dictionary<string, DiscoveredCamera> map, DiscoveredCamera cam)
	{
		if (cam == null)
		{
			return;
		}
		string text = ((!string.IsNullOrEmpty(cam.Ip)) ? cam.Ip : cam.XAddr);
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		if (map.TryGetValue(text, out var value))
		{
			if (string.IsNullOrEmpty(value.Model))
			{
				value.Model = cam.Model;
			}
			if (string.IsNullOrEmpty(value.XAddr))
			{
				value.XAddr = cam.XAddr;
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
