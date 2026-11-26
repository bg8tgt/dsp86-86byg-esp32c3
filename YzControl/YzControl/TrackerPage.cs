// ============================================================================
//  YzControl · TrackerPage.cs —— 卫星追踪页（v1.7 内置 SGP4，不再启动 OrbitLink）
//
//  布局按「追星」页的密集仪表盘组织，视觉沿用 1.6.0（深色卡片 + 青色标题）：
//    左：全部卫星 / 收藏卫星（仰角·方位·下一场入境·倒计时）
//    中：雷达·实时指向（目标针 vs 真实针）/ 世界地图（星下点·覆盖圈·6h 轨迹）
//    右：连接状态 / 电台联动（多普勒自动设频）/ 地固 QTH / 多普勒 下行·上行
//    底：开始·停止·详情 / 全自动追踪 / 更新数据 / 收藏 / 刷新 / 仰角阈值 + 状态行
//
//  关键指标（本轮新增）：
//    · 指向误差用**球面角距**算，目标 <1°，超差自动强制纠偏（闭环，不靠开环猜）
//    · 多普勒 RX/TX 同一距离率同源下发，写后回读，10 s 周期校验（偏差>200 Hz 整组重下）
//    · 过境表 1 s 刷新 + 每 5 min 后台重算，多普勒与过境同拍更新
// ============================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using YzControl.Sat;

namespace YzControl
{
    /// <summary>手动即停：云台手动命令发出前调用（S3Client 里引用）。</summary>
    internal static class TrackerLauncher
    {
        public static bool IsRunning { get { return TrackerEngine.I.Running; } }

        public static bool StopIfRunning(string reason)
        {
            bool stopped = TrackerEngine.I.RequestManualStop(reason);
            if (stopped) TrackerPage.Note("手动操作（" + reason + "）→ 已停止跟踪");
            return stopped;
        }
    }

    internal sealed class TrackerPage : Panel
    {
        private static readonly Color Bg = Color.FromArgb(12, 16, 22);
        private static readonly Color Card = Color.FromArgb(20, 26, 34);
        private static readonly Color Line = Color.FromArgb(38, 48, 60);
        private static readonly Color Fg = Color.FromArgb(226, 236, 246);
        private static readonly Color Dim = Color.FromArgb(143, 163, 181);
        private static readonly Color Acc = Color.FromArgb(34, 211, 238);
        private static readonly Color Good = Color.FromArgb(134, 239, 172);
        private static readonly Color Warn = Color.FromArgb(252, 165, 165);
        private static readonly Color Hot = Color.FromArgb(248, 113, 113);

        private static TrackerPage _instance;

        private readonly S3Client _s3;
        private readonly Func<CivLink> _civ;
        private readonly Func<RigKind> _kind;

        // ---- 左：两个卫星表
        private ListView _lvAll, _lvFav;
        private readonly List<Row> _rowsAll = new List<Row>(), _rowsFav = new List<Row>();
        private bool _needRebuild = true;

        // ---- 中：雷达 + 地图
        private Radar _radar;
        private MapView _map;
        private Label _lblSatName, _lblRangeRate, _lblAz, _lblEl, _lblRange, _lblAlt, _lblDopHere, _lblVisible;

        // ---- 右：连接 / 电台 / QTH / 多普勒
        private Label _lblConnGimbal, _lblConnRadio, _lblConnAuto;
        private ComboBox _cmbTp, _cmbMode;
        private TextBox _txtRx, _txtTx, _txtLat, _txtLon, _txtAlt, _txtGrid;
        private CheckBox _chkDop, _chkTone, _chkAuto;
        private NumericUpDown _numMinEl;
        private Label _lblDown, _lblUp;

        // ---- 底：状态
        private Label _lblStatA, _lblStatB, _lblStatC;

        private System.Windows.Forms.Timer _timer, _slow;
        private SatEntry _sel;
        private readonly Dictionary<int, PassInfo> _nextPass = new Dictionary<int, PassInfo>();
        private volatile bool _predicting;
        private DateTime _lastPred = DateTime.MinValue;
        private DateTime _lastSlow = DateTime.MinValue;
        private readonly List<string> _logLines = new List<string>();

        public TrackerPage(S3Client s3, Func<CivLink> civ, Func<RigKind> kind)
        {
            _s3 = s3;
            _civ = civ;
            _kind = kind;
            _instance = this;
            BackColor = Bg;
            ForeColor = Fg;
            Dock = DockStyle.Fill;
            BuildUi();

            TrackerEngine.I.Updated += OnEngine;
            TrackerEngine.I.Log += Note;
            RadioInit();

            SatStore.I.Init();
            RebuildLists();
            LoadQthBoxes();

            Note("就绪：左侧选卫星 → 电台联动选转发器 → ▶开始跟踪。数据目录 " + SatStore.I.DataDir);
            if (SatStore.I.Sats.Count == 0) Note("没有星历数据，请点「更新数据」联网拉取（celestrak）");

            _timer = new System.Windows.Forms.Timer { Interval = 1000 };
            _timer.Tick += delegate { TickUi(); };
            _timer.Start();

            _slow = new System.Windows.Forms.Timer { Interval = 30000 };
            _slow.Tick += delegate { SlowTick(); };
            _slow.Start();

            ThreadPool.QueueUserWorkItem(delegate { PredictAll(false); });
        }

        private void RadioInit()
        {
            try
            {
                var link = _civ != null ? _civ() : null;
                TrackerEngine.I.Radio.Attach(link, _kind != null ? _kind() : RigKind.Ic7100);
            }
            catch { }
        }

        public static Panel Build(S3Client s3, Func<CivLink> civ, Func<RigKind> kind)
        {
            return new TrackerPage(s3, civ, kind);
        }

        /// <summary>MainForm 的 /st 主轮询把同一份快照推过来（复用，不额外打 HTTP）。</summary>
        public static void PushStatus(bool online, int trackOk, double az, int tiltDeg, int state, string ssid, string err)
        {
            var e = TrackerEngine.I;
            if (!online) { e.BoardAt = DateTime.MinValue; return; }
            e.BoardAz = az;
            e.BoardEl = tiltDeg;          // /st 的 va 就是俯仰角（仰角为正），与 /goto? v 同一约定
            e.BoardAt = DateTime.UtcNow;
            e.BoardTrackOk = trackOk;
        }

        public static void Note(string msg)
        {
            var p = _instance;
            if (p == null) return;
            try
            {
                if (p.InvokeRequired) { p.BeginInvoke((Action)(() => Note(msg))); return; }
                bool stamped = msg.Length > 8 && msg[2] == ':' && msg[5] == ':';
                string line = stamped ? msg : DateTime.Now.ToString("HH:mm:ss") + "  " + msg;
                p._logLines.Add(line);
                if (p._logLines.Count > 400) p._logLines.RemoveRange(0, p._logLines.Count - 400);
                if (p._lblStatC != null) p._lblStatC.Text = line;
            }
            catch { }
        }

