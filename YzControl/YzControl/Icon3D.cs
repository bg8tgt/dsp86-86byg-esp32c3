using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace YzControl;

internal static class Icon3D
{
	private struct Key : IEquatable<Key>
	{
		public NeoIcon Kind;

		public int Size;

		public string Theme;

		public bool Equals(Key o)
		{
			if (Kind == o.Kind && Size == o.Size)
			{
				return string.Equals(Theme, o.Theme, StringComparison.Ordinal);
			}
			return false;
		}

		public override bool Equals(object o)
		{
			if (o is Key)
			{
				return Equals((Key)o);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return ((int)Kind * 397 * 397 + Size) * 397 + ((Theme != null) ? Theme.GetHashCode() : 0);
		}
	}

	private static readonly Dictionary<Key, Bitmap> _cache = new Dictionary<Key, Bitmap>();

	public static void ClearCache()
	{
		lock (_cache)
		{
			foreach (Bitmap value in _cache.Values)
			{
				try
				{
					value?.Dispose();
				}
				catch
				{
				}
			}
			_cache.Clear();
		}
	}

	public static NeoIcon Pick(string title)
	{
		string text = (title ?? "").ToLowerInvariant();
		if (text.Length == 0)
		{
			return NeoIcon.Cube;
		}
		if (Has(text, "卸载", "删除", "移除", "清理", "清除", "垃圾桶"))
		{
			return NeoIcon.Trash;
		}
		if (Has(text, "停止", "关闭", "退出", "停用", "取消", "断开", "断线"))
		{
			return NeoIcon.Cross;
		}
		if (Has(text, "发射", "ptt", "按下"))
		{
			return NeoIcon.Bolt;
		}
		if (Has(text, "播放", "开始", "启动", "启用", "运行", "接收"))
		{
			return NeoIcon.Play;
		}
		if (Has(text, "组播", "multicast", "透传", "网络", "链路", "直连", "dhcp", "以太网", "网卡", "ip", "端口", "连接"))
		{
			return NeoIcon.Globe;
		}
		if (Has(text, "wifi", "无线", "扫描"))
		{
			return NeoIcon.Signal;
		}
		if (Has(text, "卫星", "追踪", "追星", "云台", "方位", "俯仰", "仰角", "定位", "校零", "扫描", "限位", "自检", "标定"))
		{
			return NeoIcon.Satellite;
		}
		if (Has(text, "天线"))
		{
			return NeoIcon.Antenna;
		}
		if (Has(text, "电台", "频率", "ci-v", "civ", "vfo", "mem", "频道", "模式", "步进", "调谐", "存储组", "静噪", "音量", "功率", "滤波", "rit", "split", "串口", "波特率", "延时", "发帧"))
		{
			return NeoIcon.Radio;
		}
		if (Has(text, "音频", "声卡", "声音", "麦克"))
		{
			return NeoIcon.Mic;
		}
		if (Has(text, "监控", "摄像", "画面", "预览", "视频", "rtsp", "onvif", "预置位", "变倍", "抓图", "录像"))
		{
			return NeoIcon.Camera;
		}
		if (Has(text, "升级", "更新", "固件", "ota", "下载", "上传", "刷写", "烧录"))
		{
			return NeoIcon.Download;
		}
		if (Has(text, "设置", "配置", "参数", "应用", "保存", "写入", "修改", "选项", "扳手", "校准"))
		{
			return NeoIcon.Wrench;
		}
		if (Has(text, "系统", "信息", "状态", "健康", "版本", "sys", "硬件", "内存", "堆", "读取", "刷新", "查询", "读"))
		{
			return NeoIcon.Gear;
		}
		if (Has(text, "测试", "检测", "对勾", "校验", "验证", "确认", "ok"))
		{
			return NeoIcon.Check;
		}
		if (Has(text, "安全", "权限", "控制权", "仲裁", "锁定", "密码"))
		{
			return NeoIcon.Shield;
		}
		if (Has(text, "重启", "复位", "reboot", "断电"))
		{
			return NeoIcon.Power;
		}
		if (Has(text, "文件", "固件", "目录", "打开", "目录", "路径"))
		{
			return NeoIcon.Folder;
		}
		if (Has(text, "曲线", "仪表", "图形", "趋势", "电平", "信号强度"))
		{
			return NeoIcon.Chart;
		}
		if (Has(text, "时钟", "时间", "延时", "定时"))
		{
			return NeoIcon.Clock;
		}
		return NeoIcon.Cube;
	}

