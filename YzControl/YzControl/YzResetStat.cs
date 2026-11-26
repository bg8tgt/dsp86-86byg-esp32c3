using System.Collections.Generic;
using System.Globalization;

namespace YzControl;

public sealed class YzResetStat
{
	public long Panic;

	public long TaskWdt;

	public long IntWdt;

	public long Wdt;

	public long Brownout;

	public long PowerOn;

	public long Sw;

	public long ExtRst;

	public long Other;

	public int Total;

	public string Raw = "";

	public string Summary => $"panic={Panic};taskwdt={TaskWdt};intwdt={IntWdt};wdt={Wdt};brownout={Brownout};poweron={PowerOn};sw={Sw};extrst={ExtRst}";

	private static long L(Dictionary<string, string> d, string k)
	{
		if (d.TryGetValue(k, out var value) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return 0L;
	}

	public static YzResetStat Parse(string body)
	{
		YzResetStat yzResetStat = new YzResetStat();
		yzResetStat.Raw = body ?? "";
		if (yzResetStat.Raw.Length == 0)
		{
			return yzResetStat;
		}
		string[] array = yzResetStat.Raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
		string text = null;
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = array[i].Trim();
			int num = text2.IndexOf(':');
			string text3 = ((num > 0) ? text2.Substring(0, num).Trim() : "");
			if ((text3 == "统计" || text3.StartsWith("统计")) && text2.IndexOf("panic=") >= 0)
			{
				text = text2.Substring(num + 1);
				break;
			}
		}
		if (text == null)
		{
			return yzResetStat;
		}
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		string[] array2 = text.Split(';');
		for (int j = 0; j < array2.Length; j++)
		{
			int num2 = array2[j].IndexOf('=');
			if (num2 > 0)
			{
				string key = array2[j].Substring(0, num2).Trim();
				string value = array2[j].Substring(num2 + 1).Trim();
				if (!dictionary.ContainsKey(key))
				{
					dictionary[key] = value;
				}
			}
		}
		yzResetStat.Panic = L(dictionary, "panic");
		yzResetStat.TaskWdt = L(dictionary, "taskwdt");
		yzResetStat.IntWdt = L(dictionary, "intwdt");
		yzResetStat.Wdt = L(dictionary, "wdt");
		yzResetStat.Brownout = L(dictionary, "brownout");
		yzResetStat.PowerOn = L(dictionary, "poweron");
		yzResetStat.Sw = L(dictionary, "sw");
		yzResetStat.ExtRst = L(dictionary, "extrst");
		yzResetStat.Total = (int)(yzResetStat.Panic + yzResetStat.TaskWdt + yzResetStat.IntWdt + yzResetStat.Wdt + yzResetStat.Brownout + yzResetStat.PowerOn + yzResetStat.Sw + yzResetStat.ExtRst);
		return yzResetStat;
	}
}
