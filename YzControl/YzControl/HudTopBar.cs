using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace YzControl;

internal sealed class HudTopBar : Control
{
	private readonly Timer _timer;

	private string _subtitle = "";

	private bool _modeHover;

	public event EventHandler ThemeToggle;

	public HudTopBar()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		base.Height = 70;
		BackColor = Theme.Bg;
		Dock = DockStyle.Top;
		Cursor = Cursors.Default;
		_timer = new Timer();
		_timer.Interval = 1000;
		// v1.5.1：顶栏只是秒级时钟在变 —— 只重绘右侧时钟/模式那一小片，
		// 不再每秒把整条 70px 高的渐变顶栏全画一遍（降低空闲重绘面积）。
		_timer.Tick += delegate
		{
			int w = base.ClientSize.Width;
			if (w <= 0)
			{
				return;
			}
			int bandW = Math.Min(470, w);
			Invalidate(new Rectangle(w - bandW, 0, bandW, base.ClientSize.Height));
		};
		_timer.Start();
	}

	public void SetSubtitle(string s)
	{
		_subtitle = s;
		Invalidate();
	}

	public void RefreshTheme()
	{
		Invalidate();
	}

	private Rectangle ModeRect()
	{
		return new Rectangle(Math.Max(4, base.ClientSize.Width - 196), 10, 96, 50);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		bool flag = ModeRect().Contains(e.Location);
		if (flag != _modeHover)
		{
			_modeHover = flag;
			Cursor = (flag ? Cursors.Hand : Cursors.Default);
			Invalidate();
		}
		base.OnMouseMove(e);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		_modeHover = false;
		Cursor = Cursors.Default;
		Invalidate();
		base.OnMouseLeave(e);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		if (ModeRect().Contains(e.Location) && this.ThemeToggle != null)
		{
			this.ThemeToggle(this, EventArgs.Empty);
		}
		else
		{
			base.OnMouseDown(e);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && _timer != null)
		{
			try
			{
				_timer.Stop();
				_timer.Dispose();
			}
			catch
			{
			}
		}
		base.Dispose(disposing);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
		Rectangle clientRectangle = base.ClientRectangle;
		if (clientRectangle.Width <= 8)
		{
			return;
		}
		NeoPalette p = Theme.P;
		using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, clientRectangle.Width), Math.Max(1, clientRectangle.Height)), p.ShellTop, p.BgDeep, 90f))
		{
			graphics.FillRectangle(brush, clientRectangle);
		}
		using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(60, p.Accent)))
		{
			graphics.FillRectangle(brush2, 0, 0, clientRectangle.Width, 1);
		}
		using (SolidBrush brush3 = new SolidBrush(p.Accent))
		{
			graphics.FillRectangle(brush3, 0, clientRectangle.Height - 2, clientRectangle.Width, 2);
		}
		Rectangle rect = new Rectangle(14, 12, 46, 46);
		Bitmap bitmap = Icon3D.For(NeoIcon.Signal, 46, p);
		if (bitmap != null)
		{
			graphics.DrawImage(bitmap, rect);
		}
		using (Font font = Theme.FUi(15.5f, FontStyle.Bold))
		{
			TextRenderer.DrawText(graphics, "YiZhou 控制中枢", font, new Point(rect.Right + 12, 13), p.Text, TextFormatFlags.NoPadding);
		}
		if (!string.IsNullOrEmpty(_subtitle))
		{
			using Font font2 = Theme.FUi(9f, FontStyle.Bold);
			TextRenderer.DrawText(graphics, _subtitle, font2, new Point(rect.Right + 14, 40), p.TextDim, TextFormatFlags.NoPadding);
		}
		int num = clientRectangle.Width / 2 - 40;
		int num2 = clientRectangle.Width - 400;
		if (num + 200 < num2)
		{
			using (Pen pen = new Pen(Color.FromArgb(110, p.Accent), 1.3f))
			{
				for (int i = 0; i < 5; i++)
				{
					int num3 = num - 210 + i * 26;
					graphics.DrawLine(pen, num3, clientRectangle.Height / 2 + 14, num3 + 22, 12);
				}
				graphics.DrawLine(pen, num + 40, clientRectangle.Height / 2, num + 200, clientRectangle.Height / 2);
				graphics.DrawLine(pen, num - 250, clientRectangle.Height / 2, num - 80, clientRectangle.Height / 2);
			}
			using SolidBrush brush4 = new SolidBrush(p.Accent);
			graphics.FillPolygon(brush4, new Point[3]
			{
				new Point(num - 66, clientRectangle.Height / 2 - 6),
				new Point(num - 52, clientRectangle.Height / 2),
				new Point(num - 66, clientRectangle.Height / 2 + 6)
			});
		}
		Rectangle r = ModeRect();
		if (r.Right < clientRectangle.Width - 8)
		{
			using (GraphicsPath path = Theme.RoundRect(r, 9))
			{
				using (LinearGradientBrush brush5 = new LinearGradientBrush(new Rectangle(r.X, r.Y, Math.Max(2, r.Width), Math.Max(2, r.Height)), _modeHover ? p.CardHotTop : p.CardTop, _modeHover ? p.CardHotBottom : p.CardBottom, 90f))
				{
					graphics.FillPath(brush5, path);
				}
				using Pen pen2 = new Pen(_modeHover ? p.StrokeHot : p.Stroke, _modeHover ? 1.7f : 1.1f);
				graphics.DrawPath(pen2, path);
			}
			Bitmap bitmap2 = Icon3D.For(p.Light ? NeoIcon.Sun : NeoIcon.Moon, 30, p);
			if (bitmap2 != null)
			{
				graphics.DrawImage(bitmap2, new Rectangle(r.X + 8, r.Y + 10, 30, 30));
			}
			using Font font3 = Theme.FUi(9.5f, FontStyle.Bold);
			TextRenderer.DrawText(graphics, p.Light ? "白天模式" : "黑夜模式", font3, new Rectangle(r.X + 42, r.Y, r.Width - 46, r.Height), _modeHover ? p.TextOnHot : p.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
		}
		DateTime now = DateTime.Now;
		string text = now.ToString("HH:mm:ss");
		string text2 = now.ToString("yyyy-MM-dd  dddd");
		int num4 = ((r.Right < clientRectangle.Width - 8) ? (r.X - 16) : (clientRectangle.Right - 18));
		using (Font font4 = Theme.FMonoF(16.5f, FontStyle.Bold))
		{
			TextRenderer.DrawText(graphics, text, font4, new Point(num4 - TextRenderer.MeasureText(text, font4).Width, 14), p.Accent, TextFormatFlags.NoPadding);
		}
		using (Font font5 = Theme.FUi(9f, FontStyle.Bold))
		{
			TextRenderer.DrawText(graphics, text2, font5, new Point(num4 - TextRenderer.MeasureText(text2, font5).Width, 42), p.TextDim, TextFormatFlags.NoPadding);
		}
		using SolidBrush brush6 = new SolidBrush(Color.FromArgb(140, p.Accent));
		graphics.FillRectangle(brush6, 4, 6, 3, clientRectangle.Height - 14);
		graphics.FillRectangle(brush6, clientRectangle.Right - 7, 6, 3, clientRectangle.Height - 14);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
	}
}
