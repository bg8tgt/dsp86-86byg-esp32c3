using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace YzControl;

internal static class Backdrop
{
	public static Bitmap Make(Size size, NeoPalette p)
	{
		int num = Math.Max(32, size.Width);
		int num2 = Math.Max(32, size.Height);
		Bitmap bitmap = new Bitmap(num, num2);
		try
		{
			using Graphics graphics = Graphics.FromImage(bitmap);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			Rectangle rect = new Rectangle(0, 0, num, num2);
			using (LinearGradientBrush linearGradientBrush = new LinearGradientBrush(rect, p.BgDeep, p.BgMid, 118f))
			{
				linearGradientBrush.InterpolationColors = new ColorBlend(3)
				{
					Colors = new Color[3] { p.BgDeep, p.BgMid, p.BgNear },
					Positions = new float[3] { 0f, 0.42f, 1f }
				};
				graphics.FillRectangle(linearGradientBrush, rect);
			}
			Blob(graphics, new PointF((float)num * 0.3f, (float)num2 * 0.16f), (float)Math.Max(num, num2) * 0.46f, p.SpotA);
			Blob(graphics, new PointF((float)num * 0.8f, (float)num2 * 0.86f), (float)Math.Max(num, num2) * 0.4f, p.SpotB);
			int num3 = 34;
			graphics.SmoothingMode = SmoothingMode.None;
			int num4 = 0;
			int num5 = 0;
			while (num5 <= num)
			{
				using (Pen pen = new Pen((num4 % 5 == 0) ? p.GridBold : p.Grid, 1f))
				{
					graphics.DrawLine(pen, num5, 0, num5, num2);
				}
				num5 += num3;
				num4++;
			}
			num4 = 0;
			int num6 = 0;
			while (num6 <= num2)
			{
				using (Pen pen2 = new Pen((num4 % 5 == 0) ? p.GridBold : p.Grid, 1f))
				{
					graphics.DrawLine(pen2, 0, num6, num, num6);
				}
				num6 += num3;
				num4++;
			}
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (Pen pen3 = new Pen(p.Beam, 1.2f))
			{
				for (int i = 0; i < 5; i++)
				{
					int num7 = -60 + i * (num / 4 + 40);
					graphics.DrawLine(pen3, num7, num2, num7 + num2 / 2, 0);
				}
			}
			using (GraphicsPath graphicsPath = new GraphicsPath())
			{
				graphicsPath.AddEllipse(-num / 3, -num2 / 3, num + num * 2 / 3, num2 + num2 * 2 / 3);
				using PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
				pathGradientBrush.CenterColor = Color.FromArgb(0, 0, 0, 0);
				pathGradientBrush.SurroundColors = new Color[1] { p.Vignette };
				using Region clip2 = new Region(rect);
				Region clip = graphics.Clip;
				graphics.Clip = clip2;
				graphics.FillRectangle(pathGradientBrush, rect);
				graphics.Clip = clip;
			}
			using LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, num, Math.Max(2, num2 / 8)), Color.FromArgb(38, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f);
			graphics.FillRectangle(brush, 0, 0, num, Math.Max(2, num2 / 8));
		}
		catch
		{
			try
			{
				using Graphics graphics2 = Graphics.FromImage(bitmap);
				graphics2.Clear(p.BgNear);
			}
			catch
			{
			}
		}
		return bitmap;
	}

	private static void Blob(Graphics g, PointF c, float radius, Color color)
	{
		if (radius < 4f)
		{
			return;
		}
		using GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.AddEllipse(c.X - radius, c.Y - radius, radius * 2f, radius * 2f);
		using PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
		pathGradientBrush.CenterColor = color;
		pathGradientBrush.SurroundColors = new Color[1] { Color.FromArgb(0, color) };
		g.FillEllipse(pathGradientBrush, c.X - radius, c.Y - radius, radius * 2f, radius * 2f);
	}
}
