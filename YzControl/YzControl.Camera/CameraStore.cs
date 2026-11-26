using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;

namespace YzControl.Camera;

public static class CameraStore
{
	private const string DpApiPrefix = "dpapi:";

	private static readonly object _loadGate = new object();

	private static CameraConfig _cached;

	private static bool _loaded;

	public static string LastError = "";

	public static string ConfigDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YzControl");

	public static string ConfigPath => Path.Combine(ConfigDir, "cameras.xml");

	private static string ProtectPwd(string plain)
	{
		if (string.IsNullOrEmpty(plain))
		{
			return "";
		}
		if (plain.StartsWith("dpapi:", StringComparison.Ordinal))
		{
			return plain;
		}
		try
		{
			byte[] inArray = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
			return "dpapi:" + Convert.ToBase64String(inArray);
		}
		catch
		{
			return plain;
		}
	}

	private static string UnprotectPwd(string stored)
	{
		if (string.IsNullOrEmpty(stored))
		{
			return "";
		}
		if (!stored.StartsWith("dpapi:", StringComparison.Ordinal))
		{
			return stored;
		}
		try
		{
			byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(stored.Substring("dpapi:".Length)), null, DataProtectionScope.CurrentUser);
			return Encoding.UTF8.GetString(bytes);
		}
		catch
		{
			return "";
		}
	}

	public static void Prewarm()
	{
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				Load();
			}
			catch
			{
			}
		});
	}

	public static CameraConfig Load()
	{
		lock (_loadGate)
		{
			if (_loaded)
			{
				return _cached;
			}
			_cached = LoadCore();
			_loaded = true;
			return _cached;
		}
	}

	private static string NodeText(XmlElement p, string name, string def)
	{
		XmlNode xmlNode = p.SelectSingleNode(name);
		if (xmlNode == null)
		{
			return def;
		}
		string innerText = xmlNode.InnerText;
		if (innerText != null)
		{
			return innerText;
		}
		return def;
	}

	private static int NodeInt(XmlElement p, string name, int def)
	{
		string text = NodeText(p, name, null);
		if (text == null || !int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return def;
		}
		return result;
	}

	private static bool NodeBool(XmlElement p, string name, bool def)
	{
		string text = NodeText(p, name, null);
		if (text == null)
		{
			return def;
		}
		text = text.Trim();
		if (text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1")
		{
			return true;
		}
		if (text.Equals("false", StringComparison.OrdinalIgnoreCase) || text == "0")
		{
			return false;
		}
		return def;
	}

	private static CameraConfig LoadCore()
	{
		return LoadPath(ConfigPath);
	}

	public static bool IsValidHost(string h)
	{
		if (string.IsNullOrEmpty(h))
		{
			return true;
		}
		string text = h.Trim();
		if (text.Length == 0 || text.Length > 253)
		{
			return false;
		}
		if (text != h)
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
		if (text[0] == '.' || text[text.Length - 1] == '.')
		{
			return false;
		}
		return true;
	}

	internal static CameraConfig LoadPath(string path)
	{
		try
		{
			if (!File.Exists(path))
			{
				return new CameraConfig();
			}
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.Load(path);
			XmlElement documentElement = xmlDocument.DocumentElement;
			if (documentElement == null)
			{
				return new CameraConfig();
			}
			CameraConfig cameraConfig = new CameraConfig();
			cameraConfig.S3Host = NodeText(documentElement, "S3Host", cameraConfig.S3Host);
			cameraConfig.S3HostSource = NodeText(documentElement, "S3HostSource", cameraConfig.S3HostSource);
			cameraConfig.S3HostSeen = NodeText(documentElement, "S3HostSeen", cameraConfig.S3HostSeen);
			if (!IsValidHost(cameraConfig.S3Host))
			{
				string s3Host = cameraConfig.S3Host;
				cameraConfig.S3Host = new CameraConfig().S3Host;
				LastError = "云台板地址(配置里的 S3Host)非法：'" + s3Host + "'，已临时改用默认 " + cameraConfig.S3Host + "；请在界面上重新填写设备地址。";
			}
			cameraConfig.Pane1 = NodeInt(documentElement, "Pane1", cameraConfig.Pane1);
			cameraConfig.Pane2 = NodeInt(documentElement, "Pane2", cameraConfig.Pane2);
			cameraConfig.AutoTakePriority = NodeBool(documentElement, "AutoTakePriority", cameraConfig.AutoTakePriority);
			cameraConfig.DirectLink = NodeText(documentElement, "DirectLink", cameraConfig.DirectLink);
			cameraConfig.DirectCom = NodeText(documentElement, "DirectCom", cameraConfig.DirectCom);
			cameraConfig.DirectBaud = NodeInt(documentElement, "DirectBaud", cameraConfig.DirectBaud);
			cameraConfig.DirectCivAddr = NodeText(documentElement, "DirectCivAddr", cameraConfig.DirectCivAddr);
			cameraConfig.RigChannel = NodeText(documentElement, "RigChannel", cameraConfig.RigChannel);
			cameraConfig.DirectIp = NodeText(documentElement, "DirectIp", cameraConfig.DirectIp);
			cameraConfig.DirectIpSource = NodeText(documentElement, "DirectIpSource", cameraConfig.DirectIpSource);
			cameraConfig.DirectIpSeen = NodeText(documentElement, "DirectIpSeen", cameraConfig.DirectIpSeen);
			cameraConfig.DirectPort = NodeInt(documentElement, "DirectPort", cameraConfig.DirectPort);
			cameraConfig.CivPollMs = NodeInt(documentElement, "CivPollMs", cameraConfig.CivPollMs);
			cameraConfig.CivPaused = NodeBool(documentElement, "CivPaused", cameraConfig.CivPaused);
			cameraConfig.CivLegacy1C = NodeBool(documentElement, "CivLegacy1C", cameraConfig.CivLegacy1C);
			cameraConfig.CivDeepVfoB = NodeBool(documentElement, "CivDeepVfoB", cameraConfig.CivDeepVfoB);
			cameraConfig.CivReadSmeter = NodeBool(documentElement, "CivReadSmeter", cameraConfig.CivReadSmeter);
			if (cameraConfig.Cameras == null)
			{
				cameraConfig.Cameras = new List<CameraEntry>();
			}
			XmlNode xmlNode = documentElement.SelectSingleNode("Cameras");
			if (xmlNode != null)
			{
				foreach (XmlNode item in xmlNode.SelectNodes("CameraEntry"))
				{
					if (item is XmlElement p)
					{
						CameraEntry cameraEntry = new CameraEntry();
						cameraEntry.Name = NodeText(p, "Name", cameraEntry.Name);
						cameraEntry.Vendor = NodeText(p, "Vendor", cameraEntry.Vendor);
						cameraEntry.Ip = NodeText(p, "Ip", cameraEntry.Ip);
						cameraEntry.Port = NodeInt(p, "Port", cameraEntry.Port);
						cameraEntry.User = NodeText(p, "User", cameraEntry.User);
						cameraEntry.Password = UnprotectPwd(NodeText(p, "Password", ""));
						cameraEntry.Channel = NodeInt(p, "Channel", cameraEntry.Channel);
						cameraEntry.MainStream = NodeBool(p, "MainStream", cameraEntry.MainStream);
						cameraEntry.XAddr = NodeText(p, "XAddr", cameraEntry.XAddr);
						cameraEntry.Url = NodeText(p, "Url", cameraEntry.Url);
						cameraConfig.Cameras.Add(cameraEntry);
					}
				}
			}
			return cameraConfig;
		}
		catch (Exception ex)
		{
			try
			{
				if (File.Exists(path))
				{
					string destFileName = path + ".bad-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
					File.Copy(path, destFileName, overwrite: true);
				}
			}
			catch
			{
			}
			LastError = "摄像头配置读取失败（已备份原文件）：" + ex.Message;
			return new CameraConfig();
		}
	}

	public static bool Save(CameraConfig cfg)
	{
		return SaveTo(ConfigPath, cfg);
	}

	internal static bool SaveTo(string path, CameraConfig cfg)
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			CameraConfig cameraConfig = new CameraConfig();
			cameraConfig.Pane1 = cfg.Pane1;
			cameraConfig.Pane2 = cfg.Pane2;
			cameraConfig.S3Host = cfg.S3Host;
			cameraConfig.S3HostSource = cfg.S3HostSource;
			cameraConfig.S3HostSeen = cfg.S3HostSeen;
			cameraConfig.AutoTakePriority = cfg.AutoTakePriority;
			cameraConfig.DirectLink = cfg.DirectLink;
			cameraConfig.DirectCom = cfg.DirectCom;
			cameraConfig.DirectBaud = cfg.DirectBaud;
			cameraConfig.DirectCivAddr = cfg.DirectCivAddr;
			cameraConfig.RigChannel = cfg.RigChannel;
			cameraConfig.DirectIp = cfg.DirectIp;
			cameraConfig.DirectIpSource = cfg.DirectIpSource;
			cameraConfig.DirectIpSeen = cfg.DirectIpSeen;
			cameraConfig.DirectPort = cfg.DirectPort;
			cameraConfig.CivPollMs = cfg.CivPollMs;
			cameraConfig.CivPaused = cfg.CivPaused;
			cameraConfig.CivLegacy1C = cfg.CivLegacy1C;
			cameraConfig.CivDeepVfoB = cfg.CivDeepVfoB;
			cameraConfig.CivReadSmeter = cfg.CivReadSmeter;
			cameraConfig.Cameras = new List<CameraEntry>();
			if (cfg.Cameras != null)
			{
				foreach (CameraEntry camera in cfg.Cameras)
				{
					if (camera != null)
					{
						CameraEntry cameraEntry = camera.Clone();
						cameraEntry.Password = ProtectPwd(camera.Password);
						cameraConfig.Cameras.Add(cameraEntry);
					}
				}
			}
			string text = path + ".tmp";
			WriteXml(text, cameraConfig);
			if (File.Exists(path))
			{
				try
				{
					File.Replace(text, path, path + ".bak", ignoreMetadataErrors: true);
				}
				catch
				{
					try
					{
						File.Copy(path, path + ".bak", overwrite: true);
					}
					catch
					{
					}
					File.Delete(path);
					File.Move(text, path);
				}
			}
			else
			{
				File.Move(text, path);
			}
			LastError = "";
			return true;
		}
		catch (Exception ex)
		{
			LastError = "摄像头配置保存失败：" + ex.Message;
			return false;
		}
	}

	private static void Elem(XmlWriter w, string name, string val)
	{
		w.WriteStartElement(name);
		if (!string.IsNullOrEmpty(val))
		{
			w.WriteString(val);
		}
		w.WriteEndElement();
	}

	private static void WriteXml(string path, CameraConfig cfg)
	{
		XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
		xmlWriterSettings.Indent = true;
		xmlWriterSettings.IndentChars = "  ";
		xmlWriterSettings.Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
		using XmlWriter xmlWriter = XmlWriter.Create(path, xmlWriterSettings);
		xmlWriter.WriteStartDocument();
		xmlWriter.WriteStartElement("CameraConfig");
		xmlWriter.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");
		xmlWriter.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
		xmlWriter.WriteStartElement("Cameras");
		foreach (CameraEntry camera in cfg.Cameras)
		{
			if (camera != null)
			{
				xmlWriter.WriteStartElement("CameraEntry");
				Elem(xmlWriter, "Name", camera.Name);
				Elem(xmlWriter, "Vendor", camera.Vendor);
				Elem(xmlWriter, "Ip", camera.Ip);
				Elem(xmlWriter, "Port", camera.Port.ToString(CultureInfo.InvariantCulture));
				Elem(xmlWriter, "User", camera.User);
				Elem(xmlWriter, "Password", camera.Password);
				Elem(xmlWriter, "Channel", camera.Channel.ToString(CultureInfo.InvariantCulture));
				Elem(xmlWriter, "MainStream", camera.MainStream ? "true" : "false");
				Elem(xmlWriter, "XAddr", camera.XAddr);
				Elem(xmlWriter, "Url", camera.Url);
				xmlWriter.WriteEndElement();
			}
		}
		xmlWriter.WriteEndElement();
		Elem(xmlWriter, "Pane1", cfg.Pane1.ToString(CultureInfo.InvariantCulture));
		Elem(xmlWriter, "Pane2", cfg.Pane2.ToString(CultureInfo.InvariantCulture));
		Elem(xmlWriter, "S3Host", cfg.S3Host);
		Elem(xmlWriter, "S3HostSource", cfg.S3HostSource);
		Elem(xmlWriter, "S3HostSeen", cfg.S3HostSeen);
		Elem(xmlWriter, "AutoTakePriority", cfg.AutoTakePriority ? "true" : "false");
		Elem(xmlWriter, "DirectLink", cfg.DirectLink);
		Elem(xmlWriter, "DirectCom", cfg.DirectCom);
		Elem(xmlWriter, "DirectBaud", cfg.DirectBaud.ToString(CultureInfo.InvariantCulture));
		Elem(xmlWriter, "DirectCivAddr", cfg.DirectCivAddr);
		Elem(xmlWriter, "RigChannel", cfg.RigChannel);
		Elem(xmlWriter, "DirectIp", cfg.DirectIp);
		Elem(xmlWriter, "DirectIpSource", cfg.DirectIpSource);
		Elem(xmlWriter, "DirectIpSeen", cfg.DirectIpSeen);
		Elem(xmlWriter, "DirectPort", cfg.DirectPort.ToString(CultureInfo.InvariantCulture));
		Elem(xmlWriter, "CivPollMs", cfg.CivPollMs.ToString(CultureInfo.InvariantCulture));
		Elem(xmlWriter, "CivPaused", cfg.CivPaused ? "true" : "false");
		Elem(xmlWriter, "CivLegacy1C", cfg.CivLegacy1C ? "true" : "false");
		Elem(xmlWriter, "CivDeepVfoB", cfg.CivDeepVfoB ? "true" : "false");
		Elem(xmlWriter, "CivReadSmeter", cfg.CivReadSmeter ? "true" : "false");
		xmlWriter.WriteEndElement();
		xmlWriter.WriteEndDocument();
	}
}
