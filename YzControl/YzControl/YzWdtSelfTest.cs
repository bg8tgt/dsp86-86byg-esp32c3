namespace YzControl;

public static class YzWdtSelfTest
{
	public const int HangMaxSec = 120;

	public static int WatchWindowSec(YzHangStage st)
	{
		if (st.ExpectResetAfterSec <= 0)
		{
			return st.Seconds + 12;
		}
		return st.Seconds + 90;
	}

	public static YzHangStage[] Stages(int n)
	{
		if (n < 1)
		{
			n = 1;
		}
		if (n > 10)
		{
			n = 10;
		}
		YzHangStage yzHangStage = new YzHangStage();
		yzHangStage.Key = "C";
		yzHangStage.Title = "C 档 · 合法停顿不误触发（hang=5，不关中断）";
		yzHangStage.Seconds = 5;
		yzHangStage.NoInt = false;
		yzHangStage.Repeat = 1;
		yzHangStage.ExpectResetAfterSec = 0;
		yzHangStage.ExpectText = "期望**不复位**（5 s < 第 1 层 15 s）：注入跑完、hangDone +1、rstSeq 不变";
		YzHangStage yzHangStage2 = new YzHangStage();
		yzHangStage2.Key = "A";
		yzHangStage2.Title = "A 档 · 第 1 层任务看门狗（hang=20，不关中断）";
		yzHangStage2.Seconds = 20;
		yzHangStage2.NoInt = false;
		yzHangStage2.Repeat = n;
		yzHangStage2.ExpectResetAfterSec = 15;
		yzHangStage2.ExpectText = "期望**每次约 15 秒后自动重启**（TWDT 第 1 层）：每次 rstSeq +1、rst=task_wdt、wdtWhy=1";
		YzHangStage yzHangStage3 = new YzHangStage();
		yzHangStage3.Key = "B";
		yzHangStage3.Title = "B 档 · 第 2 层硬件看门狗（hang=40 + noint=1）";
		yzHangStage3.Seconds = 40;
		yzHangStage3.NoInt = true;
		yzHangStage3.Repeat = 1;
		yzHangStage3.ExpectResetAfterSec = 30;
		yzHangStage3.ExpectText = "期望**约 30 秒后自动重启**（RTC_WDT 第 2 层，连中断都被关掉也照响）：rstSeq +1、wdtWhy=1";
		return new YzHangStage[3] { yzHangStage, yzHangStage2, yzHangStage3 };
	}

	public static bool QueryAllowed(YzHangStage st, out string why)
	{
		why = "";
		if (st == null)
		{
			why = "档位为空";
			return false;
		}
		if (st.Seconds < 0 || st.Seconds > 120)
		{
			why = $"注入 {st.Seconds} s 超出固件上限 0~{120} s（固件会回 400 ERR:hang-range）";
			return false;
		}
		if (st.Repeat < 1 || st.Repeat > 10)
		{
			why = "重复次数必须在 1~10";
			return false;
		}
		return true;
	}

	public static string Judge(YzHangStage st, int rstSeqBefore, int rstSeqAfter, long elapsedMs, string rstName, int wdtWhy, out bool pass)
	{
		pass = false;
		string text = (string.IsNullOrEmpty(rstName) ? "?" : rstName);
		string text2 = wdtWhy switch
		{
			0 => "wdtWhy=0（本次复位**不是**看门狗触发的）", 
			1 => "wdtWhy=1（本次复位**是看门狗**触发的）", 
			_ => "wdtWhy=未上报", 
		};
		if (st == null)
		{
			return "（档位为空）";
		}
		if (st.ExpectResetAfterSec <= 0)
		{
			if (rstSeqAfter == rstSeqBefore)
			{
				pass = true;
				return $"✔ 符合预期：**没有复位**（rstSeq {rstSeqBefore} → {rstSeqAfter} 未变），耗时 {(double)elapsedMs / 1000.0:0.0} s；`hangDone` 应 +1（见下方 /sys 回读）—— 这一档证明『合法停顿不误触发』";
			}
			return $"✗ 不符合预期：本该**不复位**，却发生了复位（rstSeq {rstSeqBefore} → {rstSeqAfter}，rst={text}）——5 s 的停顿不该被任何一层看门狗打断，请查固件超时配置";
		}
		if (rstSeqAfter > rstSeqBefore)
		{
			pass = true;
			return string.Format("✔ 符合预期：**已自动复位**（rstSeq {0} → {1}，+{2}），耗时 {3:0.0} s（期望约 {4} s 后由{5}触发），本次 rst={6}、{7}", rstSeqBefore, rstSeqAfter, rstSeqAfter - rstSeqBefore, (double)elapsedMs / 1000.0, st.ExpectResetAfterSec, st.NoInt ? "第 2 层 RTC_WDT" : "第 1 层 TWDT", text, text2);
		}
		return string.Format("✗ 不符合预期：等满 {0:0.0} s 仍**没有复位**（rstSeq 仍 {1}）——说明{2}没有生效（这正是「整块板没有运行期看门狗」的形态 ✗），请查 wdtSub/wdtDeg/rtcwOk/rtcwTk", (double)elapsedMs / 1000.0, rstSeqAfter, st.NoInt ? "第 2 层 RTC_WDT(30 s)" : "第 1 层 TWDT(15 s)");
	}
}
