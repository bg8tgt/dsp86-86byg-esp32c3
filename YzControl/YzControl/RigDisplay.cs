using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace YzControl;

internal sealed class RigDisplay : Control
{
	private long _hz;

	private string _mode = "--";

	private bool _tx;

	private long _lat = -1L;

	private bool _link;

	private long _spHz;

	private string _spMode = "--";

	private bool _spOn;

	private string _info = "";

	public RigDisplay()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.FromArgb(12, 20, 34);
	}

	public void Set(long hz, string mode, bool tx, long lat, bool link)
	{
		Set(hz, mode, tx, lat, link, 0L, "--", spOn: false, null);
	}

	public void Set(long hz, string mode, bool tx, long lat, bool link, long spHz, string spMode, bool spOn, string extra)
	{
		// v1.5.1：电台页每秒刷新一次大屏，只有内容真的变化时才重绘
		bool changed = hz != _hz || tx != _tx || lat != _lat || link != _link || spHz != _spHz || spOn != _spOn
			|| !string.Equals(mode, _mode, StringComparison.Ordinal) || !string.Equals(spMode, _spMode, StringComparison.Ordinal)
			|| (extra != null && !string.Equals(extra, _info, StringComparison.Ordinal));
		_hz = hz;
		_mode = mode;
		_tx = tx;
		_lat = lat;
		_link = link;
		_spHz = spHz;
		_spMode = spMode;
		_spOn = spOn;
		if (extra != null)
		{
			_info = extra;
		}
		if (changed)
		{
			Invalidate();
		}
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		Rectangle clientRectangle = base.ClientRectangle;
		if (clientRectangle.Width < 80 || clientRectangle.Height < 40)
		{
			return;
		}
		Rectangle rectangle = new Rectangle(0, 0, clientRectangle.Width - 1, clientRectangle.Height - 1);
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(10, 17, 30)))
		{
			graphics.FillRectangle(brush, rectangle);
		}
		using (GraphicsPath path = HudDraw.ChamferPath(rectangle, 14))
		{
			using Pen pen = new Pen(HudDraw.Frame, 1.3f);
			graphics.DrawPath(pen, path);
		}
		HudDraw.CornerBrackets(graphics, rectangle, 16, HudDraw.Cyan);
		int num = clientRectangle.Height;
		int num2 = num / 2;
		int num3 = 10;
		string text = ((_hz > 0) ? ((double)_hz / 1000000.0).ToString("0.00000") : "--.-----");
		Color foreColor = ((_hz > 0) ? HudDraw.Cyan : Color.FromArgb(150, HudDraw.Cyan));
		float num4 = Math.Max(14f, Math.Min(28f, (float)num * 0.3f));
		int num5 = 16;
		using (Font font = Theme.FMonoF(num4, FontStyle.Bold))
		{
			Size size = TextRenderer.MeasureText(text, font);
			TextRenderer.DrawText(graphics, text, font, new Rectangle(num5, num3, size.Width + 4, num - num3 * 2), foreColor, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			using (Font font2 = Theme.FUi(8.5f, FontStyle.Bold))
			{
				TextRenderer.DrawText(graphics, "RX MHz", font2, new Point(num5 + size.Width + 6, num2 + 6), Theme.TextDim, TextFormatFlags.NoPadding);
			}
			num5 += size.Width + 54;
		}
		using (Font font3 = Theme.FMonoF(Math.Max(10.5f, Math.Min(16.5f, (float)num * 0.185f)), FontStyle.Bold))
		{
			Size size2 = TextRenderer.MeasureText(_mode, font3);
			TextRenderer.DrawText(graphics, _mode, font3, new Rectangle(num5, num3, size2.Width + 4, num - num3 * 2), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			num5 += size2.Width + 24;
		}
		string text2 = ((_spHz > 0) ? ((double)_spHz / 1000000.0).ToString("0.00000") : "--.-----");
		using (Theme.FUi(9.5f, FontStyle.Bold))
		{
			using SolidBrush brush2 = new SolidBrush(_spOn ? HudDraw.Warn : Color.FromArgb(90, HudDraw.Warn));
			graphics.FillRectangle(brush2, num5, num2 - 26, 74, 20);
		}
		using (Font font5 = Theme.FUi(9f, FontStyle.Bold))
		{
			TextRenderer.DrawText(graphics, "SPLIT", font5, new Rectangle(num5, num2 - 26, 74, 20), _spOn ? Color.Black : Theme.TextDim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		}
		using (Font font6 = Theme.FMonoF(num4 * 0.85f, FontStyle.Bold))
		{
			Size size3 = TextRenderer.MeasureText(text2, font6);
			TextRenderer.DrawText(graphics, text2, font6, new Rectangle(num5, num2 - 2, size3.Width + 4, (int)((float)num * 0.42f)), _spOn ? HudDraw.Warn : Color.FromArgb(120, HudDraw.Warn), TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
			using (Font font7 = Theme.FUi(8.5f, FontStyle.Bold))
			{
				TextRenderer.DrawText(graphics, "TX " + _spMode, font7, new Point(num5 + size3.Width + 6, num2 + 10), Theme.TextDim, TextFormatFlags.NoPadding);
			}
			num5 += size3.Width + 60;
		}
		Color color = (_tx ? HudDraw.Danger : HudDraw.Green);
		float num6 = Math.Max(12f, Math.Min(22f, (float)num * 0.24f));
		float num7 = num5;
		for (int num8 = 5; num8 >= 1; num8--)
		{
			using SolidBrush brush3 = new SolidBrush(Color.FromArgb(_tx ? 30 : 18, color));
			graphics.FillEllipse(brush3, num7 - (float)(num8 * 2), (float)num2 - num6 / 2f - (float)(num8 * 2), num6 + (float)(num8 * 4), num6 + (float)(num8 * 4));
		}
		using (SolidBrush brush4 = new SolidBrush(color))
		{
			graphics.FillEllipse(brush4, num7, (float)num2 - num6 / 2f, num6, num6);
		}
		using (Font font8 = Theme.FUi(Math.Max(9.5f, Math.Min(12.5f, (float)num * 0.145f)), FontStyle.Bold))
		{
			Size size4 = TextRenderer.MeasureText(_tx ? "发射 TX" : "接收 RX", font8);
			TextRenderer.DrawText(graphics, _tx ? "发射 TX" : "接收 RX", font8, new Rectangle((int)(num7 + num6 + 8f), 0, size4.Width + 6, num), color, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
		}
		if (!string.IsNullOrEmpty(_info))
		{
			using Font font9 = Theme.FUi(8.5f, FontStyle.Bold);
			TextRenderer.DrawText(graphics, _info, font9, new Point(18, num - 20), Theme.TextDim, TextFormatFlags.NoPadding);
		}
		using Font font10 = Theme.FUi(8.5f, FontStyle.Bold);
		int num9 = clientRectangle.Width - 226;
		if ((float)num9 > num7 + 120f)
		{
			TextRenderer.DrawText(graphics, _link ? "链路 在线" : "链路 --", font10, new Point(num9, 12), _link ? HudDraw.Green : Theme.TextDim, TextFormatFlags.NoPadding);
			TextRenderer.DrawText(graphics, "时延 " + ((_lat >= 0) ? (_lat + " ms") : "--"), font10, new Point(num9, 32), Theme.TextDim, TextFormatFlags.NoPadding);
		}
	}
}