	private static bool Has(string s, params string[] keys)
	{
		for (int i = 0; i < keys.Length; i++)
		{
			if (keys[i].Length > 0 && s.IndexOf(keys[i], StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
		}
		return false;
	}

	public static Bitmap For(string title, int size, NeoPalette p)
	{
		return For(Pick(title), size, p);
	}

	public static Bitmap For(NeoIcon kind, int size, NeoPalette p)
	{
		if (size < 12)
		{
			size = 12;
		}
		Key key = default(Key);
		key.Kind = kind;
		key.Size = size;
		key.Theme = ((p == null) ? "dark" : p.Key);
		lock (_cache)
		{
			if (_cache.TryGetValue(key, out var value) && value != null)
			{
				return value;
			}
			Bitmap bitmap = Render(kind, size, p ?? Theme.P);
			_cache[key] = bitmap;
			return bitmap;
		}
	}

	private static Bitmap Render(NeoIcon kind, int size, NeoPalette p)
	{
		Bitmap bitmap = new Bitmap(size, size);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
			float s = size;
			switch (kind)
			{
			case NeoIcon.Satellite:
				IsoSatellite(graphics, s, p);
				break;
			case NeoIcon.Radio:
				IsoRadio(graphics, s, p);
				break;
			case NeoIcon.Camera:
				IsoCamera(graphics, s, p);
				break;
			case NeoIcon.Gear:
				IsoGear(graphics, s, p);
				break;
			case NeoIcon.Trash:
				IsoTrash(graphics, s, p);
				break;
			case NeoIcon.Download:
				IsoDownload(graphics, s, p);
				break;
			case NeoIcon.Wrench:
				IsoWrench(graphics, s, p);
				break;
			case NeoIcon.Check:
				IsoCheck(graphics, s, p);
				break;
			case NeoIcon.Shield:
				IsoShield(graphics, s, p);
				break;
			case NeoIcon.Lock:
				IsoLock(graphics, s, p);
				break;
			case NeoIcon.Cylinder:
				IsoCylinderShape(graphics, s, p);
				break;
			case NeoIcon.Power:
				IsoPower(graphics, s, p);
				break;
			case NeoIcon.Bolt:
				IsoBolt(graphics, s, p);
				break;
			case NeoIcon.Signal:
				IsoSignal(graphics, s, p);
				break;
			case NeoIcon.Refresh:
				IsoRefresh(graphics, s, p);
				break;
			case NeoIcon.Folder:
				IsoFolder(graphics, s, p);
				break;
			case NeoIcon.Globe:
				IsoGlobe(graphics, s, p);
				break;
			case NeoIcon.Clock:
				IsoClock(graphics, s, p);
				break;
			case NeoIcon.Chart:
				IsoChart(graphics, s, p);
				break;
			case NeoIcon.Cross:
				IsoCross(graphics, s, p);
				break;
			case NeoIcon.Play:
				IsoPlay(graphics, s, p);
				break;
			case NeoIcon.Antenna:
				IsoAntenna(graphics, s, p);
				break;
			case NeoIcon.Mic:
				IsoMic(graphics, s, p);
				break;
			case NeoIcon.Network:
				IsoNetwork(graphics, s, p);
				break;
			case NeoIcon.Star:
				IsoStar(graphics, s, p);
				break;
			case NeoIcon.Sun:
				IsoSun(graphics, s, p);
				break;
			case NeoIcon.Moon:
				IsoMoon(graphics, s, p);
				break;
			default:
				IsoCube(graphics, s, p);
				break;
			}
		}
		return bitmap;
	}

	private static Color Lift(Color c, float f)
	{
		int val = c.R + (int)((float)(255 - c.R) * f);
		int val2 = c.G + (int)((float)(255 - c.G) * f);
		int val3 = c.B + (int)((float)(255 - c.B) * f);
		return Color.FromArgb(c.A, Math.Min(255, val), Math.Min(255, val2), Math.Min(255, val3));
	}

	private static Color Deep(Color c, float f)
	{
		return Color.FromArgb(c.A, (int)((float)(int)c.R * (1f - f)), (int)((float)(int)c.G * (1f - f)), (int)((float)(int)c.B * (1f - f)));
	}

	private static void CubeFaces(NeoPalette p, out Color top, out Color left, out Color right)
	{
		Color c = (p.Light ? Color.FromArgb(96, 156, 226) : Color.FromArgb(70, 132, 224));
		top = Lift(c, 0.52f);
		left = Deep(c, 0.42f);
		right = Lift(c, 0.1f);
	}

	private static void IsoCube(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var right);
		float num = s * 0.5f;
		float num2 = s * 0.38f;
		float num3 = s * 0.21f;
		float num4 = s * 0.36f;
		PointF pointF = new PointF(num, s * 0.1f);
		PointF pointF2 = new PointF(num + num2, pointF.Y + num3);
		PointF pointF3 = new PointF(num, pointF.Y + num3 * 2f);
		PointF pointF4 = new PointF(num - num2, pointF.Y + num3);
		Shadow(g, num, pointF3.Y + num4 + s * 0.04f, num2 * 1.05f, num3 * 0.8f, p);
		using (SolidBrush brush = new SolidBrush(left))
		{
			g.FillPolygon(brush, new PointF[4]
			{
				pointF4,
				pointF3,
				new PointF(pointF3.X, pointF3.Y + num4),
				new PointF(pointF4.X, pointF4.Y + num4)
			});
		}
		using (SolidBrush brush2 = new SolidBrush(right))
		{
			g.FillPolygon(brush2, new PointF[4]
			{
				pointF2,
				pointF3,
				new PointF(pointF3.X, pointF3.Y + num4),
				new PointF(pointF2.X, pointF2.Y + num4)
			});
		}
		using (SolidBrush brush3 = new SolidBrush(top))
		{
			g.FillPolygon(brush3, new PointF[4] { pointF, pointF2, pointF3, pointF4 });
		}
		using (Pen pen = new Pen(Color.FromArgb(200, Lift(top, 0.75f)), Math.Max(1f, s / 34f)))
		{
			g.DrawLines(pen, new PointF[3] { pointF4, pointF, pointF2 });
		}
		using Pen pen2 = new Pen(Color.FromArgb(150, Color.White), Math.Max(1f, s / 40f));
		g.DrawLine(pen2, pointF3, new PointF(pointF3.X, pointF3.Y + num4));
	}

	private static void IsoCylinderShape(Graphics g, float s, NeoPalette p)
	{
		IsoCylinder(g, s, p, null);
	}

