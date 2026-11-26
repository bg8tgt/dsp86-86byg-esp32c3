using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace YzControl;

internal sealed class BarMeter : HudInstrument
{
	private string _label = "";

	private string _unit = "";

	private double _value;

	private double _min;

	private double _max = 100.0;

	private Color _color = HudDraw.Cyan;

	private string _note = "";

	private bool _bad;

	public void Set(string label, string unit, double value, double min, double max)
	{
		// v1.5.1：条形仪每秒被喂一次，值没变就不重绘
		bool changed = !string.Equals(label, _label, StringComparison.Ordinal) || !string.Equals(unit, _unit, StringComparison.Ordinal) || Math.Abs(value - _value) > 1E-09 || Math.Abs(min - _min) > 1E-09 || Math.Abs(max - _max) > 1E-09;
		_label = label;
		_unit = unit;
		_value = value;
		_min = min;
		_max = max;
		if (changed)
		{
			Invalidate();
		}
	}

	public void SetNote(string note, bool bad)
	{
		bool changed = !string.Equals(note, _note, StringComparison.Ordinal) || bad != _bad;
		_note = note;
		_bad = bad;
		if (changed)
		{
			Invalidate();
		}
	}

	public void SetColor(Color c)
	{
		bool changed = !c.Equals(_color);
		_color = c;
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
		Rectangle rectangle = DrawFrame(graphics, _label);
		if (rectangle.Width < 40 || rectangle.Height < 14)
		{
			return;
		}
		double num = Math.Max(1E-06, _max - _min);
		double num2 = (_value - _min) / num;
		if (num2 < 0.0)
		{
			num2 = 0.0;
		}
		if (num2 > 1.0)
		{
			num2 = 1.0;
		}
		string text = _value.ToString("0.##") + _unit;
		using (Font font = Theme.FMonoF(10.5f, FontStyle.Bold))
		{
			Size size = TextRenderer.MeasureText(text, font);
			int num3 = Math.Max(30, rectangle.Width - size.Width - 10);
			int num4 = 16;
			Rectangle rect = new Rectangle(rectangle.Left, rectangle.Top + 1, num3, num4);
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(8, 14, 24)))
			{
				graphics.FillRectangle(brush, rect);
			}
			using (Pen pen = new Pen(HudDraw.Frame))
			{
				graphics.DrawRectangle(pen, rect);
			}
			int num5 = (int)((double)(rect.Width - 2) * num2);
			if (num5 > 0)
			{
				using LinearGradientBrush brush2 = new LinearGradientBrush(new Rectangle(rect.Left, rect.Top, Math.Max(2, rect.Width), rect.Height), Color.FromArgb(110, _color), _color, 0f);
				graphics.FillRectangle(brush2, new Rectangle(rect.Left + 1, rect.Top + 1, num5, rect.Height - 2));
			}
			for (int i = 1; i < 10; i++)
			{
				int num6 = rect.Left + rect.Width * i / 10;
				using Pen pen2 = new Pen(Color.FromArgb(40, HudDraw.Cyan));
				graphics.DrawLine(pen2, num6, rect.Top + 2, num6, rect.Bottom - 2);
			}
			TextRenderer.DrawText(graphics, text, font, new Point(rectangle.Right - size.Width, rectangle.Top - 1), _bad ? HudDraw.Warn : Color.White, TextFormatFlags.NoPadding);
		}
		if (string.IsNullOrEmpty(_note))
		{
			return;
		}
		using Font font2 = Theme.FUi(8.1f, FontStyle.Bold);
		TextRenderer.DrawText(graphics, _note, font2, new Point(rectangle.Left, rectangle.Top + 20), _bad ? HudDraw.Danger : Theme.TextDim, TextFormatFlags.NoPadding);
	}
}