        // ================================================================== UI
        private void BuildUi()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Bg,
                Padding = new Padding(8),
                ColumnCount = 3,
                RowCount = 3
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));   // 左：卫星表
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43f));   // 中：雷达 + 地图
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27f));   // 右：连接/电台/QTH/多普勒
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 300f));       // 主体上部
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));        // 地图 / 多普勒
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96f));        // 工具条 + 状态
            Controls.Add(root);

            // ---------------- 左列：全部卫星 / 收藏卫星
            var colL = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, ColumnCount = 1, RowCount = 2 };
            colL.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));
            colL.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));
            _lvAll = MkSatList(delegate { OnSatPicked(_lvAll, _rowsAll); });
            _lvFav = MkSatList(delegate { OnSatPicked(_lvFav, _rowsFav); });
            var cAll = MakeCard("全部卫星");
            cAll.Controls.Add(_lvAll);
            var cFav = MakeCard("收藏卫星");
            cFav.Controls.Add(_lvFav);
            colL.Controls.Add(cAll, 0, 0);
            colL.Controls.Add(cFav, 0, 1);
            root.Controls.Add(colL, 0, 0);
            root.SetRowSpan(colL, 2);

            // ---------------- 中列：雷达（上）/ 地图（下）
            var colM = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, ColumnCount = 1, RowCount = 2 };
            colM.RowStyles.Add(new RowStyle(SizeType.Absolute, 300f));
            colM.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            var cRadar = MakeCard("雷达 · 实时指向");
            var tr = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, ColumnCount = 2, RowCount = 1 };
            tr.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250f));
            tr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _radar = new Radar { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0) };
            tr.Controls.Add(_radar, 0, 0);

            var tv = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, ColumnCount = 2, RowCount = 8 };
            tv.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76f));
            tv.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int i = 0; i < 8; i++) tv.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            _lblSatName = MkValueLabel("未选择卫星", Acc);
            _lblRangeRate = MkValueLabel("距离变化率 —", Fg);
            _lblAz = MkValueLabel("方位 AZ —", Fg);
            _lblEl = MkValueLabel("俯仰 EL —", Fg);
            _lblRange = MkValueLabel("距离 —", Fg);
            _lblAlt = MkValueLabel("高度 —", Fg);
            _lblDopHere = MkValueLabel("多普勒 —", Fg);
            _lblVisible = MkValueLabel("可见性 —", Dim);
            tv.Controls.Add(MkLabel("卫星", Dim), 0, 0); tv.Controls.Add(_lblSatName, 1, 0);
            tv.Controls.Add(_lblRangeRate, 0, 1); tv.SetColumnSpan(_lblRangeRate, 2);
            tv.Controls.Add(MkLabel("方位", Dim), 0, 2); tv.Controls.Add(_lblAz, 1, 2);
            tv.Controls.Add(MkLabel("俯仰", Dim), 0, 3); tv.Controls.Add(_lblEl, 1, 3);
            tv.Controls.Add(MkLabel("距离", Dim), 0, 4); tv.Controls.Add(_lblRange, 1, 4);
            tv.Controls.Add(MkLabel("高度", Dim), 0, 5); tv.Controls.Add(_lblAlt, 1, 5);
            tv.Controls.Add(MkLabel("多普勒", Dim), 0, 6); tv.Controls.Add(_lblDopHere, 1, 6);
            tv.Controls.Add(_lblVisible, 0, 7); tv.SetColumnSpan(_lblVisible, 2);
            tr.Controls.Add(tv, 1, 0);
            cRadar.Controls.Add(tr);
            colM.Controls.Add(cRadar, 0, 0);

            var cMap = MakeCard("世界地图 · 星下点 / 覆盖圈 / 6 小时轨迹");
            _map = new MapView { Dock = DockStyle.Fill };
            cMap.Controls.Add(_map);
            colM.Controls.Add(cMap, 0, 1);
            root.Controls.Add(colM, 1, 0);
            root.SetRowSpan(colM, 2);

            // ---------------- 右列：连接 / 电台 / QTH / 多普勒
            var colR = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, ColumnCount = 1, RowCount = 2 };
            colR.RowStyles.Add(new RowStyle(SizeType.Absolute, 400f));
            colR.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var boxR = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Bg, ColumnCount = 1, RowCount = 3 };
            boxR.RowStyles.Add(new RowStyle(SizeType.Absolute, 136f));
            boxR.RowStyles.Add(new RowStyle(SizeType.Absolute, 132f));
            boxR.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // 连接状态
            var cConn = MakeCard("连接状态（只读）");
            var tc = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, ColumnCount = 2, RowCount = 4 };
            tc.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56f));
            tc.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tc.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            tc.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            tc.RowStyles.Add(new RowStyle(SizeType.Absolute, 20f));
            tc.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            _lblConnGimbal = MkValueLabel("云台 —", Fg);
            _lblConnRadio = MkValueLabel("电台 —", Fg);
            _lblConnAuto = MkValueLabel("自动设频：开", Dim);
            var fConn = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, WrapContents = false };
            fConn.Controls.Add(MkBtn("取得控制权", delegate { TakePriority(); }));
            fConn.Controls.Add(MkBtn("硬件重启", delegate { RebootS3(); }));
            fConn.Controls.Add(MkBtn("刷新", delegate { RebuildLists(); SlowTick(); }));
            tc.Controls.Add(MkLabel("云台", Dim), 0, 0); tc.Controls.Add(_lblConnGimbal, 1, 0);
            tc.Controls.Add(MkLabel("电台", Dim), 0, 1); tc.Controls.Add(_lblConnRadio, 1, 1);
            tc.Controls.Add(_lblConnAuto, 0, 2); tc.SetColumnSpan(_lblConnAuto, 2);
            tc.Controls.Add(fConn, 0, 3); tc.SetColumnSpan(fConn, 2);
            cConn.Controls.Add(tc);
            boxR.Controls.Add(cConn, 0, 0);

            // 电台联动
            var cRadio = MakeCard("电台联动");
            var trd = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, ColumnCount = 4, RowCount = 3 };
            trd.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54f));
            trd.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            trd.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62f));
            trd.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96f));
            trd.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
            trd.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            trd.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            _chkDop = new CheckBox
            {
                Text = "多普勒自动设频",
                Checked = true,
                AutoSize = true,
                ForeColor = Fg,
                BackColor = Card,
                Margin = new Padding(0, 4, 8, 0)
            };
            _chkDop.CheckedChanged += delegate
            {
                TrackerEngine.I.DopplerOn = _chkDop.Checked;
                TrackerEngine.I.Radio.AutoTune = _chkDop.Checked;
                _lblConnAuto.Text = "自动设频：" + (_chkDop.Checked ? "开" : "关");
                Note("自动设频：" + (_chkDop.Checked ? "开" : "关"));
            };
            _chkTone = new CheckBox
            {
                Text = "同步亚音",
                Checked = true,
                AutoSize = true,
                ForeColor = Fg,
                BackColor = Card,
                Margin = new Padding(0, 4, 4, 0)
            };
            _chkTone.CheckedChanged += delegate { TrackerEngine.I.Radio.SyncTones = _chkTone.Checked; };
            var fChk = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, WrapContents = false };
            fChk.Controls.Add(_chkDop);
            fChk.Controls.Add(_chkTone);
            trd.Controls.Add(fChk, 0, 0); trd.SetColumnSpan(fChk, 4);

            _cmbTp = MkCombo();
            _cmbTp.SelectedIndexChanged += delegate { OnTpChanged(); };
            trd.Controls.Add(MkLabel("转发器", Dim), 0, 1);
            trd.Controls.Add(_cmbTp, 1, 1); trd.SetColumnSpan(_cmbTp, 3);

            _cmbMode = MkCombo();
            foreach (var m in new[] { "FM", "USB", "LSB", "CW", "CW-R", "AM", "DV" }) _cmbMode.Items.Add(m);
            _cmbMode.SelectedIndex = 0;
            _txtRx = MkText();
            _txtTx = MkText();
            trd.Controls.Add(MkLabel("模式", Dim), 0, 2);
            trd.Controls.Add(_cmbMode, 1, 2);
            trd.Controls.Add(MkLabel("RX Hz", Dim), 2, 2);
            trd.Controls.Add(_txtRx, 3, 2);
            cRadio.Controls.Add(trd);
            boxR.Controls.Add(cRadio, 0, 1);

            // 地固 QTH
            var cQth = MakeCard("地固 QTH");
            var tq = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, ColumnCount = 4, RowCount = 3 };
            tq.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44f));
            tq.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tq.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
            tq.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
            tq.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            tq.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50f));
            tq.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tq.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
            tq.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
            tq.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            _txtLat = MkText(); _txtLon = MkText(); _txtAlt = MkText(); _txtGrid = MkText();
            tq.Controls.Add(MkLabel("纬度", Dim), 0, 0); tq.Controls.Add(_txtLat, 1, 0);
            tq.Controls.Add(MkLabel("经度", Dim), 2, 0); tq.Controls.Add(_txtLon, 3, 0);
            tq.Controls.Add(MkLabel("海拔", Dim), 0, 1); tq.Controls.Add(_txtAlt, 1, 1);
            tq.Controls.Add(MkLabel("网格", Dim), 2, 1); tq.Controls.Add(_txtGrid, 3, 1);
            var fQ = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, WrapContents = false };
            fQ.Controls.Add(MkBtn("按网格换算", delegate { GridToBoxes(); }));
            fQ.Controls.Add(MkBtn("保存 QTH", delegate { SaveQth(); }));
            tq.Controls.Add(fQ, 0, 2); tq.SetColumnSpan(fQ, 4);
            cQth.Controls.Add(tq);
            boxR.Controls.Add(cQth, 0, 2);
            colR.Controls.Add(boxR, 0, 0);

            // 多普勒（下）
            var cDop = MakeCard("多普勒（RX / TX 同一距离率）");
            var td = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, ColumnCount = 2, RowCount = 5 };
            td.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54f));
            td.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            td.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            td.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
            td.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
            td.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            td.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _lblDown = MkValueLabel("下行 —", Acc);
            _lblUp = MkValueLabel("上行 —", Acc);
            td.Controls.Add(MkLabel("下行", Dim), 0, 0); td.Controls.Add(_lblDown, 1, 0);
            td.Controls.Add(MkLabel("上行", Dim), 0, 1); td.Controls.Add(_lblUp, 1, 1);
            var fD = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, WrapContents = false };
            fD.Controls.Add(MkBtn("立即设频", delegate { ApplyNow(); }));
            fD.Controls.Add(MkBtn("更新电台参数", delegate { ReapplyAll(); }));
            td.Controls.Add(fD, 0, 2); td.SetColumnSpan(fD, 2);
            td.Controls.Add(MkLabel("TX Hz", Dim), 0, 3); td.Controls.Add(_txtTx, 1, 3);
            var fD2 = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Card, WrapContents = false };
            fD2.Controls.Add(MkBtn("追踪日志", delegate { ShowLog(); }));
            td.Controls.Add(fD2, 0, 4); td.SetColumnSpan(fD2, 2);
            cDop.Controls.Add(td);
            colR.Controls.Add(cDop, 0, 1);
            root.Controls.Add(colR, 2, 0);
            root.SetRowSpan(colR, 2);

            // ---------------- 底部工具条 + 状态
            var foot = new Panel { Dock = DockStyle.Fill, BackColor = Bg, Margin = new Padding(0, 6, 0, 0) };
            var fTools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, BackColor = Bg, WrapContents = false };
            fTools.Controls.Add(MkBtnAccent("▶ 开始追踪", delegate { StartTrack(); }));
            fTools.Controls.Add(MkBtn("■ 停止", delegate { TrackerEngine.I.Stop("界面停止"); }));
            fTools.Controls.Add(MkBtn("详情 · 过境", delegate { ShowPasses(); }));
            var sp1 = new Label { Width = 12, Text = "", BackColor = Bg };
            fTools.Controls.Add(sp1);
            _chkAuto = new CheckBox
            {
                Text = "全自动追踪",
                AutoSize = true,
                ForeColor = Fg,
                BackColor = Bg,
                Margin = new Padding(2, 8, 10, 0)
            };
            fTools.Controls.Add(_chkAuto);
            fTools.Controls.Add(MkBtn("更新数据", delegate { DoUpdate(); }));
            fTools.Controls.Add(MkBtn("☆ 收藏", delegate { DoFav(); }));
            fTools.Controls.Add(MkBtn("刷新", delegate { TrackerEngine.I.Radio.Reset(); RebuildLists(); PredictAll(true); }));
            fTools.Controls.Add(MkLabel("仰角≥", Dim));
            _numMinEl = new NumericUpDown
            {
                DecimalPlaces = 1,
                Minimum = 0m,
                Maximum = 45m,
                Increment = 0.5m,
                Value = 1.0m,
                Width = 56,
                BackColor = Color.FromArgb(28, 36, 46),
                ForeColor = Fg,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 4, 4, 0)
            };
            fTools.Controls.Add(_numMinEl);
            _lblStatA = new Label { Dock = DockStyle.Top, Height = 18, ForeColor = Dim, BackColor = Bg, Text = "星历：—" };
            _lblStatB = new Label { Dock = DockStyle.Top, Height = 18, ForeColor = Dim, BackColor = Bg, Text = "S3：—" };
            _lblStatC = new Label { Dock = DockStyle.Top, Height = 18, ForeColor = Acc, BackColor = Bg, Text = "" };
            foot.Controls.Add(_lblStatC);
            foot.Controls.Add(_lblStatB);
            foot.Controls.Add(_lblStatA);
            foot.Controls.Add(fTools);
            root.Controls.Add(foot, 0, 2);
            root.SetColumnSpan(foot, 3);
        }

        private ListView MkSatList(EventHandler onSelect)
        {
            var lv = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                BackColor = Color.FromArgb(16, 21, 28),
                ForeColor = Fg,
                BorderStyle = BorderStyle.None,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = new Font("Microsoft YaHei UI", 9f)
            };
            lv.Columns.Add("卫星", 116);
            lv.Columns.Add("类型", 50);
            lv.Columns.Add("仰角", 46);
            lv.Columns.Add("入境", 76);
            lv.Columns.Add("倒计时", 68);
            lv.SelectedIndexChanged += onSelect;
            lv.DoubleClick += delegate { StartTrack(); };
            return lv;
        }

        private static ComboBox MkCombo()
        {
            return new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(28, 36, 46),
                ForeColor = Fg,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 3, 6, 3),
                Font = new Font("Microsoft YaHei UI", 9f)
            };
        }

        private static TextBox MkText()
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(28, 36, 46),
                ForeColor = Fg,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 3, 6, 3),
                Font = new Font("Consolas", 9f)
            };
        }

        private static Panel MakeCard(string title)
        {
            var p = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Card,
                Padding = new Padding(8, 24, 8, 6),
                Margin = new Padding(0, 0, 6, 6)
            };
            p.Controls.Add(new Label
            {
                Text = title,
                Location = new Point(9, 5),
                AutoSize = true,
                ForeColor = Acc,
                BackColor = Card,
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold)
            });
            return p;
        }

        private static Label MkLabel(string t, Color c)
        {
            return new Label
            {
                Text = t,
                ForeColor = c,
                BackColor = Color.Transparent,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(2, 4, 4, 0),
                AutoSize = false,
                Width = 60
            };
        }

        private static Label MkValueLabel(string t, Color c)
        {
            return new Label
            {
                Text = t,
                ForeColor = c,
                BackColor = Color.Transparent,
                AutoSize = true,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(2, 5, 4, 0)
            };
        }

        private static Button MkBtn(string t, EventHandler h)
        {
            var b = new Button
            {
                Text = t,
                Height = 22,
                Width = t.Length * 13 + 22,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(28, 36, 46),
                ForeColor = Fg,
                Margin = new Padding(0, 2, 5, 2),
                Cursor = Cursors.Hand,
                Font = new Font("Microsoft YaHei UI", 8.5f)
            };
            b.FlatAppearance.BorderColor = Line;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 50, 62);
            b.Click += h;
            return b;
        }

        private static Button MkBtnAccent(string t, EventHandler h)
        {
            var b = MkBtn(t, h);
            b.BackColor = Color.FromArgb(18, 78, 96);
            b.FlatAppearance.BorderColor = Acc;
            return b;
        }

        // ================================================================ 数据层
        private class Row
        {
            public SatEntry Sat;
            public ListViewItem Item;
        }

        private void RebuildLists()
        {
            _needRebuild = false;
            var all = new List<SatEntry>();
            foreach (var s in SatStore.I.Sats) if (s.Model != null) all.Add(s);   // 深空暂不支持
            all.Sort((a, b) =>
            {
                if (a.Favorite != b.Favorite) return a.Favorite ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            var fav = new List<SatEntry>();
            foreach (var s in all) if (s.Favorite) fav.Add(s);

            FillList(_lvAll, _rowsAll, all);
            FillList(_lvFav, _rowsFav, fav);
            if (_sel == null && all.Count > 0) SelectSat(all[0]);
            else if (_sel != null) SelectSat(_sel);
        }

        private void FillList(ListView lv, List<Row> rows, List<SatEntry> sats)
        {
            if (lv == null) return;
            lv.BeginUpdate();
            lv.Items.Clear();
            rows.Clear();
            foreach (var s in sats)
            {
                var it = new ListViewItem(s.Name);
                it.SubItems.Add(SatKind(s));
                it.SubItems.Add("—");
                it.SubItems.Add("—");
                it.SubItems.Add("—");
                it.Tag = s;
                lv.Items.Add(it);
                rows.Add(new Row { Sat = s, Item = it });
            }
            lv.EndUpdate();
        }

        private static string SatKind(SatEntry s)
        {
            string n = (s.Name ?? "").ToUpperInvariant();
            if (n.Contains("NOAA") || n.Contains("METOP") || n.Contains("METEOR") || n.Contains("SUOMI")
                || n.Contains("FENGYUN") || n.Contains("ELEKTRO")) return "气象";
            if (n.Contains("ISS") || n.Contains("CSS") || n.Contains("TIANHE")) return "空间站";
            if (n.Contains("AO-") || n.Contains("FO-") || n.Contains("SO-") || n.Contains("RS-")
                || n.Contains("IO-") || n.Contains("PO-") || n.Contains("BO-")) return "业余";
            if (n.Contains("CUBE") || n.Contains("CUBESAT") || n.Contains("SAT")) return "业余";
            return "业务";
        }

        private void SelectSat(SatEntry s)
        {
            _sel = s;
            if (s == null) return;
            foreach (var r in _rowsAll) if (ReferenceEquals(r.Sat, s)) { r.Item.Selected = true; r.Item.Focused = true; break; }
            foreach (var r in _rowsFav) if (ReferenceEquals(r.Sat, s)) { r.Item.Selected = true; break; }
            _cmbTp.BeginUpdate();
            _cmbTp.Items.Clear();
            foreach (var tp in s.Transponders) _cmbTp.Items.Add(tp.Describe());
            _cmbTp.EndUpdate();
            if (s.Transponders.Count > 0) _cmbTp.SelectedIndex = 0;
            else
            {
                TrackerEngine.I.SetTransponder(null);
                _cmbTp.Items.Add("（该星无转发器记录）");
                _cmbTp.SelectedIndex = 0;
            }
            _lblSatName.Text = s.Name + " · NORAD " + s.Norad;
            _map.Sat = s;
            _map.Invalidate();
            UpdateSatNow();
        }

        private void OnSatPicked(ListView lv, List<Row> rows)
        {
            if (lv.SelectedIndices.Count == 0) return;
            int i = lv.SelectedIndices[0];
            if (i < 0 || i >= rows.Count) return;
            var s = rows[i].Sat;
            if (ReferenceEquals(s, _sel)) return;
            SelectSat(s);
        }

        private void OnTpChanged()
        {
            if (_sel == null) return;
            int i = _cmbTp.SelectedIndex;
            var tp = (i >= 0 && i < _sel.Transponders.Count) ? _sel.Transponders[i] : null;
            TrackerEngine.I.SetTransponder(tp);
            if (tp != null)
            {
                Note("转发器：" + tp.Describe());
                string m = string.IsNullOrEmpty(tp.Mode) ? "FM" : tp.Mode;
                int k = _cmbMode.Items.IndexOf(m);
                if (k >= 0) _cmbMode.SelectedIndex = k;
            }
        }

        /// <summary>后台为所有卫星算「下一场入境」（每 5 min 一次，供两张表用）。</summary>
        private void PredictAll(bool force)
        {
            if (_predicting) return;
            if (!force && (DateTime.UtcNow - _lastPred).TotalMinutes < 5) return;
            _predicting = true;
            try
            {
                var site = SatStore.I.Station;
                double minEl = (double)_numMinEl.Value;
                var found = new Dictionary<int, PassInfo>();
                foreach (var s in SatStore.I.Sats)
                {
                    if (s.Model == null) continue;
                    try
                    {
                        var ps = PassPredictor.Predict(s.Model, s.Name, site, DateTime.UtcNow, 3.0, Math.Max(0.5, minEl), 1);
                        if (ps != null && ps.Count > 0) found[s.Norad] = ps[0];
                    }
                    catch { }
                }
                lock (_nextPass)
                {
                    _nextPass.Clear();
                    foreach (var kv in found) _nextPass[kv.Key] = kv.Value;
                }
                _lastPred = DateTime.UtcNow;
            }
            finally { _predicting = false; }
        }

        private PassInfo NextPassOf(SatEntry s)
        {
            if (s == null) return null;
            lock (_nextPass)
            {
                PassInfo p;
                return _nextPass.TryGetValue(s.Norad, out p) ? p : null;
            }
        }

        // ================================================================ 刷新
        private void TickUi()
        {
            try
            {
                if (_needRebuild) RebuildLists();
                DateTime now = DateTime.UtcNow;
                UpdateList(_rowsAll, now);
                UpdateList(_rowsFav, now);
                UpdateSatNow();
                UpdateConn();
                _map.Tick(now);
                _lblStatA.Text = "星历：" + SatStore.I.Sats.Count + " 颗 · " + SatStore.I.AgeText()
                    + " · 转发器 " + CountTp() + " · 列表 " + _rowsAll.Count + "/" + _rowsFav.Count
                    + (SatStore.I.DeepSpaceCount > 0 ? " · 深空跳过 " + SatStore.I.DeepSpaceCount : "");
                var e = TrackerEngine.I;
                bool online = (now - e.BoardAt).TotalSeconds < 10;
                _lblStatB.Text = "S3：" + (online ? "在线" : "离线/未连")
                    + (online ? " · 方位 " + e.BoardAz.ToString("0.0") + "° / 俯仰 " + e.BoardEl.ToString("0.0") + "°"
                              + " · 跟踪客户端 " + (e.BoardTrackOk == 1 ? "有" : "无") : "");
            }
            catch { }
        }

        private int CountTp()
        {
            int n = 0;
            foreach (var s in SatStore.I.Sats) n += s.Transponders.Count;
            return n;
        }

        private void UpdateList(List<Row> rows, DateTime now)
        {
            if (rows.Count == 0) return;
            var site = SatStore.I.Station;
            foreach (var r in rows)
            {
                double az, el, rng;
                try { PassPredictor.ElAz(r.Sat.Model, site, now, out az, out el, out rng); }
                catch { continue; }
                var it = r.Item;
                it.SubItems[2].Text = el.ToString("0.0") + "°";
                it.ForeColor = el > 0 ? Good : Fg;
                var p = NextPassOf(r.Sat);
                if (p != null && p.LosUtc > now)
                {
                    bool nowPass = p.AosUtc <= now && p.LosUtc >= now;
                    it.SubItems[3].Text = (nowPass ? "进行中 " : "") + p.AosUtc.ToLocalTime().ToString("MM-dd HH:mm");
                    it.SubItems[4].Text = nowPass
                        ? (p.MaxEl.ToString("0.0") + "° 高点")
                        : Countdown(p.AosUtc, now);
                }
                else
                {
                    it.SubItems[3].Text = "—";
                    it.SubItems[4].Text = "—";
                }
            }
        }

        private static string Countdown(DateTime t, DateTime now)
        {
            double s = (t - now).TotalSeconds;
            if (s < 0) return "已结束";
            if (s < 3600) return "T-" + (int)(s / 60) + " 分 " + (int)(s % 60) + " 秒";
            return "T-" + (int)(s / 3600) + " 时 " + (int)(s % 3600 / 60) + " 分";
        }

        /// <summary>选中星的实时几何：雷达、地图、多普勒下行/上行都吃这一份。</summary>
        private void UpdateSatNow()
        {
            var s = _sel;
            var site = SatStore.I.Station;
            LookAngle la = new LookAngle();
            bool ok = false;
            if (s != null && s.Model != null) ok = PassPredictor.Look(s.Model, site, DateTime.UtcNow, out la);
            if (!ok)
            {
                _lblAz.Text = "方位 AZ —";
                _lblEl.Text = "俯仰 EL —";
                _lblRange.Text = "距离 —";
                _lblAlt.Text = "高度 —";
                _lblDopHere.Text = "多普勒 —";
                _lblVisible.Text = "可见性 —";
                _lblRangeRate.Text = "距离变化率 —";
                _radar.TargetEl = -90; _radar.HasTarget = false;
                return;
            }
            var tp = TrackerEngine.I.CurrentTp(s);
            long dl = tp != null ? tp.DownlinkHz : 0, ul = tp != null ? tp.UplinkHz : 0;
            double sub;
            Vec3 pos, vel;
            s.Model.Propagate((DateTime.UtcNow - s.Model.EpochUtc).TotalMinutes, out pos, out vel);
            sub = pos.Len() - 6378.135;

            _lblAz.Text = "方位 AZ " + la.AzDeg.ToString("0.0") + "°";
            _lblEl.Text = "俯仰 EL " + la.ElDeg.ToString("0.0") + "°";
            _lblRange.Text = "距离 " + la.RangeKm.ToString("0") + " km";
            _lblAlt.Text = "高度 " + sub.ToString("0") + " km";
            _lblRangeRate.Text = "距离变化率 " + la.RangeRateKmS.ToString("+0.000;-0.000;0.000") + " km/s";
            _lblDopHere.Text = "多普勒 " + (dl > 0 ? Coord.DopplerShiftHz(la.RangeRateKmS, dl).ToString("+0;-0;0") + " Hz" : "—");
            _lblVisible.Text = la.ElDeg > 0
                ? ("可见性 可见（仰角 " + la.ElDeg.ToString("0.0") + "°）")
                : ("可见性 不可见（仰角 " + la.ElDeg.ToString("0.0") + "°）");
            _lblVisible.ForeColor = la.ElDeg > 0 ? Good : Dim;

            var e = TrackerEngine.I;
            _radar.TargetAz = la.AzDeg;
            _radar.TargetEl = la.ElDeg;
            _radar.HasTarget = true;
            bool fresh = (DateTime.UtcNow - e.BoardAt).TotalSeconds < 5 && !double.IsNaN(e.BoardAz);
            _radar.HasActual = fresh;
            _radar.ActualAz = e.BoardAz;
            _radar.ActualEl = e.BoardEl;
            _radar.ErrDeg = fresh ? TrackerEngine.SepDeg(la.AzDeg, la.ElDeg, e.BoardAz, e.BoardEl) : double.NaN;
            _radar.Invalidate();

            if (dl > 0)
            {
                long rx = (long)Math.Round(dl * (1.0 - la.RangeRateKmS / 299792.458));
                long tx = ul > 0 ? (long)Math.Round(ul * (1.0 + la.RangeRateKmS / 299792.458)) : 0;
                _lblDown.Text = "下行 " + RigPlan.MHz(rx) + "   " + (rx - dl).ToString("+0;-0;0") + " Hz";
                _lblUp.Text = tx > 0 ? ("上行 " + RigPlan.MHz(tx) + "   " + (tx - ul).ToString("+0;-0;0") + " Hz") : "上行 —（单频）";
                if (!_txtRx.Focused && !_txtTx.Focused)
                {
                    _txtRx.Text = rx.ToString(CultureInfo.InvariantCulture);
                    _txtTx.Text = tx.ToString(CultureInfo.InvariantCulture);
                }
            }
            else
            {
                _lblDown.Text = "下行 —（未选转发器）";
                _lblUp.Text = "上行 —";
            }
        }

        private void UpdateConn()
        {
            var e = TrackerEngine.I;
            bool online = (DateTime.UtcNow - e.BoardAt).TotalSeconds < 10;
            _lblConnGimbal.Text = (_s3 != null ? _s3.Host : "—") + (online ? "  ● 已连接" : "  ○ 未连接");
            _lblConnGimbal.ForeColor = online ? Good : Warn;
            CivLink l = null;
            try { l = _civ != null ? _civ() : null; } catch { }
            var rd = e.Radio;
            bool rig = l != null && l.IsOpen;
            _lblConnRadio.Text = rig
                ? ("● 已连接 " + (rd.Kind == RigKind.Ic705 ? "IC-705" : "IC-7100") + " · " + rd.StatusText())
                : "○ 未连接（在「电台」页连接后回本页点「更新电台参数」）";
            _lblConnRadio.ForeColor = rig ? Good : Dim;
        }

        // ================================================================ 动作
        private void SlowTick()
        {
            if (SatStore.I.Sats.Count == 0) return;
            if ((DateTime.UtcNow - _lastPred).TotalMinutes > 5) ThreadPool.QueueUserWorkItem(delegate { PredictAll(true); });
            UpdateConn();

            // 全自动追踪：收藏星 90 s 内入境 → 自动开始
            if (_chkAuto != null && _chkAuto.Checked && !TrackerEngine.I.Running)
            {
                var now = DateTime.UtcNow;
                foreach (var s in SatStore.I.Sats)
                {
                    if (!s.Favorite || s.Model == null) continue;
                    var p = NextPassOf(s);
                    if (p == null) continue;
                    if (p.AosUtc <= now.AddSeconds(90) && p.LosUtc > now)
                    {
                        Note("全自动追踪：" + s.Name + " 入境，自动开始");
                        _sel = s;
                        TrackerEngine.I.DopplerOn = _chkDop.Checked;
                        TrackerEngine.I.Radio.AutoTune = _chkDop.Checked;
                        TrackerEngine.I.Start(s, p, _s3);
                        break;
                    }
                }
            }
        }

        private void StartTrack()
        {
            if (_sel == null) { Note("先选一颗卫星"); return; }
            var pass = NextPassOf(_sel);
            RadioInit();
            TrackerEngine.I.DopplerOn = _chkDop.Checked;
            TrackerEngine.I.Radio.AutoTune = _chkDop.Checked;
            TrackerEngine.I.Radio.SyncTones = _chkTone.Checked;
            TrackerEngine.I.Start(_sel, pass, _s3);
        }

        private void DoFav()
        {
            if (_sel == null) return;
            SatStore.I.ToggleFavorite(_sel);
            Note((_sel.Favorite ? "已收藏 " : "已取消收藏 ") + _sel.Name);
            _needRebuild = true;
        }

        private void DoUpdate()
        {
            Note("开始在线更新星历与转发器…");
            ThreadPool.QueueUserWorkItem(delegate
            {
                int n = 0;
                try { n = SatStore.I.UpdateOnline(Note, CancellationToken.None); }
                catch (Exception ex) { Note("更新失败：" + ex.Message); }
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        _needRebuild = true;
                        Note(n > 0 ? ("更新完成：" + n + " 颗") : "更新完成（沿用原有缓存）");
                        PredictAll(true);
                    }));
                }
                catch { }
            });
        }

        private void ShowPasses()
        {
            if (_sel == null) { Note("先选一颗卫星"); return; }
            var s = _sel;
            var site = SatStore.I.Station;
            List<PassInfo> ps = null;
            try { ps = PassPredictor.Predict(s.Model, s.Name, site, DateTime.UtcNow, 24.0, 0.5, 12); }
            catch { }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(s.Name + " · 未来 24 小时过境（本地时间 · 仰角≥0.5°）");
            sb.AppendLine();
            if (ps == null || ps.Count == 0) sb.AppendLine("（无过境）");
            else
                foreach (var p in ps)
                {
                    sb.AppendLine(p.AosUtc.ToLocalTime().ToString("MM-dd HH:mm:ss") + " → "
                        + p.LosUtc.ToLocalTime().ToString("HH:mm:ss")
                        + "   AOS 方位 " + p.AosAz.ToString("0") + "°   最大仰角 " + p.MaxEl.ToString("0.0") + "°"
                        + "   时长 " + Math.Round(p.DurationSec / 60) + " 分"
                        + (p.IsNow ? "   ← 正在过境" : ""));
                }
            MessageBox.Show(this, sb.ToString(), "过境详情", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowLog()
        {
            var sb = new System.Text.StringBuilder();
            lock (_logLines) { foreach (var l in _logLines) sb.AppendLine(l); }
            using (var f = new Form())
            {
                f.Text = "追踪日志";
                f.Size = new Size(760, 520);
                f.StartPosition = FormStartPosition.CenterParent;
                f.BackColor = Color.FromArgb(16, 21, 28);
                var t = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    WordWrap = false,
                    BackColor = Color.FromArgb(16, 21, 28),
                    ForeColor = Color.FromArgb(190, 210, 225),
                    Font = new Font("Consolas", 9f),
                    Text = sb.ToString()
                };
                f.Controls.Add(t);
                f.ShowDialog(this);
            }
        }

        private void TakePriority()
        {
            string ip = LocalIp();
            bool ok = _s3 != null && _s3.SetPriority(ip);
            Note(ok ? ("已取得云台控制权（" + ip + "）") : ("取得控制权失败：" + (_s3 != null ? _s3.LastError : "")));
        }

        private void RebootS3()
        {
            if (MessageBox.Show(this, "确定重启 S3 中转站？云台/电台/摄像头会短暂断开（约 10 秒）。",
                "硬件重启确认", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes) return;
            bool ok = _s3 != null && _s3.Cmd("RB");
            Note(ok ? "已下发硬件重启指令" : ("重启指令失败：" + (_s3 != null ? _s3.LastError : "")));
        }

        private static string LocalIp()
        {
            try
            {
                var host = Dns.GetHostName();
                foreach (var a in Dns.GetHostAddresses(host))
                    if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)) return a.ToString();
            }
            catch { }
            return "0.0.0.0";
        }

        /// <summary>立即设频：用界面上的 RX/TX Hz 直接下发一组（多普勒关掉时手动纠偏用）。</summary>
        private void ApplyNow()
        {
            var sat = _sel;
            if (sat == null) { Note("先选一颗卫星"); return; }
            long rx = ParseHz(_txtRx.Text);
            long tx = ParseHz(_txtTx.Text);
            if (rx <= 0) { Note("RX Hz 无效"); return; }
            var rp = new RigPlan
            {
                RxHz = rx,
                TxHz = tx,
                RxMode = (_cmbMode.SelectedItem as string) ?? "FM",
                TxMode = (_cmbMode.SelectedItem as string) ?? "FM",
                Split = tx > 0 && Math.Abs(tx - rx) > 1,
                SatName = sat.Name
            };
            var tp = TrackerEngine.I.CurrentTp(sat);
            if (tp != null && TrackerEngine.I.Radio.SyncTones) { rp.TxTone = tp.TxTone; rp.RxTone = tp.RxTone; }
            RadioInit();
            TrackerEngine.I.Radio.FullApply(rp);
            Note("立即设频：" + rp + " · " + TrackerEngine.I.Radio.LastNote);
        }

        private void ReapplyAll()
        {
            RadioInit();
            var sat = _sel;
            if (sat == null) { Note("先选一颗卫星"); return; }
            LookAngle la;
            if (!PassPredictor.Look(sat.Model, SatStore.I.Station, DateTime.UtcNow, out la)) { Note("递推失败"); return; }
            var plan = TrackerEngine.I.BuildPlan(sat, la, DateTime.UtcNow);
            if (plan == null) { Note("该星没有转发器记录，无法自动设频"); return; }
            TrackerEngine.I.Radio.FullApply(plan);
            Note("已整组重配：" + plan + " · " + TrackerEngine.I.Radio.LastNote);
        }

        private static long ParseHz(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) return 0;
            double v;
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return 0;
            if (s.IndexOf('.') >= 0 || v < 3000) v *= 1e6;      // 允许写 145.9775 MHz
            return (long)Math.Round(v);
        }

        // ------------------------------------------------------------ QTH
        private void LoadQthBoxes()
        {
            var s = SatStore.I.Station;
            _txtLat.Text = s.LatDeg.ToString("0.0000", CultureInfo.InvariantCulture);
            _txtLon.Text = s.LonDeg.ToString("0.0000", CultureInfo.InvariantCulture);
            _txtAlt.Text = s.AltM.ToString("0", CultureInfo.InvariantCulture);
            _txtGrid.Text = GridOf(s.LatDeg, s.LonDeg);
        }

        private void GridToBoxes()
        {
            double lat, lon;
            if (!GridToLatLon(_txtGrid.Text, out lat, out lon)) { Note("网格格式不对（例：OM89aj / PL09mu）"); return; }
            _txtLat.Text = lat.ToString("0.0000", CultureInfo.InvariantCulture);
            _txtLon.Text = lon.ToString("0.0000", CultureInfo.InvariantCulture);
            Note("网格 " + _txtGrid.Text.ToUpperInvariant() + " → " + _txtLat.Text + "°N / " + _txtLon.Text + "°E");
        }

        private void SaveQth()
        {
            double lat, lon, alt;
            if (!double.TryParse(_txtLat.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
                || !double.TryParse(_txtLon.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out lon))
            { Note("经纬度无效"); return; }
            if (!double.TryParse(_txtAlt.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out alt)) alt = 0;
            if (lat < -90 || lat > 90 || lon < -180 || lon > 180) { Note("经纬度超出范围"); return; }
            SatStore.I.Station = new YzControl.Sat.Site(lat, lon, alt);
            try { SatStore.I.SaveUser(); } catch { }
            _txtGrid.Text = GridOf(lat, lon);
            _map.Invalidate();
            Note("QTH 已保存：" + lat.ToString("0.0000") + "°N / " + lon.ToString("0.0000") + "°E / "
                + alt.ToString("0") + " m · 网格 " + _txtGrid.Text);
            PredictAll(true);
        }

        private static string GridOf(double lat, double lon)
        {
            lon += 180.0; lat += 90.0;
            if (lon < 0) lon = 0; if (lon > 359.999) lon = 359.999;
            if (lat < 0) lat = 0; if (lat > 179.999) lat = 179.999;
            int a = (int)(lon / 20), b = (int)(lat / 10);
            int c = (int)((lon % 20) / 2), d = (int)(lat % 10);
            int e = (int)((lon % 2) / (2.0 / 24)), f = (int)((lat % 1) / (1.0 / 24));
            return "" + (char)('A' + a) + (char)('A' + b) + c + d + (char)('a' + e) + (char)('a' + f);
        }

        private static bool GridToLatLon(string g, out double lat, out double lon)
        {
            lat = lon = 0;
            g = (g ?? "").Trim().ToUpperInvariant();
            if (g.Length < 4) return false;
            int a = g[0] - 'A', b = g[1] - 'A', c = g[2] - '0', d = g[3] - '0';
            if (a < 0 || a > 17 || b < 0 || b > 17 || c < 0 || c > 9 || d < 0 || d > 9) return false;
            lon = a * 20.0 + c * 2.0 - 180.0;
            lat = b * 10.0 + d * 1.0 - 90.0;
            if (g.Length >= 6)
            {
                int e = g[4] - 'A', f = g[5] - 'A';
                if (e >= 0 && e < 24 && f >= 0 && f < 24)
                {
                    lon += e * (2.0 / 24) + (1.0 / 24);
                    lat += f * (1.0 / 24) + (0.5 / 24);
                }
            }
            else { lon += 1.0; lat += 0.5; }
            return true;
        }

        // ------------------------------------------------------------ 引擎事件
        private void OnEngine(EngineSnapshot s)
        {
            if (s == null) return;
            try
            {
                if (InvokeRequired) { BeginInvoke((Action)(() => OnEngineUi(s))); return; }
                OnEngineUi(s);
            }
            catch { }
        }

        private void OnEngineUi(EngineSnapshot s)
        {
            _radar.ErrDeg = s.ErrDeg;
            _radar.MaxErr = s.MaxErrDeg;
            _radar.Running = s.Running;
            _radar.Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { TrackerEngine.I.Updated -= OnEngine; } catch { }
                try { TrackerEngine.I.Log -= Note; } catch { }
                try { if (_timer != null) { _timer.Stop(); _timer.Dispose(); _timer = null; } } catch { }
                try { if (_slow != null) { _slow.Stop(); _slow.Dispose(); _slow = null; } } catch { }
                if (ReferenceEquals(_instance, this)) _instance = null;
            }
            base.Dispose(disposing);
        }

        // ================================================================ 自绘控件
        /// <summary>雷达：目标针（青）+ 真实云台针（红），中心误差读数。</summary>
        private sealed class Radar : Control
        {
            public double TargetAz, TargetEl = -90, ActualAz, ActualEl;
            public bool HasTarget, HasActual, Running;
            public double ErrDeg = double.NaN, MaxErr;

            public Radar()
            {
                DoubleBuffered = true;
                BackColor = Color.FromArgb(14, 19, 26);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(BackColor);
                int w = Width, h = Height;
                int d = Math.Min(w, h) - 18;
                if (d < 40) return;
                int cx = w / 2, cy = h / 2, rad = d / 2;

                using (var pen = new Pen(Color.FromArgb(40, 56, 72), 1f))
                {
                    for (int k = 1; k <= 3; k++) g.DrawEllipse(pen, cx - rad * k / 3, cy - rad * k / 3, rad * 2 * k / 3, rad * 2 * k / 3);
                    g.DrawLine(pen, cx - rad, cy, cx + rad, cy);
                    g.DrawLine(pen, cx, cy - rad, cx, cy + rad);
                }
                using (var br = new SolidBrush(Dim))
                using (var f = new Font("Microsoft YaHei UI", 8f))
                {
                    g.DrawString("N", f, br, cx - 4, cy - rad - 15);
                    g.DrawString("S", f, br, cx - 4, cy + rad + 2);
                    g.DrawString("E", f, br, cx + rad + 2, cy - 7);
                    g.DrawString("W", f, br, cx - rad - 13, cy - 7);
                    g.DrawString("90°", f, br, cx - 12, cy - rad + 2);
                    g.DrawString("0°", f, br, cx - 8, cy + 2);
                }

                if (HasTarget)
                {
                    double r = rad * (90.0 - Math.Max(TargetEl, 0)) / 90.0;
                    DrawNeedle(g, cx, cy, TargetAz, r, Acc, 2f);
                    using (var dot = new SolidBrush(Acc)) g.FillEllipse(dot, (float)(cx + r * Math.Sin(TargetAz * Math.PI / 180)) - 3,
                        (float)(cy - r * Math.Cos(TargetAz * Math.PI / 180)) - 3, 6, 6);
                }
                if (HasActual)
                {
                    double r = rad * (90.0 - Math.Max(ActualEl, 0)) / 90.0;
                    DrawNeedle(g, cx, cy, ActualAz, r, Hot, 1.4f);
                }
                using (var br = new SolidBrush(Fg))
                using (var f = new Font("Consolas", 8.5f))
                {
                    string s1 = HasTarget ? ("AZ " + TargetAz.ToString("0.0") + " / EL " + TargetEl.ToString("0.0")) : "未选择卫星";
                    g.DrawString(s1, f, br, 8, h - 32);
                    string s2 = double.IsNaN(ErrDeg) ? (HasActual ? "误差 —" : "红针 = 真实云台角度")
                        : ("误差 " + ErrDeg.ToString("0.00") + "°" + (ErrDeg <= 1.0 ? " ✓" : " ✗") + "  峰值 " + MaxErr.ToString("0.00"));
                    using (var b2 = new SolidBrush(double.IsNaN(ErrDeg) ? Dim : (ErrDeg <= 1.0 ? Good : Warn)))
                        g.DrawString(s2, f, b2, 8, h - 18);
                }
            }

            private static void DrawNeedle(Graphics g, int cx, int cy, double azDeg, double len, Color c, float wd)
            {
                double a = azDeg * Math.PI / 180.0;
                float x = (float)(cx + len * Math.Sin(a)), y = (float)(cy - len * Math.Cos(a));
                using (var pen = new Pen(c, wd)) g.DrawLine(pen, cx, cy, x, y);
            }
        }

        /// <summary>等距圆柱世界地图：陆地轮廓 + 经纬网 + 星下点/覆盖圈/6h 轨迹 + QTH。</summary>
        private sealed class MapView : Control
        {
            public SatEntry Sat;
            public YzControl.Sat.Site Qth = YzControl.Sat.Site.Default;
            private List<PointF[]> _land;
            private List<PointF> _track = new List<PointF>();
            private double _subLat, _subLon, _foot, _altKm;
            private DateTime _trackAt = DateTime.MinValue;
            private int _trackForNorad = -1;

            public MapView()
            {
                DoubleBuffered = true;
                BackColor = Color.FromArgb(10, 14, 20);
                LoadLand();
            }

            private void LoadLand()
            {
                try
                {
                    string exe = Path.GetDirectoryName(typeof(MapView).Assembly.Location) ?? ".";
                    string p = Path.Combine(exe, "Sat", "world_land.txt");
                    if (!File.Exists(p)) p = Path.Combine(exe, "world_land.txt");
                    if (!File.Exists(p)) return;
                    _land = new List<PointF[]>();
                    foreach (var line in File.ReadAllLines(p))
                    {
                        if (line.Length < 8) continue;
                        var parts = line.Split(';');
                        var pts = new PointF[parts.Length];
                        int n = 0;
                        foreach (var s in parts)
                        {
                            int k = s.IndexOf(',');
                            if (k <= 0) continue;
                            float x, y;
                            if (!float.TryParse(s.Substring(0, k), NumberStyles.Float, CultureInfo.InvariantCulture, out x)) continue;
                            if (!float.TryParse(s.Substring(k + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out y)) continue;
                            pts[n++] = new PointF(x, y);
                        }
                        if (n >= 3) { var t = new PointF[n]; Array.Copy(pts, t, n); _land.Add(t); }
                    }
                }
                catch { }
            }

            /// <summary>每秒调用：星下点每拍更新，6h 轨迹每 60 s 或换星时重算。</summary>
            public void Tick(DateTime utc)
            {
                var s = Sat;
                if (s == null || s.Model == null) { if (_track.Count > 0) { _track.Clear(); Invalidate(); } return; }
                Qth = SatStore.I.Station;
                Vec3 r, v;
                try
                {
                    s.Model.Propagate((utc - s.Model.EpochUtc).TotalMinutes, out r, out v);
                    Coord.SubPoint(utc, r, out _subLat, out _subLon, out _altKm);
                    _foot = Coord.FootprintDeg(_altKm);
                }
                catch { return; }

                if (_trackForNorad != s.Norad || (utc - _trackAt).TotalSeconds > 60)
                {
                    _trackForNorad = s.Norad;
                    _trackAt = utc;
                    var pts = new List<PointF>();
                    for (int i = 0; i <= 72; i++)
                    {
                        double t = (utc - s.Model.EpochUtc).TotalMinutes + i * 5.0;   // 未来 6 h，5 min 一采
                        Vec3 rr, vv;
                        if (!s.Model.Propagate(t, out rr, out vv)) break;
                        double la, lo, al;
                        Coord.SubPoint(utc.AddMinutes(i * 5.0), rr, out la, out lo, out al);
                        pts.Add(new PointF((float)lo, (float)la));
                    }
                    _track = pts;
                }
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(BackColor);
                int w = Width, h = Height;
                if (w < 40 || h < 30) return;

                Func<double, float> X = lon => (float)((lon + 180.0) / 360.0 * w);
                Func<double, float> Y = lat => (float)((90.0 - lat) / 180.0 * h);

                // 陆地
                if (_land != null)
                {
                    using (var fill = new SolidBrush(Color.FromArgb(28, 44, 56)))
                    using (var edge = new Pen(Color.FromArgb(46, 74, 90), 0.8f))
                    {
                        foreach (var ring in _land)
                        {
                            if (ring.Length < 3) continue;
                            var pts = new PointF[ring.Length];
                            for (int i = 0; i < ring.Length; i++) pts[i] = new PointF(X(ring[i].X), Y(ring[i].Y));
                            try { g.FillPolygon(fill, pts); } catch { }
                            try { g.DrawPolygon(edge, pts); } catch { }
                        }
                    }
                }
                // 经纬网
                using (var pen = new Pen(Color.FromArgb(26, 40, 54), 1f))
                {
                    for (int lon = -150; lon <= 150; lon += 30) g.DrawLine(pen, X(lon), 0, X(lon), h);
                    for (int lat = -60; lat <= 60; lat += 30) g.DrawLine(pen, 0, Y(lat), w, Y(lat));
                }
                using (var eq = new Pen(Color.FromArgb(44, 66, 82), 1f))
                    g.DrawLine(eq, 0, Y(0), w, Y(0));

                // 6h 轨迹
                if (_track.Count > 1)
                {
                    using (var pen = new Pen(Color.FromArgb(120, 34, 211, 238), 1.2f))
                    {
                        for (int i = 1; i < _track.Count; i++)
                        {
                            float x1 = X(_track[i - 1].X), y1 = Y(_track[i - 1].Y);
                            float x2 = X(_track[i].X), y2 = Y(_track[i].Y);
                            if (Math.Abs(x2 - x1) > w * 0.5f) continue;       // 跨 ±180 不连线
                            g.DrawLine(pen, x1, y1, x2, y2);
                        }
                    }
                }
                // 覆盖圈 + 星下点
                if (Sat != null && Sat.Model != null)
                {
                    float sx = X(_subLon), sy = Y(_subLat);
                    float rw = (float)(2 * _foot / 360.0 * w), rh = (float)(2 * _foot / 180.0 * h);
                    using (var pen = new Pen(Color.FromArgb(150, 34, 211, 238), 1f))
                    using (var fill = new SolidBrush(Color.FromArgb(28, 34, 211, 238)))
                    {
                        g.FillEllipse(fill, sx - rw / 2, sy - rh / 2, rw, rh);
                        g.DrawEllipse(pen, sx - rw / 2, sy - rh / 2, rw, rh);
                    }
                    using (var dot = new SolidBrush(Acc)) g.FillEllipse(dot, sx - 3.5f, sy - 3.5f, 7, 7);
                    using (var f = new Font("Consolas", 8f))
                    using (var br = new SolidBrush(Fg))
                        g.DrawString(Sat.Name + "  " + _subLat.ToString("0.0") + "° / " + _subLon.ToString("0.0")
                            + "°  " + _altKm.ToString("0") + " km", f, br, 6, 4);
                }
                // QTH
                float qx = X(Qth.LonDeg), qy = Y(Qth.LatDeg);
                using (var pen = new Pen(Color.FromArgb(250, 204, 21), 1.6f))
                {
                    g.DrawLine(pen, qx - 5, qy, qx + 5, qy);
                    g.DrawLine(pen, qx, qy - 5, qx, qy + 5);
                }
                using (var f = new Font("Microsoft YaHei UI", 8f))
                using (var br = new SolidBrush(Color.FromArgb(250, 204, 21)))
                    g.DrawString("QTH " + Qth.LatDeg.ToString("0.00") + "/" + Qth.LonDeg.ToString("0.00"), f, br, qx + 6, qy - 14);
            }
        }
    }
}
