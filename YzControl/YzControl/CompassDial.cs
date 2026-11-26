using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace YzControl;

internal sealed class CompassDial : HudInstrument
{
	private double _az;

	private double _target = double.NaN;

	private string _state = "停止";

	private bool _scanning;

	public bool CenterDial { get; set; }

	public void Set(double az, string state, bool scanning)
	{
		// v1.5.1：值没变就不重绘（原来每秒都整盘重画一次，纯浪费）
		bool changed = Math.Abs(az - _az) > 0.05 || !string.Equals(state, _state, StringComparison.Ordinal) || scanning != _scanning;
		_az = az;
		_state = state;
		_scanning = scanning;
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
		Rectangle rectangle = DrawFrame(graphics, "方位罗盘（AZ）");
		if (rectangle.Width < 40 || rectangle.Height < 40)
		{
			return;
		}
		float num = (float)rectangle.Left + (float)rectangle.Width / 2f;
		float num2 = (float)rectangle.Top + (float)rectangle.Height / 2f + 4f;
		float num3 = (float)Math.Min(rectangle.Width, rectangle.Height) / 2f - Math.Max(7f, (float)Math.Min(rectangle.Width, rectangle.Height) * 0.1f);
		using (GraphicsPath graphicsPath = new GraphicsPath())
		{
			graphicsPath.AddEllipse(num - num3, num2 - num3, num3 * 2f, num3 * 2f);
			HudDraw.GlowPath(graphics, graphicsPath, HudDraw.Cyan, 3, 1.2f);
		}
		using (Pen pen = new Pen(Color.FromArgb(70, HudDraw.Cyan), 1f))
		{
			graphics.DrawEllipse(pen, num - num3 * 0.72f, num2 - num3 * 0.72f, num3 * 1.44f, num3 * 1.44f);
		}
		for (int i = 0; i < 360; i += 5)
		{
			double num4 = (double)(i - 90) * Math.PI / 180.0;
			bool flag = i % 30 == 0;
			bool flag2 = i % 10 == 0;
			float num5 = (flag ? (num3 * 0.15f) : (flag2 ? (num3 * 0.1f) : (num3 * 0.055f)));
			float x = num + (float)Math.Cos(num4) * (num3 - num5);
			float y = num2 + (float)Math.Sin(num4) * (num3 - num5);
			float x2 = num + (float)Math.Cos(num4) * num3;
			float y2 = num2 + (float)Math.Sin(num4) * num3;
			using Pen pen2 = new Pen(flag ? HudDraw.Cyan : Color.FromArgb(90, HudDraw.Cyan), flag ? 1.6f : 1f);
			graphics.DrawLine(pen2, x, y, x2, y2);
		}
		string[] array = new string[4] { "北 0°", "东 90°", "南 180°", "西 270°" };
		for (int j = 0; j < 4; j++)
		{
			double num6 = (double)(j * 90 - 90) * Math.PI / 180.0;
			float num7 = num + (float)Math.Cos(num6) * (num3 - num3 * 0.34f);
			float num8 = num2 + (float)Math.Sin(num6) * (num3 - num3 * 0.34f);
			using Font font = Theme.FUi(Math.Max(6.5f, num3 * 0.12f), FontStyle.Bold);
			Size size = TextRenderer.MeasureText(array[j], font);
			TextRenderer.DrawText(graphics, array[j], font, new Point((int)(num7 - (float)size.Width / 2f), (int)(num8 - (float)size.Height / 2f)), Color.FromArgb(200, HudDraw.Cyan), TextFormatFlags.NoPadding);
		}
		if (!double.IsNaN(_target))
		{
			double num9 = (_target - 90.0) * Math.PI / 180.0;
			float num10 = num + (float)Math.Cos(num9) * (num3 - 6f);
			float num11 = num2 + (float)Math.Sin(num9) * (num3 - 6f);
			using SolidBrush brush = new SolidBrush(HudDraw.Green);
			graphics.FillEllipse(brush, num10 - 4f, num11 - 4f, 8f, 8f);
		}
		double num12 = (_az - 90.0) * Math.PI / 180.0;
		float x3 = num + (float)Math.Cos(num12) * (num3 - 16f);
		float y3 = num2 + (float)Math.Sin(num12) * (num3 - 16f);
		float x4 = num - (float)Math.Cos(num12) * (num3 * 0.28f);
		float y4 = num2 - (float)Math.Sin(num12) * (num3 * 0.28f);
		Color color = (_scanning ? HudDraw.Green : HudDraw.Cyan);
		using (Pen pen3 = new Pen(Color.FromArgb(60, color), 5f))
		{
			graphics.DrawLine(pen3, x4, y4, x3, y3);
		}
		using (Pen pen4 = new Pen(color, 2f))
		{
			graphics.DrawLine(pen4, x4, y4, x3, y3);
		}
		HudInstrument.GlowDot(graphics, num, num2, 4.5f, HudDraw.Cyan);
		using (Font font2 = Theme.FMonoF(Math.Max(9.5f, num3 * 0.33f), FontStyle.Bold))
		{
			string text = _az.ToString("0.0") + "°";
			TextRenderer.DrawText(graphics, text, font2, new Point((int)(num - (float)TextRenderer.MeasureText(text, font2).Width / 2f), (int)(num2 + num3 * 0.22f)), Color.White, TextFormatFlags.NoPadding);
		}
		using Font font3 = Theme.FUi(Math.Max(6.5f, num3 * 0.14f), FontStyle.Bold);
		string text2 = _state + (_scanning ? " · 扫描中" : "");
		TextRenderer.DrawText(graphics, text2, font3, new Point((int)(num - (float)TextRenderer.MeasureText(text2, font3).Width / 2f), (int)(num2 + num3 * 0.22f + Math.Max(14f, num3 * 0.36f))), _scanning ? HudDraw.Green : Theme.TextDim, TextFormatFlags.NoPadding);
	}
}
