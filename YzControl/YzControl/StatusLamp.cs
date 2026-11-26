using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace YzControl;

internal sealed class StatusLamp : HudInstrument
{
	private string _label = "";

	private string _value = "";

	private Color _color = Theme.TextDim;

	public void Set(string label, string value, Color color)
	{
		// v1.5.1：文字/颜色没变就不重绘
		bool changed = !string.Equals(label, _label, StringComparison.Ordinal) || !string.Equals(value, _value, StringComparison.Ordinal) || !color.Equals(_color);
		_label = label;
		_value = value;
		_color = color;
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
		Rectangle rectangle = DrawFrame(graphics, null);
		if (rectangle.Width < 20 || rectangle.Height < 12)
		{
			return;
		}
		float num = (float)rectangle.Top + (float)rectangle.Height / 2f;
		HudInstrument.GlowDot(graphics, rectangle.Left + 12, num, 5f, _color);
		using (Font font = Theme.FUi(8.7f, FontStyle.Bold))
		{
			TextRenderer.DrawText(graphics, _label, font, new Point(rectangle.Left + 26, (int)(num - 9f)), Theme.TextDim, TextFormatFlags.NoPadding);
		}
		using Font font2 = Theme.FUi(9.5f, FontStyle.Bold);
		Size size = TextRenderer.MeasureText(_value, font2);
		TextRenderer.DrawText(graphics, _value, font2, new Point(rectangle.Right - size.Width, (int)(num - 9f)), _color, TextFormatFlags.NoPadding);
	}
}
