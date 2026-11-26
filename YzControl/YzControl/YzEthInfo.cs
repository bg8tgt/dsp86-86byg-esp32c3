using System.Globalization;

namespace YzControl;

public sealed class YzEthInfo
{
	public string Raw = "";

	public int Up = -1;

	public int Link = -1;

	public int Dhcp = -1;

	public string Ip = "";

	public long Rx;

	public long Tx;

	public long TxFail;

	public long RxErr;

	public long LinkFlaps;

	public bool LinkDown => Link == 0;

	public bool NoIp => YzOtaGate.IsZeroIp(Ip);

	public static YzEthInfo Parse(string body)
	{
		YzEthInfo yzEthInfo = new YzEthInfo();
		yzEthInfo.Raw = body ?? "";
		string[] array = yzEthInfo.Raw.Split(';');
		for (int i = 0; i < array.Length; i++)
		{
			int num = array[i].IndexOf('=');
			if (num > 0)
			{
				string text = array[i].Substring(0, num).Trim();
				string text2 = array[i].Substring(num + 1).Trim();
				switch (text)
				{
				case "up":
					yzEthInfo.Up = P(text2, -1);
					break;
				case "link":
					yzEthInfo.Link = P(text2, -1);
					break;
				case "dhcp":
					yzEthInfo.Dhcp = P(text2, -1);
					break;
				case "ip":
					yzEthInfo.Ip = text2;
					break;
				case "rx":
					yzEthInfo.Rx = P(text2, 0);
					break;
				case "tx":
					yzEthInfo.Tx = P(text2, 0);
					break;
				case "txf":
					yzEthInfo.TxFail = P(text2, 0);
					break;
				case "rxe":
					yzEthInfo.RxErr = P(text2, 0);
					break;
				case "lf":
					yzEthInfo.LinkFlaps = P(text2, 0);
					break;
				}
			}
		}
		return yzEthInfo;
	}

	private static int P(string v, int def)
	{
		if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return def;
		}
		return result;
	}

	public string Text(string staIp)
	{
		if (Link == 0)
		{
			return string.Format("板子以太网未连接（link=0），当前走 WiFi（ip={0}）· up={1} dhcp={2} 有线ip={3} · rx={4} tx={5} txf={6} rxe={7} 链路抖动lf={8}", string.IsNullOrEmpty(staIp) ? "?" : staIp, Up, Dhcp, (Ip.Length == 0) ? "?" : Ip, Rx, Tx, TxFail, RxErr, LinkFlaps);
		}
		if (Link == 1)
		{
			return string.Format("板子以太网已连接（link=1）· up={0} 有线ip={1} 无线ip={2} · rx={3} tx={4} txf={5} rxe={6} 链路抖动lf={7}", Up, (Ip.Length == 0) ? "(未拿到地址)" : Ip, string.IsNullOrEmpty(staIp) ? "?" : staIp, Rx, Tx, TxFail, RxErr, LinkFlaps);
		}
		return "板子以太网状态未上报（该固件早于本轮，或 /eth 未读到）";
	}
}
