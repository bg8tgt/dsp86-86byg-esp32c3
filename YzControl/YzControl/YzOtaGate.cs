using System.Globalization;

namespace YzControl;

public static class YzOtaGate
{
	public sealed class Decision
	{
		public bool Allowed;

		public bool NeedWifiFix;

		public bool TokenNeeded;

		public string Why = "";

		public string Hint = "";
	}

	public static bool IsZeroIp(string ip)
	{
		if (string.IsNullOrEmpty(ip))
		{
			return true;
		}
		string text = ip.Trim();
		if (text.Length != 0 && !(text == "0.0.0.0"))
		{
			return text == "-";
		}
		return true;
	}

	public static bool SameSubnet24(string a, string b)
	{
		if (IsZeroIp(a) || IsZeroIp(b))
		{
			return false;
		}
		string[] array = a.Trim().Split('.');
		string[] array2 = b.Trim().Split('.');
		if (array.Length != 4 || array2.Length != 4)
		{
			return false;
		}
		for (int i = 0; i < 3; i++)
		{
			if (!int.TryParse(array[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				return false;
			}
			if (!int.TryParse(array2[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2))
			{
				return false;
			}
			if (result != result2)
			{
				return false;
			}
		}
		return true;
	}

	public static Decision Decide(string pcIp, YzOtasInfo o)
	{
		Decision decision = new Decision();
		if (o == null)
		{
			decision.Why = "读不到 /otas（设备未应答）";
			decision.Hint = "先点「① 读取 /otas」确认设备在线；必要时点顶部「重新发现板子」。";
			return decision;
		}
		if (IsZeroIp(pcIp))
		{
			decision.Why = "无法确定本机用于访问板子的地址（pcIp=" + pcIp + "）";
			decision.Hint = "检查本机网卡与板子是否在同一网段。";
			return decision;
		}
		if (o.HasToken)
		{
			decision.TokenNeeded = true;
			decision.Allowed = false;
			decision.Why = "固件已设置 OTA 令牌（otat=1）";
			decision.Hint = "本程序不猜令牌：请在设备端用 /set?otat= 清空令牌，或改用网页版升级页手工填令牌。";
			return decision;
		}
		bool flag = SameSubnet24(pcIp, o.Ip);
		bool flag2 = SameSubnet24(pcIp, o.EthIp);
		if (flag || flag2)
		{
			decision.Allowed = true;
			decision.Why = string.Format("放行：本机 {0} 与板子 {1}（{2}）同 /24", pcIp, flag ? ("无线 STA " + o.Ip) : ("有线 eth " + o.EthIp), flag ? "sta" : "eth");
			decision.Hint = "可以直接上传固件。";
			return decision;
		}
		decision.Allowed = false;
		decision.NeedWifiFix = true;
		decision.Why = string.Format("会被固件拒绝（403 ERR:ota-subnet）：本机 {0} 与 STA {1} / 有线 {2} 都不在同一 /24", pcIp, (o.Ip.Length == 0) ? "?" : o.Ip, (o.EthIp.Length == 0) ? "?" : o.EthIp);
		decision.Hint = (IsZeroIp(o.Ip) ? "板子无线未连接（ip=0.0.0.0）⇒ 点「② 一键 OTA」会自动 POST /wifi 让 STA 拿到同网段地址，再上传。" : "点「② 一键 OTA」会先 POST /wifi 重新配网（需要 SSID/密码），拿到同网段地址后再上传。");
		return decision;
	}
}
