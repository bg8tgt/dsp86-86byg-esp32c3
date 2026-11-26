using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace YzControl;

internal static class HudDraw
{
	public static Color Cyan => Theme.P.Accent;

	public static Color CyanDim => Theme.P.AccentSoft;

	public static Color Green => Theme.P.Ok;

	public static Color Warn => Theme.P.Warn;

	public static Color Danger => Theme.P.Danger;

	public static Color GridLine => Theme.P.Grid;

	public static Color GridBold => Theme.P.GridBold;

	public static Color Frame => Theme.P.Stroke;

	public static GraphicsPath ChamferPath(Rectangle r, int cut)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
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

	public static void CornerBrackets(Graphics g, Rectangle r, int len, Color c)
	{
		using Pen pen = new Pen(c, 1.6f);
		g.DrawLine(pen, r.Left, r.Top, r.Left + len, r.Top);
		g.DrawLine(pen, r.Left, r.Top, r.Left, r.Top + len);
		g.DrawLine(pen, r.Right - len, r.Top, r.Right, r.Top);
		g.DrawLine(pen, r.Right, r.Top, r.Right, r.Top + len);
		g.DrawLine(pen, r.Left, r.Bottom - len, r.Left, r.Bottom);
		g.DrawLine(pen, r.Left, r.Bottom, r.Left + len, r.Bottom);
		g.DrawLine(pen, r.Right - len, r.Bottom, r.Right, r.Bottom);
		g.DrawLine(pen, r.Right, r.Bottom - len, r.Right, r.Bottom);
	}

	public static void GlowPath(Graphics g, GraphicsPath path, Color c, int passes, float baseWidth)
	{
		float glowAlpha = Theme.P.GlowAlpha;
		for (int num = passes; num >= 1; num--)
		{
			int num2 = (int)((float)(14 + 40 / num) * glowAlpha);
			if (num2 > 0)
			{
				using Pen pen = new Pen(Color.FromArgb(Math.Min(90, num2), c), baseWidth + (float)num * 1.8f);
				g.DrawPath(pen, path);
			}
		}
		using Pen pen2 = new Pen(c, baseWidth);
		g.DrawPath(pen2, path);
	}

