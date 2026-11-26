using System.Text;

namespace YzControl;

public static class YzBootStage
{
	public const int Ready = 9;

	public static readonly string[] Names = new string[10] { "", "NVS", "IO", "NET", "ROUTES", "HTTP", "HTTPS", "BUS", "WDT", "READY" };

	public static string Name(int n)
	{
		if (n >= 1 && n < Names.Length)
		{
			return Names[n];
		}
		return "?";
	}

	public static string Table()
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 1; i < Names.Length; i++)
		{
			if (i > 1)
			{
				stringBuilder.Append(" · ");
			}
			stringBuilder.Append("S").Append(i).Append(' ')
				.Append(Names[i]);
		}
		return stringBuilder.ToString();
	}

	public static string Text(int bootSt)
	{
		if (bootSt >= 9)
		{
			return "启动完成 ✓（bootSt=9 = S9 READY 已打印）";
		}
		if (bootSt >= 1)
		{
			int num = bootSt + 1;
			return string.Format("⚠ 卡在第 {0} 阶段 ✗（已完成 S{0} {1}，S{2} {3} 未完成）—— 启动未走完，此时**看门狗可能还没武装**（武装在 S8），请勿远离现场/断电重启前先看串口", bootSt, Name(bootSt), num, Name(num));
		}
		return "bootSt=0/缺字段：设备未上报启动阶段（老固件，或刚重启还没来得及上报）——此时无法据此判定启动是否完成";
	}

	public static bool Stuck(int bootSt)
	{
		if (bootSt >= 0)
		{
			return bootSt < 9;
		}
		return false;
	}
}
