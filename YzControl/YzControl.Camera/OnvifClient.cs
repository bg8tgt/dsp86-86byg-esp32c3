using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace YzControl.Camera;

public sealed class OnvifClient
{
	private const string NsSoap = "http://www.w3.org/2003/05/soap-envelope";

	private const string NsDevice = "http://www.onvif.org/ver10/device/wsdl";

	private const string NsMedia = "http://www.onvif.org/ver10/media/wsdl";

	private const string NsSchema = "http://www.onvif.org/ver10/schema";

	private const string NsPtz = "http://www.onvif.org/ver20/ptz/wsdl";

	private const string NsWsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";

	private const string NsWsu = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";

	private const string TypePasswordDigest = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest";

	private const string TypeNonceBase64 = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary";

	private const string ActGetSystemDateAndTime = "http://www.onvif.org/ver10/device/wsdl/GetSystemDateAndTime";

	private const string ActGetDeviceInformation = "http://www.onvif.org/ver10/device/wsdl/GetDeviceInformation";

	private const string ActGetProfiles = "http://www.onvif.org/ver10/media/wsdl/GetProfiles";

	private const string ActGetStreamUri = "http://www.onvif.org/ver10/media/wsdl/GetStreamUri";

	private const string ActPtzContinuousMove = "http://www.onvif.org/ver20/ptz/wsdl/ContinuousMove";

	private const string ActPtzStop = "http://www.onvif.org/ver20/ptz/wsdl/Stop";

	private const string ActPtzGetPresets = "http://www.onvif.org/ver20/ptz/wsdl/GetPresets";

	private const string ActPtzGotoPreset = "http://www.onvif.org/ver20/ptz/wsdl/GotoPreset";

	private const string ActPtzSetPreset = "http://www.onvif.org/ver20/ptz/wsdl/SetPreset";

	private const int HttpTimeoutMs = 8000;

	private readonly string _xaddr;

	private readonly string _user;

	private readonly string _password;

	private string _lastError = "";

	private TimeSpan _clockOffset = TimeSpan.Zero;

	private bool _clockChecked;

	private bool _clockSynced;

	private bool _clockMeasured;

	public string LastError => _lastError;

	public TimeSpan ClockOffset => _clockOffset;

	public bool ClockSynced => _clockSynced;

	public bool ClockMeasured => _clockMeasured;

	public OnvifClient(string xaddr, string user, string password)
	{
		_xaddr = (xaddr ?? "").Trim();
		_user = user ?? "";
		_password = password ?? "";
	}

	public void SetClockOffset(TimeSpan offset)
	{
		_clockOffset = offset;
		_clockChecked = true;
		_clockSynced = true;
		_clockMeasured = false;
	}

	public OnvifDeviceInfo GetDeviceInformation()
	{
		_lastError = "";
		try
		{
			if (!Invoke("http://www.onvif.org/ver10/device/wsdl/GetDeviceInformation", "<tds:GetDeviceInformation/>", secured: true, out var response))
			{
				return null;
			}
			XmlElement xmlElement = FindFirst(response, "GetDeviceInformationResponse");
			if (xmlElement == null)
			{
				_lastError = "响应中缺少 GetDeviceInformationResponse";
				return null;
			}
			OnvifDeviceInfo result = new OnvifDeviceInfo
			{
				Manufacturer = ChildText(xmlElement, "Manufacturer"),
				Model = ChildText(xmlElement, "Model"),
				FirmwareVersion = ChildText(xmlElement, "FirmwareVersion"),
				SerialNumber = ChildText(xmlElement, "SerialNumber"),
				HardwareId = ChildText(xmlElement, "HardwareId")
			};
			_lastError = "";
			return result;
		}
		catch (Exception ex)
		{
			_lastError = "GetDeviceInformation 异常: " + ex.Message;
			return null;
		}
	}

