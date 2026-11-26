using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Threading;
using System.Windows.Forms;

namespace Lx311Host
{
    static class Pal
    {
        public static readonly Color BgTop = Color.FromArgb(10, 20, 36);
        public static readonly Color BgBot = Color.FromArgb(28, 47, 77);
        public static readonly Color Head = Color.FromArgb(11, 23, 40);
        public static readonly Color CardTop = Color.FromArgb(23, 37, 61);
        public static readonly Color CardBot = Color.FromArgb(14, 24, 40);
        public static readonly Color CardEdge = Color.FromArgb(43, 61, 92);
        public static readonly Color Ink = Color.FromArgb(232, 238, 247);
        public static readonly Color Mute = Color.FromArgb(143, 160, 184);
        public static readonly Color Green = Color.FromArgb(62, 224, 138);
        public static readonly Color GreenD = Color.FromArgb(18, 163, 92);
        public static readonly Color Blue = Color.FromArgb(90, 169, 255);
        public static readonly Color BlueD = Color.FromArgb(31, 95, 208);
        public static readonly Color Red = Color.FromArgb(255, 123, 110);
        public static readonly Color RedD = Color.FromArgb(198, 40, 40);
        public static readonly Color Amber = Color.FromArgb(255, 198, 92);
        public static readonly Color AmberD = Color.FromArgb(207, 125, 10);
        public static readonly Color Cyan = Color.FromArgb(57, 210, 192);
    }

