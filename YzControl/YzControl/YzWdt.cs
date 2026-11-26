using System.Text;

namespace YzControl;

public static class YzWdt
{
	public const int TwdtMs = 15000;

	public const int RtcwMs = 30000;

	public const long RtcwTkExpected = 1048575L;

	public const int WdtVerC38 = 4;

	public static string TickWantNote(YzSysSnapshot s)
	{
		if (s == null || !s.Has("rtcwTkWant") || s.RtcwTkWant < 0)
		{
			return "";
		}
		if (s.RtcwTkWant == 1048575)
		{
			return "";
		}
		return $"⚠ 现场上报 rtcwTkWant={s.RtcwTkWant}（≠ c38 的默认 {1048575L}）⇒ 该固件**改过**第 2 层超时值/慢钟换算；本程序**只用设备自己上报的 want 值**判「是否武装」⇒ 只要 rtcwTk == rtcwTkWant 就算已武装 ✓（但自测 B 档的『约 30 秒』是按 c38 的 30 s 写的，未必成立 —— 见档位提示）";
	}

	public static string ExpectAdvisory(YzSysSnapshot s)
	{
		if (s == null)
		{
			return "（没有 /sys 快照，无法给出档位前提提示；建议先点「刷新诊断 /sys」再自测）";
		}
		StringBuilder stringBuilder = new StringBuilder();
		if (!Layer1Armed(s))
		{
			stringBuilder.AppendLine("· ⚠ **第 1 层未订阅/已降级**（wdtSub=" + s.WdtSub + "、wdtDeg=" + s.WdtDeg + "）⇒ A 档（hang=20）**不会**被 TWDT 复位；此时 A 档「不复位」是**固件配置问题**，不是测试失败的解释。");
		}
		else
		{
			stringBuilder.AppendLine("· 第 1 层已订阅（wdtSub=1、wdtDeg=0）⇒ A 档（hang=20）应当被 TWDT 复位（c38 的超时 = 15 s）。");
		}
		if (!Layer2Armed(s))
		{
			stringBuilder.AppendLine("· ⚠ **第 2 层未武装**（rtcwOk=" + s.RtcwOk + "、rtcwEn=" + s.RtcwEn + "、rtcwStg0=" + s.RtcwStg0 + "）⇒ B 档（hang=40&noint=1）**不会**复位；请先处理第 2 层，再跑自测。");
		}
		else
		{
			stringBuilder.AppendLine("· 第 2 层判定为已武装（rtcwOk=1、rtcwEn=1、rtcwStg0=3、rtcwTk=rtcwTkWant=" + s.RtcwTk + "）。");
		}
		string text = TickWantNote(s);
		if (text.Length > 0)
		{
			stringBuilder.AppendLine("· " + text);
		}
		if (s.BootSt >= 0 && s.BootSt < 9)
		{
			stringBuilder.AppendLine("· ⚠ 当前 bootSt=" + s.BootSt + " < 9：" + YzBootStage.Text(s.BootSt) + " ⇒ 建议先解决启动问题再自测。");
		}
		stringBuilder.AppendLine("· 档位期望（c36~c38 口径）：C 档不复位 / A 档约 15 s（注入 20 s）⇒ 若要验证第 1 层，hang 必须 > 该层超时；B 档约 30 s（注入 40 s + 关中断）。判定一律用 `/sys` 的 rstSeq/rst/wdtWhy 真值 ✓");
		return stringBuilder.ToString();
	}

	public static bool Layer2TickMismatch(YzSysSnapshot s)
	{
		if (s == null)
		{
			return false;
		}
		if (!s.Has("rtcwTk") || !s.Has("rtcwTkWant"))
		{
			return false;
		}
		return s.RtcwTk != s.RtcwTkWant;
	}

	public static bool Layer2Armed(YzSysSnapshot s)
	{
		if (s == null)
		{
			return false;
		}
		if (s.RtcwEn == 1 && s.RtcwStg0 == 3)
		{
			return s.RtcwOk == 1;
		}
		return false;
	}

	public static bool Layer1Armed(YzSysSnapshot s)
	{
		if (s == null)
		{
			return false;
		}
		if (s.WdtSub == 1)
		{
			return s.WdtDeg == 0;
		}
		return false;
	}

