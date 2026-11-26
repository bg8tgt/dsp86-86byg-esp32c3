using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace YzControl;

internal sealed class NeoGroupLabel : Label
{
	private Color CardBack
	{
		get
		{
			if (!Theme.P.Light)
			{
				return Theme.P.BgMid;
			}
			return Color.FromArgb(243, 247, 252);
		}
	}

	public NeoGroupLabel()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = (Theme.P.Light ? Color.FromArgb(243, 247, 252) : Theme.P.BgMid);
		Font = Theme.FGroup;
		AutoSize = false;
		base.Height = 30;
		base.Width = 300;
		base.Margin = new Padding(0, 3, 0, 0);
	}

	protected override void OnTextChanged(EventArgs e)
	{
		int num = TextRenderer.MeasureText(Spaced(Text), Font).Width + 34;
		if (num > 60)
		{
			base.Width = num;
		}
		Invalidate();
		base.OnTextChanged(e);
	}

	private static string Spaced(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder(s.Length * 2);
		for (int i = 0; i < s.Length; i++)
		{
			stringBuilder.Append(s[i]);
			stringBuilder.Append(' ');
		}
		return stringBuilder.ToString().TrimEnd();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		NeoPalette p = Theme.P;
		Rectangle clientRectangle = base.ClientRectangle;
		if (clientRectangle.Width <= 6 || clientRectangle.Height <= 6)
		{
			return;
		}
		using (SolidBrush brush = new SolidBrush(CardBack))
		{
			graphics.FillRectangle(brush, clientRectangle);
		}
		using (SolidBrush brush2 = new SolidBrush(p.Accent))
		{
			graphics.FillRectangle(brush2, 0, clientRectangle.Height / 2 - 8, 3, 16);
		}
		using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(90, p.Accent)))
		{
			graphics.FillRectangle(brush3, 5, clientRectangle.Height / 2 - 4, 2, 8);
		}
		string text = Spaced(Text);
		Size size = TextRenderer.MeasureText(text, Font);
		TextRenderer.DrawText(bounds: new Rectangle(14, (clientRectangle.Height - size.Height) / 2, Math.Max(10, clientRectangle.Width - 20), size.Height), dc: graphics, text: text, font: Font, foreColor: p.TextDim, flags: TextFormatFlags.NoPadding);
		int num = 14 + size.Width + 10;
		int num2 = clientRectangle.Right - 2;
		if (num2 - num <= 20)
		{
			return;
		}
		using LinearGradientBrush brush4 = new LinearGradientBrush(new Rectangle(num, clientRectangle.Height / 2, num2 - num, 1), Color.FromArgb(120, p.Accent), Color.FromArgb(0, p.Accent), 0f);
		graphics.FillRectangle(brush4, num, clientRectangle.Height / 2, num2 - num, 1);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
	}

	private void PaintParentBack(Graphics g, Rectangle r)
	{
		Control control = base.Parent;
		while (control != null && !(control.GetType() == typeof(Panel)) && !(control.GetType().Name == "HudPanel"))
		{
			control = control.Parent;
		}
		Region clip = g.Clip;
		g.SetClip(r, CombineMode.Replace);
		try
		{
			bool flag = false;
			if (control != null)
			{
				FieldInfo field = typeof(Control).GetField("EventPaint", BindingFlags.Static | BindingFlags.NonPublic);
				object obj = ((field != null) ? field.GetValue(null) : null);
				FieldInfo field2 = typeof(Component).GetField("events", BindingFlags.Instance | BindingFlags.NonPublic);
				if (obj != null && field2 != null && field2.GetValue(control) is EventHandlerList eventHandlerList && eventHandlerList[obj] is PaintEventHandler paintEventHandler)
				{
					using (PaintEventArgs e = new PaintEventArgs(g, r))
					{
						paintEventHandler(control, e);
					}
					flag = true;
				}
			}
			if (!flag)
			{
				Color color = control?.BackColor ?? Theme.P.BgNear;
				if (color.A == 0)
				{
					color = Theme.P.BgNear;
				}
				using SolidBrush brush = new SolidBrush(color);
				g.FillRectangle(brush, r);
				return;
			}
		}
		catch
		{
		}
		finally
		{
			g.Clip = clip;
		}
	}
}
