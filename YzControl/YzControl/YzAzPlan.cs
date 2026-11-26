using System;

namespace YzControl;

public static class YzAzPlan
{
	public sealed class Plan
	{
		public double Cur;

		public double Tgt;

		public double TravelDeg;

		public double TravelSec;

		public bool LongWay;

		public bool Standstill;

		public string Dir = "";

		public string Hint = "";

		public int NoWrap = 1;
	}

	public static double Clamp360(double d)
	{
		if (double.IsNaN(d) || d < 0.0 || d > 360.0)
		{
			return 0.0;
		}
		return d;
	}

	public static double WrapDelta(double d)
	{
		while (d >= 180.0)
		{
			d -= 360.0;
		}
		while (d < -180.0)
		{
			d += 360.0;
		}
		return d;
	}

	public static Plan Compute(double cur, double tgt, long msPerDeg, int noWrap)
	{
		Plan plan = new Plan();
		plan.Cur = Clamp360(cur);
		plan.Tgt = Clamp360(tgt);
		plan.NoWrap = ((noWrap != 0) ? 1 : 0);
		if (msPerDeg <= 0)
		{
			msPerDeg = 0L;
		}
		if (plan.NoWrap == 1)
		{
			plan.TravelDeg = Math.Abs(plan.Tgt - plan.Cur);
			plan.Standstill = plan.TravelDeg < 0.05;
			plan.Dir = (plan.Standstill ? "已到位" : ((plan.Tgt > plan.Cur) ? "正转（方位增大）" : "反转（方位减小）"));
		}
		else
		{
			double num = WrapDelta(plan.Tgt - plan.Cur);
			plan.TravelDeg = Math.Abs(num);
			plan.Standstill = plan.TravelDeg < 0.05;
			plan.Dir = (plan.Standstill ? "已到位" : ((num > 0.0) ? "正转（最短路径）" : "反转（最短路径）"));
		}
		plan.TravelSec = ((msPerDeg <= 0) ? 0.0 : (plan.TravelDeg * (double)msPerDeg / 1000.0));
		plan.LongWay = plan.TravelDeg > 180.0;
		plan.Hint = hint(plan);
		return plan;
	}

	private static string hint(Plan p)
	{
		string text = $"当前 {p.Cur:0.#}° → 目标 {p.Tgt:0.#}°：{p.Dir} {p.TravelDeg:0.#}°";
		if (p.Standstill)
		{
			return text + "（已在目标点，不会动）";
		}
		if (p.LongWay)
		{
			return text + $"\u3000⚠ 将绕行 ≈{p.TravelSec:0.#} s（走内部长边，不穿越 0/360 端点）";
		}
		return text + $"\u3000≈{p.TravelSec:0.#} s";
	}

	public static string EndpointHint(double curAz)
	{
		if (curAz >= 359.5)
		{
			return "⚠ 已在上端 360°：只允许「方位减小」（界面【向右 ▶】/ R）离开——朝外会被固件 409 拒绝（azblk=1）";
		}
		if (curAz <= 0.5)
		{
			return "⚠ 已在下端 0°：只允许「方位增大」（界面【◀ 向左】/ L）离开——朝外会被固件 409 拒绝（azblk=2）";
		}
		return "";
	}
}