	private static string Rc(int v)
	{
		return v switch
		{
			-9999 => "未上报", 
			-1 => "-1（未走该分支）", 
			0 => "0（成功）", 
			_ => v + "（⚠ 非 0）", 
		};
	}

	public static YzWdtView Build(YzSysSnapshot s)
	{
		YzWdtView yzWdtView = new YzWdtView();
		if (s == null)
		{
			yzWdtView.L1 = (yzWdtView.L2 = (yzWdtView.Boot = (yzWdtView.Sil = "（未读到 /sys，无法判定）")));
			yzWdtView.L1Sev = (yzWdtView.L2Sev = (yzWdtView.BootSev = (yzWdtView.SilSev = YzSev.Warn)));
			return yzWdtView;
		}
		if (!s.Has("wdtSub") && !s.Has("wdtLpEn") && !s.Has("wdtDeg"))
		{
			yzWdtView.L1Sev = YzSev.Warn;
			yzWdtView.L1 = "该固件早于 c36/c37：/sys 里**没有**第 1 层看门狗字段（wdtSub/wdtLpEn/wdtDeg）⇒ 无法判定（请以串口 [WDT1] 行或整机升级后再看）";
		}
		else
		{
			bool num = Layer1Armed(s);
			bool flag = s.WdtAdd != -9999 && s.WdtAdd != 0;
			bool flag2 = s.WdtRe != -9999 && s.WdtRe != 0 && s.WdtRe != -1;
			bool flag3 = s.WdtIn != -9999 && s.WdtIn != 0 && s.WdtIn != -1;
			int l1Sev = ((!num) ? 2 : ((flag || flag2 || flag3 || s.WdtFix > 0) ? 1 : 0));
			yzWdtView.L1Sev = (YzSev)l1Sev;
			yzWdtView.L1 = string.Format("wdtSub={0}（{1}）· 降级 wdtDeg={2}（{3}）· 自愈 wdtFix={4}{5} · 订阅返回码 wdtAdd={6} · 配置返回码 wdtRe={7} / 初始化 wdtIn={8} · 喂狗 wdtFeed={9}（递增）· 最长喂狗间隔 wdtLpMax={10} · 看门狗接线版本 wdtVer={11}（c38=4）", s.WdtSub, (s.WdtSub == 1) ? "已订阅 ⇒ 卡死 15 s 会 panic 重启 ✓" : ((s.WdtSub == 0) ? "⚠ **未订阅/已降级** ⇒ 第 1 层不会有 15 s 复位 ✗" : "未上报"), s.WdtDeg, (s.WdtDeg == 0) ? "0 正常" : "1 ⚠ 已降级（只剩第 2 层 + 业务心跳）", s.WdtFix, (s.WdtFix > 0) ? "（⚠ 执行过自愈 delete+reconfigure+add —— 说明订阅曾失败过）" : "（正常 0）", Rc(s.WdtAdd), Rc(s.WdtRe), Rc(s.WdtIn), s.WdtFeed, (s.WdtLpMaxMs < 0) ? "未上报" : (s.WdtLpMaxMs + " ms"), (s.WdtVer < 0) ? "未上报" : (s.WdtVer + ((s.WdtVer > 4) ? "（比 c38 的 4 更新）" : "（c36=2 / c37=3 / c38=4）")));
		}
		if (!s.Has("rtcwEn") && !s.Has("rtcwStg0") && !s.Has("rtcwOk") && !s.Has("rtcwTk"))
		{
			yzWdtView.L2Sev = YzSev.Warn;
			yzWdtView.L2 = "该固件早于 c36/c38：/sys 里**没有**第 2 层看门狗字段（rtcwEn/rtcwStg0/rtcwOk/rtcwTk）⇒ 无法判定";
		}
		else
		{
			bool flag4 = Layer2TickMismatch(s);
			bool flag5 = Layer2Armed(s);
			yzWdtView.Layer2Unarmed = flag4 || !flag5;
			yzWdtView.L2Sev = (yzWdtView.Layer2Unarmed ? YzSev.Bad : YzSev.Ok);
			string text = ((s.RtcwTk < 0 || s.RtcwTkWant < 0) ? "rtcwTk/rtcwTkWant=未上报（该固件早于 c38，没有 ticks 回读）" : ((!flag4) ? $"rtcwTk={s.RtcwTk} == rtcwTkWant={s.RtcwTkWant} ⇒ 超时值回读一致 ✓" : $"rtcwTk={s.RtcwTk} ≠ rtcwTkWant={s.RtcwTkWant} ⇒ **第 2 层未武装** ✗（硬件里回读到的超时 ticks 与固件**自己**算出的期望值不一致 —— 这种「看门狗」要么不响、要么乱响，必须当故障处理；判据用的是设备上报的 want 值，不是本程序写死的常数）"));
			yzWdtView.L2 = string.Format("rtcwOk={0}（{1}）· rtcwEn={2}（1 = 已使能）· rtcwStg0={3}（3 = RESET_SYSTEM，即「真会复位」）· {4}{5}", s.RtcwOk, (s.RtcwOk == 1) ? "1 = 启用+回读一致 ✓" : ((s.RtcwOk == 0) ? "0 ⚠ **第 2 层未武装** ✗" : "未上报"), s.RtcwEn, s.RtcwStg0, text, (s.RtcwRc == -9999) ? "" : (" · rtcwRc=" + s.RtcwRc));
			if (yzWdtView.Layer2Unarmed && s.RtcwOk == 1 && !flag4)
			{
				yzWdtView.L2 += "\u3000⚠ 第 2 层未武装（en/STAGE0/rtcwOk 未同时满足）";
			}
		}
		yzWdtView.Boot = string.Format("{0} · 武装点 wdtArmS={1}（{2}）· 阶段名表：{3}", YzBootStage.Text(s.BootSt), s.WdtArmS, (s.WdtArmS == 7) ? "7 = S7 BUS 之后 ⇒ 看门狗**最后才启用** ✓（HTTP 早已可用，武装失败也堵不死启动）" : ((s.WdtArmS < 0) ? "未上报" : "⚠ 非 7：与 c38 的「最后才启用」不符，请核对固件"), YzBootStage.Table());
		yzWdtView.BootSev = ((s.BootSt < 9) ? ((s.BootSt < 1) ? YzSev.Warn : YzSev.Bad) : YzSev.Ok);
		long silN = s.SilN;
		string text2;
		YzSev yzSev;
		if (silN < 0)
		{
			text2 = "silN=未上报（该固件早于 c37）";
			yzSev = YzSev.Warn;
		}
		else if (silN == 0L)
		{
			text2 = "silN=0（没有被抑制的重复日志 ⇒ 没有刷屏）";
			yzSev = YzSev.Ok;
		}
		else
		{
			text2 = string.Format("silN={0}（**有 {0} 条重复日志被限频闸抑制** —— 说明曾经出现过刷屏源，已被压住 ✓）", silN);
			yzSev = YzSev.Warn;
		}
		yzWdtView.Sil = string.Format("{0} · wto={1}（TWDT 超时次数，**应恒 0**）· hangN={2} / hangDone={3}（{4}）· 本次复位 rst={5}、wdtWhy={6}（{7}）", text2, s.Wto, s.HangN, s.HangDone, (s.HangDone == 0L) ? "0 = 注入后都被复位了 ✓（量产后应恒 0）" : "⚠ 非 0 = 有注入「跑完却没被复位」 ⇒ 看门狗没生效 ✗", (s.Rst.Length == 0) ? "?" : s.Rst, s.WdtWhy, (s.WdtWhy == 1) ? "1 = 本次复位**是看门狗**触发的" : ((s.WdtWhy == 0) ? "0 = 本次复位**不是**看门狗触发（上电/软重启/掉电…）" : "未上报"));
		yzWdtView.SilSev = ((yzSev != 0 || s.Wto != 0L || s.HangDone != 0L) ? YzSev.Warn : YzSev.Ok);
		if (s.Wto != 0L || s.HangDone != 0L)
		{
			yzWdtView.SilSev = YzSev.Bad;
		}
		yzWdtView.Advisory = ExpectAdvisory(s);
		return yzWdtView;
	}
}
