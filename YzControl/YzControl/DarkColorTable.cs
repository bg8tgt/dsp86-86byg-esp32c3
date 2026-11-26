using System.Drawing;
using System.Windows.Forms;

namespace YzControl;

internal sealed class DarkColorTable : ProfessionalColorTable
{
	public override Color MenuStripGradientBegin => Theme.P.ShellTop;

	public override Color MenuStripGradientEnd => Theme.P.ShellTop;

	public override Color MenuItemSelected => Theme.P.CardHotBottom;

	public override Color MenuItemBorder => Theme.P.Accent;

	public override Color MenuBorder => Theme.P.Stroke;

	public override Color ToolStripDropDownBackground => Theme.P.ShellTop;

	public override Color ImageMarginGradientBegin => Theme.P.ShellTop;

	public override Color ImageMarginGradientMiddle => Theme.P.ShellTop;

	public override Color ImageMarginGradientEnd => Theme.P.ShellTop;

	public override Color SeparatorDark => Theme.P.Stroke;

	public override Color SeparatorLight => Theme.P.Stroke;

	public override Color StatusStripGradientBegin => Theme.P.ShellTop;

	public override Color StatusStripGradientEnd => Theme.P.ShellTop;

	public override Color ToolStripGradientBegin => Theme.P.ShellTop;

	public override Color ToolStripGradientMiddle => Theme.P.ShellTop;

	public override Color ToolStripGradientEnd => Theme.P.ShellTop;

	public override Color ButtonSelectedHighlight => Theme.P.CardHotBottom;

	public override Color ButtonSelectedBorder => Theme.P.Accent;

	public override Color ButtonPressedHighlight => Theme.P.CardHotBottom;
}