	private static void IsoCylinder(Graphics g, float s, NeoPalette p, string band)
	{
		CubeFaces(p, out var top, out var left, out var right);
		float num = s * 0.5f;
		float num2 = s * 0.32f;
		float num3 = s * 0.13f;
		float num4 = s * 0.16f;
		float num5 = s * 0.8f;
		Shadow(g, num, num5 + s * 0.05f, num2 * 1.15f, num3 * 0.9f, p);
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(num - num2, num4, num2 * 2f, num5 - num4), Lift(right, 0.25f), left, 0f))
		{
			g.FillRectangle(brush, num - num2, num4, num2 * 2f, num5 - num4);
		}
		using (SolidBrush brush2 = new SolidBrush(Deep(left, 0.35f)))
		{
			g.FillEllipse(brush2, num - num2, num5 - num3, num2 * 2f, num3 * 2f);
		}
		using (SolidBrush brush3 = new SolidBrush(top))
		{
			g.FillEllipse(brush3, num - num2, num4 - num3, num2 * 2f, num3 * 2f);
		}
		using (Pen pen = new Pen(Color.FromArgb(190, Lift(top, 0.7f)), Math.Max(1f, s / 36f)))
		{
			g.DrawEllipse(pen, num - num2, num4 - num3, num2 * 2f, num3 * 2f);
		}
		using (Pen pen2 = new Pen(Color.FromArgb(120, Color.White), Math.Max(1f, s / 26f)))
		{
			g.DrawLine(pen2, num - num2 * 0.45f, num4 + num3 * 0.4f, num - num2 * 0.45f, num5 - num3 * 0.6f);
		}
		for (int i = 0; i < 3; i++)
		{
			float num6 = num4 + (num5 - num4) * (0.32f + (float)i * 0.22f);
			using Pen pen3 = new Pen(Color.FromArgb(120, top), Math.Max(1f, s / 42f));
			g.DrawArc(pen3, num - num2, num6 - num3, num2 * 2f, num3 * 2f, 0f, 180f);
		}
	}

	private static void IsoShield(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.5f;
		float num2 = s * 0.56f;
		float num3 = s * 0.14f;
		float num4 = s * 0.88f;
		Shadow(g, num, num4 + s * 0.03f, num2 * 0.5f, s * 0.05f, p);
		PointF[] points = new PointF[6]
		{
			new PointF(num, num3),
			new PointF(num + num2 / 2f, num3 + s * 0.1f),
			new PointF(num + num2 / 2f, s * 0.52f),
			new PointF(num, num4),
			new PointF(num - num2 / 2f, s * 0.52f),
			new PointF(num - num2 / 2f, num3 + s * 0.1f)
		};
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(num - num2 / 2f, num3, num2, num4 - num3), top, left, 90f))
		{
			g.FillPolygon(brush, points);
		}
		using (Pen pen = new Pen(Color.FromArgb(210, Lift(top, 0.65f)), Math.Max(1f, s / 30f)))
		{
			g.DrawPolygon(pen, points);
		}
		using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(60, 0, 0, 0)))
		{
			g.FillPolygon(brush2, new PointF[4]
			{
				new PointF(num, num3),
				new PointF(num + num2 / 2f, num3 + s * 0.1f),
				new PointF(num + num2 / 2f, s * 0.52f),
				new PointF(num, num4)
			});
		}
		using SolidBrush brush3 = new SolidBrush(Color.FromArgb(235, Color.White));
		g.FillPolygon(brush3, Star(num, s * 0.46f, s * 0.15f, s * 0.062f, 5));
	}

	private static void IsoLock(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.5f;
		using (Pen pen = new Pen(Color.FromArgb(230, Lift(top, 0.35f)), Math.Max(1.6f, s / 13f)))
		{
			g.DrawArc(pen, num - s * 0.15f, s * 0.14f, s * 0.3f, s * 0.34f, 180f, 180f);
		}
		RectangleF rect = new RectangleF(num - s * 0.29f, s * 0.36f, s * 0.58f, s * 0.44f);
		Shadow(g, num, rect.Bottom + s * 0.04f, s * 0.28f, s * 0.045f, p);
		using (LinearGradientBrush brush = new LinearGradientBrush(rect, top, left, 90f))
		{
			g.FillRectangle(brush, rect);
		}
		using (Pen pen2 = new Pen(Color.FromArgb(210, Lift(top, 0.6f)), Math.Max(1f, s / 32f)))
		{
			g.DrawRectangle(pen2, rect.X, rect.Y, rect.Width, rect.Height);
		}
		using SolidBrush brush2 = new SolidBrush(Color.FromArgb(220, Color.White));
		g.FillEllipse(brush2, num - s * 0.045f, s * 0.5f, s * 0.09f, s * 0.09f);
		g.FillRectangle(brush2, num - s * 0.018f, s * 0.56f, s * 0.036f, s * 0.12f);
	}

	private static void IsoGear(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var right);
		float num = s * 0.5f;
		float num2 = s * 0.52f;
		float num3 = s * 0.4f;
		float num4 = s * 0.28f;
		Shadow(g, num, num2 + num3 + s * 0.03f, num3 * 0.95f, s * 0.05f, p);
		using (SolidBrush brush = new SolidBrush(left))
		{
			for (int i = 0; i < 8; i++)
			{
				double num5 = (double)i * Math.PI / 4.0;
				PointF[] array = new PointF[4];
				for (int j = 0; j < 4; j++)
				{
					double num6 = num5 - 0.3 + (double)j * 0.2;
					float num7 = ((j == 0 || j == 3) ? num4 : (num3 + s * 0.07f));
					array[j] = new PointF(num + (float)Math.Cos(num6) * num7, num2 + (float)Math.Sin(num6) * num7);
				}
				g.FillPolygon(brush, array);
			}
		}
		using (LinearGradientBrush brush2 = new LinearGradientBrush(new RectangleF(num - num3, num2 - num3, num3 * 2f, num3 * 2f), top, right, 60f))
		{
			g.FillEllipse(brush2, num - num3, num2 - num3, num3 * 2f, num3 * 2f);
		}
		using (Pen pen = new Pen(Color.FromArgb(200, Lift(top, 0.7f)), Math.Max(1f, s / 34f)))
		{
			g.DrawEllipse(pen, num - num3, num2 - num3, num3 * 2f, num3 * 2f);
		}
		using (SolidBrush brush3 = new SolidBrush(Deep(left, 0.35f)))
		{
			g.FillEllipse(brush3, num - num4, num2 - num4, num4 * 2f, num4 * 2f);
		}
		using Pen pen2 = new Pen(Color.FromArgb(120, Color.White), Math.Max(1f, s / 40f));
		g.DrawArc(pen2, num - num4, num2 - num4, num4 * 2f, num4 * 2f, 200f, 90f);
	}

	private static void IsoTrash(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var _, out var _, out var _);
		Color c = (p.Light ? Color.FromArgb(120, 158, 214) : Color.FromArgb(92, 132, 200));
		float num = s * 0.5f;
		float num2 = s * 0.28f;
		float num3 = s * 0.86f;
		float num4 = s * 0.52f;
		float num5 = num4 / 2f;
		float num6 = s * 0.055f;
		Shadow(g, num, num3 + s * 0.03f, num5 * 1.05f, s * 0.045f, p);
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(num - num5, num2, num4, num3 - num2), Lift(c, 0.35f), Deep(c, 0.45f), 0f))
		{
			g.FillPolygon(brush, new PointF[4]
			{
				new PointF(num - num5, num2),
				new PointF(num + num5, num2),
				new PointF(num + num5 * 0.8f, num3),
				new PointF(num - num5 * 0.8f, num3)
			});
		}
		using (SolidBrush brush2 = new SolidBrush(Lift(c, 0.62f)))
		{
			g.FillEllipse(brush2, num - num5 * 1.12f, num2 - num6 * 1.6f, num5 * 2.24f, num6 * 2.4f);
		}
		using (Pen pen = new Pen(Color.FromArgb(210, Lift(c, 0.8f)), Math.Max(1f, s / 34f)))
		{
			g.DrawEllipse(pen, num - num5 * 1.12f, num2 - num6 * 1.6f, num5 * 2.24f, num6 * 2.4f);
		}
		using (SolidBrush brush3 = new SolidBrush(Deep(c, 0.25f)))
		{
			g.FillRectangle(brush3, num - s * 0.07f, num2 - s * 0.09f, s * 0.14f, s * 0.05f);
		}
		using Pen pen2 = new Pen(Color.FromArgb(140, Color.White), Math.Max(1f, s / 44f));
		for (int i = -1; i <= 1; i++)
		{
			float num7 = num + (float)i * num5 * 0.42f;
			g.DrawLine(pen2, num7, num2 + s * 0.08f, num7, num3 - s * 0.06f);
		}
	}

	private static void IsoDownload(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.5f;
		Shadow(g, num, s * 0.86f, s * 0.24f, s * 0.05f, p);
		using (SolidBrush brush = new SolidBrush(Lift(top, 0.25f)))
		{
			g.FillPolygon(brush, new PointF[7]
			{
				new PointF(num, s * 0.62f),
				new PointF(num + s * 0.19f, s * 0.4f),
				new PointF(num + s * 0.075f, s * 0.4f),
				new PointF(num + s * 0.075f, s * 0.14f),
				new PointF(num - s * 0.075f, s * 0.14f),
				new PointF(num - s * 0.075f, s * 0.4f),
				new PointF(num - s * 0.19f, s * 0.4f)
			});
		}
		using (Pen pen = new Pen(Color.FromArgb(220, Lift(top, 0.75f)), Math.Max(1f, s / 32f)))
		{
			g.DrawLine(pen, num - s * 0.075f, s * 0.14f, num + s * 0.075f, s * 0.14f);
			g.DrawLine(pen, num + s * 0.075f, s * 0.4f, num + s * 0.19f, s * 0.4f);
			g.DrawLine(pen, num - s * 0.075f, s * 0.4f, num - s * 0.19f, s * 0.4f);
		}
		using (LinearGradientBrush brush2 = new LinearGradientBrush(new RectangleF(num - s * 0.32f, s * 0.7f, s * 0.64f, s * 0.14f), left, top, 90f))
		{
			g.FillRectangle(brush2, num - s * 0.32f, s * 0.7f, s * 0.64f, s * 0.14f);
		}
		using Pen pen2 = new Pen(Color.FromArgb(200, Lift(top, 0.6f)), 1f);
		g.DrawRectangle(pen2, num - s * 0.32f, s * 0.7f, s * 0.64f, s * 0.14f);
	}

	private static void IsoWrench(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var _, out var _);
		float num = s * 0.5f;
		Shadow(g, num, s * 0.88f, s * 0.22f, s * 0.045f, p);
		using (Pen pen = new Pen(Lift(top, 0.18f), Math.Max(2.4f, s / 8.5f)))
		{
			pen.StartCap = LineCap.Round;
			pen.EndCap = LineCap.Round;
			g.DrawLine(pen, num - s * 0.14f, s * 0.72f, num + s * 0.16f, s * 0.26f);
		}
		using (Pen pen2 = new Pen(Color.FromArgb(150, Color.White), Math.Max(1f, s / 26f)))
		{
			g.DrawLine(pen2, num - s * 0.17f, s * 0.7f, num + s * 0.13f, s * 0.25f);
		}
		using (SolidBrush brush = new SolidBrush(Lift(top, 0.42f)))
		{
			PointF[] points = new PointF[4]
			{
				new PointF(num + s * 0.06f, s * 0.3f),
				new PointF(num + s * 0.22f, s * 0.1f),
				new PointF(num + s * 0.38f, s * 0.22f),
				new PointF(num + s * 0.2f, s * 0.4f)
			};
			g.FillPolygon(brush, points);
		}
		using Pen pen3 = new Pen(Color.FromArgb(200, Lift(top, 0.75f)), Math.Max(1f, s / 34f));
		g.DrawPolygon(pen3, new PointF[4]
		{
			new PointF(num + s * 0.06f, s * 0.3f),
			new PointF(num + s * 0.22f, s * 0.1f),
			new PointF(num + s * 0.38f, s * 0.22f),
			new PointF(num + s * 0.2f, s * 0.4f)
		});
	}

	private static void IsoCheck(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.5f;
		float num2 = s * 0.52f;
		Shadow(g, num, s * 0.86f, s * 0.26f, s * 0.045f, p);
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(s * 0.14f, s * 0.14f, s * 0.72f, s * 0.72f), top, left, 60f))
		{
			g.FillEllipse(brush, s * 0.14f, s * 0.14f, s * 0.72f, s * 0.72f);
		}
		using (Pen pen = new Pen(Color.FromArgb(220, Lift(top, 0.72f)), Math.Max(1.4f, s / 22f)))
		{
			g.DrawEllipse(pen, s * 0.14f, s * 0.14f, s * 0.72f, s * 0.72f);
		}
		using Pen pen2 = new Pen(Color.White, Math.Max(2.2f, s / 7.5f));
		pen2.StartCap = LineCap.Round;
		pen2.EndCap = LineCap.Round;
		pen2.LineJoin = LineJoin.Round;
		g.DrawLines(pen2, new PointF[3]
		{
			new PointF(num - s * 0.2f, num2 + s * 0.02f),
			new PointF(num - s * 0.05f, num2 + s * 0.17f),
			new PointF(num + s * 0.21f, num2 - s * 0.16f)
		});
	}

	private static void IsoPower(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var _, out var _);
		float num = s * 0.5f;
		float num2 = s * 0.54f;
		float num3 = s * 0.3f;
		Shadow(g, num, s * 0.88f, num3 * 0.9f, s * 0.04f, p);
		using (Pen pen = new Pen(Lift(top, 0.25f), Math.Max(2.4f, s / 8f)))
		{
			pen.StartCap = LineCap.Round;
			g.DrawArc(pen, num - num3, num2 - num3, num3 * 2f, num3 * 2f, -60f, 300f);
		}
		using (Pen pen2 = new Pen(Color.FromArgb(160, Color.White), Math.Max(1f, s / 30f)))
		{
			g.DrawArc(pen2, num - num3 * 0.72f, num2 - num3 * 0.72f, num3 * 1.44f, num3 * 1.44f, -50f, 100f);
		}
		using Pen pen3 = new Pen(Lift(top, 0.5f), Math.Max(2.4f, s / 8f));
		pen3.StartCap = LineCap.Round;
		g.DrawLine(pen3, num, num2 - num3 - s * 0.06f, num, num2 - s * 0.02f);
	}

	private static void IsoBolt(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.5f;
		Shadow(g, num, s * 0.9f, s * 0.2f, s * 0.04f, p);
		PointF[] points = new PointF[6]
		{
			new PointF(num + s * 0.06f, s * 0.08f),
			new PointF(num - s * 0.24f, s * 0.54f),
			new PointF(num - s * 0.02f, s * 0.54f),
			new PointF(num - s * 0.1f, s * 0.92f),
			new PointF(num + s * 0.26f, s * 0.42f),
			new PointF(num + s * 0.03f, s * 0.42f)
		};
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(num - s * 0.3f, s * 0.08f, s * 0.6f, s * 0.84f), Lift(top, 0.55f), left, 45f))
		{
			g.FillPolygon(brush, points);
		}
		using Pen pen = new Pen(Color.FromArgb(220, Lift(top, 0.85f)), Math.Max(1f, s / 34f));
		g.DrawPolygon(pen, points);
	}

	private static void IsoSignal(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.5f;
		Shadow(g, num, s * 0.88f, s * 0.34f, s * 0.045f, p);
		for (int i = 0; i < 3; i++)
		{
			float num2 = s * (0.2f + (float)i * 0.13f);
			using Pen pen = new Pen((i == 1) ? Lift(top, 0.5f) : Lift(top, 0.15f), Math.Max(1.8f, s / 11f));
			pen.StartCap = LineCap.Round;
			g.DrawArc(pen, num - num2, s * 0.52f - num2, num2 * 2f, num2 * 2f, 200f, 140f);
		}
		using (SolidBrush brush = new SolidBrush(Deep(left, 0.2f)))
		{
			g.FillEllipse(brush, num - s * 0.1f, s * 0.6f, s * 0.2f, s * 0.2f);
		}
		using SolidBrush brush2 = new SolidBrush(Lift(top, 0.6f));
		g.FillEllipse(brush2, num - s * 0.06f, s * 0.63f, s * 0.1f, s * 0.09f);
	}

	private static void IsoRefresh(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var _, out var _);
		float num = s * 0.5f;
		float num2 = s * 0.52f;
		float num3 = s * 0.28f;
		Shadow(g, num, s * 0.88f, num3 * 0.9f, s * 0.04f, p);
		using (Pen pen = new Pen(Lift(top, 0.28f), Math.Max(2.4f, s / 8.5f)))
		{
			pen.StartCap = LineCap.Round;
			g.DrawArc(pen, num - num3, num2 - num3, num3 * 2f, num3 * 2f, 40f, 270f);
		}
		using (SolidBrush brush = new SolidBrush(Lift(top, 0.5f)))
		{
			g.FillPolygon(brush, new PointF[3]
			{
				new PointF(num + num3 * 0.05f, num2 - num3 * 1.28f),
				new PointF(num + num3 * 0.98f, num2 - num3 * 0.72f),
				new PointF(num + num3 * 0.05f, num2 - num3 * 0.2f)
			});
		}
		using Pen pen2 = new Pen(Color.FromArgb(140, Color.White), Math.Max(1f, s / 32f));
		g.DrawArc(pen2, num - num3 * 0.6f, num2 - num3 * 0.6f, num3 * 1.2f, num3 * 1.2f, 60f, 90f);
	}

	private static void IsoFolder(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.12f;
		float num2 = s * 0.76f;
		float num3 = s * 0.32f;
		float num4 = s * 0.46f;
		Shadow(g, s * 0.5f, num3 + num4 + s * 0.04f, num2 * 0.5f, s * 0.04f, p);
		using (SolidBrush brush = new SolidBrush(Deep(left, 0.15f)))
		{
			g.FillRectangle(brush, num, num3 - s * 0.09f, num2 * 0.44f, s * 0.12f);
		}
		using (LinearGradientBrush brush2 = new LinearGradientBrush(new RectangleF(num, num3, num2, num4), Lift(top, 0.25f), left, 90f))
		{
			g.FillRectangle(brush2, num, num3, num2, num4);
		}
		using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(70, Lift(top, 0.7f))))
		{
			g.FillPolygon(brush3, new PointF[4]
			{
				new PointF(num, num3),
				new PointF(num + num2, num3),
				new PointF(num + num2, num3 + num4 * 0.3f),
				new PointF(num, num3 + num4 * 0.3f)
			});
		}
		using Pen pen = new Pen(Color.FromArgb(210, Lift(top, 0.75f)), Math.Max(1f, s / 34f));
		g.DrawRectangle(pen, num, num3, num2, num4);
	}

	private static void IsoGlobe(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.5f;
		float num2 = s * 0.52f;
		float num3 = s * 0.36f;
		Shadow(g, num, s * 0.9f, num3 * 0.9f, s * 0.04f, p);
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(num - num3, num2 - num3, num3 * 2f, num3 * 2f), Lift(top, 0.4f), left, 60f))
		{
			g.FillEllipse(brush, num - num3, num2 - num3, num3 * 2f, num3 * 2f);
		}
		using (Pen pen = new Pen(Color.FromArgb(200, Lift(top, 0.8f)), Math.Max(1f, s / 36f)))
		{
			g.DrawEllipse(pen, num - num3, num2 - num3, num3 * 2f, num3 * 2f);
			g.DrawEllipse(pen, num - num3 * 0.42f, num2 - num3, num3 * 0.84f, num3 * 2f);
			g.DrawLine(pen, num - num3, num2, num + num3, num2);
			g.DrawArc(pen, num - num3, num2 - num3 * 0.55f, num3 * 2f, num3 * 1.1f, 0f, 180f);
		}
		using Pen pen2 = new Pen(Color.FromArgb(140, Color.White), Math.Max(1f, s / 30f));
		g.DrawArc(pen2, num - num3 * 0.72f, num2 - num3 * 0.78f, num3 * 1.3f, num3 * 1.3f, 170f, 80f);
	}

	private static void IsoClock(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.5f;
		float num2 = s * 0.54f;
		float num3 = s * 0.34f;
		Shadow(g, num, s * 0.9f, num3 * 0.9f, s * 0.04f, p);
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(num - num3, num2 - num3, num3 * 2f, num3 * 2f), top, left, 90f))
		{
			g.FillEllipse(brush, num - num3, num2 - num3, num3 * 2f, num3 * 2f);
		}
		using (Pen pen = new Pen(Color.FromArgb(210, Lift(top, 0.78f)), Math.Max(1.2f, s / 30f)))
		{
			g.DrawEllipse(pen, num - num3, num2 - num3, num3 * 2f, num3 * 2f);
		}
		using (Pen pen2 = new Pen(Color.White, Math.Max(1.8f, s / 16f)))
		{
			pen2.StartCap = LineCap.Round;
			pen2.EndCap = LineCap.Round;
			g.DrawLine(pen2, num, num2, num, num2 - num3 * 0.58f);
			g.DrawLine(pen2, num, num2, num + num3 * 0.46f, num2 + num3 * 0.16f);
		}
		using SolidBrush brush2 = new SolidBrush(Color.White);
		g.FillEllipse(brush2, num - s * 0.035f, num2 - s * 0.035f, s * 0.07f, s * 0.07f);
	}

	private static void IsoChart(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		Shadow(g, s * 0.5f, s * 0.88f, s * 0.3f, s * 0.04f, p);
		float num = s * 0.82f;
		using (Pen pen = new Pen(Color.FromArgb(170, Lift(top, 0.5f)), Math.Max(1.2f, s / 34f)))
		{
			g.DrawLine(pen, s * 0.14f, num, s * 0.86f, num);
			g.DrawLine(pen, s * 0.14f, s * 0.16f, s * 0.14f, num);
		}
		Color[] array = new Color[4]
		{
			Deep(left, 0.1f),
			Lift(top, 0.15f),
			Lift(top, 0.5f),
			Lift(top, 0.85f)
		};
		float num2 = s * 0.105f;
		for (int i = 0; i < 4; i++)
		{
			float num3 = s * (0.16f + (float)i * 0.13f);
			float num4 = s * 0.24f + (float)i * s * 0.15f;
			using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(num4, num - num3, num2, num3), array[i], Deep(array[i], 0.4f), 90f))
			{
				g.FillRectangle(brush, num4, num - num3, num2, num3);
			}
			using Pen pen2 = new Pen(Color.FromArgb(180, Color.White), Math.Max(1f, s / 60f));
			g.DrawLine(pen2, num4, num - num3, num4 + num2, num - num3);
		}
	}

	private static void IsoCross(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.5f;
		float num2 = s * 0.52f;
		float num3 = s * 0.32f;
		Shadow(g, num, s * 0.88f, num3 * 0.9f, s * 0.04f, p);
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(num - num3, num2 - num3, num3 * 2f, num3 * 2f), top, left, 60f))
		{
			g.FillEllipse(brush, num - num3, num2 - num3, num3 * 2f, num3 * 2f);
		}
		using (Pen pen = new Pen(Color.FromArgb(200, Lift(top, 0.7f)), Math.Max(1f, s / 34f)))
		{
			g.DrawEllipse(pen, num - num3, num2 - num3, num3 * 2f, num3 * 2f);
		}
		using Pen pen2 = new Pen(Color.White, Math.Max(2.2f, s / 8.5f));
		pen2.StartCap = LineCap.Round;
		pen2.EndCap = LineCap.Round;
		float num4 = num3 * 0.44f;
		g.DrawLine(pen2, num - num4, num2 - num4, num + num4, num2 + num4);
		g.DrawLine(pen2, num + num4, num2 - num4, num - num4, num2 + num4);
	}

	private static void IsoPlay(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.5f;
		float num2 = s * 0.52f;
		float num3 = s * 0.34f;
		Shadow(g, num, s * 0.9f, num3 * 0.9f, s * 0.04f, p);
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(num - num3, num2 - num3, num3 * 2f, num3 * 2f), top, left, 60f))
		{
			g.FillEllipse(brush, num - num3, num2 - num3, num3 * 2f, num3 * 2f);
		}
		using (Pen pen = new Pen(Color.FromArgb(200, Lift(top, 0.72f)), Math.Max(1f, s / 34f)))
		{
			g.DrawEllipse(pen, num - num3, num2 - num3, num3 * 2f, num3 * 2f);
		}
		using SolidBrush brush2 = new SolidBrush(Color.White);
		g.FillPolygon(brush2, new PointF[3]
		{
			new PointF(num - num3 * 0.34f, num2 - num3 * 0.52f),
			new PointF(num + num3 * 0.58f, num2),
			new PointF(num - num3 * 0.34f, num2 + num3 * 0.52f)
		});
	}

	private static void IsoSatellite(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.42f;
		float num2 = s * 0.52f;
		Shadow(g, s * 0.5f, s * 0.9f, s * 0.3f, s * 0.04f, p);
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(num - s * 0.3f, num2 - s * 0.2f, s * 0.6f, s * 0.4f), Lift(top, 0.6f), left, 60f))
		{
			g.FillEllipse(brush, num - s * 0.3f, num2 - s * 0.2f, s * 0.6f, s * 0.4f);
		}
		using (Pen pen = new Pen(Color.FromArgb(220, Lift(top, 0.8f)), Math.Max(1.2f, s / 30f)))
		{
			g.DrawEllipse(pen, num - s * 0.3f, num2 - s * 0.2f, s * 0.6f, s * 0.4f);
		}
		using (SolidBrush brush2 = new SolidBrush(Deep(left, 0.3f)))
		{
			g.FillEllipse(brush2, num - s * 0.06f, num2 - s * 0.05f, s * 0.12f, s * 0.1f);
		}
		using (Pen pen2 = new Pen(Lift(top, 0.2f), Math.Max(1.8f, s / 14f)))
		{
			pen2.StartCap = LineCap.Round;
			g.DrawLine(pen2, num, num2 + s * 0.18f, num, num2 + s * 0.38f);
			g.DrawLine(pen2, num - s * 0.16f, num2 + s * 0.38f, num + s * 0.16f, num2 + s * 0.38f);
			g.DrawLine(pen2, num, num2 + s * 0.02f, num + s * 0.2f, num2 - s * 0.22f);
		}
		using (SolidBrush brush3 = new SolidBrush(Lift(top, 0.75f)))
		{
			g.FillEllipse(brush3, num + s * 0.15f, num2 - s * 0.3f, s * 0.12f, s * 0.12f);
		}
		for (int i = 1; i <= 2; i++)
		{
			using Pen pen3 = new Pen(Color.FromArgb(200 - i * 40, Lift(top, 0.9f)), Math.Max(1.2f, s / 24f));
			g.DrawArc(pen3, num + s * (0.08f + (float)i * 0.1f), num2 - s * (0.26f + (float)i * 0.06f), s * (0.26f + (float)i * 0.1f), s * (0.26f + (float)i * 0.1f), -60f, 80f);
		}
	}

	private static void IsoRadio(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.5f;
		float num2 = s * 0.62f;
		float num3 = s * 0.34f;
		float num4 = num - num2 / 2f;
		float num5 = s * 0.48f;
		Shadow(g, num, num5 + num3 + s * 0.06f, num2 * 0.5f, s * 0.04f, p);
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(num4, num5, num2, num3), Lift(top, 0.35f), left, 90f))
		{
			g.FillRectangle(brush, num4, num5, num2, num3);
		}
		using (Pen pen = new Pen(Color.FromArgb(210, Lift(top, 0.75f)), Math.Max(1f, s / 34f)))
		{
			g.DrawRectangle(pen, num4, num5, num2, num3);
		}
		using (Pen pen2 = new Pen(Color.FromArgb(150, Color.White), Math.Max(1f, s / 48f)))
		{
			for (int i = 0; i < 3; i++)
			{
				float num6 = num5 + num3 * (0.26f + (float)i * 0.22f);
				g.DrawLine(pen2, num4 + num2 * 0.1f, num6, num4 + num2 * 0.5f, num6);
			}
		}
		using (SolidBrush brush2 = new SolidBrush(Lift(top, 0.62f)))
		{
			g.FillEllipse(brush2, num4 + num2 * 0.62f, num5 + num3 * 0.24f, num2 * 0.26f, num2 * 0.26f);
		}
		using (Pen pen3 = new Pen(Color.FromArgb(200, Color.White), 1f))
		{
			g.DrawEllipse(pen3, num4 + num2 * 0.62f, num5 + num3 * 0.24f, num2 * 0.26f, num2 * 0.26f);
		}
		using (Pen pen4 = new Pen(Lift(top, 0.3f), Math.Max(1.6f, s / 15f)))
		{
			pen4.StartCap = LineCap.Round;
			g.DrawLine(pen4, num + num2 * 0.3f, num5, num + num2 * 0.58f, s * 0.12f);
		}
		for (int j = 1; j <= 2; j++)
		{
			using Pen pen5 = new Pen(Color.FromArgb(210 - j * 55, Lift(top, 0.95f)), Math.Max(1.2f, s / 26f));
			g.DrawArc(pen5, num + num2 * (0.44f + (float)j * 0.1f), s * (0.1f + (float)j * 0.03f), s * (0.22f + (float)j * 0.1f), s * (0.22f + (float)j * 0.1f), -70f, 90f);
		}
	}

	private static void IsoCamera(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float cx = s * 0.5f;
		float num = s * 0.54f;
		float num2 = s * 0.4f;
		float num3 = s * 0.12f;
		float num4 = s * 0.3f;
		Shadow(g, cx, num4 + num2 + s * 0.06f, s * 0.3f, s * 0.04f, p);
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(num3, num4, num, num2), Lift(top, 0.35f), left, 90f))
		{
			g.FillRectangle(brush, num3, num4, num, num2);
		}
		using (Pen pen = new Pen(Color.FromArgb(210, Lift(top, 0.72f)), Math.Max(1f, s / 34f)))
		{
			g.DrawRectangle(pen, num3, num4, num, num2);
		}
		float num5 = s * 0.17f;
		float num6 = num3 + num * 0.52f;
		float num7 = num4 + num2 * 0.5f;
		using (LinearGradientBrush brush2 = new LinearGradientBrush(new RectangleF(num6 - num5, num7 - num5, num5 * 2f, num5 * 2f), Deep(left, 0.5f), Lift(top, 0.5f), 45f))
		{
			g.FillEllipse(brush2, num6 - num5, num7 - num5, num5 * 2f, num5 * 2f);
		}
		using (Pen pen2 = new Pen(Color.FromArgb(230, Lift(top, 0.85f)), Math.Max(1.2f, s / 30f)))
		{
			g.DrawEllipse(pen2, num6 - num5, num7 - num5, num5 * 2f, num5 * 2f);
		}
		using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(190, Color.White)))
		{
			g.FillEllipse(brush3, num6 - num5 * 0.5f, num7 - num5 * 0.6f, num5 * 0.44f, num5 * 0.36f);
		}
		using (SolidBrush brush4 = new SolidBrush(Lift(top, 0.15f)))
		{
			g.FillPolygon(brush4, new PointF[4]
			{
				new PointF(num3 + num, num4 + num2 * 0.22f),
				new PointF(s * 0.9f, num4 + num2 * 0.06f),
				new PointF(s * 0.9f, num4 + num2 * 0.52f),
				new PointF(num3 + num, num4 + num2 * 0.78f)
			});
		}
		using (Pen pen3 = new Pen(Color.FromArgb(190, Lift(top, 0.7f)), Math.Max(1f, s / 40f)))
		{
			g.DrawLines(pen3, new PointF[4]
			{
				new PointF(num3 + num, num4 + num2 * 0.22f),
				new PointF(s * 0.9f, num4 + num2 * 0.06f),
				new PointF(s * 0.9f, num4 + num2 * 0.52f),
				new PointF(num3 + num, num4 + num2 * 0.78f)
			});
		}
		using SolidBrush brush5 = new SolidBrush(Lift(top, 0.55f));
		g.FillRectangle(brush5, num3 + num * 0.14f, num4 - s * 0.09f, num * 0.3f, s * 0.09f);
	}

	private static void IsoAntenna(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var _, out var _);
		float num = s * 0.5f;
		Shadow(g, num, s * 0.9f, s * 0.26f, s * 0.04f, p);
		using (Pen pen = new Pen(Lift(top, 0.35f), Math.Max(2f, s / 13f)))
		{
			pen.StartCap = LineCap.Round;
			g.DrawLine(pen, num, s * 0.86f, num, s * 0.26f);
			g.DrawLine(pen, num - s * 0.24f, s * 0.16f, num + s * 0.24f, s * 0.16f);
			g.DrawLine(pen, num - s * 0.24f, s * 0.16f, num, s * 0.32f);
			g.DrawLine(pen, num + s * 0.24f, s * 0.16f, num, s * 0.32f);
		}
		using (Pen pen2 = new Pen(Color.FromArgb(150, Color.White), Math.Max(1f, s / 30f)))
		{
			g.DrawLine(pen2, num - s * 0.02f, s * 0.82f, num - s * 0.02f, s * 0.3f);
		}
		using SolidBrush brush = new SolidBrush(Lift(top, 0.6f));
		g.FillEllipse(brush, num - s * 0.05f, s * 0.84f, s * 0.1f, s * 0.06f);
	}

	private static void IsoMic(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.5f;
		Shadow(g, num, s * 0.92f, s * 0.24f, s * 0.04f, p);
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(num - s * 0.13f, s * 0.12f, s * 0.26f, s * 0.44f), Lift(top, 0.55f), left, 0f))
		{
			g.FillEllipse(brush, num - s * 0.13f, s * 0.12f, s * 0.26f, s * 0.44f);
		}
		using (Pen pen = new Pen(Color.FromArgb(210, Lift(top, 0.8f)), Math.Max(1f, s / 34f)))
		{
			g.DrawEllipse(pen, num - s * 0.13f, s * 0.12f, s * 0.26f, s * 0.44f);
		}
		using (SolidBrush brush2 = new SolidBrush(Lift(top, 0.85f)))
		{
			g.FillRectangle(brush2, num - s * 0.09f, s * 0.17f, s * 0.05f, s * 0.34f);
		}
		using Pen pen2 = new Pen(Lift(top, 0.3f), Math.Max(1.8f, s / 14f));
		g.DrawArc(pen2, num - s * 0.22f, s * 0.42f, s * 0.44f, s * 0.34f, 0f, 180f);
		pen2.StartCap = LineCap.Round;
		g.DrawLine(pen2, num, s * 0.76f, num, s * 0.88f);
		g.DrawLine(pen2, num - s * 0.13f, s * 0.88f, num + s * 0.13f, s * 0.88f);
	}

	private static void IsoNetwork(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.5f;
		Shadow(g, num, s * 0.88f, s * 0.3f, s * 0.04f, p);
		PointF[] array = new PointF[3]
		{
			new PointF(num, s * 0.2f),
			new PointF(s * 0.2f, s * 0.72f),
			new PointF(s * 0.8f, s * 0.72f)
		};
		using (Pen pen = new Pen(Color.FromArgb(200, Lift(top, 0.4f)), Math.Max(1.6f, s / 18f)))
		{
			g.DrawLine(pen, array[0], array[1]);
			g.DrawLine(pen, array[0], array[2]);
			g.DrawLine(pen, array[1], array[2]);
		}
		for (int i = 0; i < 3; i++)
		{
			float num2 = s * 0.115f;
			using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(array[i].X - num2, array[i].Y - num2, num2 * 2f, num2 * 2f), Lift(top, 0.6f), left, 45f))
			{
				g.FillEllipse(brush, array[i].X - num2, array[i].Y - num2, num2 * 2f, num2 * 2f);
			}
			using Pen pen2 = new Pen(Color.FromArgb(220, Lift(top, 0.9f)), Math.Max(1f, s / 40f));
			g.DrawEllipse(pen2, array[i].X - num2, array[i].Y - num2, num2 * 2f, num2 * 2f);
		}
	}

	private static void IsoStar(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.5f;
		float num2 = s * 0.52f;
		Shadow(g, num, s * 0.9f, s * 0.26f, s * 0.04f, p);
		PointF[] points = Star(num, num2, s * 0.4f, s * 0.17f, 5);
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(num - s * 0.4f, num2 - s * 0.4f, s * 0.8f, s * 0.8f), Lift(top, 0.6f), left, 60f))
		{
			g.FillPolygon(brush, points);
		}
		using Pen pen = new Pen(Color.FromArgb(230, Lift(top, 0.95f)), Math.Max(1f, s / 34f));
		g.DrawPolygon(pen, points);
	}

	private static void IsoSun(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.5f;
		float num2 = s * 0.5f;
		float num3 = s * 0.24f;
		Shadow(g, num, num2 + num3 + s * 0.08f, num3 * 0.9f, s * 0.035f, p);
		for (int i = 0; i < 8; i++)
		{
			double num4 = (double)i * Math.PI / 4.0;
			float x = num + (float)Math.Cos(num4) * (num3 * 1.3f);
			float y = num2 + (float)Math.Sin(num4) * (num3 * 1.3f);
			float x2 = num + (float)Math.Cos(num4) * (num3 * 1.92f);
			float y2 = num2 + (float)Math.Sin(num4) * (num3 * 1.92f);
			using Pen pen = new Pen(Color.FromArgb(220, top), Math.Max(1.4f, s / 20f));
			pen.StartCap = LineCap.Round;
			pen.EndCap = LineCap.Round;
			g.DrawLine(pen, x, y, x2, y2);
		}
		using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(num - num3, num2 - num3, num3 * 2f, num3 * 2f), Lift(top, 0.75f), left, 55f))
		{
			g.FillEllipse(brush, num - num3, num2 - num3, num3 * 2f, num3 * 2f);
		}
		using (Pen pen2 = new Pen(Color.FromArgb(230, Lift(top, 1f)), Math.Max(1f, s / 34f)))
		{
			g.DrawEllipse(pen2, num - num3, num2 - num3, num3 * 2f, num3 * 2f);
		}
		using SolidBrush brush2 = new SolidBrush(Color.FromArgb(180, Color.White));
		g.FillEllipse(brush2, num - num3 * 0.55f, num2 - num3 * 0.62f, num3 * 0.5f, num3 * 0.4f);
	}

	private static void IsoMoon(Graphics g, float s, NeoPalette p)
	{
		CubeFaces(p, out var top, out var left, out var _);
		float num = s * 0.5f;
		float num2 = s * 0.5f;
		float num3 = s * 0.34f;
		Shadow(g, num, num2 + num3 + s * 0.06f, num3 * 0.85f, s * 0.035f, p);
		using (GraphicsPath graphicsPath = new GraphicsPath())
		{
			graphicsPath.AddEllipse(num - num3, num2 - num3, num3 * 2f, num3 * 2f);
			graphicsPath.AddEllipse(num - num3 + num3 * 0.62f, num2 - num3 * 1.06f, num3 * 2f, num3 * 2f);
			using (LinearGradientBrush brush = new LinearGradientBrush(new RectangleF(num - num3, num2 - num3, num3 * 2f, num3 * 2f), Lift(top, 0.8f), left, 55f))
			{
				g.FillPath(brush, graphicsPath);
			}
			using Pen pen = new Pen(Color.FromArgb(220, Lift(top, 1f)), Math.Max(1f, s / 36f));
			g.DrawPath(pen, graphicsPath);
		}
		using SolidBrush brush2 = new SolidBrush(Color.FromArgb(170, Color.White));
		g.FillEllipse(brush2, num - num3 * 0.72f, num2 - num3 * 0.68f, num3 * 0.34f, num3 * 0.44f);
	}

	private static PointF[] Star(float cx, float cy, float rOut, float rIn, int points)
	{
		PointF[] array = new PointF[points * 2];
		for (int i = 0; i < array.Length; i++)
		{
			double num = -Math.PI / 2.0 + (double)i * Math.PI / (double)points;
			float num2 = ((i % 2 == 0) ? rOut : rIn);
			array[i] = new PointF(cx + (float)Math.Cos(num) * num2, cy + (float)Math.Sin(num) * num2);
		}
		return array;
	}

	private static void Shadow(Graphics g, float cx, float cy, float rx, float ry, NeoPalette p)
	{
		if (rx < 1f || ry < 1f)
		{
			return;
		}
		using GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.AddEllipse(cx - rx, cy - ry, rx * 2f, ry * 2f);
		using PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
		pathGradientBrush.CenterColor = p.Shadow;
		pathGradientBrush.SurroundColors = new Color[1] { Color.FromArgb(0, p.Shadow) };
		g.FillEllipse(pathGradientBrush, cx - rx, cy - ry, rx * 2f, ry * 2f);
	}
}
