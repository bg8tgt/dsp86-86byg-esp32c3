using System.Globalization;

namespace YzControl;

public sealed class YzOtasInfo
{
	public string Raw = "";

	public int OtaT = -1;

	public int Subnet = -1;

	public int Rate = -1;

	public string Ip = "";

	public string EthIp = "";

	public bool HasToken => OtaT == 1;

	public bool StaUp => !YzOtaGate.IsZeroIp(Ip);

	public string Summary => string.Format("令牌闸门 otat={0}\u3000同网段闸门 subnet={1}\u3000限速 rate={2}s\u3000STA ip={3}\u3000有线 eth={4}", (OtaT < 0) ? "?" : OtaT.ToString(CultureInfo.InvariantCulture), (Subnet < 0) ? "?" : Subnet.ToString(CultureInfo.InvariantCulture), Rate, (Ip.Length == 0) ? "?" : Ip, (EthIp.Length == 0) ? "?" : EthIp);

	public static YzOtasInfo Parse(string body)
	{
		YzOtasInfo yzOtasInfo = new YzOtasInfo();
		yzOtasInfo.Raw = body ?? "";
		string[] array = yzOtasInfo.Raw.Split(';');
		for (int i = 0; i < array.Length; i++)
		{
			int num = array[i].IndexOf('=');
			if (num > 0)
			{
				string text = array[i].Substring(0, num).Trim();
				string text2 = array[i].Substring(num + 1).Trim();
				switch (text)
				{
				case "otat":
					yzOtasInfo.OtaT = ParseI(text2, -1);
					break;
				case "subnet":
					yzOtasInfo.Subnet = ParseI(text2, -1);
					break;
				case "rate":
					yzOtasInfo.Rate = ParseI(text2, -1);
					break;
				case "ip":
					yzOtasInfo.Ip = text2;
					break;
				case "eth":
					yzOtasInfo.EthIp = text2;
					break;
				}
			}
		}
		return yzOtasInfo;
	}

	private static int ParseI(string v, int def)
	{
		if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return def;
		}
		return result;
	}
}
