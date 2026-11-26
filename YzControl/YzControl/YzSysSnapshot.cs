using System;
using System.Collections.Generic;
using System.Globalization;

namespace YzControl;

public sealed class YzSysSnapshot
{
	public string Raw = "";

	public readonly Dictionary<string, string> Map = new Dictionary<string, string>(StringComparer.Ordinal);

	public string Ver => Get("ver", "");

	public string Bld => Get("bld", "");

	public long UpSec => GetL("up", 0L);

	public int Heap => GetI("heap", 0);

	public int HeapMin => GetI("heap_min", 0);

	public long LoopMaxMs => GetL("loopmax", 0L);

	public int Crumb => GetI("crumb", -1);

	public string CrumbName => Get("crname", "");

	public int RstSeq => GetI("rstSeq", -1);

	public int LastWasPanic => GetI("panic", -1);

	public long VErr => GetL("verr", 0L);

	public int Bod => GetI("bod", -1);

	public int BodHw => GetI("bodHw", -1);

	public int BodPre => GetI("bodPre", -1);

	public long BodWr => GetL("bodWr", 0L);

	public long BodClamp => GetL("bodClamp", 0L);

	public int BodErr => GetI("bodErr", 0);

	public int BodDet => GetI("bodDet", -1);

	public int Rec => GetI("rec", 0);

	public int RecRst => GetI("recrst", -1);

	public long RecN => GetL("recn", 0L);

	public string RecName => Get("recName", "");

	public int PTrace => GetI("ptrace", -1);

	public long WdtFeed => GetL("wdtFeed", 0L);

	public int WdtAdd => GetI("wdtAdd", GetI("wdtAddRc", -9999));

	public int WdtLpEn => GetI("wdtLpEn", -1);

	public long WdtLpMaxMs => GetL("wdtLpMax", -1L);

	public int RtcwEn => GetI("rtcwEn", -1);

	public int RtcwStg0 => GetI("rtcwStg0", -1);

	public int RtcwOk => GetI("rtcwOk", -1);

	public int RtcwRc => GetI("rtcwRc", -9999);

	public long HangN => GetL("hangN", 0L);

	public long HangDone => GetL("hangDone", 0L);

	public int HangNoInt => GetI("hangNoInt", -1);

	public int WdtWhy => GetI("wdtWhy", -1);

	public int WdtVer => GetI("wdtVer", -1);

	public int WdtSub => GetI("wdtSub", -1);

	public int WdtFix => GetI("wdtFix", -1);

	public int WdtDeg => GetI("wdtDeg", -1);

	public long SilN => GetL("silN", -1L);

	public int WdtRe => GetI("wdtRe", -9999);

	public int WdtIn => GetI("wdtIn", -9999);

	public long RtcwTk => GetL("rtcwTk", -1L);

	public long RtcwTkWant => GetL("rtcwTkWant", -1L);

	public int BootSt => GetI("bootSt", -1);

	public int WdtArmS => GetI("wdtArmS", -1);

	public long Wto => GetL("wto", 0L);

	public string Rst => Get("rst", "");

	public string StaIp => Get("ip", "");

	public string EthIp => Get("eip", "");

	public int AzBlk => GetI("azblk", 0);

	public long AzBlkCnt => GetL("azblkn", 0L);

	public string AzBlkName => Get("azBlkName", YzFw.AzBlkName(AzBlk));

	public long Msh => GetL("msh", 0L);

	public long ElAcc => GetL("elacc", 0L);

	public long ElRej => GetL("elrej", 0L);

	public int ElRc => GetI("elrc", -1);

	public long ElLastX10 => GetL("ellast", -32768L);

	public long EcOk => GetL("ecok", 0L);

	public long EcBad => GetL("ecbad", 0L);

	public long EcRange => GetL("ecrange", 0L);

	public long EcPrio => GetL("ecprio", 0L);

	public string EcLast => Get("eclast", "");

	public long NvsWrOk => GetL("nvsWrOk", 0L);

	public long NvsWrErr => GetL("nvsWrErr", 0L);

	public long NvsWrN => GetL("nvsWrN", 0L);

	public long NvsFlushN => GetL("nvsFlushN", 0L);

	public long NvsFlushW => GetL("nvsFlushW", 0L);

	public string NvsErrLog => Get("nvsErrLog", "");

	public long HttpRl => GetL("httpRl", 0L);

	public long TlsRl => GetL("tlsRl", 0L);

	public long HttpRlS => GetL("httpRlS", 0L);

	public long TlsRlS => GetL("tlsRlS", 0L);

	public long HttpLh => GetL("httpLh", 0L);

	public long TlsLh => GetL("tlsLh", 0L);

	public int HttpAct => GetI("httpAct", 0);

	public int TlsAct => GetI("tlsAct", 0);

	public long RlWin => GetL("rlWin", 0L);

	public long Lh80 => GetL("lh80", 0L);

	public long Lh443 => GetL("lh443", 0L);

	public string Rlip80 => Get("rlip80", "");

	public int OtaSt => GetI("otast", -1);

	public int OtaErr => GetI("otaerr", 0);

	public long OtaW => GetL("otaw", 0L);

	public long OtaTot => GetL("otatot", 0L);

	public int Ap => GetI("ap", -1);

	public string Mac => Get("mac", "");

	public string Chip => Get("chip", "");

	public bool Has(string k)
	{
		return Map.ContainsKey(k);
	}

	public string Get(string k)
	{
		if (!Map.TryGetValue(k, out var value))
		{
			return null;
		}
		return value;
	}

	public string Get(string k, string def)
	{
		string text = Get(k);
		if (text != null)
		{
			return text;
		}
		return def;
	}

	public long GetL(string k, long def)
	{
		string text = Get(k);
		if (text != null && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return def;
	}

	public int GetI(string k, int def)
	{
		return (int)GetL(k, def);
	}

	public double GetD(string k, double def)
	{
		string text = Get(k);
		if (text != null && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return def;
	}

	public static YzSysSnapshot Parse(string body)
	{
		YzSysSnapshot yzSysSnapshot = new YzSysSnapshot();
		yzSysSnapshot.Raw = body ?? "";
		string[] array = yzSysSnapshot.Raw.Split(';');
		for (int i = 0; i < array.Length; i++)
		{
			int num = array[i].IndexOf('=');
			if (num > 0)
			{
				string text = array[i].Substring(0, num).Trim();
				string value = array[i].Substring(num + 1);
				if (text.Length != 0 && !yzSysSnapshot.Map.ContainsKey(text))
				{
					yzSysSnapshot.Map[text] = value;
				}
			}
		}
		return yzSysSnapshot;
	}
}
