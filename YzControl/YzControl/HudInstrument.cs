using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace YzControl;

internal abstract class HudInstrument : Control
{
	protected HudInstrument()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.FromArgb(12, 20, 34);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
	}

	protected Rectangle DrawFrame(Graphics g, string title)
	{
		Rectangle clientRectangle = base.ClientRectangle;
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(12, 20, 34)))
		{
			g.FillRectangle(brush, clientRectangle);
		}
		Rectangle r = new Rectangle(1, 1, clientRectangle.Width - 3, clientRectangle.Height - 3);
		using (GraphicsPath path = HudDraw.ChamferPath(r, 10))
		{
			using Pen pen = new Pen(HudDraw.Frame, 1.2f);
			g.DrawPath(pen, path);
		}
		HudDraw.CornerBrackets(g, r, 12, Color.FromArgb(180, HudDraw.Cyan));
		if (!string.IsNullOrEmpty(title))
		{
			using Font font = Theme.FUi(9.5f, FontStyle.Bold);
			TextRenderer.DrawText(g, title, font, new Point(10, 7), HudDraw.Cyan, TextFormatFlags.NoPadding);
		}
		return new Rectangle(r.Left + 6, r.Top + 26, r.Width - 12, r.Height - 32);
	}

	protected static void GlowDot(Graphics g, float cx, float cy, float r, Color c)
	{
		for (int num = 5; num >= 1; num--)
		{
			using SolidBrush brush = new SolidBrush(Color.FromArgb(22, c));
			g.FillEllipse(brush, cx - r - (float)num * 1.6f, cy - r - (float)num * 1.6f, (r + (float)num * 1.6f) * 2f, (r + (float)num * 1.6f) * 2f);
		}
		using (SolidBrush brush2 = new SolidBrush(c))
		{
			g.FillEllipse(brush2, cx - r, cy - r, r * 2f, r * 2f);
		}
		using SolidBrush brush3 = new SolidBrush(Color.FromArgb(230, 255, 255, 255));
		g.FillEllipse(brush3, cx - r * 0.35f, cy - r * 0.35f, r * 0.7f, r * 0.7f);
	}
}
