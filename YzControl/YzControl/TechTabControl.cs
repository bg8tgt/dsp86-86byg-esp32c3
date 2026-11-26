using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace YzControl;

internal sealed class TechTabControl : TabControl
{
	public TechTabControl()
	{
		SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		base.DrawMode = TabDrawMode.OwnerDrawFixed;
		base.ItemSize = new Size(190, 38);
		base.SizeMode = TabSizeMode.Fixed;
		base.Padding = new Point(16, 6);
		Font = Theme.FUi(10.5f, FontStyle.Bold);
	}

	protected override void OnDrawItem(DrawItemEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		Rectangle tabRect = GetTabRect(e.Index);
		bool flag = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
		NeoPalette p = Theme.P;
		using (SolidBrush brush = new SolidBrush(flag ? p.CardTop : p.ShellTop))
		{
			graphics.FillRectangle(brush, tabRect);
		}
		if (flag)
		{
			using SolidBrush brush2 = new SolidBrush(p.Accent);
			graphics.FillRectangle(brush2, tabRect.X + 6, tabRect.Bottom - 3, tabRect.Width - 12, 3);
		}
		using (Pen pen = new Pen(p.Stroke))
		{
			graphics.DrawLine(pen, tabRect.Right - 1, tabRect.Top + 8, tabRect.Right - 1, tabRect.Bottom - 8);
		}
		TextRenderer.DrawText(graphics, base.TabPages[e.Index].Text, Font, tabRect, flag ? p.Text : p.TextDim, TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
	}
}
