using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace YzControl;

internal class NeoButton : Button
{
	private bool _hover;

	private bool _down;

	private bool _highlight;

	private Timer _anim;

	private float _lift;

	public bool Highlight
	{
		get
		{
			return _highlight;
		}
		set
		{
			if (_highlight != value)
			{
				_highlight = value;
				Invalidate();
			}
		}
	}

	private int IconSize
	{
		get
		{
			int num = base.Height;
			if (num >= 60)
			{
				return 34;
			}
			if (num >= 40)
			{
				return 24;
			}
			return 18;
		}
	}

	public NeoButton()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		base.FlatStyle = FlatStyle.Flat;
		base.FlatAppearance.BorderSize = 0;
		base.FlatAppearance.CheckedBackColor = Color.Transparent;
		base.FlatAppearance.MouseOverBackColor = Color.Transparent;
		base.FlatAppearance.MouseDownBackColor = Color.Transparent;
		BackColor = Color.Transparent;
		ForeColor = Theme.Text;
		Cursor = Cursors.Hand;
		base.Margin = new Padding(4, 5, 4, 5);
		Font = Theme.FCard;
		base.Height = 44;
		base.Width = 116;
		base.Padding = new Padding(4, 4, 4, 4);
	}

	public void AutoSizeToText(int minWidth, int maxWidth)
	{
		int num = TextRenderer.MeasureText(Text ?? "", Font).Width;
		int iconSize = IconSize;
		int val = Math.Max(num + 46, iconSize + 46);
		if (base.Height <= base.Width)
		{
			base.Width = Math.Max(minWidth, Math.Min(maxWidth, val));
		}
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		_hover = true;
		StartAnim();
		Invalidate();
		base.OnMouseEnter(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hover = false;
		_down = false;
		StartAnim();
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		_down = true;
		Invalidate();
		base.OnMouseDown(e);
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		_down = false;
		Invalidate();
		base.OnMouseUp(e);
	}

	protected override void OnTextChanged(EventArgs e)
	{
		Invalidate();
		base.OnTextChanged(e);
	}

	protected override void OnEnabledChanged(EventArgs e)
	{
		Invalidate();
		base.OnEnabledChanged(e);
	}

	private void StartAnim()
	{
		if (_anim == null)
		{
			_anim = new Timer();
			_anim.Interval = 16;
			_anim.Tick += delegate
			{
				float num = (_hover ? 1f : 0f);
				float num2 = num - _lift;
				if (Math.Abs(num2) < 0.08f)
				{
					_lift = num;
					_anim.Stop();
				}
				else
				{
					_lift += num2 * 0.28f;
				}
				Invalidate();
			};
		}
		if (!_anim.Enabled)
		{
			_anim.Start();
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && _anim != null)
		{
			try
			{
				_anim.Stop();
				_anim.Dispose();
			}
			catch
			{
			}
			_anim = null;
		}
		base.Dispose(disposing);
	}

	private static GraphicsPath Chamfer(Rectangle r, int cut)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		if (r.Width <= 2 || r.Height <= 2)
		{
			graphicsPath.AddRectangle(r);
			return graphicsPath;
		}
		cut = Math.Max(2, Math.Min(cut, Math.Min(r.Width, r.Height) / 3));
		graphicsPath.AddLine(r.Left + cut, r.Top, r.Right - cut, r.Top);
		graphicsPath.AddLine(r.Right - cut, r.Top, r.Right, r.Top + cut);
		graphicsPath.AddLine(r.Right, r.Top + cut, r.Right, r.Bottom - cut);
		graphicsPath.AddLine(r.Right, r.Bottom - cut, r.Right - cut, r.Bottom);
		graphicsPath.AddLine(r.Right - cut, r.Bottom, r.Left + cut, r.Bottom);
		graphicsPath.AddLine(r.Left + cut, r.Bottom, r.Left, r.Bottom - cut);
		graphicsPath.AddLine(r.Left, r.Bottom - cut, r.Left, r.Top + cut);
		graphicsPath.AddLine(r.Left, r.Top + cut, r.Left + cut, r.Top);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		NeoPalette p = Theme.P;
		bool enabled = base.Enabled;
		bool flag = enabled && (_hover || _down);
		bool flag2 = enabled && _highlight;
		float num = 1f + 0.05f * _lift;
		int num2 = (int)Math.Round(3f * _lift) - (_down ? 1 : 0);
		int num3 = base.Width - 2;
		int num4 = base.Height - 2 - Math.Abs(num2);
		if (num3 < 4)
		{
			num3 = 4;
		}
		if (num4 < 4)
		{
			num4 = 4;
		}
		int num5 = (int)Math.Round((float)num3 * num);
		int num6 = (int)Math.Round((float)num4 * num);
		if (num5 > base.Width - 1)
		{
			num5 = base.Width - 1;
		}
		if (num6 > base.Height - 1)
		{
			num6 = base.Height - 1;
		}
		Rectangle r = new Rectangle((base.Width - num5) / 2, (base.Height - num6) / 2 - num2, num5, num6);
		int num7 = Math.Max(6, Math.Min(r.Width, r.Height) / 4);
		if (num7 > 16)
		{
			num7 = 16;
		}
		int num8 = Math.Max(4, r.Height / 9);
		using (GraphicsPath path2 = Chamfer(r, num7))
		{
			int num9 = (int)((float)(p.Light ? 46 : 110) * (enabled ? 1f : 0.5f) * (flag ? 1.15f : 1f));
			if (num9 > 200)
			{
				num9 = 200;
			}
			if (num9 > 0)
			{
				Rectangle rectangle = new Rectangle(r.X + 2, r.Y + 3 + (flag ? 2 : 0), r.Width, r.Height);
				using GraphicsPath path = Theme.RoundRect(rectangle, 7);
				using SolidBrush brush = new SolidBrush(Color.FromArgb(num9, p.Shadow));
				Region clip = graphics.Clip;
				using (Region region = new Region(path))
				{
					region.Exclude(path2);
					graphics.Clip = region;
					graphics.FillRectangle(brush, rectangle);
				}
				graphics.Clip = clip;
			}
			Color baseColor = ((!enabled) ? Color.FromArgb(0, p.Glow) : (flag2 ? p.GlowSel : (flag ? p.GlowHot : p.Glow)));
			float num10 = p.GlowAlpha * (flag2 ? 1f : (flag ? 0.85f : 0.45f));
			if (enabled)
			{
				for (int num11 = (flag2 ? 4 : (flag ? 3 : 2)); num11 >= 1; num11--)
				{
					int num12 = (int)((float)(10 + 34 / num11) * num10);
					if (num12 > 0)
					{
						using Pen pen = new Pen(Color.FromArgb(Math.Min(200, num12), baseColor), 1.4f + (float)num11 * 2.1f);
						pen.LineJoin = LineJoin.Round;
						graphics.DrawPath(pen, path2);
					}
				}
			}
			Color color;
			Color color2;
			if (!enabled)
			{
				color = p.CardBottom;
				color2 = Deepen(p.CardBottom, 0.35f);
			}
			else if (flag2)
			{
				color = p.CardSelTop;
				color2 = p.CardSelBottom;
			}
			else if (flag)
			{
				color = p.CardHotTop;
				color2 = p.CardHotBottom;
			}
			else
			{
				color = p.CardTop;
				color2 = p.CardBottom;
			}
			using (LinearGradientBrush brush2 = new LinearGradientBrush(new Rectangle(r.X, r.Y, r.Width, Math.Max(2, r.Height)), color, color2, 90f))
			{
				graphics.FillPath(brush2, path2);
			}
			RectangleF rect = new RectangleF((float)r.X + 2f, (float)r.Y + 2f, Math.Max(2f, (float)r.Width - 4f), Math.Max(2f, (float)r.Height / 2f));
			using (GraphicsPath path3 = Chamfer(new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height), Math.Max(3, num7 - 2)))
			{
				using LinearGradientBrush brush3 = new LinearGradientBrush(rect, Color.FromArgb(p.Light ? 150 : 46, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f);
				graphics.FillPath(brush3, path3);
			}
			using (Pen pen2 = new Pen((!enabled) ? p.FieldEdge : (flag2 ? p.StrokeSel : (flag ? p.StrokeHot : p.Stroke)), (flag || flag2) ? 1.8f : 1.1f))
			{
				pen2.LineJoin = LineJoin.Round;
				graphics.DrawPath(pen2, path2);
			}
			using Pen pen3 = new Pen(Color.FromArgb((!enabled) ? 60 : (flag2 ? 220 : 140), p.Accent), 1.6f);
			int num13 = Math.Max(5, num7 + 2);
			graphics.DrawLine(pen3, r.Left + num7, r.Top, r.Left + num7 + num13, r.Top);
			graphics.DrawLine(pen3, r.Right, r.Bottom - num7, r.Right, r.Bottom - num7 - num13);
		}
		bool flag3 = r.Height >= 56;
		Rectangle rect2;
		Rectangle bounds;
		if (flag3)
		{
			int num14 = Math.Min(IconSize + 4, Math.Max(18, r.Height / 2));
			rect2 = new Rectangle(r.Left + (r.Width - num14) / 2, r.Top + num8, num14, num14);
			bounds = new Rectangle(r.Left + 2, rect2.Bottom + 1, r.Width - 4, Math.Max(12, r.Bottom - rect2.Bottom - 4));
		}
		else
		{
			Size size = TextRenderer.MeasureText(Text ?? "", Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
			int num15 = r.Width - 14;
			int num16 = Math.Min(IconSize, Math.Max(14, r.Height - 8));
			if (num15 - num16 - 5 < size.Width)
			{
				num16 = num15 - size.Width - 5;
				if (num16 < 12)
				{
					num16 = 0;
				}
			}
			if (num16 > 0)
			{
				rect2 = new Rectangle(r.Left + 7, r.Top + (r.Height - num16) / 2, num16, num16);
				bounds = new Rectangle(rect2.Right + 5, r.Top, Math.Max(8, r.Right - rect2.Right - 9), r.Height);
			}
			else
			{
				rect2 = Rectangle.Empty;
				bounds = new Rectangle(r.Left + 2, r.Top, Math.Max(8, r.Width - 4), r.Height);
			}
		}
		if (enabled && rect2.Width >= 12)
		{
			Bitmap bitmap = Icon3D.For(Text, rect2.Width, p);
			if (bitmap != null)
			{
				graphics.DrawImage(bitmap, rect2);
			}
		}
		else
		{
			using SolidBrush brush4 = new SolidBrush(p.TextDisabled);
			graphics.FillEllipse(brush4, rect2.Left + rect2.Width / 2 - 3, rect2.Top + rect2.Height / 2 - 3, 6, 6);
		}
		Color foreColor = ((!enabled) ? p.TextDisabled : (flag2 ? p.TextOnHot : (flag ? p.TextOnHot : p.Text)));
		TextFormatFlags textFormatFlags = (flag3 ? (TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak) : (TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter));
		if (!flag3)
		{
			textFormatFlags |= TextFormatFlags.HorizontalCenter;
		}
		TextRenderer.DrawText(graphics, Text, Font, bounds, foreColor, textFormatFlags);
		if (Focused && ShowFocusCues && enabled)
		{
			Rectangle rect3 = new Rectangle(2, 2, base.Width - 5, base.Height - 5);
			using Pen pen4 = new Pen(Color.FromArgb(120, p.Accent), 1f);
			pen4.DashStyle = DashStyle.Dot;
			graphics.DrawRectangle(pen4, rect3);
		}
	}

	private static Color Deepen(Color c, float f)
	{
		return Color.FromArgb(c.A, (int)((float)(int)c.R * (1f - f)), (int)((float)(int)c.G * (1f - f)), (int)((float)(int)c.B * (1f - f)));
	}
}