	public List<OnvifProfile> GetProfiles()
	{
		List<OnvifProfile> list = new List<OnvifProfile>();
		_lastError = "";
		try
		{
			if (!Invoke("http://www.onvif.org/ver10/media/wsdl/GetProfiles", "<trt:GetProfiles/>", secured: true, out var response))
			{
				return list;
			}
			List<XmlElement> list2 = new List<XmlElement>();
			CollectByLocalName(response, "Profiles", list2);
			for (int i = 0; i < list2.Count; i++)
			{
				XmlElement xmlElement = list2[i];
				OnvifProfile onvifProfile = new OnvifProfile();
				onvifProfile.Token = Attr(xmlElement, "token");
				onvifProfile.Name = ChildText(xmlElement, "Name");
				XmlElement xmlElement2 = FindFirst(xmlElement, "Resolution");
				if (xmlElement2 != null)
				{
					if (int.TryParse(ChildText(xmlElement2, "Width"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
					{
						onvifProfile.Width = result;
					}
					if (int.TryParse(ChildText(xmlElement2, "Height"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
					{
						onvifProfile.Height = result2;
					}
				}
				list.Add(onvifProfile);
			}
			if (list.Count == 0)
			{
				_lastError = "设备未返回任何 Profile";
			}
			else
			{
				_lastError = "";
			}
			return list;
		}
		catch (Exception ex)
		{
			_lastError = "GetProfiles 异常: " + ex.Message;
			return list;
		}
	}

	public string GetStreamUri(string profileToken)
	{
		_lastError = "";
		if (string.IsNullOrEmpty(profileToken))
		{
			_lastError = "profileToken 为空";
			return null;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("<trt:GetStreamUri>");
			stringBuilder.Append("<trt:StreamSetup>");
			stringBuilder.Append("<tt:Stream>RTP-Unicast</tt:Stream>");
			stringBuilder.Append("<tt:Transport><tt:Protocol>RTSP</tt:Protocol></tt:Transport>");
			stringBuilder.Append("</trt:StreamSetup>");
			stringBuilder.Append("<trt:ProfileToken>").Append(Esc(profileToken)).Append("</trt:ProfileToken>");
			stringBuilder.Append("</trt:GetStreamUri>");
			if (!Invoke("http://www.onvif.org/ver10/media/wsdl/GetStreamUri", stringBuilder.ToString(), secured: true, out var response))
			{
				return null;
			}
			XmlElement xmlElement = FindFirst(response, "Uri");
			string text = ((xmlElement != null) ? (xmlElement.InnerText ?? "").Trim() : "");
			if (string.IsNullOrEmpty(text))
			{
				_lastError = "响应中缺少 Uri";
				return null;
			}
			_lastError = "";
			return AddCredentials(text);
		}
		catch (Exception ex)
		{
			_lastError = "GetStreamUri 异常: " + ex.Message;
			return null;
		}
	}

	public bool PtzContinuousMove(string profileToken, float pan, float tilt, float zoom)
	{
		_lastError = "";
		if (string.IsNullOrEmpty(profileToken))
		{
			_lastError = "profileToken 为空";
			return false;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("<tptz:ContinuousMove>");
			stringBuilder.Append("<tptz:ProfileToken>").Append(Esc(profileToken)).Append("</tptz:ProfileToken>");
			stringBuilder.Append("<tptz:Velocity>");
			stringBuilder.Append("<tt:PanTilt x=\"").Append(Fmt(pan)).Append("\" y=\"")
				.Append(Fmt(tilt))
				.Append("\"/>");
			stringBuilder.Append("<tt:Zoom x=\"").Append(Fmt(zoom)).Append("\"/>");
			stringBuilder.Append("</tptz:Velocity>");
			stringBuilder.Append("</tptz:ContinuousMove>");
			if (!Invoke("http://www.onvif.org/ver20/ptz/wsdl/ContinuousMove", stringBuilder.ToString(), secured: true, out var _))
			{
				return false;
			}
			_lastError = "";
			return true;
		}
		catch (Exception ex)
		{
			_lastError = "PtzContinuousMove 异常: " + ex.Message;
			return false;
		}
	}

	public bool PtzStop(string profileToken)
	{
		_lastError = "";
		if (string.IsNullOrEmpty(profileToken))
		{
			_lastError = "profileToken 为空";
			return false;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("<tptz:Stop>");
			stringBuilder.Append("<tptz:ProfileToken>").Append(Esc(profileToken)).Append("</tptz:ProfileToken>");
			stringBuilder.Append("<tptz:PanTilt>true</tptz:PanTilt>");
			stringBuilder.Append("<tptz:Zoom>true</tptz:Zoom>");
			stringBuilder.Append("</tptz:Stop>");
			if (!Invoke("http://www.onvif.org/ver20/ptz/wsdl/Stop", stringBuilder.ToString(), secured: true, out var _))
			{
				return false;
			}
			_lastError = "";
			return true;
		}
		catch (Exception ex)
		{
			_lastError = "PtzStop 异常: " + ex.Message;
			return false;
		}
	}

	public List<string> GetPresets(string profileToken)
	{
		List<string> list = new List<string>();
		_lastError = "";
		if (string.IsNullOrEmpty(profileToken))
		{
			_lastError = "profileToken 为空";
			return list;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("<tptz:GetPresets>");
			stringBuilder.Append("<tptz:ProfileToken>").Append(Esc(profileToken)).Append("</tptz:ProfileToken>");
			stringBuilder.Append("</tptz:GetPresets>");
			if (!Invoke("http://www.onvif.org/ver20/ptz/wsdl/GetPresets", stringBuilder.ToString(), secured: true, out var response))
			{
				return list;
			}
			List<XmlElement> list2 = new List<XmlElement>();
			CollectByLocalName(response, "Preset", list2);
			for (int i = 0; i < list2.Count; i++)
			{
				string text = Attr(list2[i], "token");
				if (!string.IsNullOrEmpty(text) && !list.Contains(text))
				{
					list.Add(text);
				}
			}
			_lastError = "";
			return list;
		}
		catch (Exception ex)
		{
			_lastError = "GetPresets 异常: " + ex.Message;
			return list;
		}
	}

	public bool GotoPreset(string profileToken, string presetToken)
	{
		_lastError = "";
		if (string.IsNullOrEmpty(profileToken))
		{
			_lastError = "profileToken 为空";
			return false;
		}
		if (string.IsNullOrEmpty(presetToken))
		{
			_lastError = "presetToken 为空";
			return false;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("<tptz:GotoPreset>");
			stringBuilder.Append("<tptz:ProfileToken>").Append(Esc(profileToken)).Append("</tptz:ProfileToken>");
			stringBuilder.Append("<tptz:PresetToken>").Append(Esc(presetToken)).Append("</tptz:PresetToken>");
			stringBuilder.Append("</tptz:GotoPreset>");
			if (!Invoke("http://www.onvif.org/ver20/ptz/wsdl/GotoPreset", stringBuilder.ToString(), secured: true, out var _))
			{
				return false;
			}
			_lastError = "";
			return true;
		}
		catch (Exception ex)
		{
			_lastError = "GotoPreset 异常: " + ex.Message;
			return false;
		}
	}

	public string SetPreset(string profileToken, string presetName)
	{
		_lastError = "";
		if (string.IsNullOrEmpty(profileToken))
		{
			_lastError = "profileToken 为空";
			return null;
		}
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("<tptz:SetPreset>");
			stringBuilder.Append("<tptz:ProfileToken>").Append(Esc(profileToken)).Append("</tptz:ProfileToken>");
			if (!string.IsNullOrEmpty(presetName))
			{
				stringBuilder.Append("<tptz:PresetName>").Append(Esc(presetName)).Append("</tptz:PresetName>");
			}
			stringBuilder.Append("</tptz:SetPreset>");
			if (!Invoke("http://www.onvif.org/ver20/ptz/wsdl/SetPreset", stringBuilder.ToString(), secured: true, out var response))
			{
				return null;
			}
			string text = ChildTextOfResponse(response, "SetPresetResponse", "PresetToken");
			if (string.IsNullOrEmpty(text))
			{
				_lastError = "响应中缺少 PresetToken";
				return null;
			}
			_lastError = "";
			return text;
		}
		catch (Exception ex)
		{
			_lastError = "SetPreset 异常: " + ex.Message;
			return null;
		}
	}

	private void EnsureClock()
	{
		if (_clockChecked)
		{
			return;
		}
		_clockChecked = true;
		string lastError = _lastError;
		try
		{
			if (!Invoke("http://www.onvif.org/ver10/device/wsdl/GetSystemDateAndTime", "<tds:GetSystemDateAndTime/>", secured: false, out var response))
			{
				return;
			}
			XmlElement xmlElement = FindFirst(response, "UTCDateTime");
			if (xmlElement == null)
			{
				return;
			}
			int num = ParseInt(ChildText(xmlElement, "Year"));
			int num2 = ParseInt(ChildText(xmlElement, "Month"));
			int num3 = ParseInt(ChildText(xmlElement, "Day"));
			int num4 = ParseInt(ChildText(xmlElement, "Hour"));
			int num5 = ParseInt(ChildText(xmlElement, "Minute"));
			int num6 = ParseInt(ChildText(xmlElement, "Second"));
			if (num >= 1970 && num2 >= 1 && num2 <= 12 && num3 >= 1 && num3 <= 31 && num4 >= 0 && num4 <= 23 && num5 >= 0 && num5 <= 59)
			{
				switch (num6)
				{
				case 60:
					num6 = 59;
					break;
				case 0:
				case 1:
				case 2:
				case 3:
				case 4:
				case 5:
				case 6:
				case 7:
				case 8:
				case 9:
				case 10:
				case 11:
				case 12:
				case 13:
				case 14:
				case 15:
				case 16:
				case 17:
				case 18:
				case 19:
				case 20:
				case 21:
				case 22:
				case 23:
				case 24:
				case 25:
				case 26:
				case 27:
				case 28:
				case 29:
				case 30:
				case 31:
				case 32:
				case 33:
				case 34:
				case 35:
				case 36:
				case 37:
				case 38:
				case 39:
				case 40:
				case 41:
				case 42:
				case 43:
				case 44:
				case 45:
				case 46:
				case 47:
				case 48:
				case 49:
				case 50:
				case 51:
				case 52:
				case 53:
				case 54:
				case 55:
				case 56:
				case 57:
				case 58:
				case 59:
					break;
				default:
					return;
				}
				DateTime dateTime = new DateTime(num, num2, num3, num4, num5, num6, DateTimeKind.Utc);
				_clockOffset = dateTime - DateTime.UtcNow;
				_clockSynced = true;
				_clockMeasured = true;
			}
		}
		catch
		{
		}
		finally
		{
			_lastError = lastError;
		}
	}

	private DateTime DeviceNowUtc()
	{
		return DateTime.UtcNow + _clockOffset;
	}

	private string BuildSecurityHeader()
	{
		byte[] array = new byte[16];
		using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
		{
			randomNumberGenerator.GetBytes(array);
		}
		string text = DeviceNowUtc().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		byte[] bytes2 = Encoding.UTF8.GetBytes(_password);
		byte[] array2 = new byte[array.Length + bytes.Length + bytes2.Length];
		Buffer.BlockCopy(array, 0, array2, 0, array.Length);
		Buffer.BlockCopy(bytes, 0, array2, array.Length, bytes.Length);
		Buffer.BlockCopy(bytes2, 0, array2, array.Length + bytes.Length, bytes2.Length);
		byte[] inArray;
		using (SHA1 sHA = SHA1.Create())
		{
			inArray = sHA.ComputeHash(array2);
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("<wsse:Security xmlns:wsse=\"").Append("http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd").Append("\"");
		stringBuilder.Append(" xmlns:wsu=\"").Append("http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd").Append("\" s:mustUnderstand=\"1\">");
		stringBuilder.Append("<wsse:UsernameToken>");
		stringBuilder.Append("<wsse:Username>").Append(Esc(_user)).Append("</wsse:Username>");
		stringBuilder.Append("<wsse:Password Type=\"").Append("http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest").Append("\">")
			.Append(Convert.ToBase64String(inArray))
			.Append("</wsse:Password>");
		stringBuilder.Append("<wsse:Nonce EncodingType=\"").Append("http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary").Append("\">")
			.Append(Convert.ToBase64String(array))
			.Append("</wsse:Nonce>");
		stringBuilder.Append("<wsu:Created>").Append(text).Append("</wsu:Created>");
		stringBuilder.Append("</wsse:UsernameToken>");
		stringBuilder.Append("</wsse:Security>");
		return stringBuilder.ToString();
	}

	private static string BuildEnvelope(string bodyXml, string securityHeader)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
		stringBuilder.Append("<s:Envelope xmlns:s=\"").Append("http://www.w3.org/2003/05/soap-envelope").Append("\"");
		stringBuilder.Append(" xmlns:tds=\"").Append("http://www.onvif.org/ver10/device/wsdl").Append("\"");
		stringBuilder.Append(" xmlns:trt=\"").Append("http://www.onvif.org/ver10/media/wsdl").Append("\"");
		stringBuilder.Append(" xmlns:tt=\"").Append("http://www.onvif.org/ver10/schema").Append("\"");
		stringBuilder.Append(" xmlns:tptz=\"").Append("http://www.onvif.org/ver20/ptz/wsdl").Append("\">");
		stringBuilder.Append("<s:Header>");
		if (!string.IsNullOrEmpty(securityHeader))
		{
			stringBuilder.Append(securityHeader);
		}
		stringBuilder.Append("</s:Header>");
		stringBuilder.Append("<s:Body>").Append(bodyXml).Append("</s:Body>");
		stringBuilder.Append("</s:Envelope>");
		return stringBuilder.ToString();
	}

	private bool Invoke(string soapAction, string bodyXml, bool secured, out XmlDocument response)
	{
		response = null;
		if (string.IsNullOrEmpty(_xaddr))
		{
			_lastError = "xaddr 为空";
			return false;
		}
		string securityHeader = "";
		if (secured)
		{
			EnsureClock();
			securityHeader = BuildSecurityHeader();
		}
		string s = BuildEnvelope(bodyXml, securityHeader);
		byte[] bytes = Encoding.UTF8.GetBytes(s);
		try
		{
			HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(_xaddr);
			httpWebRequest.Method = "POST";
			httpWebRequest.ContentType = "application/soap+xml; charset=utf-8; action=\"" + soapAction + "\"";
			httpWebRequest.Headers.Add("SOAPAction", "\"" + soapAction + "\"");
			httpWebRequest.Timeout = 8000;
			httpWebRequest.ReadWriteTimeout = 8000;
			httpWebRequest.KeepAlive = false;
			httpWebRequest.ContentLength = bytes.Length;
			try
			{
				httpWebRequest.Proxy = null;
			}
			catch
			{
			}
			try
			{
				httpWebRequest.ServicePoint.Expect100Continue = false;
			}
			catch
			{
			}
			using (Stream stream = httpWebRequest.GetRequestStream())
			{
				stream.Write(bytes, 0, bytes.Length);
			}
			HttpWebResponse httpWebResponse = null;
			try
			{
				httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
			}
			catch (WebException ex)
			{
				httpWebResponse = ex.Response as HttpWebResponse;
				if (httpWebResponse == null)
				{
					_lastError = "网络错误: " + ex.Message;
					return false;
				}
			}
			int statusCode;
			string text;
			string text2;
			using (httpWebResponse)
			{
				statusCode = (int)httpWebResponse.StatusCode;
				text = httpWebResponse.StatusDescription ?? "";
				using Stream stream2 = httpWebResponse.GetResponseStream();
				if (stream2 == null)
				{
					_lastError = "设备返回空响应流 (HTTP " + statusCode + ")";
					return false;
				}
				using StreamReader streamReader = new StreamReader(stream2, Encoding.UTF8);
				text2 = streamReader.ReadToEnd();
			}
			if (string.IsNullOrEmpty(text2))
			{
				_lastError = "设备返回空响应 (HTTP " + statusCode + ")";
				return false;
			}
			XmlDocument xmlDocument = new XmlDocument();
			try
			{
				xmlDocument.LoadXml(text2);
			}
			catch (XmlException ex2)
			{
				_lastError = "响应不是合法 XML: " + ex2.Message + " | 原文: " + Clip(text2, 300);
				return false;
			}
			if (IsFault(xmlDocument))
			{
				_lastError = ExtractFault(xmlDocument);
				return false;
			}
			if (statusCode != 200)
			{
				_lastError = "HTTP " + statusCode + " " + text;
				return false;
			}
			_lastError = "";
			response = xmlDocument;
			return true;
		}
		catch (Exception ex3)
		{
			_lastError = ex3.GetType().Name + ": " + ex3.Message;
			return false;
		}
	}

	private static bool IsFault(XmlDocument doc)
	{
		return FindFirst(doc, "Fault") != null;
	}

	private static string ExtractFault(XmlDocument doc)
	{
		string text = "";
		XmlElement xmlElement = FindFirst(doc, "Reason");
		if (xmlElement != null)
		{
			text = ChildText(xmlElement, "Text");
		}
		if (string.IsNullOrEmpty(text) && xmlElement != null)
		{
			text = (xmlElement.InnerText ?? "").Trim();
		}
		if (string.IsNullOrEmpty(text))
		{
			text = FirstText(doc, "faultstring");
		}
		if (string.IsNullOrEmpty(text))
		{
			text = FirstText(doc, "Value");
		}
		if (string.IsNullOrEmpty(text))
		{
			text = FirstText(doc, "Subcode");
		}
		if (string.IsNullOrEmpty(text))
		{
			text = FirstText(doc, "faultcode");
		}
		if (string.IsNullOrEmpty(text))
		{
			XmlElement xmlElement2 = FindFirst(doc, "Fault");
			if (xmlElement2 != null)
			{
				text = (xmlElement2.InnerText ?? "").Trim();
			}
		}
		if (string.IsNullOrEmpty(text))
		{
			text = "SOAP Fault（无详细信息）";
		}
		return "SOAP Fault: " + Clip(text, 400);
	}

	private static string ChildTextOfResponse(XmlDocument doc, string responseLocalName, string childLocalName)
	{
		XmlElement xmlElement = FindFirst(doc, responseLocalName);
		if (xmlElement == null)
		{
			return "";
		}
		string text = ChildText(xmlElement, childLocalName);
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		return FirstText(doc, childLocalName);
	}

	private static XmlElement FindFirst(XmlNode root, string localName)
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
			XmlElement xmlElement2 = FindFirst(childNode, localName);
			if (xmlElement2 != null)
			{
				return xmlElement2;
			}
		}
		return null;
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

	private static string ChildText(XmlNode node, string localName)
	{
		if (node == null)
		{
			return "";
		}
		foreach (XmlNode childNode in node.ChildNodes)
		{
			if (childNode.LocalName == localName)
			{
				return (childNode.InnerText ?? "").Trim();
			}
		}
		return "";
	}

	private static string FirstText(XmlNode root, string localName)
	{
		XmlElement xmlElement = FindFirst(root, localName);
		if (xmlElement != null)
		{
			return (xmlElement.InnerText ?? "").Trim();
		}
		return "";
	}

	private static string Attr(XmlElement el, string name)
	{
		if (el == null || el.Attributes == null)
		{
			return "";
		}
		for (int i = 0; i < el.Attributes.Count; i++)
		{
			XmlAttribute xmlAttribute = el.Attributes[i];
			if (string.Equals(xmlAttribute.LocalName, name, StringComparison.OrdinalIgnoreCase) || string.Equals(xmlAttribute.Name, name, StringComparison.OrdinalIgnoreCase))
			{
				return (xmlAttribute.Value ?? "").Trim();
			}
		}
		return "";
	}

	private string AddCredentials(string uri)
	{
		if (string.IsNullOrEmpty(uri))
		{
			return uri;
		}
		if (string.IsNullOrEmpty(_user))
		{
			return uri;
		}
		int num = uri.IndexOf("://", StringComparison.Ordinal);
		if (num < 0)
		{
			return uri;
		}
		string text = uri.Substring(num + 3);
		int num2 = text.IndexOf('/');
		if (((num2 < 0) ? text : text.Substring(0, num2)).IndexOf('@') >= 0)
		{
			return uri;
		}
		string text2 = Uri.EscapeDataString(_user) + ":" + Uri.EscapeDataString(_password);
		return uri.Substring(0, num + 3) + text2 + "@" + text;
	}

	private static string Esc(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
			.Replace("\"", "&quot;")
			.Replace("'", "&apos;");
	}

	private static int ParseInt(string s)
	{
		if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return -1;
	}

	private static float Clamp(float v)
	{
		if (v < -1f)
		{
			return -1f;
		}
		if (v > 1f)
		{
			return 1f;
		}
		return v;
	}

	private static string Fmt(float v)
	{
		return Clamp(v).ToString("0.######", CultureInfo.InvariantCulture);
	}

	private static string Clip(string s, int max)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		string text = s.Trim();
		if (text.Length > max)
		{
			return text.Substring(0, max) + "...";
		}
		return text;
	}
}
