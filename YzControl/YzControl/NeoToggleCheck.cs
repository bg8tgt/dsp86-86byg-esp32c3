using System.Windows.Forms;

namespace YzControl;

internal sealed class NeoToggleCheck : CheckBox
{
	public void EnableCustomPaint()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
	}
}
