using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace YzControl;

internal sealed class StatusDot : Control
{
	private DotState _state;

	private string _text = "";

	private readonly ToolTip _tip;

	private string _tipText = "";

	private int _minWidth;

	private bool _fixedWidth;

	public bool ShowCaption = true;

	public string Tip
	{
		get
		{
			return _tipText;
		}
		set
		{
			_tipText = value ?? "";
			try
			{
				_tip.SetToolTip(this, _tipText);
			}
			catch
			{
			}
		}
	}

	public DotState State
	{
		get
		{
			return _state;
		}
		set
		{
			if (_state != value)
			{
				_state = value;
				Invalidate();
			}
		}
	}

	public string Caption
	{
		get
		{
			return _text;
		}
		set
		{
			if (_text != value)
			{
				_text = value ?? "";
				Invalidate();
			}
		}
	}

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

	public StatusDot()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		BackColor = Color.Transparent;
		base.Height = 26;
		base.Width = 122;
		Font = Theme.FSmall;
		base.Margin = new Padding(4, 0, 4, 0);
		_tip = new ToolTip();
		_tip.InitialDelay = 250;
		_tip.ReshowDelay = 120;
	}

	public void Set(DotState st, string caption)
	{
		Set(st, caption, null);
	}

	public void Set(DotState st, string caption, string tip)
	{
		if (tip != null)
		{
			_tipText = tip;
			try
			{
				_tip.SetToolTip(this, tip);
			}
			catch
			{
			}
		}
		bool num = _state != st || _text != (caption ?? "");
		_state = st;
		_text = caption ?? "";
		AutoFitWidth();
		if (num)
		{
			Invalidate();
		}
	}

	private void AutoFitWidth()
	{
		if (!_fixedWidth)
		{
			int num2;
			if (_text.Length > 0)
			{
				int num = TextRenderer.MeasureText(_text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
				num2 = 23 + num + 8;
			}
			else
			{
				num2 = 24;
			}
			if (num2 < 32)
			{
				num2 = 32;
			}
			if (num2 > 420)
			{
				num2 = 420;
			}
			if (num2 > _minWidth)
			{
				_minWidth = num2;
			}
			if (base.Width != _minWidth)
			{
				base.Width = _minWidth;
			}
		}
	}

	protected override void OnFontChanged(EventArgs e)
	{
		base.OnFontChanged(e);
		AutoFitWidth();
	}

	public void FixWidth(int w)
	{
		_fixedWidth = true;
		base.Width = w;
	}

	public static Color ColorOf(DotState s)
	{
		NeoPalette p = Theme.P;
		switch (s)
		{
		case DotState.Ok:
			if (!p.Light)
			{
				return Color.FromArgb(46, 226, 140);
			}
			return Color.FromArgb(8, 150, 96);
		case DotState.Fail:
			if (!p.Light)
			{
				return Color.FromArgb(255, 96, 96);
			}
			return Color.FromArgb(202, 38, 38);
		case DotState.Busy:
			if (!p.Light)
			{
				return Color.FromArgb(255, 196, 60);
			}
			return Color.FromArgb(190, 124, 0);
		case DotState.Off:
			return Color.FromArgb(140, 150, 165);
		default:
			if (!p.Light)
			{
				return Color.FromArgb(120, 138, 162);
			}
			return Color.FromArgb(148, 158, 176);
		}
	}

	public void NoCaption()
	{
		ShowCaption = false;
		Caption = "";
		_fixedWidth = true;
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		Rectangle clientRectangle = base.ClientRectangle;
		if (clientRectangle.Width < 4 || clientRectangle.Height < 4)
		{
			return;
		}
		using (SolidBrush brush = new SolidBrush(CardBack))
		{
			graphics.FillRectangle(brush, clientRectangle);
		}
		Color color = ColorOf(_state);
		float glowAlpha = Theme.P.GlowAlpha;
		int num = clientRectangle.Height / 2;
		int num2 = Math.Max(9, Math.Min(14, clientRectangle.Height - 10));
		Rectangle rect = new Rectangle(4, num - num2 / 2, num2, num2);
		if (_state == DotState.Ok || _state == DotState.Fail || _state == DotState.Busy)
		{
			int val = Math.Max(1, (clientRectangle.Height - rect.Height) / 2 - 1);
			for (int num3 = 3; num3 >= 1; num3--)
			{
				int num4 = (int)((float)(26 + 34 / num3) * glowAlpha);
				if (num4 > 0)
				{
					int num5 = Math.Min(num3 * 2 + 1, val);
					if (num5 > 0)
					{
						using SolidBrush brush2 = new SolidBrush(Color.FromArgb(Math.Min(140, num4), color));
						graphics.FillEllipse(brush2, Rectangle.Inflate(rect, num5, num5));
					}
				}
			}
		}
		using (GraphicsPath graphicsPath = new GraphicsPath())
		{
			graphicsPath.AddEllipse(rect);
			using PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
			pathGradientBrush.CenterColor = Color.FromArgb(255, Math.Min(255, color.R + 70), Math.Min(255, color.G + 70), Math.Min(255, color.B + 70));
			pathGradientBrush.SurroundColors = new Color[1] { color };
			graphics.FillPath(pathGradientBrush, graphicsPath);
		}
		using (Pen pen = new Pen(Color.FromArgb(190, Theme.P.Light ? Color.FromArgb(70, 82, 104) : Color.White), 1f))
		{
			graphics.DrawEllipse(pen, rect);
		}
		using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(190, Color.White)))
		{
			graphics.FillEllipse(brush3, rect.X + rect.Width / 5, rect.Y + rect.Height / 6, Math.Max(2, rect.Width / 3), Math.Max(2, rect.Height / 3));
		}
		if (ShowCaption && !string.IsNullOrEmpty(_text))
		{
			int num6 = rect.Right + 5;
			TextRenderer.DrawText(bounds: new Rectangle(num6, 0, Math.Max(4, clientRectangle.Right - num6), clientRectangle.Height), dc: graphics, text: _text, font: Font, foreColor: Theme.P.TextDim, flags: TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
		}
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
	}
}
