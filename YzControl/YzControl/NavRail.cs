using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace YzControl;

internal sealed class NavRail : Control
{
	private sealed class Item
	{
		public HudIcon Icon;

		public NeoIcon Art;

		public string Text;

		public string Tag;
	}

	private readonly List<Item> _items = new List<Item>();

	private int _selected;

	private int _hover = -1;

	public int SelectedIndex
	{
		get
		{
			return _selected;
		}
		set
		{
			if (value >= 0 && value < _items.Count && value != _selected)
			{
				_selected = value;
				Invalidate();
				if (this.SelectedIndexChanged != null)
				{
					this.SelectedIndexChanged(this, EventArgs.Empty);
				}
			}
		}
	}

	private int ItemHeight
	{
		get
		{
			if (_items.Count != 0)
			{
				return Math.Max(78, Math.Min(104, (base.Height - 34) / _items.Count));
			}
			return 96;
		}
	}

	public event EventHandler SelectedIndexChanged;

	public NavRail()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		base.Width = 116;
		BackColor = Theme.Bg;
		Cursor = Cursors.Hand;
	}

	private static NeoIcon ArtOf(HudIcon i, string text)
	{
		return i switch
		{
			HudIcon.Satellite => NeoIcon.Satellite, 
			HudIcon.Radio => NeoIcon.Radio, 
			HudIcon.Camera => NeoIcon.Camera, 
			HudIcon.Chip => NeoIcon.Gear, 
			HudIcon.Radar => NeoIcon.Signal, 
			HudIcon.Antenna => NeoIcon.Antenna, 
			HudIcon.Waveform => NeoIcon.Chart, 
			HudIcon.Device => NeoIcon.Camera, 
			_ => Icon3D.Pick(text), 
		};
	}

	public void Add(HudIcon icon, string text, string tag)
	{
		Item item = new Item();
		item.Icon = icon;
		item.Text = text;
		item.Tag = tag;
		item.Art = ArtOf(icon, text);
		_items.Add(item);
		Invalidate();
	}

	private Rectangle RectOf(int i)
	{
		int itemHeight = ItemHeight;
		return new Rectangle(6, 28 + i * itemHeight, base.Width - 12, itemHeight - 8);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		int num = -1;
		for (int i = 0; i < _items.Count; i++)
		{
			if (RectOf(i).Contains(e.Location))
			{
				num = i;
				break;
			}
		}
		if (num != _hover)
		{
			_hover = num;
			Invalidate();
		}
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_hover = -1;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		for (int i = 0; i < _items.Count; i++)
		{
			if (RectOf(i).Contains(e.Location))
			{
				SelectedIndex = i;
				break;
			}
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		NeoPalette p = Theme.P;
		using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, base.Width), Math.Max(1, base.Height)), p.ShellTop, p.BgDeep, 90f))
		{
			graphics.FillRectangle(brush, base.ClientRectangle);
		}
		using (Pen pen = new Pen(p.Stroke))
		{
			graphics.DrawLine(pen, base.Width - 1, 0, base.Width - 1, base.Height);
		}
		using (Font font = Theme.FGroup)
		{
			TextRenderer.DrawText(graphics, "导 航", font, new Rectangle(10, 6, base.Width - 18, 18), p.TextDim, TextFormatFlags.VerticalCenter);
		}
		using (SolidBrush brush2 = new SolidBrush(p.Accent))
		{
			graphics.FillRectangle(brush2, 10, 22, 14, 2);
		}
		for (int i = 0; i < _items.Count; i++)
		{
			Rectangle rectangle = RectOf(i);
			bool flag = i == _selected;
			bool flag2 = i == _hover;
			int num = ((flag2 && !flag) ? (-2) : 0);
			Rectangle r = new Rectangle(rectangle.X + (flag2 ? (-2) : 0), rectangle.Y + num + (flag2 ? (-1) : 0), rectangle.Width + (flag2 ? 4 : 0), rectangle.Height + (flag2 ? 4 : 0) + 3);
			using (GraphicsPath path = HudDraw.ChamferPath(r, 11))
			{
				if (flag || flag2)
				{
					Color baseColor = (flag ? p.GlowSel : p.GlowHot);
					for (int num2 = (flag ? 4 : 2); num2 >= 1; num2--)
					{
						int num3 = (int)((float)(10 + 30 / num2) * p.GlowAlpha * (flag ? 1f : 0.7f));
						if (num3 > 0)
						{
							using Pen pen2 = new Pen(Color.FromArgb(Math.Min(180, num3), baseColor), 1.3f + (float)num2 * 2f);
							pen2.LineJoin = LineJoin.Round;
							graphics.DrawPath(pen2, path);
						}
					}
				}
				using (GraphicsPath graphicsPath = HudDraw.ChamferPath(new Rectangle(r.X + 2, r.Y + 4, r.Width, r.Height), 11))
				{
					using SolidBrush brush3 = new SolidBrush(Color.FromArgb(150, p.Shadow));
					Region clip = graphics.Clip;
					using (Region region = new Region(graphicsPath))
					{
						region.Exclude(path);
						graphics.Clip = region;
						graphics.FillRectangle(brush3, graphicsPath.GetBounds());
					}
					graphics.Clip = clip;
				}
				Color color = (flag ? p.CardSelTop : (flag2 ? p.CardHotTop : p.CardTop));
				Color color2 = (flag ? p.CardSelBottom : (flag2 ? p.CardHotBottom : p.CardBottom));
				using (LinearGradientBrush brush4 = new LinearGradientBrush(new Rectangle(r.X, r.Y, Math.Max(2, r.Width), Math.Max(2, r.Height)), color, color2, 90f))
				{
					graphics.FillPath(brush4, path);
				}
				using Pen pen3 = new Pen(flag ? p.StrokeSel : (flag2 ? p.StrokeHot : p.Stroke), (flag || flag2) ? 1.7f : 1.1f);
				pen3.LineJoin = LineJoin.Round;
				graphics.DrawPath(pen3, path);
			}
			if (flag)
			{
				using SolidBrush brush5 = new SolidBrush(p.Accent);
				graphics.FillRectangle(brush5, 0, r.Top + 12, 3, r.Height - 24);
			}
			int num4 = Math.Max(24, Math.Min(42, (int)((float)r.Height * 0.48f)));
			Rectangle rect = new Rectangle(r.Left + (r.Width - num4) / 2, r.Top + 6, num4, num4);
			Bitmap bitmap = Icon3D.For(_items[i].Art, num4, p);
			if (bitmap != null)
			{
				graphics.DrawImage(bitmap, rect);
			}
			Color foreColor = (flag ? p.TextOnHot : (flag2 ? p.Text : p.TextDim));
			Rectangle bounds = new Rectangle(r.Left + 2, rect.Bottom + 4, Math.Max(10, r.Width - 4), Math.Max(14, r.Bottom - rect.Bottom - 6));
			using (Font font2 = Theme.FUi(9.25f, FontStyle.Bold))
			{
				TextRenderer.DrawText(graphics, _items[i].Text, font2, bounds, foreColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
			}
			if (!string.IsNullOrEmpty(_items[i].Tag))
			{
				using Font font3 = Theme.FMonoF(7.8f, FontStyle.Bold);
				TextRenderer.DrawText(graphics, _items[i].Tag, font3, new Rectangle(r.Left, r.Bottom - 14, r.Width, 12), Color.FromArgb(200, flag ? p.TextOnHot : p.AccentSoft), TextFormatFlags.HorizontalCenter);
			}
		}
	}
}
