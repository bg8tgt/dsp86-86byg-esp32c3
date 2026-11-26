using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace YzControl;

internal static class Theme
{
	private static readonly NeoPalette Dark = BuildDark();

	private static readonly NeoPalette Light = BuildLight();

	private static NeoPalette _p = Dark;

	private static readonly Dictionary<string, Font> _fontCache = new Dictionary<string, Font>();

	private static readonly Dictionary<string, string> _familyCache = new Dictionary<string, string>();

	private static readonly string[] UiFamilyCandidates = new string[6] { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans SC", "Source Han Sans SC", "SimHei", "SimSun" };

	private static readonly string[] MonoFamilyCandidates = new string[4] { "Consolas", "Cascadia Mono", "Courier New", "Lucida Console" };

	public static readonly Font FForm = FUi(10f, FontStyle.Bold);

	internal const int ToggleGlyph = 14;

	internal const int ToggleTextGap = 10;

	public static NeoPalette P => _p;

	public static bool IsLight => _p.Light;

	public static Color Bg => _p.BgNear;

	public static Color Surface => _p.ShellTop;

	public static Color SurfaceAlt => _p.CardTop;

	public static Color SurfaceHi => _p.CardHotBottom;

	public static Color Border => _p.Stroke;

	public static Color Accent => _p.Accent;

	public static Color Accent2 => _p.Accent2;

	public static Color Text => _p.Text;

	public static Color TextDim => _p.TextDim;

	public static Color Ok => _p.Ok;

	public static Color Warn => _p.Warn;

	public static Color Danger => _p.Danger;

	public static Font FTitle => FUi(15.5f, FontStyle.Bold);

	public static Font FHead => FUi(11f, FontStyle.Bold);

	public static Font FBody => FUi(10f, FontStyle.Bold);

	public static Font FSmall => FUi(9f, FontStyle.Bold);

	public static Font FMono => FMonoF(10f, FontStyle.Bold);

	public static Font FMonoBig => FMonoF(12.5f, FontStyle.Bold);

	public static Font FCard => FUi(9.5f, FontStyle.Bold);

	public static Font FCardSmall => FUi(8.75f, FontStyle.Bold);

	public static Font FGroup => FUi(9.25f, FontStyle.Bold);

	public static event EventHandler Changed;

	private static NeoPalette BuildDark()
	{
		return new NeoPalette
		{
			Key = "dark",
			Light = false,
			BgDeep = Color.FromArgb(4, 10, 24),
			BgMid = Color.FromArgb(11, 27, 58),
			BgNear = Color.FromArgb(6, 14, 33),
			Grid = Color.FromArgb(20, 60, 110),
			GridBold = Color.FromArgb(34, 88, 150),
			Vignette = Color.FromArgb(140, 0, 0, 0),
			Beam = Color.FromArgb(46, 90, 190, 255),
			SpotA = Color.FromArgb(64, 40, 110, 220),
			SpotB = Color.FromArgb(46, 30, 170, 210),
			CardTop = Color.FromArgb(30, 60, 108),
			CardBottom = Color.FromArgb(11, 24, 48),
			CardHotTop = Color.FromArgb(58, 116, 190),
			CardHotBottom = Color.FromArgb(18, 42, 84),
			CardSelTop = Color.FromArgb(86, 162, 240),
			CardSelBottom = Color.FromArgb(20, 62, 130),
			ShellTop = Color.FromArgb(20, 40, 72),
			ShellBottom = Color.FromArgb(8, 16, 32),
			Stroke = Color.FromArgb(64, 116, 184),
			StrokeHot = Color.FromArgb(138, 198, 255),
			StrokeSel = Color.FromArgb(196, 230, 255),
			Glow = Color.FromArgb(70, 90, 170, 255),
			GlowHot = Color.FromArgb(190, 92, 176, 255),
			GlowSel = Color.FromArgb(230, 126, 200, 255),
			Shadow = Color.FromArgb(170, 0, 0, 0),
			Text = Color.FromArgb(242, 247, 255),
			TextDim = Color.FromArgb(185, 199, 218),
			TextOnHot = Color.White,
			TextDisabled = Color.FromArgb(126, 142, 166),
			Accent = Color.FromArgb(56, 208, 255),
			Accent2 = Color.FromArgb(70, 140, 250),
			AccentSoft = Color.FromArgb(120, 56, 208, 255),
			Ok = Color.FromArgb(52, 211, 153),
			Warn = Color.FromArgb(251, 191, 36),
			Danger = Color.FromArgb(255, 118, 118),
			Field = Color.FromArgb(7, 15, 30),
			FieldEdge = Color.FromArgb(36, 66, 108),
			Sel = Color.FromArgb(86, 162, 240),
			GlowAlpha = 1f
		};
	}

	private static NeoPalette BuildLight()
	{
		return new NeoPalette
		{
			Key = "light",
			Light = true,
			BgDeep = Color.FromArgb(222, 231, 243),
			BgMid = Color.FromArgb(244, 248, 253),
			BgNear = Color.FromArgb(215, 226, 240),
			Grid = Color.FromArgb(226, 234, 244),
			GridBold = Color.FromArgb(212, 224, 238),
			Vignette = Color.FromArgb(20, 120, 138, 168),
			Beam = Color.FromArgb(34, 96, 158, 236),
			SpotA = Color.FromArgb(52, 158, 202, 246),
			SpotB = Color.FromArgb(40, 128, 190, 236),
			CardTop = Color.FromArgb(255, 255, 255),
			CardBottom = Color.FromArgb(220, 233, 248),
			CardHotTop = Color.FromArgb(255, 255, 255),
			CardHotBottom = Color.FromArgb(196, 221, 248),
			CardSelTop = Color.FromArgb(120, 181, 246),
			CardSelBottom = Color.FromArgb(46, 116, 200),
			ShellTop = Color.FromArgb(255, 255, 255),
			ShellBottom = Color.FromArgb(228, 238, 250),
			Stroke = Color.FromArgb(142, 172, 206),
			StrokeHot = Color.FromArgb(58, 128, 206),
			StrokeSel = Color.FromArgb(30, 94, 172),
			Glow = Color.FromArgb(46, 70, 140, 220),
			GlowHot = Color.FromArgb(120, 58, 130, 210),
			GlowSel = Color.FromArgb(170, 42, 112, 196),
			Shadow = Color.FromArgb(70, 60, 85, 120),
			Text = Color.FromArgb(14, 21, 32),
			TextDim = Color.FromArgb(58, 70, 87),
			TextOnHot = Color.White,
			TextDisabled = Color.FromArgb(138, 148, 163),
			Accent = Color.FromArgb(14, 122, 200),
			Accent2 = Color.FromArgb(38, 96, 190),
			AccentSoft = Color.FromArgb(150, 20, 118, 196),
			Ok = Color.FromArgb(6, 148, 104),
			Warn = Color.FromArgb(196, 128, 4),
			Danger = Color.FromArgb(206, 42, 42),
			Field = Color.White,
			FieldEdge = Color.FromArgb(168, 189, 214),
			Sel = Color.FromArgb(38, 96, 190),
			GlowAlpha = 0.62f
		};
	}

	private static string ResolveFamily(string[] candidates, string cacheKey)
	{
		if (_familyCache.TryGetValue(cacheKey, out var value))
		{
			return value;
		}
		string text = null;
		try
		{
			using InstalledFontCollection installedFontCollection = new InstalledFontCollection();
			for (int i = 0; i < candidates.Length; i++)
			{
				if (text != null)
				{
					break;
				}
				for (int j = 0; j < installedFontCollection.Families.Length; j++)
				{
					if (string.Equals(installedFontCollection.Families[j].Name, candidates[i], StringComparison.OrdinalIgnoreCase) && installedFontCollection.Families[j].IsStyleAvailable(FontStyle.Bold))
					{
						text = installedFontCollection.Families[j].Name;
						break;
					}
				}
			}
		}
		catch
		{
		}
		if (text == null)
		{
			text = SystemFonts.DefaultFont.FontFamily.Name;
		}
		_familyCache[cacheKey] = text;
		return text;
	}

	public static Font F(string family, float size, FontStyle style)
	{
		string[] obj = new string[5]
		{
			family,
			"|",
			size.ToString("0.##", CultureInfo.InvariantCulture),
			"|",
			null
		};
		int num = (int)style;
		obj[4] = num.ToString();
		string key = string.Concat(obj);
		lock (_fontCache)
		{
			if (_fontCache.TryGetValue(key, out var value) && value != null)
			{
				return value;
			}
			try
			{
				value = new Font(family, size, style);
			}
			catch
			{
				value = new Font(SystemFonts.DefaultFont.FontFamily, size, style);
			}
			_fontCache[key] = value;
			return value;
		}
	}

	public static Font FUi(float size, FontStyle style)
	{
		return F(ResolveFamily(UiFamilyCandidates, "ui"), size, style);
	}

	public static Font FMonoF(float size, FontStyle style)
	{
		return F(ResolveFamily(MonoFamilyCandidates, "mono"), size, style);
	}

	/// <summary>
	/// v1.5.1：现场自愈。某些 Windows/GDI+ 环境下，**缓存里的 Font 实例**会在运行一段时间后
	/// 变成"无效句柄"——把它赋给新控件时 Control.set_Font → Font.GetHeight 抛
	/// ArgumentException("参数无效")；而新建的 Font 实例完全正常。
	/// 这里先备份引用（避免 Dispose 正在被既有控件使用的对象），只清空缓存字典并重建，
	/// 后续 F() 调用就会创建全新实例。页面构建失败时调用一次即可。
	/// </summary>
	public static void HealFontCache()
	{
		lock (_fontCache)
		{
			_fontCache.Clear();
		}
		RebuildFonts();
	}

	public static void ClearFontCache()
	{
		lock (_fontCache)
		{
			foreach (Font value in _fontCache.Values)
			{
				try
				{
					if (value != null && value != SystemFonts.DefaultFont)
					{
						value.Dispose();
					}
				}
				catch
				{
				}
			}
			_fontCache.Clear();
		}
		RebuildFonts();
	}

	private static void RebuildFonts()
	{
		_ = FTitle;
		_ = FHead;
		_ = FBody;
		_ = FSmall;
		_ = FMono;
		_ = FMonoBig;
		_ = FCard;
		_ = FCardSmall;
		_ = FGroup;
	}

	private static string ConfigPath()
	{
		try
		{
			string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YzControl");
			Directory.CreateDirectory(text);
			return Path.Combine(text, "ui.json");
		}
		catch
		{
			return null;
		}
	}

	public static void Load()
	{
		_p = Dark;
		try
		{
			string text = ConfigPath();
			if (text == null || !File.Exists(text))
			{
				return;
			}
			string text2 = File.ReadAllText(text);
			int num = text2.IndexOf("\"theme\"", StringComparison.OrdinalIgnoreCase);
			if (num >= 0)
			{
				int num2 = text2.IndexOf(':', num);
				int num3 = text2.IndexOf('"', num2 + 1);
				int num4 = text2.IndexOf('"', num3 + 1);
				if (num3 >= 0 && num4 >= 0 && string.Equals(text2.Substring(num3 + 1, num4 - num3 - 1).Trim(), "light", StringComparison.OrdinalIgnoreCase))
				{
					_p = Light;
				}
			}
		}
		catch
		{
			_p = Dark;
		}
	}

	public static void Save()
	{
		try
		{
			string text = ConfigPath();
			if (text != null)
			{
				File.WriteAllText(text, "{\r\n  \"theme\": \"" + _p.Key + "\",\r\n  \"savedAt\": \"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\"\r\n}\r\n", Encoding.UTF8);
			}
		}
		catch
		{
		}
	}

	public static bool Toggle()
	{
		_p = (_p.Light ? Dark : Light);
		Save();
		if (Theme.Changed != null)
		{
			try
			{
				Theme.Changed(null, EventArgs.Empty);
			}
			catch
			{
			}
		}
		return _p.Light;
	}

	public static void Apply(Form f)
	{
		f.BackColor = _p.BgNear;
		f.ForeColor = _p.Text;
		f.Font = FForm;
		if (f.IsHandleCreated)
		{
			f.Invalidate(invalidateChildren: true);
		}
	}

	public static void ApplyMenu(MenuStrip m)
	{
		m.BackColor = _p.ShellTop;
		m.ForeColor = _p.Text;
		m.Renderer = new DarkMenuRenderer();
		m.Padding = new Padding(6, 2, 0, 2);
	}

	public static void ApplyStatus(StatusStrip s)
	{
		s.BackColor = _p.ShellTop;
		s.ForeColor = _p.TextDim;
		s.SizingGrip = false;
	}

	public static void ApplyToolStrip(ToolStrip s)
	{
		if (s == null)
		{
			return;
		}
		s.BackColor = _p.ShellTop;
		s.ForeColor = _p.Text;
		s.Renderer = new DarkMenuRenderer();
		foreach (ToolStripItem item in s.Items)
		{
			if (item is ToolStripTextBox t)
			{
				StyleToolStripBox(t);
			}
			else if (item is ToolStripLabel)
			{
				item.ForeColor = _p.TextDim;
			}
			else if (item is ToolStripButton)
			{
				item.ForeColor = _p.Text;
			}
			else
			{
				item.ForeColor = _p.Text;
			}
		}
	}

	private static void StyleToolStripBox(ToolStripTextBox t)
	{
		t.BackColor = _p.Field;
		t.ForeColor = _p.Text;
		t.BorderStyle = BorderStyle.FixedSingle;
	}

	public static void StyleLabel(Label l, bool dim)
	{
		l.BackColor = Color.Transparent;
		l.ForeColor = (dim ? _p.TextDim : _p.Text);
		l.Font = FBody;
	}

	private static void ApplyWhenReady(Control c, MethodInvoker apply)
	{
		if (c == null || c.IsDisposed)
		{
			return;
		}
		if (c.IsHandleCreated)
		{
			try
			{
				apply();
				return;
			}
			catch
			{
				return;
			}
		}
		EventHandler h = null;
		h = delegate
		{
			c.HandleCreated -= h;
			try
			{
				apply();
			}
			catch
			{
			}
		};
		c.HandleCreated += h;
	}

	public static void StyleTextBox(TextBox t)
	{
		if (t.Multiline)
		{
			t.BackColor = (_p.Light ? Color.FromArgb(250, 252, 255) : Color.FromArgb(8, 13, 22));
			t.ForeColor = _p.Text;
		}
		else
		{
			t.BackColor = _p.Field;
			t.ForeColor = _p.Text;
		}
		t.BorderStyle = BorderStyle.FixedSingle;
		t.Font = FBody;
	}

	public static void StyleCombo(ComboBox c)
	{
		ApplyWhenReady(c, delegate
		{
			c.BackColor = _p.Field;
			c.ForeColor = _p.Text;
			c.FlatStyle = FlatStyle.Flat;
			c.Font = FBody;
		});
	}

	public static void StyleCheck(CheckBox c)
	{
		c.BackColor = Color.Transparent;
		c.ForeColor = _p.Text;
		c.Font = FBody;
		c.FlatStyle = FlatStyle.Flat;
	}

	public static void StyleToggle(Control c)
	{
		if (c != null)
		{
			c.Cursor = Cursors.Hand;
			NeoToggle neoToggle = c as NeoToggle;
			NeoToggleCheck neoToggleCheck = c as NeoToggleCheck;
			if (neoToggle != null)
			{
				neoToggle.EnableCustomPaint();
			}
			else
			{
				neoToggleCheck?.EnableCustomPaint();
			}
			c.BackColor = Color.Transparent;
			c.ForeColor = _p.Text;
			c.Paint -= PaintToggle;
			c.Paint += PaintToggle;
			c.Invalidate();
		}
	}

	public static int ToggleWidth(Control c)
	{
		if (c == null)
		{
			return 120;
		}
		return 26 + TextRenderer.MeasureText(c.Text ?? "", c.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width + 8;
	}

	private static void PaintToggle(object sender, PaintEventArgs e)
	{
		if (!(sender is Control control))
		{
			return;
		}
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		bool flag = control is RadioButton;
		bool flag2 = false;
		if (flag)
		{
			flag2 = ((RadioButton)control).Checked;
		}
		else if (control is CheckBox)
		{
			flag2 = ((CheckBox)control).Checked;
		}
		bool flag3 = control.ClientRectangle.Contains(control.PointToClient(Cursor.Position));
		int num = (control.ClientSize.Height - 14) / 2;
		if (num < 1)
		{
			num = 1;
		}
		Rectangle rectangle = new Rectangle(2, num, 14, 14);
		using (SolidBrush brush = new SolidBrush(_p.BgNear))
		{
			graphics.FillRectangle(brush, control.ClientRectangle);
		}
		using (GraphicsPath path = (flag ? CirclePath(rectangle) : RoundRect(rectangle, 4)))
		{
			using (SolidBrush brush2 = new SolidBrush(_p.Field))
			{
				graphics.FillPath(brush2, path);
			}
			using Pen pen = new Pen(flag2 ? _p.StrokeSel : (flag3 ? _p.StrokeHot : _p.FieldEdge), (flag2 || flag3) ? 1.6f : 1f);
			graphics.DrawPath(pen, path);
		}
		if (flag2)
		{
			if (flag)
			{
				using SolidBrush brush3 = new SolidBrush(_p.Sel);
				graphics.FillEllipse(brush3, Rectangle.Inflate(rectangle, -4, -4));
			}
			else
			{
				using Pen pen2 = new Pen(_p.Sel, 2.2f);
				pen2.StartCap = LineCap.Round;
				pen2.EndCap = LineCap.Round;
				graphics.DrawLines(pen2, new Point[3]
				{
					new Point(rectangle.Left + 3, rectangle.Top + 7),
					new Point(rectangle.Left + 6, rectangle.Top + 10),
					new Point(rectangle.Left + 11, rectangle.Top + 3)
				});
			}
		}
		int num2 = rectangle.Right + 10;
		Rectangle bounds = new Rectangle(num2, 0, Math.Max(4, control.ClientSize.Width - num2 - 4), control.ClientSize.Height);
		TextRenderer.DrawText(graphics, control.Text, control.Font, bounds, control.Enabled ? _p.Text : _p.TextDisabled, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
	}

	public static void StyleListView(ListView v)
	{
		v.BackColor = _p.Field;
		v.ForeColor = _p.Text;
		v.BorderStyle = BorderStyle.FixedSingle;
		v.Font = FBody;
		v.OwnerDraw = true;
		v.DrawColumnHeader -= LvHeader;
		v.DrawItem -= LvItem;
		v.DrawColumnHeader += LvHeader;
		v.DrawItem += LvItem;
	}

	private static void LvHeader(object sender, DrawListViewColumnHeaderEventArgs e)
	{
		using (SolidBrush brush = new SolidBrush(_p.CardTop))
		{
			e.Graphics.FillRectangle(brush, e.Bounds);
		}
		using (Pen pen = new Pen(_p.Stroke))
		{
			e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 3, e.Bounds.Right - 1, e.Bounds.Bottom - 3);
			e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
		}
		TextRenderer.DrawText(e.Graphics, e.Header.Text, FHead, e.Bounds, _p.Accent, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
	}

	private static void LvItem(object sender, DrawListViewItemEventArgs e)
	{
		ListView listView = (ListView)sender;
		if (e.Item == null)
		{
			return;
		}
		bool flag = (e.State & ListViewItemStates.Selected) != 0;
		e.DrawDefault = false;
		using (SolidBrush brush = new SolidBrush(flag ? _p.CardSelBottom : ((e.ItemIndex % 2 == 0) ? _p.Field : _p.CardBottom)))
		{
			e.Graphics.FillRectangle(brush, e.Bounds);
		}
		if (flag)
		{
			using Pen pen = new Pen(_p.Accent);
			e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Top, e.Bounds.Left, e.Bounds.Bottom);
		}
		Color foreColor = (flag ? _p.TextOnHot : _p.Text);
		int num = e.Bounds.Left + 4;
		for (int i = 0; i < e.Item.SubItems.Count && i < listView.Columns.Count; i++)
		{
			int width = listView.Columns[i].Width;
			Rectangle bounds = new Rectangle(num, e.Bounds.Top, width - 6, e.Bounds.Height);
			TextRenderer.DrawText(e.Graphics, e.Item.SubItems[i].Text, FBody, bounds, foreColor, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter);
			num += width;
		}
	}

	public static void ApplyTheme(Control root)
	{
		if (root == null)
		{
			return;
		}
		ApplyOne(root);
		foreach (Control control in root.Controls)
		{
			ApplyTheme(control);
		}
		if (root is HudTopBar hudTopBar)
		{
			hudTopBar.RefreshTheme();
		}
		if (root is Form)
		{
			Form obj = (Form)root;
			obj.BackColor = _p.BgNear;
			obj.ForeColor = _p.Text;
		}
		root.Invalidate(invalidateChildren: true);
	}

	private static void ApplyOne(Control c)
	{
		if (c is NeoGroupLabel)
		{
			c.BackColor = _p.BgNear;
			c.Invalidate();
		}
		else if (!(c is NeoButton) && !(c is HudPanel) && !(c is HudTopBar) && !(c is HudPageHeader) && !(c is RigDisplay) && !(c is CompassDial) && !(c is ElevationGauge) && !(c is StatusLamp) && !(c is BarMeter))
		{
			if (c is TextBox)
			{
				StyleTextBox((TextBox)c);
			}
			else if (c is ComboBox)
			{
				StyleCombo((ComboBox)c);
			}
			else if (c is ListView)
			{
				StyleListView((ListView)c);
			}
			else if (c is Label)
			{
				Label label = (Label)c;
				label.ForeColor = ((label.ForeColor == Dark.Accent || label.ForeColor == Light.Accent) ? _p.Accent : (IsDim(label.ForeColor) ? _p.TextDim : _p.Text));
			}
			else if (c is CheckBox || c is RadioButton)
			{
				c.ForeColor = _p.Text;
			}
			else if (c is HudPanel)
			{
				c.BackColor = _p.BgNear;
			}
			else if (c is TableLayoutPanel || c is FlowLayoutPanel || c is Panel)
			{
				c.BackColor = Color.Transparent;
			}
			else
			{
				c.ForeColor = _p.Text;
			}
		}
	}

	private static bool IsDim(Color c)
	{
		if (!(c == Dark.TextDim) && !(c == Light.TextDim))
		{
			return c == Color.FromArgb(124, 147, 176);
		}
		return true;
	}

	public static Button MakeButton(string text, EventHandler onClick)
	{
		NeoButton neoButton = new NeoButton();
		neoButton.Text = text;
		neoButton.AutoSizeToText(108, 250);
		if (onClick != null)
		{
			neoButton.Click += onClick;
		}
		return neoButton;
	}

	public static GraphicsPath RoundRect(Rectangle r, int radius)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		int num = radius * 2;
		if (num <= 0)
		{
			graphicsPath.AddRectangle(r);
			return graphicsPath;
		}
		graphicsPath.AddArc(r.X, r.Y, num, num, 180f, 90f);
		graphicsPath.AddArc(r.Right - num - 1, r.Y, num, num, 270f, 90f);
		graphicsPath.AddArc(r.Right - num - 1, r.Bottom - num - 1, num, num, 0f, 90f);
		graphicsPath.AddArc(r.X, r.Bottom - num - 1, num, num, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}

	public static GraphicsPath CirclePath(Rectangle r)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.AddEllipse(r);
		return graphicsPath;
	}

	public static Label SectionLabel(string text)
	{
		return new NeoGroupLabel
		{
			Text = text
		};
	}

	public static Panel MakeHeader(string title, string subtitle)
	{
		Panel host = new Panel();
		host.Dock = DockStyle.Top;
		host.Height = 66;
		host.Paint += delegate(object s, PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle clientRectangle = host.ClientRectangle;
			using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, clientRectangle.Width), Math.Max(1, clientRectangle.Height)), _p.CardTop, _p.BgNear, 0f))
			{
				graphics.FillRectangle(brush, clientRectangle);
			}
			using (Pen pen = new Pen(Color.FromArgb((int)(28f * _p.GlowAlpha), _p.Accent), 1f))
			{
				for (int i = clientRectangle.Width - 260; i < clientRectangle.Width + 240; i += 18)
				{
					graphics.DrawLine(pen, i, clientRectangle.Bottom, i + 60, clientRectangle.Top);
				}
			}
			using (SolidBrush brush2 = new SolidBrush(_p.Accent))
			{
				graphics.FillRectangle(brush2, 0, clientRectangle.Height - 2, clientRectangle.Width, 2);
				graphics.FillRectangle(brush2, 0, 0, 4, clientRectangle.Height);
			}
			TextRenderer.DrawText(graphics, title, FTitle, new Point(18, 9), _p.Text, TextFormatFlags.NoPadding);
			TextRenderer.DrawText(graphics, subtitle, FSmall, new Point(19, 38), _p.TextDim, TextFormatFlags.NoPadding);
		};
		return host;
	}

	public static Panel MakeCard()
	{
		Panel p = new Panel();
		p.BackColor = _p.CardTop;
		p.Paint += delegate(object s, PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (GraphicsPath path = RoundRect(new Rectangle(0, 0, p.Width - 1, p.Height - 1), 8))
			{
				using Pen pen = new Pen(_p.Stroke);
				graphics.DrawPath(pen, path);
			}
			using SolidBrush brush = new SolidBrush(Color.FromArgb((int)(70f * _p.GlowAlpha), _p.Accent));
			graphics.FillRectangle(brush, 1, 10, 3, p.Height - 20);
		};
		return p;
	}
}
