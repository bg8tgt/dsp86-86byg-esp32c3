using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace YzControl;

internal sealed class HudPageHeader : Control
{
	private HudIcon _icon;

	private string _title = "";

	private string _subtitle = "";

	public HudPageHeader()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		base.Height = 62;
		BackColor = Theme.Bg;
		Dock = DockStyle.Top;
	}

	public void Set(HudIcon icon, string title, string subtitle)
	{
		_icon = icon;
		_title = title;
		_subtitle = subtitle;
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		Rectangle clientRectangle = base.ClientRectangle;
		if (clientRectangle.Width <= 4)
		{
			return;
		}
		using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, clientRectangle.Width), Math.Max(1, clientRectangle.Height)), Color.FromArgb(22, 40, 64), Color.FromArgb(10, 16, 28), 0f))
		{
			graphics.FillRectangle(brush, clientRectangle);
		}
		Rectangle box = new Rectangle(12, 11, 40, 40);
		HudDraw.IconBadge(graphics, box, _icon, HudDraw.Cyan, glow: true);
		using (Font font = Theme.FUi(14.25f, FontStyle.Bold))
		{
			TextRenderer.DrawText(graphics, _title, font, new Point(box.Right + 12, 12), Color.White, TextFormatFlags.NoPadding);
		}
		if (!string.IsNullOrEmpty(_subtitle))
		{
			using Font font2 = Theme.FUi(9f, FontStyle.Bold);
			TextRenderer.DrawText(graphics, _subtitle, font2, new Point(box.Right + 14, 36), Color.FromArgb(150, HudDraw.Cyan), TextFormatFlags.NoPadding);
		}
		int num = clientRectangle.Right - 260;
		using (Pen pen = new Pen(Color.FromArgb(90, HudDraw.Cyan), 1.2f))
		{
			for (int i = 0; i < 4; i++)
			{
				int num2 = num + i * 26;
				graphics.DrawLine(pen, num2, clientRectangle.Bottom - 8, num2 + 18, 10);
			}
			graphics.DrawLine(pen, num + 120, clientRectangle.Height / 2, num + 250, clientRectangle.Height / 2);
		}
		using (SolidBrush brush2 = new SolidBrush(HudDraw.Cyan))
		{
			graphics.FillPolygon(brush2, new Point[3]
			{
				new Point(num + 250, clientRectangle.Height / 2 - 5),
				new Point(num + 262, clientRectangle.Height / 2),
				new Point(num + 250, clientRectangle.Height / 2 + 5)
			});
		}
		using SolidBrush brush3 = new SolidBrush(HudDraw.Cyan);
		graphics.FillRectangle(brush3, 0, clientRectangle.Height - 2, clientRectangle.Width, 2);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
	}
}