    static class Gfx
    {
        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = rad * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Shadow(Graphics g, RectangleF r, float rad, int dy, int layers, int spread, int alpha)
        {
            for (int i = layers; i >= 1; i--)
            {
                float k = (float)i / layers;
                RectangleF rr = new RectangleF(r.X - spread * k * 0.5f,
                                              r.Y + dy - spread * k * 0.45f,
                                              r.Width + spread * k, r.Height + spread * k);
                using (GraphicsPath p = Round(rr, rad + k * 4))
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(alpha * (0.16 + 0.10 * (1 - k))), 0, 0, 0)))
                    g.FillPath(b, p);
            }
        }

        public static void VGrad(Graphics g, RectangleF r, Color top, Color bottom, float rad)
        {
            using (GraphicsPath p = Round(r, rad))
            {
                if (r.Width <= 0 || r.Height <= 0) return;
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2), top, bottom, 90f))
                    g.FillPath(b, p);
            }
        }

        public static void Card(Graphics g, RectangleF r)
        {
            Shadow(g, r, 16, -4, 4, 8, 46);
            VGrad(g, r, Pal.CardTop, Pal.CardBot, 16);
            using (GraphicsPath p = Round(r, 16))
            using (Pen pen = new Pen(Pal.CardEdge, 1f))
                g.DrawPath(pen, p);
        }

        public static void Fmt(Graphics g, string s, Font f, Color c, float x, float y)
        {
            using (SolidBrush b = new SolidBrush(c))
                g.DrawString(s, f, b, x, y);
        }

        public static void Fmt(Graphics g, string s, Font f, Color c, float x, float y, StringAlignment ha, StringAlignment va, RectangleF box)
        {
            using (StringFormat sf = new StringFormat())
            using (SolidBrush b = new SolidBrush(c))
            {
                sf.Alignment = ha;
                sf.LineAlignment = va;
                g.DrawString(s, f, b, box, sf);
            }
        }

        public static void Button(Graphics g, RectangleF r, string label, Color cTop, Color cBot,
                                  bool hover, bool pressed, bool glow, bool enabled, Font f)
        {
            float rad = 14;
            if (!enabled)
            {
                cTop = Color.FromArgb(58, 70, 90);
                cBot = Color.FromArgb(40, 50, 66);
            }
            if (glow && enabled)
            {
                int[] ks = new int[] { 16, 10, 5 };
                int[] al = new int[] { 22, 32, 46 };
                for (int i = 0; i < 3; i++)
                {
                    RectangleF gr = RectangleF.Inflate(r, ks[i] / 2f, ks[i] / 2f);
                    using (GraphicsPath p = Round(gr, rad + ks[i] / 2f))
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(al[i], cTop)))
                        g.FillPath(b, p);
                }
            }
            Shadow(g, r, rad, pressed ? -2 : -5, 5, 7, enabled ? 90 : 40);

            if (pressed)
                r = new RectangleF(r.X, r.Y + 1.5f, r.Width, r.Height - 1.5f);
            Color t = pressed ? Dark(cTop, 0.16f) : cTop;
            Color b2 = pressed ? Dark(cBot, 0.16f) : cBot;
            if (hover && enabled && !pressed) { t = Light(cTop, 0.08f); b2 = Light(cBot, 0.08f); }

            VGrad(g, r, t, b2, rad);

            if (!pressed && enabled)
            {
                RectangleF hi = new RectangleF(r.X + 7, r.Y + 7, r.Width - 14, 10);
                using (GraphicsPath p = Round(hi, 5))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(52, 255, 255, 255)))
                    g.FillPath(b, p);
            }
            RectangleF lo = new RectangleF(r.X + 6, r.Bottom - 14, r.Width - 12, 9);
            using (GraphicsPath p = Round(lo, 4))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(pressed ? 62 : 46, 0, 0, 0)))
                g.FillPath(b, p);

            using (GraphicsPath p = Round(r, rad))
            using (Pen pen = new Pen(Color.FromArgb(52, 255, 255, 255), 1f))
                g.DrawPath(pen, p);

            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                g.DrawString(label, f, sh, new RectangleF(r.X + 1, r.Y + 1.5f, r.Width, r.Height), sf);
            using (SolidBrush tb = new SolidBrush(enabled ? Color.White : Color.FromArgb(150, 160, 175)))
                g.DrawString(label, f, tb, r, sf);
            sf.Dispose();
        }

        public static void Lamp(Graphics g, float cx, float cy, float r, Color c, bool on)
        {
            if (on)
            {
                float[] kk = new float[] { 3.4f, 2.3f, 1.5f };
                int[] aa = new int[] { 26, 40, 66 };
                for (int i = 0; i < 3; i++)
                {
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(aa[i], c)))
                        g.FillEllipse(b, cx - r * kk[i], cy - r * kk[i], r * 2 * kk[i], r * 2 * kk[i]);
                }
                RectangleF rr = new RectangleF(cx - r, cy - r, 2 * r, 2 * r);
                using (GraphicsPath p = new GraphicsPath())
                {
                    p.AddEllipse(rr);
                    using (PathGradientBrush pb = new PathGradientBrush(p))
                    {
                        pb.CenterColor = Light(c, 0.55f);
                        pb.SurroundColors = new Color[] { c };
                        g.FillPath(pb, p);
                    }
                }
            }
            else
            {
                RectangleF rr = new RectangleF(cx - r, cy - r, 2 * r, 2 * r);
                using (LinearGradientBrush b = new LinearGradientBrush(rr,
                    Color.FromArgb(52, 68, 95), Color.FromArgb(28, 41, 65), 90f))
                    g.FillEllipse(b, rr);
            }
        }

        public static Color Dark(Color c, float k)
        {
            return Color.FromArgb(c.A,
                (int)(c.R * (1 - k)), (int)(c.G * (1 - k)), (int)(c.B * (1 - k)));
        }

        public static Color Light(Color c, float k)
        {
            return Color.FromArgb(c.A,
                (int)(c.R + (255 - c.R) * k),
                (int)(c.G + (255 - c.G) * k),
                (int)(c.B + (255 - c.B) * k));
        }
    }

    class MainForm : Form
    {
        const int W = 900, H = 520;

        IPlc _link;
        bool _sim = true;
        bool _wantConnect;
        string _ip = "192.168.0.250";
        int _port = 502;
        byte _unit = 1;

        volatile bool _connected;
        int _rtt;
        int _errs;
        string _err = "";
        DateTime _lastOk = DateTime.MinValue;

        readonly object _lock = new object();
        bool[] _coils = new bool[32];
        ushort[] _regs = new ushort[16];

        RectangleF[] _btn = new RectangleF[4];
        RectangleF _connRect;
        int _hover = -1, _pressed = -1;
        bool _connHover;

        TextBox _ipBox, _portBox, _unitBox;
        System.Windows.Forms.Timer _timer;
        Thread _poll;
        volatile bool _running = true;

        Font f8, f9, f10, f11, f13b, f15b, f17b, f20b, f30b;

        public MainForm()
        {
            Text = "LX311 电机正反转上位机";
            ClientSize = new Size(W, H);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Pal.BgTop;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            string fam = "Microsoft YaHei";
            try
            {
                Font test = new Font(fam, 9f);
                test.Dispose();
            }
            catch { fam = "SimSun"; }

            f8 = new Font(fam, 8.5f);
            f9 = new Font(fam, 9f);
            f10 = new Font(fam, 9.5f);
            f11 = new Font(fam, 10.5f);
            f13b = new Font(fam, 12.5f, FontStyle.Bold);
            f15b = new Font(fam, 14.5f, FontStyle.Bold);
            f17b = new Font(fam, 16.5f, FontStyle.Bold);
            f20b = new Font(fam, 19f, FontStyle.Bold);
            f30b = new Font(fam, 26f, FontStyle.Bold);

            _ipBox = MakeBox(_ip, 410, 24, 108);
            _portBox = MakeBox("502", 532, 24, 50);
            _unitBox = MakeBox("1", 618, 24, 36);

            _ipBox.TextChanged += delegate { _ip = _ipBox.Text.Trim(); };
            _portBox.TextChanged += delegate
            {
                int p; if (int.TryParse(_portBox.Text.Trim(), out p) && p > 0 && p < 65536) _port = p;
            };
            _unitBox.TextChanged += delegate
            {
                byte u; if (byte.TryParse(_unitBox.Text.Trim(), out u)) _unit = u;
            };

            _sim = true;
            _link = new SimPlc();

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 100;
            _timer.Tick += delegate { Invalidate(); };
            _timer.Start();

            _poll = new Thread(PollLoop);
            _poll.IsBackground = true;
            _poll.Start();
        }

        TextBox MakeBox(string text, int x, int y, int w)
        {
            TextBox t = new TextBox();
            t.Text = text;
            t.Bounds = new Rectangle(x, y, w, 22);
            t.BackColor = Color.FromArgb(16, 28, 46);
            t.ForeColor = Pal.Ink;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.Font = f9;
            Controls.Add(t);
            return t;
        }

        DateTime _lastTry = DateTime.MinValue;

        void PollLoop()
        {
            while (_running)
            {
                IPlc l = _link;
                bool ok = false;
                try
                {
                    if (l is SimPlc) ((SimPlc)l).Tick();

                    if (l != null && l.Connected)
                    {
                        DateTime t0 = DateTime.Now;
                        bool[] c = l.ReadCoils(0x0000, 0x0020);
                        ushort[] r = l.ReadHolding(0x0000, 0x0008);
                        if (c != null && r != null)
                        {
                            lock (_lock)
                            {
                                _coils = c;
                                _regs = r;
                            }
                            _rtt = (int)(DateTime.Now - t0).TotalMilliseconds;
                            _lastOk = DateTime.Now;
                            _connected = true;
                            _err = "";
                            ok = true;
                        }
                    }

                    if (!ok)
                    {
                        _connected = false;
                        if (_wantConnect && (DateTime.Now - _lastTry).TotalSeconds > 2)
                        {
                            _lastTry = DateTime.Now;
                            if (!(l is SimPlc))
                            {
                                if (l != null) l.Close();
                                IPlc nl = new ModbusTcp();
                                if (nl.Connect(_ip, _port, _unit)) { _link = nl; _err = "已重连"; }
                                else { _err = "连接失败：检查 IP / 网线 / PLC 是否 RUN"; _errs++; }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _connected = false;
                    _err = ex.Message;
                    _errs++;
                }
                Thread.Sleep(150);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(
                new Rectangle(0, 0, W, H), Pal.BgTop, Pal.BgBot, 90f))
                e.Graphics.FillRectangle(b, 0, 0, W, H);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool conn = _connected;
            bool fwd = Get(0x10), rev = Get(0x11), flt = Get(0x12);
            int step = fwd ? 1 : (rev ? 2 : 0);

            DrawHeader(g, conn);
            DrawCard1(g, step, fwd, rev, flt, Get(0x15));
            DrawCard2(g, flt);
            DrawCard3(g, conn);
            DrawBottom(g, conn, flt);
        }

        bool Get(int i)
        {
            lock (_lock) return i < _coils.Length && _coils[i];
        }

        int Reg(int i)
        {
            lock (_lock) return i < _regs.Length ? _regs[i] : 0;
        }

        void DrawHeader(Graphics g, bool conn)
        {
            g.FillRectangle(new SolidBrush(Pal.Head), 0, 0, W, 72);
            using (Pen p = new Pen(Color.FromArgb(43, 74, 114), 1f))
                g.DrawLine(p, 0, 72, W, 72);

            Gfx.Fmt(g, "LX311  电机正反转", f20b, Color.White, 22, 14);
            Gfx.Fmt(g, "Hollysys LX-CU311   ·   Modbus TCP", f8, Color.FromArgb(127, 157, 192), 24, 46);

            Gfx.Fmt(g, "IP", f8, Pal.Mute, 388, 29);
            Gfx.Fmt(g, ":", f9, Pal.Mute, 522, 27);
            Gfx.Fmt(g, "站号", f8, Pal.Mute, 588, 29);

            // 连接按钮
            _connRect = new RectangleF(664, 20, 86, 30);
            bool hover = _connHover;
            Gfx.Button(g, _connRect, conn ? "断开" : "连接",
                       conn ? Pal.Red : Pal.Blue, conn ? Pal.RedD : Pal.BlueD,
                       hover, false, false, true, f9);

            Gfx.Lamp(g, 774, 35, 5, Pal.Green, conn);
            Gfx.Fmt(g, conn ? "通讯正常" : (_err.Length > 0 ? "通讯异常" : "未连接"), f8,
                    conn ? Color.FromArgb(169, 214, 191) : Color.FromArgb(200, 140, 130), 786, 28);

            string right = _sim ? "离线模拟模式" : (_ip + " : " + _port);
            Gfx.Fmt(g, right, f8, Pal.Mute, W - 20 - TextW(g, right, f8), 52);
        }

        float TextW(Graphics g, string s, Font f)
        {
            return g.MeasureString(s, f).Width;
        }

        void DrawCard1(Graphics g, int step, bool fwd, bool rev, bool flt, bool run)
        {
            RectangleF card = new RectangleF(16, 84, 284, 340);
            Gfx.Card(g, card);
            Gfx.Fmt(g, "电机状态", f10, Pal.Mute, 32, 100);

            float cx = 112, cy = 300, r = 40;
            using (SolidBrush b = new SolidBrush(Color.FromArgb(110, 27, 43, 69)))
                g.FillEllipse(b, cx - r - 8, cy - r - 8, (r + 8) * 2, (r + 8) * 2);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(cx - r, cy - r, r * 2, r * 2);
                using (PathGradientBrush pb = new PathGradientBrush(p))
                {
                    pb.CenterColor = Color.FromArgb(30, 44, 70);
                    pb.SurroundColors = new Color[] { Color.FromArgb(12, 22, 38) };
                    g.FillPath(pb, p);
                }
                using (Pen pen = new Pen(Color.FromArgb(60, 90, 134), 2f))
                    g.DrawPath(pen, p);
            }
            Gfx.Fmt(g, "M", f30b, Color.FromArgb(207, 224, 245), cx - 14, cy - 26);
            Gfx.Fmt(g, "3~", f10, Color.FromArgb(127, 157, 192), cx - 10, cy + 12);

            // 转向箭头
            Color cR = step == 1 ? Pal.Green : Color.FromArgb(61, 79, 108);
            Color cL = step == 2 ? Pal.Blue : Color.FromArgb(61, 79, 108);
            Arrow(g, cx + 50, cy, 20, cR, true);
            Arrow(g, cx - 50, cy, 20, cL, false);

            // 状态灯
            Gfx.Lamp(g, 206, 150, 7, Pal.Green, fwd);
            Gfx.Fmt(g, "正转运行", f10, fwd ? Pal.Ink : Color.FromArgb(101, 120, 154), 220, 142);
            Gfx.Lamp(g, 206, 190, 7, Pal.Blue, rev);
            Gfx.Fmt(g, "反转运行", f10, rev ? Pal.Ink : Color.FromArgb(101, 120, 154), 220, 182);
            Gfx.Lamp(g, 206, 230, 7, Pal.Red, flt);
            Gfx.Fmt(g, "故障", f10, flt ? Pal.Ink : Color.FromArgb(101, 120, 154), 220, 222);
            Gfx.Lamp(g, 206, 270, 7, Pal.Cyan, run);
            Gfx.Fmt(g, "运行中", f10, run ? Pal.Ink : Color.FromArgb(101, 120, 154), 220, 262);

            Gfx.Fmt(g, "当前方向", f9, Pal.Mute, 34, 362);
            string dir = step == 1 ? "正转" : (step == 2 ? "反转" : "停止");
            Color dc = step == 1 ? Color.FromArgb(111, 240, 170) : (step == 2 ? Pal.Blue : Color.FromArgb(120, 140, 170));
            Gfx.Fmt(g, dir, f20b, dc, 34, 380);
        }

        void Arrow(Graphics g, float x, float y, float len, Color c, bool right)
        {
            using (Pen p = new Pen(c, 3f))
            using (SolidBrush b = new SolidBrush(c))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                float x2 = right ? x + len : x - len;
                g.DrawLine(p, x, y, x2, y);
                PointF[] tri = right
                    ? new PointF[] { new PointF(x2 + 8, y), new PointF(x2 - 2, y - 7), new PointF(x2 - 2, y + 7) }
                    : new PointF[] { new PointF(x2 - 8, y), new PointF(x2 + 2, y - 7), new PointF(x2 + 2, y + 7) };
                g.FillPolygon(b, tri);
            }
        }

        void DrawCard2(Graphics g, bool flt)
        {
            RectangleF card = new RectangleF(310, 84, 280, 340);
            Gfx.Card(g, card);
            Gfx.Fmt(g, "运行数据", f10, Pal.Mute, 326, 100);

            int sec = Reg(0);
            string hms = string.Format("{0:00} : {1:00} : {2:00}", sec / 3600, (sec / 60) % 60, sec % 60);
            int mins = Reg(4);
            string hm = string.Format("{0:00} : {1:00}", mins / 60, mins % 60);
            int starts = Reg(1);
            int code = Reg(2);

            Row(g, 326, 136, "本次运行", hms, Pal.Ink);
            Row(g, 326, 204, "累计运行", hm, Pal.Ink);
            Row(g, 326, 272, "累计启动", starts.ToString() + "  次", Pal.Ink);

            Gfx.Fmt(g, "故障代码", f9, Pal.Mute, 326, 336);
            string codeTxt = code == 0 ? "E00   无故障" : (code == 1 ? "E01   热继动作" : "E02   急停按下");
            Gfx.Fmt(g, codeTxt, f15b, code == 0 ? Color.FromArgb(111, 240, 170) : Pal.Red, 326, 352);
        }

        void Row(Graphics g, float x, float y, string label, string value, Color c)
        {
            Gfx.Fmt(g, label, f9, Pal.Mute, x, y);
            Gfx.Fmt(g, value, f17b, c, x, y + 18);
        }

        void DrawCard3(Graphics g, bool conn)
        {
            RectangleF card = new RectangleF(600, 84, 284, 340);
            Gfx.Card(g, card);
            Gfx.Fmt(g, "操作按钮", f10, Pal.Mute, 616, 100);

            _btn[0] = new RectangleF(616, 132, 122, 118);
            _btn[1] = new RectangleF(748, 132, 122, 118);
            _btn[2] = new RectangleF(616, 268, 122, 118);
            _btn[3] = new RectangleF(748, 268, 122, 118);

            bool fwd = Get(0x10);
            Gfx.Button(g, _btn[0], "正转启动", Pal.Green, Pal.GreenD,
                       _hover == 0, _pressed == 0, fwd, conn, f13b);
            Gfx.Button(g, _btn[1], "反转启动", Pal.Blue, Pal.BlueD,
                       _hover == 1, _pressed == 1, Get(0x11), conn, f13b);
            Gfx.Button(g, _btn[2], "停止", Pal.Red, Pal.RedD,
                       _hover == 2, _pressed == 2, false, conn, f15b);
            Gfx.Button(g, _btn[3], "故障复位", Pal.Amber, Pal.AmberD,
                       _hover == 3, _pressed == 3, false, conn, f13b);

            if (!conn)
                Gfx.Fmt(g, "未连接 PLC，按钮已锁定", f8, Color.FromArgb(150, 120, 110), 640, 400);
        }

        void DrawBottom(Graphics g, bool conn, bool flt)
        {
            RectangleF strip = new RectangleF(16, 436, 868, 44);
            Gfx.Shadow(g, strip, 10, -3, 3, 5, 60);
            Gfx.VGrad(g, strip, flt ? Color.FromArgb(60, 26, 30) : Color.FromArgb(21, 34, 56),
                      flt ? Color.FromArgb(46, 18, 22) : Color.FromArgb(16, 26, 42), 10);
            using (GraphicsPath p = Gfx.Round(strip, 10))
            using (Pen pen = new Pen(flt ? Color.FromArgb(160, 70, 60) : Pal.CardEdge, 1f))
                g.DrawPath(pen, p);

            Color ic = flt ? Pal.Red : Pal.Green;
            g.FillEllipse(new SolidBrush(Color.FromArgb(60, ic)), 30, 449, 18, 18);
            if (flt)
                Gfx.Fmt(g, "!", f11, ic, 37, 448);
            else
                Gfx.Fmt(g, "OK", f8, ic, 32, 452);

            string msg;
            if (flt)
            {
                int code = Reg(2);
                msg = code == 1 ? "热继电器动作 — 检查电机负载后按【故障复位】"
                                : (code == 2 ? "急停已按下 — 排除后按【故障复位】" : "故障锁存 — 检查后按【故障复位】");
            }
            else if (!conn)
                msg = "PLC 未连接 — " + (_err.Length > 0 ? _err : "点击右上角【连接】");
            else
                msg = "系统正常 · 无未确认报警";
            Gfx.Fmt(g, msg, f11, flt ? Pal.Red : Color.FromArgb(207, 224, 245), 52, 447);

            string st = _sim ? "离线模拟 · 按钮可测试" : (conn
                ? string.Format("通讯正常 · {0} ms · 错误 {1} 次", _rtt, _errs)
                : string.Format("通讯中断 · 错误 {0} 次", _errs));
            Gfx.Fmt(g, st, f8, Pal.Mute, 20, 492);
            string ver = "v1.0 · .NET 4.0 · Win7 / 8 / 10 / 11";
            Gfx.Fmt(g, ver, f8, Color.FromArgb(93, 113, 145), W - 20 - TextW(g, ver, f8), 492);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ActiveControl = null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = -1;
            for (int i = 0; i < 4; i++)
                if (_btn[i].Contains(e.Location)) h = i;
            bool ch = _connRect.Contains(e.Location);
            if (h != _hover || ch != _connHover)
            {
                _hover = h;
                _connHover = ch;
                Cursor = (h >= 0 || ch) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = -1;
            _connHover = false;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (_connRect.Contains(e.Location))
            {
                ToggleConnect();
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                if (_btn[i].Contains(e.Location))
                {
                    _pressed = i;
                    Write(i, true);
                    Invalidate();
                    break;
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_pressed >= 0)
            {
                Write(_pressed, false);
                _pressed = -1;
                Invalidate();
            }
            base.OnMouseUp(e);
        }

        void ToggleConnect()
        {
            if (_connected && !_sim)
            {
                _wantConnect = false;
                IPlc l = _link;
                _link = new SimPlc();
                _sim = true;
                if (l != null) l.Close();
                _connected = false;
            }
            else
            {
                IPlc l = new ModbusTcp();
                if (l.Connect(_ip, _port, _unit))
                {
                    _link = l;
                    _sim = false;
                    _wantConnect = true;
                    _connected = true;
                }
                else
                {
                    _err = "连接失败：检查 IP / 网线 / PLC 是否 RUN";
                    _errs++;
                    _connected = false;
                    _wantConnect = false;
                }
            }
            Invalidate();
        }

        void Write(int idx, bool on)
        {
            IPlc l = _link;
            if (l == null || !l.Connected) return;
            ushort addr = (ushort)idx;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { l.WriteCoil(addr, on); }
                catch { }
            });
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _running = false;
            _wantConnect = false;
            try { if (_link != null) _link.Close(); } catch { }
            base.OnFormClosing(e);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}




