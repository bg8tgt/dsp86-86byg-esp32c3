using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace YzControl;

internal sealed class ElevationGauge : HudInstrument
{
	private double _el;

	private double _limUp = 70.0;

	private double _limDown = -70.0;

	private double _target = double.NaN;

	private string _state = "停止";

	public void Set(double el, double limUp, double limDown, string state)
	{
		// v1.5.1：只在角度/限位/状态真的变了才重绘
		bool changed = Math.Abs(el - _el) > 0.05 || Math.Abs(limUp - _limUp) > 0.01 || Math.Abs(limDown - _limDown) > 0.01 || !string.Equals(state, _state, StringComparison.Ordinal);
		_el = el;
		_limUp = limUp;
		_limDown = limDown;
		_state = state;
		if (changed)
		{
			Invalidate();
		}
	}

	public void SetTarget(double t)
	{
		bool changed = double.IsNaN(t) != double.IsNaN(_target) || Math.Abs(t - _target) > 0.1;
		_target = t;
		if (changed)
		{
			Invalidate();
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		Rectangle rectangle = DrawFrame(graphics, "俯仰仪表（EL）");
		if (rectangle.Width < 40 || rectangle.Height < 40)
		{
			return;
		}
		float num = rectangle.Left + 14;
		float num2 = (float)rectangle.Top + (float)rectangle.Height / 2f + 10f;
		float num3 = Math.Min(rectangle.Width - 30, (float)(rectangle.Height - 40) * 1.35f);
		using (Pen pen = new Pen(Color.FromArgb(70, HudDraw.Cyan), 1.4f))
		{
			graphics.DrawArc(pen, num - num3, num2 - num3, num3 * 2f, num3 * 2f, -90f, 180f);
		}
		for (int i = -90; i <= 90; i += 5)
		{
			double num4 = (double)(-i) * Math.PI / 180.0;
			bool flag = i % 30 == 0;
			float num5 = (flag ? 11 : 5);
			float x = num + (float)Math.Cos(num4) * (num3 - num5);
			float y = num2 + (float)Math.Sin(num4) * (num3 - num5);
			float x2 = num + (float)Math.Cos(num4) * num3;
			float y2 = num2 + (float)Math.Sin(num4) * num3;
			using (Pen pen2 = new Pen(flag ? HudDraw.Cyan : Color.FromArgb(90, HudDraw.Cyan), flag ? 1.5f : 1f))
			{
				graphics.DrawLine(pen2, x, y, x2, y2);
			}
			if (flag)
			{
				using Font font = Theme.FMonoF(8f, FontStyle.Bold);
				string text = i + "°";
				Size size = TextRenderer.MeasureText(text, font);
				float num6 = num + (float)Math.Cos(num4) * (num3 + 14f);
				float num7 = num2 + (float)Math.Sin(num4) * (num3 + 14f);
				TextRenderer.DrawText(graphics, text, font, new Point((int)(num6 - (float)size.Width / 2f), (int)(num7 - (float)size.Height / 2f)), Theme.TextDim, TextFormatFlags.NoPadding);
			}
		}
		DrawMarker(graphics, num, num2, num3, _limUp, HudDraw.Danger);
		DrawMarker(graphics, num, num2, num3, _limDown, HudDraw.Danger);
		if (!double.IsNaN(_target))
		{
			DrawMarker(graphics, num, num2, num3, _target, HudDraw.Green);
		}
		double num8 = (0.0 - _el) * Math.PI / 180.0;
		float num9 = num + (float)Math.Cos(num8) * (num3 - 14f);
		float num10 = num2 + (float)Math.Sin(num8) * (num3 - 14f);
		using (Pen pen3 = new Pen(Color.FromArgb(55, HudDraw.Cyan), 6f))
		{
			graphics.DrawLine(pen3, num, num2, num9, num10);
		}
		using (Pen pen4 = new Pen(HudDraw.Cyan, 2f))
		{
			graphics.DrawLine(pen4, num, num2, num9, num10);
		}
		HudInstrument.GlowDot(graphics, num9, num10, 4f, HudDraw.Cyan);
		HudInstrument.GlowDot(graphics, num, num2, 3.5f, HudDraw.Cyan);
		using (Font font2 = Theme.FMonoF(14.5f, FontStyle.Bold))
		{
			TextRenderer.DrawText(graphics, _el.ToString("0.0") + "°", font2, new Point((int)num + 6, (int)(num2 - num3 * 0.45f)), Color.White, TextFormatFlags.NoPadding);
		}
		using Font font3 = Theme.FUi(8.5f, FontStyle.Bold);
		TextRenderer.DrawText(graphics, "限位 " + _limDown.ToString("0") + "° ~ " + _limUp.ToString("0") + "°", font3, new Point((int)num + 6, (int)(num2 - num3 * 0.45f + 20f)), Theme.TextDim, TextFormatFlags.NoPadding);
		TextRenderer.DrawText(graphics, _state, font3, new Point((int)num + 6, (int)(num2 - num3 * 0.45f + 36f)), HudDraw.Cyan, TextFormatFlags.NoPadding);
	}

	private static void DrawMarker(Graphics g, float cx, float cy, float R, double val, Color c)
	{
		double num = (0.0 - val) * Math.PI / 180.0;
		float num2 = cx + (float)Math.Cos(num) * R;
		float num3 = cy + (float)Math.Sin(num) * R;
		using SolidBrush brush = new SolidBrush(c);
		g.FillPolygon(brush, new Point[4]
		{
			new Point((int)num2, (int)num3 - 5),
			new Point((int)num2 + 5, (int)num3),
			new Point((int)num2, (int)num3 + 5),
			new Point((int)num2 - 5, (int)num3)
		});
	}
}
