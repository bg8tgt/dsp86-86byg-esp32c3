using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace YzControl;

public sealed class YzRstHistory
{
	public string Raw = "";

	public int Total;

	public long Panic;

	public long TaskWdt;

	public long IntWdt;

	public long Wdt;

	public long Brownout;

	public long PowerOn;

	public long Sw;

	public long ExtRst;

	public readonly List<YzRstEntry> Entries = new List<YzRstEntry>();

	public long Counted => Panic + TaskWdt + IntWdt + Wdt + Brownout + PowerOn + Sw + ExtRst;

	public string Summary => $"panic={Panic};taskwdt={TaskWdt};intwdt={IntWdt};wdt={Wdt};brownout={Brownout};poweron={PowerOn};sw={Sw};extrst={ExtRst}";

	public string LastLine
	{
		get
		{
			if (Entries.Count == 0)
			{
				return "（无记录）";
			}
			YzRstEntry yzRstEntry = Entries[Entries.Count - 1];
			return $"最近一次复位：{YzFw.RstName(yzRstEntry.Name)} · 面包屑 {yzRstEntry.CrumbText} · 运行 {yzRstEntry.UpSec}s · 最低堆 {yzRstEntry.HeapMin} · loop峰值 {yzRstEntry.LoopMax}ms";
		}
	}

	private static long L(string k, Dictionary<string, string> d)
	{
		if (d.TryGetValue(k, out var value) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return 0L;
	}

	public static YzRstHistory Parse(string body)
	{
		YzRstHistory yzRstHistory = new YzRstHistory();
		yzRstHistory.Raw = body ?? "";
		if (yzRstHistory.Raw.Length == 0)
		{
			return yzRstHistory;
		}
		string[] array = yzRstHistory.Raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			if (text.Length == 0)
			{
				continue;
			}
			if (text.StartsWith("复位历史", StringComparison.Ordinal))
			{
				int num = text.IndexOf("累计", StringComparison.Ordinal);
				if (num < 0)
				{
					continue;
				}
				string text2 = "";
				for (int j = num + 2; j < text.Length; j++)
				{
					if (text[j] >= '0' && text[j] <= '9')
					{
						text2 += text[j];
					}
					else if (text2.Length > 0)
					{
						break;
					}
				}
				if (int.TryParse(text2, out var result))
				{
					yzRstHistory.Total = result;
				}
			}
			else if (text.StartsWith("统计", StringComparison.Ordinal))
			{
				int num2 = text.IndexOf(':');
				if (num2 < 0)
				{
					continue;
				}
				Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
				string[] array2 = text.Substring(num2 + 1).Split(';');
				for (int k = 0; k < array2.Length; k++)
				{
					int num3 = array2[k].IndexOf('=');
					if (num3 > 0)
					{
						string key = array2[k].Substring(0, num3).Trim();
						if (!dictionary.ContainsKey(key))
						{
							dictionary[key] = array2[k].Substring(num3 + 1).Trim();
						}
					}
				}
				yzRstHistory.Panic = L("panic", dictionary);
				yzRstHistory.TaskWdt = L("taskwdt", dictionary);
				yzRstHistory.IntWdt = L("intwdt", dictionary);
				yzRstHistory.Wdt = L("wdt", dictionary);
				yzRstHistory.Brownout = L("brownout", dictionary);
				yzRstHistory.PowerOn = L("poweron", dictionary);
				yzRstHistory.Sw = L("sw", dictionary);
				yzRstHistory.ExtRst = L("extrst", dictionary);
			}
			else
			{
				if (text.StartsWith("含义", StringComparison.Ordinal) || text.StartsWith("格式", StringComparison.Ordinal) || text.StartsWith("面包屑含义", StringComparison.Ordinal) || text.StartsWith("--", StringComparison.Ordinal))
				{
					continue;
				}
				int num4 = text.IndexOf('/');
				if (num4 <= 0)
				{
					continue;
				}
				YzRstEntry yzRstEntry = new YzRstEntry();
				yzRstEntry.Raw = text;
				yzRstEntry.Name = text.Substring(0, num4).Trim();
				string[] array3 = text.Split('/');
				for (int l = 1; l < array3.Length; l++)
				{
					string text3 = array3[l].Trim();
					int num5 = text3.IndexOf('@');
					if (num5 > 0)
					{
						yzRstEntry.PeakCrumb = ParseTail(text3.Substring(num5 + 1));
						text3 = text3.Substring(0, num5);
					}
					if (text3.StartsWith("c", StringComparison.OrdinalIgnoreCase))
					{
						yzRstEntry.Crumb = (int)ParseTail(text3.Substring(1));
					}
					else if (text3.StartsWith("up", StringComparison.OrdinalIgnoreCase))
					{
						yzRstEntry.UpSec = ParseTail(text3.Substring(2));
					}
					else if (text3.StartsWith("hm", StringComparison.OrdinalIgnoreCase))
					{
						yzRstEntry.HeapMin = ParseTail(text3.Substring(2));
					}
					else if (text3.StartsWith("lm", StringComparison.OrdinalIgnoreCase))
					{
						yzRstEntry.LoopMax = ParseTail(text3.Substring(2));
					}
				}
				yzRstHistory.Entries.Add(yzRstEntry);
			}
		}
		return yzRstHistory;
	}

	private static long ParseTail(string s)
	{
		long num = 0L;
		long num2 = 1L;
		int i = 0;
		if (s.Length > 0 && s[0] == '-')
		{
			num2 = -1L;
			i = 1;
		}
		bool flag = false;
		for (; i < s.Length && s[i] >= '0' && s[i] <= '9'; i++)
		{
			num = num * 10 + (s[i] - 48);
			flag = true;
		}
		if (!flag)
		{
			return 0L;
		}
		return num2 * num;
	}

	public string TableText()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("累计复位 " + Total + " 次\u3000统计 " + Summary);
		stringBuilder.AppendLine("类型\t面包屑\t上次运行(s)\t最低堆(B)\tloop峰值(ms)\t峰值面包屑");
		for (int num = Entries.Count - 1; num >= 0; num--)
		{
			YzRstEntry yzRstEntry = Entries[num];
			stringBuilder.AppendLine(string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}", YzFw.RstName(yzRstEntry.Name), yzRstEntry.CrumbText, yzRstEntry.UpSec, yzRstEntry.HeapMin, yzRstEntry.LoopMax, (yzRstEntry.PeakCrumb == 0L) ? "-" : yzRstEntry.PeakCrumb.ToString()));
		}
		return stringBuilder.ToString();
	}
}
