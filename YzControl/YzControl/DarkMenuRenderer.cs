using System.Drawing;
using System.Windows.Forms;

namespace YzControl;

internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
	public DarkMenuRenderer()
		: base(new DarkColorTable())
	{
	}

	protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
	{
		bool flag = e.Item.Selected || e.Item.Pressed;
		Rectangle rect = new Rectangle(Point.Empty, e.Item.Size);
		NeoPalette p = Theme.P;
		using (SolidBrush brush = new SolidBrush(flag ? p.CardHotBottom : p.ShellTop))
		{
			e.Graphics.FillRectangle(brush, rect);
		}
		if (flag)
		{
			using (Pen pen = new Pen(p.Accent))
			{
				e.Graphics.DrawLine(pen, rect.Left, rect.Top, rect.Right, rect.Top);
			}
		}
	}

	protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
	{
		e.TextColor = ((e.Item.Selected || e.Item.Pressed) ? Theme.P.TextOnHot : Theme.P.Text);
		base.OnRenderItemText(e);
	}

	protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
	{
		using SolidBrush brush = new SolidBrush((e.ToolStrip is ToolStripDropDown) ? Theme.P.ShellBottom : Theme.P.ShellTop);
		e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.ToolStrip.Size));
	}
}
