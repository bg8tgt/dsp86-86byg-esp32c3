using System.Collections.Generic;
using System.Globalization;

namespace YzControl;

public sealed class YzTrackSnapshot
{
	public readonly List<YzTrackCli> Slots = new List<YzTrackCli>();

	public string CtrlIp = "0.0.0.0";

	public int CtrlSlot = -1;

	public int Arb = 1;

	public long Rejected;

	public int MaxSlots = 4;

	public string Raw = "";

	public int OnlineCount
	{
		get
		{
			int num = 0;
			for (int i = 0; i < Slots.Count; i++)
			{
				if (Slots[i].On)
				{
					num++;
				}
			}
			return num;
		}
	}

	public YzTrackCli Controller
	{
		get
		{
			for (int i = 0; i < Slots.Count; i++)
			{
				if (Slots[i].Ctrl)
				{
					return Slots[i];
				}
			}
			return null;
		}
	}

	private static void Kv(string part, out string k, out string v)
	{
		int num = part.IndexOf('=');
		if (num <= 0)
		{
			k = "";
			v = "";
		}
		else
		{
			k = part.Substring(0, num).Trim();
			v = part.Substring(num + 1).Trim();
		}
	}

	private static long L(string s, long def)
	{
		if (!long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return def;
		}
		return result;
	}

	public static YzTrackSnapshot Parse(string body)
	{
		YzTrackSnapshot yzTrackSnapshot = new YzTrackSnapshot();
		yzTrackSnapshot.Raw = body ?? "";
		if (yzTrackSnapshot.Raw.Length == 0)
		{
			return yzTrackSnapshot;
		}
		string[] array = yzTrackSnapshot.Raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			if (text.Length == 0)
			{
				continue;
			}
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			string[] array2 = text.Split(';');
			for (int j = 0; j < array2.Length; j++)
			{
				Kv(array2[j], out var k, out var v);
				if (k.Length > 0 && !dictionary.ContainsKey(k))
				{
					dictionary[k] = v;
				}
			}
			if (dictionary.TryGetValue("ctrl_ip", out var value))
			{
				yzTrackSnapshot.CtrlIp = value;
				if (dictionary.TryGetValue("ctrl_slot", out value))
				{
					yzTrackSnapshot.CtrlSlot = (int)L(value, -1L);
				}
				if (dictionary.TryGetValue("arb", out value))
				{
					yzTrackSnapshot.Arb = (int)L(value, 1L);
				}
				if (dictionary.TryGetValue("rej", out value))
				{
					yzTrackSnapshot.Rejected = L(value, 0L);
				}
				if (dictionary.TryGetValue("max", out value))
				{
					yzTrackSnapshot.MaxSlots = (int)L(value, 4L);
				}
			}
			else if (dictionary.ContainsKey("slot"))
			{
				YzTrackCli yzTrackCli = new YzTrackCli();
				if (dictionary.TryGetValue("slot", out value))
				{
					yzTrackCli.Slot = (int)L(value, 0L);
				}
				if (dictionary.TryGetValue("ip", out value))
				{
					yzTrackCli.Ip = value;
				}
				if (dictionary.TryGetValue("on", out value))
				{
					yzTrackCli.On = L(value, 0L) != 0;
				}
				if (dictionary.TryGetValue("last", out value))
				{
					yzTrackCli.Last = L(value, 0L) != 0;
				}
				if (dictionary.TryGetValue("act", out value))
				{
					yzTrackCli.ActAgeSec = L(value, 0L);
				}
				if (dictionary.TryGetValue("conn", out value))
				{
					yzTrackCli.ConnAgeSec = L(value, 0L);
				}
				if (dictionary.TryGetValue("ctrl", out value))
				{
					yzTrackCli.Ctrl = L(value, 0L) != 0;
				}
				if (dictionary.TryGetValue("rep", out value))
				{
					yzTrackCli.Rep = L(value, 0L) != 0;
				}
				if (dictionary.TryGetValue("idle", out value))
				{
					yzTrackCli.Idle = L(value, 0L) != 0;
				}
				yzTrackSnapshot.Slots.Add(yzTrackCli);
			}
		}
		return yzTrackSnapshot;
	}
}
