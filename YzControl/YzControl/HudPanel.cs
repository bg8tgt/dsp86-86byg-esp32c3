using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace YzControl;

internal sealed class HudPanel : Panel
{
	public HudPanel()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Theme.Bg;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Rectangle clientRectangle = base.ClientRectangle;
		if (clientRectangle.Width <= 2 || clientRectangle.Height <= 2)
		{
			return;
		}
		HudDraw.GridBackground(e.Graphics, clientRectangle, 34);
		Rectangle r = new Rectangle(1, 1, clientRectangle.Width - 3, clientRectangle.Height - 3);
		using (GraphicsPath path = HudDraw.ChamferPath(r, 14))
		{
			using Pen pen = new Pen(HudDraw.Frame, 1.2f);
			e.Graphics.DrawPath(pen, path);
		}
		HudDraw.CornerBrackets(e.Graphics, r, 18, HudDraw.Cyan);
		base.OnPaint(e);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
	}
}
