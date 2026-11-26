using System.Collections.Generic;
using System.Globalization;

namespace YzControl;

public sealed class YzStatus
{
	public double Az;

	public int State;

	public long MsPerDeg;

	public int Wifi;

	public string Ip = "";

	public int TiltDeg;

	public int VState;

	public string Ssid = "";

	public int TrackOk;

	public int LimUp = 70;

	public int LimDown = -70;

	public string Title = "";

	public string Ver = "";

	public int Proto;

	public long ProtoRej;

	public long TiltRaw;

	public int Ssl;

	public int Heap;

	public string EthIp = "";

	public int TiltDead;

	public int SelfTest = 1;

	public string PrioIp = "0.0.0.0";

	public long MsPerDegV = 1000L;

	public string ElSrc = "";

	public double ElSen;

	public double ElInt;

	public double ElDiff;

	public string Raw = "";

	public int NoWrap = 1;

	public long PitchZeroCnt;

	public int VRev = 1;

	public long VErr;

	public int Bod = -1;

	public int BodHw = -1;

	public int PTrace = -1;

	public int Rec;

	public int RecRst = -1;

	public int AzBlk;

	public long AzBlkCnt;

	public long ElAcc;

	public long ElRej;

	public int ElRc = -1;

	public long ElLastX10 = -32768L;

	public long EcOk;

	public long EcBad;

	public long EcRange;

	public long EcPrio;

	public string EcLast = "";

	public int WdtVer = -1;

	public int BootSt = -1;

	public string ElSrcText
	{
		get
		{
			string text = (ElSrc ?? "").Trim().ToLowerInvariant();
			switch (text)
			{
			default:
				if (text.Length != 0)
				{
					break;
				}
				return "未知";
			case "sen":
				return "传感器";
			case "int":
				return "积分";
			case "fuse":
				return "融合";
			case null:
				break;
			}
			return ElSrc;
		}
	}

	public string AzBlkText
	{
		get
		{
			if (AzBlk != 0)
			{
				return YzFw.AzBlkName(AzBlk) + "（累计 " + AzBlkCnt + "）";
			}
			return "无（累计 " + AzBlkCnt + "）";
		}
	}

	public string ElRcText => YzFw.ElRejName(ElRc);

	public string ElLastText
	{
		get
		{
			if (ElLastX10 > -32768)
			{
				return ((double)ElLastX10 / 10.0).ToString("0.0", CultureInfo.InvariantCulture) + "°";
			}
			return "从未";
		}
	}

	public string RecText => YzRecDiag.Text(Rec, RecRst, -1L);

	public string StateText => State switch
	{
		1 => "向左转中", 
		2 => "向右转中", 
		3 => "回零中", 
		4 => "往返扫描", 
		5 => "上电自检", 
		6 => "定位中", 
		_ => "已停止", 
	};

	private static string Get(Dictionary<string, string> d, string k, string def)
	{
		if (!d.TryGetValue(k, out var value))
		{
			return def;
		}
		return value;
	}

