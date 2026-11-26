using System;

namespace YzControl;

public static class YzHttpMsg
{
	public static string Explain(int code, string body)
	{
		string text = (body ?? "").Trim();
		string text2 = text.ToLowerInvariant();
		if (text2.IndexOf("az-limit-top", StringComparison.Ordinal) >= 0)
		{
			return "已在上端 360°：只能向 0 方向走（方位减小 →【向右 ▶】/ R，用户口径 360→270→180→0）。这是固件的端点保护（/st 的 azblk=1），不是软件故障——离开端点随时可走。";
		}
		if (text2.IndexOf("az-limit-bottom", StringComparison.Ordinal) >= 0)
		{
			return "已在下端 0°：只能向 360 方向走（方位增大 →【◀ 向左】/ L，用户口径 0→180→270→360）。这是固件的端点保护（/st 的 azblk=2），不是软件故障——离开端点随时可走。";
		}
		if (text2.IndexOf("pitch-limit-up", StringComparison.Ordinal) >= 0)
		{
			return "俯仰已到上限（vu）：只允许下行「▼ 俯角 / VD」。这是限位保护，不是软件故障。";
		}
		if (text2.IndexOf("pitch-limit-down", StringComparison.Ordinal) >= 0)
		{
			return "俯仰已到下限（vd）：只允许上行「▲ 仰角 / VU」。这是限位保护，不是软件故障。";
		}
		if (text2.IndexOf("pitch-polarity-fixed", StringComparison.Ordinal) >= 0)
		{
			return "c30 起**禁止运行期俯仰极性翻转**（会造成 va 与物理位置镜像 → 顶死）：如需反向请改接线或刷配套镜像。";
		}
		if (text2.IndexOf("va=0 only", StringComparison.Ordinal) >= 0 || (code == 400 && text2 == "err:args(va=0 only)"))
		{
			return "/set?va= 只接受 0（= 俯仰姿态归零）；非 0 一律 400。请改用「俯仰当前姿态归零」按钮（/cmd?g=VZ）。";
		}
		if (text2.IndexOf("ota-subnet", StringComparison.Ordinal) >= 0)
		{
			return "上传被固件的**网段闸门**拒绝（403 ERR:ota-subnet）：本机与板子的无线 STA / 有线地址都不在同一 /24。请点「② 一键 OTA（自动修复网段闸门）」——它会先 POST /wifi 让 STA 拿到同网段地址再上传。";
		}
		if (text2.IndexOf("ota-token", StringComparison.Ordinal) >= 0)
		{
			return "固件已设置 OTA 令牌（otat=1）：上传必须带 ?t=<令牌> 或 X-Yz-Token 头。本程序不猜令牌——请在设备端 /set?otat= 清空令牌，或用网页版升级页手工填令牌。";
		}
		if (text2.IndexOf("ota-ratelimit", StringComparison.Ordinal) >= 0 || code == 429)
		{
			return "上传被固件的**速率限制**拒绝（429 ERR:ota-ratelimit）：距上次真正开始上传不到 3 秒。等 3 秒再点一次即可。";
		}
		if (text2.IndexOf("ota-incomplete", StringComparison.Ordinal) >= 0)
		{
			return "OTA 上传不完整（ERR:ota-incomplete）：镜像没写完。请重试；必要时断电重启后改用 USB 整片烧录。";
		}
		if (text2.IndexOf("err:busy", StringComparison.Ordinal) >= 0 || code == 409)
		{
			return "设备忙（HTTP " + code + " ERR:busy）：云台正在运动/自检中，等它停下再试。";
		}
		if (text2.IndexOf("err:args", StringComparison.Ordinal) < 0)
		{
			switch (code)
			{
			case 400:
				break;
			case 503:
				return "设备侧 Web 自保护限流（HTTP 503）：本机请求过密，等 1~2 秒再试（不影响 DTrac 的 4533）。";
			case 403:
				return "被固件拒绝（HTTP 403）：" + ((text.Length == 0) ? "无正文" : text);
			case 0:
				if (text.Length != 0)
				{
					return text;
				}
				return "无应答（超时/连接失败）";
			default:
				return "HTTP " + code + ((text.Length == 0) ? "" : ("：" + text));
			}
		}
		return "参数被拒（HTTP 400 ERR:args）：请检查输入的数值范围（方位 0~360、俯仰在 vd~vu 之间、速度在允许区间）。";
	}

	public static bool LooksOk(int code, string body)
	{
		string text = (body ?? "").Trim();
		if (code == 200)
		{
			return text.StartsWith("OK", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}
}