	public static void GridBackground(Graphics g, Rectangle bounds, int spacing)
	{
		if (bounds.Width <= 2 || bounds.Height <= 2)
		{
			return;
		}
		NeoPalette p = Theme.P;
		g.SmoothingMode = SmoothingMode.None;
		using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height), p.BgMid, p.BgDeep, 115f))
		{
			g.FillRectangle(brush, bounds);
		}
		// v1.5.1 性能：网格线原来**每条线都 new 一个 Pen**（一屏 ~80 支/次重绘，纯 GDI 抖动）；
		// 现在按主题缓存两支笔（普通/每 5 条加粗），换主题时 ClearCache 重建。
		Pen penLine = GridPen(bold: false);
		Pen penBold = GridPen(bold: true);
		int num = 0;
		int num2 = bounds.Left;
		while (num2 <= bounds.Right)
		{
			g.DrawLine((num % 5 == 0) ? penBold : penLine, num2, bounds.Top, num2, bounds.Bottom);
			num2 += spacing;
			num++;
		}
		num = 0;
		int num3 = bounds.Top;
		while (num3 <= bounds.Bottom)
		{
			g.DrawLine((num % 5 == 0) ? penBold : penLine, bounds.Left, num3, bounds.Right, num3);
			num3 += spacing;
			num++;
		}
		g.SmoothingMode = SmoothingMode.AntiAlias;
		// 暗角：PathGradientBrush 每次重建很贵（每次重绘分配 + 光栅化），按尺寸+主题缓存
		PathGradientBrush vignette = VignetteBrush(bounds, p);
		if (vignette != null)
		{
			using Region clip2 = new Region(bounds);
			Region clip = g.Clip;
			g.Clip = clip2;
			g.FillRectangle(vignette, bounds);
			g.Clip = clip;
		}
	}

	// ---- v1.5.1 绘图资源缓存（主题切换时由 ClearCache 清空） ----
	private static Pen _penGrid;

	private static Pen _penGridBold;

	private static PathGradientBrush _vignette;

	private static string _vignetteKey = "";

	private static string _penKey = "";

	private static Pen GridPen(bool bold)
	{
		// 注意：key 只跟主题相关，**不能带 bold**，否则先取普通笔再取加粗笔时会把刚用过的笔 Dispose 掉
		// → DrawLine(已释放的 Pen) 抛 ArgumentException（v1.5.1 自测抓到的真实回归）。
		string key = Theme.P.Key;
		if (_penKey != key)
		{
			DisposePen(ref _penGrid);
			DisposePen(ref _penGridBold);
			_penKey = key;
		}
		if (bold)
		{
			if (_penGridBold == null)
			{
				_penGridBold = new Pen(GridBold);
			}
			return _penGridBold;
		}
		if (_penGrid == null)
		{
			_penGrid = new Pen(GridLine);
		}
		return _penGrid;
	}

	private static void DisposePen(ref Pen p)
	{
		try
		{
			p?.Dispose();
		}
		catch
		{
		}
		p = null;
	}

	private static PathGradientBrush VignetteBrush(Rectangle bounds, NeoPalette p)
	{
		string key = p.Key + "@" + bounds.Width + "x" + bounds.Height;
		if (key == _vignetteKey && _vignette != null)
		{
			return _vignette;
		}
		try
		{
			_vignette?.Dispose();
		}
		catch
		{
		}
		_vignette = null;
		_vignetteKey = key;
		try
		{
			using GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddEllipse(bounds.Left - bounds.Width / 3, bounds.Top - bounds.Height / 3, bounds.Width + bounds.Width * 2 / 3, bounds.Height + bounds.Height * 2 / 3);
			PathGradientBrush b = new PathGradientBrush(graphicsPath);
			b.CenterColor = Color.FromArgb(0, 0, 0, 0);
			b.SurroundColors = new Color[1] { p.Vignette };
			_vignette = b;
			return _vignette;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>主题切换时调用：丢弃所有缓存的画笔/笔刷（颜色来自 Theme.P）。</summary>
	public static void ClearCache()
	{
		DisposePen(ref _penGrid);
		DisposePen(ref _penGridBold);
		try
		{
			_vignette?.Dispose();
		}
		catch
		{
		}
		_vignette = null;
		_vignetteKey = "";
		_penKey = "";
	}

	public static void IconBadge(Graphics g, Rectangle box, HudIcon kind, Color color, bool glow)
	{
		g.SmoothingMode = SmoothingMode.AntiAlias;
		int num = Math.Min(box.Width, box.Height);
		Rectangle rect = new Rectangle(box.Left + (box.Width - num) / 2, box.Top + (box.Height - num) / 2, num, num);
		if (glow)
		{
			using GraphicsPath graphicsPath = new GraphicsPath();
			graphicsPath.AddEllipse(rect);
			GlowPath(g, graphicsPath, color, 3, 1.4f);
		}
		using (SolidBrush brush = new SolidBrush(Color.FromArgb(38, color)))
		{
			g.FillEllipse(brush, rect);
		}
		using (Pen pen = new Pen(Color.FromArgb(150, color), 1.4f))
		{
			g.DrawEllipse(pen, rect);
		}
		Rectangle rect2 = Rectangle.Inflate(rect, -4, -4);
		using (Pen pen2 = new Pen(Color.FromArgb(70, color), 1f))
		{
			pen2.DashStyle = DashStyle.Dot;
			g.DrawEllipse(pen2, rect2);
		}
		Rectangle r = Rectangle.Inflate(rect, -num / 4, -num / 4);
		DrawIcon(g, r, kind, color);
	}

	public static void DrawIcon(Graphics g, Rectangle r, HudIcon kind, Color c)
	{
		g.SmoothingMode = SmoothingMode.AntiAlias;
		float num = (float)r.Left + (float)r.Width / 2f;
		float num2 = (float)r.Top + (float)r.Height / 2f;
		float num3 = (float)Math.Min(r.Width, r.Height) / 2f;
		using Pen pen = new Pen(c, Math.Max(1.6f, num3 / 7f));
		using SolidBrush brush = new SolidBrush(c);
		switch (kind)
		{
		case HudIcon.Satellite:
		{
			Rectangle rect4 = new Rectangle((int)(num - num3 * 0.9f), (int)(num2 - num3 * 0.35f), (int)(num3 * 1.8f), (int)(num3 * 1.3f));
			g.DrawArc(pen, rect4, 200f, 140f);
			g.DrawLine(pen, num, num2 + num3 * 0.55f, num, num2 + num3 * 1.05f);
			g.DrawLine(pen, num - num3 * 0.45f, num2 + num3 * 1.05f, num + num3 * 0.45f, num2 + num3 * 1.05f);
			g.DrawLine(pen, num, num2 + num3 * 0.35f, num + num3 * 0.5f, num2 - num3 * 0.25f);
			g.FillEllipse(brush, num + num3 * 0.42f, num2 - num3 * 0.34f, num3 * 0.22f, num3 * 0.22f);
			for (int m = 1; m <= 2; m++)
			{
				g.DrawArc(pen, num - num3 * (0.15f + (float)m * 0.3f), num2 - num3 * (0.35f + (float)m * 0.32f), num3 * (0.3f + (float)m * 0.6f), num3 * (0.3f + (float)m * 0.6f), 200f, 60f);
			}
			break;
		}
		case HudIcon.Radio:
		{
			g.DrawLine(pen, num - num3 * 0.55f, num2 + num3, num + num3 * 0.35f, num2 - num3);
			g.FillEllipse(brush, num - num3 * 0.68f, num2 + num3 * 0.82f, num3 * 0.28f, num3 * 0.28f);
			for (int k = 1; k <= 3; k++)
			{
				g.DrawArc(pen, num - num3 * (0.2f + (float)k * 0.28f), num2 - num3 * (0.9f - (float)k * 0.12f), num3 * (0.4f + (float)k * 0.56f), num3 * (0.4f + (float)k * 0.56f), -60f, 120f);
			}
			break;
		}
		case HudIcon.Camera:
		{
			Rectangle rect2 = new Rectangle((int)(num - num3 * 0.95f), (int)(num2 - num3 * 0.5f), (int)(num3 * 1.5f), (int)(num3 * 0.95f));
			g.DrawRectangle(pen, rect2);
			g.DrawEllipse(pen, num + num3 * 0.1f, num2 - num3 * 0.28f, num3 * 0.55f, num3 * 0.55f);
			g.DrawLine(pen, rect2.Right, num2 - num3 * 0.28f, num + num3 * 1.05f, num2 - num3 * 0.5f);
			g.DrawLine(pen, rect2.Right, num2 + num3 * 0.28f, num + num3 * 1.05f, num2 + num3 * 0.5f);
			g.DrawLine(pen, rect2.Right, num2 - num3 * 0.5f, num + num3 * 1.05f, num2 - num3 * 0.5f);
			g.DrawLine(pen, rect2.Right, num2 + num3 * 0.28f, num + num3 * 1.05f, num2 + num3 * 0.28f);
			break;
		}
		case HudIcon.Device:
		{
			Rectangle rect = new Rectangle((int)(num - num3 * 0.9f), (int)(num2 - num3 * 0.9f), (int)(num3 * 1.8f), (int)(num3 * 1.8f));
			g.DrawRectangle(pen, rect);
			g.DrawRectangle(pen, Rectangle.Inflate(rect, -(int)(num3 * 0.5f), -(int)(num3 * 0.5f)));
			g.FillEllipse(brush, num - num3 * 0.14f, num2 - num3 * 0.14f, num3 * 0.28f, num3 * 0.28f);
			break;
		}
		case HudIcon.Radar:
			g.DrawEllipse(pen, num - num3, num2 - num3, num3 * 2f, num3 * 2f);
			g.DrawEllipse(pen, num - num3 * 0.6f, num2 - num3 * 0.6f, num3 * 1.2f, num3 * 1.2f);
			g.DrawLine(pen, num, num2, num + num3 * 0.85f, num2 - num3 * 0.5f);
			g.FillEllipse(brush, num + num3 * 0.55f, num2 - num3 * 0.7f, num3 * 0.24f, num3 * 0.24f);
			g.FillEllipse(brush, num - num3 * 0.55f, num2 + num3 * 0.35f, num3 * 0.2f, num3 * 0.2f);
			break;
		case HudIcon.Chip:
		{
			Rectangle rect3 = new Rectangle((int)(num - num3 * 0.62f), (int)(num2 - num3 * 0.62f), (int)(num3 * 1.24f), (int)(num3 * 1.24f));
			g.DrawRectangle(pen, rect3);
			g.DrawRectangle(pen, Rectangle.Inflate(rect3, -(int)(num3 * 0.24f), -(int)(num3 * 0.24f)));
			for (int l = 0; l < 4; l++)
			{
				float num6 = (0f - num3) * 0.42f + (float)l * num3 * 0.28f;
				g.DrawLine(pen, num + num6, rect3.Top, num + num6, (float)rect3.Top - num3 * 0.3f);
				g.DrawLine(pen, num + num6, rect3.Bottom, num + num6, (float)rect3.Bottom + num3 * 0.3f);
				g.DrawLine(pen, rect3.Left, num2 + num6, (float)rect3.Left - num3 * 0.3f, num2 + num6);
				g.DrawLine(pen, rect3.Right, num2 + num6, (float)rect3.Right + num3 * 0.3f, num2 + num6);
			}
			break;
		}
		case HudIcon.Antenna:
			g.DrawLine(pen, num, num2 + num3, num, num2 - num3 * 0.2f);
			g.DrawLine(pen, num - num3 * 0.7f, num2 - num3 * 0.9f, num + num3 * 0.7f, num2 - num3 * 0.9f);
			g.DrawLine(pen, num - num3 * 0.7f, num2 - num3 * 0.9f, num, num2 - num3 * 0.2f);
			g.DrawLine(pen, num + num3 * 0.7f, num2 - num3 * 0.9f, num, num2 - num3 * 0.2f);
			break;
		case HudIcon.Waveform:
		{
			PointF[] array = new PointF[13];
			for (int j = 0; j < array.Length; j++)
			{
				float num5 = (float)j / (float)(array.Length - 1);
				array[j] = new PointF(num - num3 + num5 * 2f * num3, num2 - (float)Math.Sin((double)num5 * Math.PI * 3.0) * num3 * 0.75f);
			}
			g.DrawLines(pen, array);
			break;
		}
		default:
		{
			for (int i = 0; i < 4; i++)
			{
				float num4 = num3 * (0.35f + (float)i * 0.22f);
				g.FillRectangle(brush, num - num3 * 0.75f + (float)i * num3 * 0.42f, num2 + num3 * 0.75f - num4, num3 * 0.26f, num4);
			}
			break;
		}
		}
	}
}