	private static double GetD(Dictionary<string, string> d, string k, double def)
	{
		if (d.TryGetValue(k, out var value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return def;
	}

	private static long GetL(Dictionary<string, string> d, string k, long def)
	{
		if (d.TryGetValue(k, out var value) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return def;
	}

	public static YzStatus Parse(string body)
	{
		YzStatus yzStatus = new YzStatus();
		yzStatus.Raw = body ?? "";
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		string[] array = yzStatus.Raw.Split(';');
		for (int i = 0; i < array.Length; i++)
		{
			int num = array[i].IndexOf('=');
			if (num > 0)
			{
				string key = array[i].Substring(0, num).Trim();
				string value = array[i].Substring(num + 1);
				if (!dictionary.ContainsKey(key))
				{
					dictionary[key] = value;
				}
			}
		}
		yzStatus.Az = GetD(dictionary, "a", 0.0);
		yzStatus.State = (int)GetL(dictionary, "s", 0L);
		yzStatus.MsPerDeg = GetL(dictionary, "k", 104L);
		yzStatus.Wifi = (int)GetL(dictionary, "w", 0L);
		yzStatus.Ip = Get(dictionary, "i", "");
		yzStatus.TiltDeg = (int)GetL(dictionary, "va", 0L);
		yzStatus.VState = (int)GetL(dictionary, "vs", 0L);
		yzStatus.Ssid = Get(dictionary, "ws", "");
		yzStatus.TrackOk = (int)GetL(dictionary, "tk", 0L);
		yzStatus.LimUp = (int)GetL(dictionary, "vu", 70L);
		yzStatus.LimDown = (int)GetL(dictionary, "vd", -70L);
		yzStatus.Title = Get(dictionary, "tl", "");
		yzStatus.Ver = Get(dictionary, "ver", "");
		yzStatus.Proto = (int)GetL(dictionary, "pr", 0L);
		yzStatus.ProtoRej = GetL(dictionary, "pf", 0L);
		yzStatus.TiltRaw = GetL(dictionary, "ta", 0L);
		yzStatus.Ssl = (int)GetL(dictionary, "ssl", 0L);
		yzStatus.Heap = (int)GetL(dictionary, "hs", 0L);
		yzStatus.EthIp = Get(dictionary, "e", "");
		yzStatus.TiltDead = (int)GetL(dictionary, "tad", 0L);
		yzStatus.SelfTest = (int)GetL(dictionary, "sf", 1L);
		yzStatus.PrioIp = Get(dictionary, "pc", "0.0.0.0");
		yzStatus.MsPerDegV = GetL(dictionary, "k2", 1000L);
		yzStatus.ElSrc = Get(dictionary, "elsrc", "");
		yzStatus.ElSen = GetD(dictionary, "elsen", 0.0);
		yzStatus.ElInt = GetD(dictionary, "elint", 0.0);
		yzStatus.ElDiff = GetD(dictionary, "eldiff", 0.0);
		yzStatus.NoWrap = (int)GetL(dictionary, "nw", 1L);
		yzStatus.PitchZeroCnt = GetL(dictionary, "vz", 0L);
		yzStatus.VRev = (int)GetL(dictionary, "vrev", 1L);
		yzStatus.VErr = GetL(dictionary, "verr", 0L);
		yzStatus.Bod = (int)GetL(dictionary, "bod", -1L);
		yzStatus.BodHw = (int)GetL(dictionary, "bodhw", -1L);
		yzStatus.PTrace = (int)GetL(dictionary, "ptrace", -1L);
		yzStatus.Rec = (int)GetL(dictionary, "rec", 0L);
		yzStatus.RecRst = (int)GetL(dictionary, "recrst", -1L);
		yzStatus.AzBlk = (int)GetL(dictionary, "azblk", 0L);
		yzStatus.AzBlkCnt = GetL(dictionary, "azblkn", 0L);
		yzStatus.ElAcc = GetL(dictionary, "elacc", 0L);
		yzStatus.ElRej = GetL(dictionary, "elrej", 0L);
		yzStatus.ElRc = (int)GetL(dictionary, "elrc", -1L);
		yzStatus.ElLastX10 = GetL(dictionary, "ellast", -32768L);
		yzStatus.EcOk = GetL(dictionary, "ecok", 0L);
		yzStatus.EcBad = GetL(dictionary, "ecbad", 0L);
		yzStatus.EcRange = GetL(dictionary, "ecrange", 0L);
		yzStatus.EcPrio = GetL(dictionary, "ecprio", 0L);
		yzStatus.EcLast = Get(dictionary, "eclast", "");
		yzStatus.WdtVer = (int)GetL(dictionary, "wdtVer", -1L);
		yzStatus.BootSt = (int)GetL(dictionary, "bootSt", -1L);
		return yzStatus;
	}

	public static double DegPerSec(long msPerDeg)
	{
		if (msPerDeg > 0)
		{
			return 1000.0 / (double)msPerDeg;
		}
		return 0.0;
	}
}
