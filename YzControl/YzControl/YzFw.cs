namespace YzControl;

public static class YzFw
{
	public static string AzBlkName(int code)
	{
		return code switch
		{
			1 => "上端 360° 朝外被拒（只许反转离开）", 
			2 => "下端 0° 朝外被拒（只许正转离开）", 
			3 => "走不动，已自动退出运动态并停机", 
			_ => "无", 
		};
	}

	public static string ElRejName(int code)
	{
		return code switch
		{
			1 => "手动全停保持窗口内（协议侧动作被暂缓 2 秒）", 
			2 => "俯仰回零中", 
			3 => "数值非法（NaN/越界）", 
			_ => "无", 
		};
	}

	public static string CrumbText(int crumb)
	{
		return crumb switch
		{
			0 => "无（RTC 被掉电清空）", 
			1 => "loop起点", 
			2 => "以太网任务", 
			3 => "EasyComm轮询", 
			4 => "HTTPS", 
			5 => "屏幕", 
			6 => "电机/倾角", 
			7 => "WiFi仲裁", 
			8 => "网页处理", 
			9 => "OTA", 
			81 => "接口:/st", 
			82 => "接口:/sys", 
			83 => "接口:/cmd", 
			84 => "接口:/goto", 
			85 => "接口:/主页", 
			86 => "接口:/js", 
			87 => "接口:/set", 
			88 => "接口:/radio", 
			89 => "接口:其他", 
			101 => "接口:/wifi", 
			102 => "接口:/scan", 
			103 => "接口:/eth", 
			104 => "接口:/az", 
			105 => "接口:/otas", 
			106 => "接口:/rlink", 
			107 => "接口:/ap", 
			108 => "接口:/ac", 
			109 => "接口:/rst", 
			110 => "接口:/panic", 
			111 => "接口:/rgrp", 
			_ => "c" + crumb, 
		};
	}

	public static string BodErrName(int code)
	{
		return code switch
		{
			1 => "回读不符（写入未生效）", 
			2 => "非法或被安全下限钳位", 
			3 => "拒绝（不允许调到比当前档更不宽容）", 
			4 => "NVS 里的值越界", 
			_ => "无", 
		};
	}

	public static string RstName(string nm)
	{
		if (nm == null)
		{
			return "未知";
		}
		switch (nm.Trim().ToLowerInvariant())
		{
		case "panic":
			return "真崩溃（panic，看 /panic 现场）";
		case "taskwdt":
			return "任务看门狗（某个任务长时间独占 CPU）";
		case "intwdt":
			return "中断看门狗";
		case "wdt":
			return "看门狗复位";
		case "brownout":
			return "供电跌落（电气问题）";
		case "brown":
			return "供电跌落（电气问题）";
		case "poweron":
			return "上电（真实掉电/上电）";
		case "sw":
			return "软件主动重启（ESP.restart，如 /cmd?g=RB）";
		case "extrst":
		case "ext":
			return "外部复位（RST 引脚/按键）";
		case "deep":
			return "深度睡眠唤醒";
		case "unk":
			return "未记录（RTC 已被掉电清空）";
		default:
			return nm;
		}
	}

	public static string RecRstName(int code)
	{
		return code switch
		{
			0 => "未记录", 
			1 => "上电（poweron）", 
			2 => "外部复位（ext）", 
			3 => "软件重启（sw）", 
			4 => "真崩溃（panic）", 
			5 => "中断看门狗（intwdt）", 
			6 => "任务看门狗（taskwdt）", 
			7 => "看门狗（wdt）", 
			8 => "深度睡眠（deep）", 
			9 => "供电跌落（brownout）", 
			_ => "码 " + code, 
		};
	}
}
