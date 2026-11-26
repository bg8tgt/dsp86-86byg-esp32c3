using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using YzControl.Camera;
using YzControl.Net;
// v1.6.0：WinForms 自己也有 System.Windows.Forms.LinkState（超链接用），这里显式指定链路状态枚举
using LinkState = YzControl.Net.LinkState;

namespace YzControl;

public sealed class MainForm : Form
{
	private sealed class OnvifClock
	{
		public TimeSpan Offset;

		public DateTime SyncedUtc;
	}

	private sealed class PtzJob
	{
		public int Kind;

		public CameraEntry Cam;

		public float Pan;

		public float Tilt;

		public float Zoom;

		public string PresetText;
	}

	private readonly S3Client _s3 = new S3Client();

	private NotifyIcon _tray;

	private bool _reallyExit;

	private bool _trayTipShown;

	private CameraConfig _cfg = new CameraConfig();

	private IVideoEngine _s1;

	private IVideoEngine _s2;

	private Panel _pane1;

	private Panel _pane2;

	private Label _lblPane1;

	private Label _lblPane2;

	private StatusStrip _status;

	private ToolStripStatusLabel _lblS3;

	private ToolStripStatusLabel _lblAz;

	private ToolStripStatusLabel _lblPrio;

	private ToolStripStatusLabel _lblCam;

	private ToolStripStatusLabel _lblMsg;

	private ToolStripStatusLabel _lblBoard;

	private TextBox _txtHost;

	private string _boardSeen = "";

	private Button _btnRediscover;

	private int _discoverBusy;

	private int _s3FailStreak;

	private long _lastAutoDiscoverTicks;

	private bool _hostBoxSync;

	private string _manualHost = "";

	private bool _ipChangeNotified;

	private const int AutoDiscoverMinGapMs = 20000;

	private const int DiscoverTimeoutMs = 800;

	private const int ProbeTimeoutMs = 500;

	private TextBox _txtAddr1;

	private TextBox _txtAddr2;

	private TextBox _txtUser1;

	private TextBox _txtUser2;

	private TextBox _txtPass1;

	private TextBox _txtPass2;

	private Label _lblWinState1;

	private Label _lblWinState2;

	private NumericUpDown _numAz;

	private NumericUpDown _numEl;

	private NumericUpDown _numSpeed;

	private NumericUpDown _numUp;

	private NumericUpDown _numDown;

	private NumericUpDown _numSpeedV;

	private Label _lblSpeedNow;

	private Label _lblSpeedVNow;

	private Label _lblSpeedEdit;

	private Label _lblSpeedVEdit;

	private Label _lblElSrc;

	private Label _lblElCalc;

	private Label _lblElDiff;

	private CheckBox _chkSelfTest;

	private bool _selfTestSync;

	private Label _lblDtracText;

	private Label _lblDtracDial;

	private Label _lblDtracSys;

	private CheckBox _chkAllow4533;

	private Button _btnQuery4533;

	private Label _lblFwRot;

	private Label _lblFwOta;

	private Label _lblRstInfo;

	private TextBox _txtSys;

	private System.Windows.Forms.Timer _trackTimer;

	private Label _lblAzPlan;

	private Label _lblAzEndpoint;

	private Label _lblPzNote;

	private Button _btnPitchZero;

	private Button _btnPtrace;

	private Label _lblRec;

	private Button _btnRecLeave;

	private Label _lblDiagAz;

	private Label _lblDiagPitch;

	private Label _lblDiagWdt;

	private Label _lblDiagProto;

	private Label _lblDiagEl;

	private Label _lblDiagNvs;

	private TextBox _txtDiag;


	private System.Windows.Forms.Timer _diagTimer;

	private int _diagBusy;

	private YzSysSnapshot _lastDiag;

	private string _lastDiagText = "";

	private Label _lblOtas;

	private TextBox _txtOtaSsid;

	private TextBox _txtOtaPass;

	private Button _btnOtaOneKey;

	private Label _lblWdtL1;

	private Label _lblWdtL2;

	private Label _lblWdtBoot;

	private Label _lblWdtSil;

	private Label _lblWdtTest;

	private Button _btnWdtSelfTest;

	private Button _btnWdtReadback;

	private NumericUpDown _numHangN;

	private Label _lblNetPath;

	private int _wdtTestBusy;

	private volatile bool _wdtTestActive;

	private string _wdtTestLog = "";

	private YzEthInfo _lastEth;

	private ComboBox _cboPreset;

	private Label _lblPtzInfo;

	private Label _lblRadioUrl;

	private TextBox _txtRadioStatus;

	private Panel[] _pages;

	// v1.5.1：页签按需构建 + 页面进出钩子（惰性加载海康 SDK / VLC / 电台轮询）
	// v1.6.0：5 个页签（新增「卫星追踪」）—— 数组长度必须与 NavTitles/_pages 一致，
	// 否则点第 5 项会 IndexOutOfRange / 静默不切页。
	private readonly bool[] _pageBuilt = new bool[5];

	private readonly int[] _pageBuildMs = new int[5];

	// v1.5.1：某些 Windows/GDI+ 环境会在"鼠标消息里新建控件并设置 Font"时抛
	// ArgumentException("参数无效")——这是 GDI+ 的暂时性失败，隔一个消息循环重试即可。
	private readonly int[] _pageBuildRetry = new int[5];

	private int _currentPage = -1;

	private bool _monitorInited;

	// v1.5.1：主界面「高级诊断」折叠区（固件诊断 / 看门狗 / DTrac / 网络通路）
	private List<Control> _advCtrls;

	private bool _advOpen;

	private Button _advBtn;

	// v1.5.1：电台页「高级链路 / 原始调试」折叠区（S3 板载中继 / 云台板串口 / 手动发帧）
	private List<Control> _radAdvCtrls;

	private bool _radAdvOpen;

	private Button _radAdvBtn;

	// v1.5.1：直连状态文字（已连接 / 未连接）
	private Label _lblDirectTxt;

	// v1.5.1：系统页"最近 3 条复位"行的行高（无数据时收成 0，避免留空洞）
	private RowStyle _rstRowStyle;

	// v1.5.1：系统页最近 3 条复位记录（其余进「复位历史…」弹窗）
	private Label _lblRstTop3;

	// v1.5.1：系统页精简后的“关键信息”摘要
	private Label _lblSysBrief;

	private Control _toolBar;

	private Control _topBar;

	private bool _fullScreen;

	private FormBorderStyle _fsBorder;

	private FormWindowState _fsState;

	private Rectangle _fsBounds;

	private HudPageHeader _hdr;

	private NavRail _rail;

	private Panel _host;

	private static readonly HudIcon[] NavIcons = new HudIcon[5]
	{
		HudIcon.Satellite,
		HudIcon.Radio,
		HudIcon.Camera,
		HudIcon.Chip,
	HudIcon.Satellite
	};

	private static readonly string[] NavTitles = new string[5] { "卫星追踪云台", "电台", "监控（摄像机）", "系统（升级 · 信息）", "卫星追踪" };

	private static readonly string[] NavSubs = new string[5] { "独立卫星追踪云台（不是摄像头云台）· 走 S3 的 HTTP(80) 与追星协议 TCP(4533) · 右侧为实时图形仪表", "电台控制台 · 在软件内以 HTTPS(443) 打开电台页并读取电台状态（不另开网页）", "摄像头（辅助监视）：两个窗口各填「地址 + 用户名 + 密码」→ 添加并播放（只填 IP 会自动挑模式）+ 抓图/录像，全部只在本页", "S3 固件升级(OTA) · 硬件与运行信息(/sys) · 网络(以太网)。本页不含任何监控内容", "卫星追踪（内置 SGP4 引擎）· 选星 / 过境 / 跟踪 · 多普勒自动设频 · 手动即停" };

	private RigDisplay _rigDisplay;

	private ComboBox _cboProto;

	private FlowLayoutPanel _ctrlCol;

	private Panel _mini;

	private Label _lblMini;

	private IVideoEngine _miniEngine;

	private readonly int[] _paneStartSeq = new int[2];

	private int _miniSeq;

	private CompassDial _dAz;

	private ElevationGauge _dEl;

	private StatusLamp _lampRun;

	private StatusLamp _lampSensor;

	private StatusLamp _lampPrio;

	private StatusLamp _lampTrack;

	private BarMeter _mSpeed;

	private BarMeter _mAdc;

	private BarMeter _mHeap;

	private Thread _poll;

	private volatile bool _running;

	private YzStatus _lastStatus;

	private readonly int _startPage;

	private Bitmap _bgBmp;

	private Size _bgSize = Size.Empty;

	private string _bgTheme = "";

	private System.Windows.Forms.Timer _bgTimer;

	private StatusDot _dotS3;

	private StatusDot _dotS3Bar;

	private StatusDot _dotNet;

	private StatusDot _dotDirect;

	private StatusDot _dotMcast;

	private StatusDot _dotWin1;

	private StatusDot _dotWin2;

	private string _lastS3Err = "";

	private long _mcastPkts;

	private readonly DotState[] _paneDot = new DotState[2];

	internal const int DotLabelGap = 14;

	internal const int ControlGap = 12;

	public static string DumpUiPath = "";

	public static readonly string YzAppVer = ReadYzVer();

	private string _fwBldCache = "";

	private string _fwVerCache = "";

	// v1.5.1：完整 /sys 原文缓存（「读取设备信息」弹窗与系统页「详情」共用）
	private string _lastSysText = "";

	private TextBox _txtRigFreq;

	private ComboBox _cboRigMode;

	private ComboBox _cboStep;

	private ComboBox _cboBaud;

	private TextBox _txtCivAddr;

	private TextBox _txtTxDly;

	private TextBox _txtRxDly;

	private TextBox _txtNetIp;

	private TextBox _txtNetPort;

	private TextBox _txtSendHex;

	private ComboBox _cboNetMode;

	private Label _lblNetState;

	private Label _lblDirect;

	private TextBox _txtMcGroup;

	private TextBox _txtMcPort;

	private TextBox _txtMcIface;

	private Label _lblMcState;

	private ComboBox _cboMcDev;

	private McastAudio _mcast;

	private CivLink _civ;

	private readonly object _civGate = new object();

	private CivPoller _civPoller;

	// v1.6.0：直连链路状态只订阅一次（CivLink 可能在不同入口被创建）
	private bool _civStateHooked;

	private int _civPollStart;

	private RigSnapshot _rigSnap = new RigSnapshot();

	private string _rigChannel = "direct";

	private volatile bool _rigPollPaused;

	private int _rigPollMs = 2000;

	private bool _rigFellBack;

	private string _directRxHex = "";

	private DateTime _rigLastUpdate = DateTime.MinValue;

	private long _civPollOk;

	private long _civPollFail;

	private TextBox _txtDirectIp;

	private TextBox _txtDirectPort;

	private ComboBox _cboCivPoll;

	private Button _btnCivPause;

	private Button _btnCivReadSplit;

	private NeoToggle _tgRigChannel;

	private NeoToggle _tgLegacy1C;

	private NeoToggle _tgDeepVfoB;

	private NeoToggle _tgReadSmeter;

	private Label _lblCivSrc;

	private Label _lblCivTime;

	private Label _lblCivSplit;

	private Label _lblCivTx;

	private Label _lblCivMode;

	private Label _lblCivMisc;

	private RadioButton _rbDirectTcp;

	private RadioButton _rbDirectCom;

	private FlowLayoutPanel _rowDirectCom;

	private FlowLayoutPanel _rowDirectCom2;


	private ComboBox _cboDirectCom;

	private ComboBox _cboDirectBaud;

	private TextBox _txtDirectAddr;

	private StatusLamp _lampHttps;

	private StatusLamp _lampSerial;

	private StatusLamp _lampNet;

	private StatusLamp _lampTcp;

	private System.Windows.Forms.Timer _rigTimer;

	private bool _rigPttOn;

	private volatile bool _rigPolling;

	private int _rigFail;

	private TrackBar _tbRigVol;

	private TrackBar _tbRigSql;

	private Label _lblRigVolVal;

	private Label _lblRigSqlVal;

	private Label _lblRigCh;

	private Button _btnRigVfo;

	private Button _btnRigMem;

	private System.Windows.Forms.Timer _tRigLvl;

	private volatile bool _rigVolDrag;

	private volatile bool _rigSqlDrag;

	private bool _rigLvlSync;

	private int _rigVolPending = -1;

	private int _rigSqlPending = -1;

	private int _rigVolAt;

	private int _rigSqlAt;

	private string _rigVmCur = "?";

	private string _rigChCur = "0";

	private TextBox _txtRigChN;

	private Button[] _btnRigGrp = new Button[5];

	private Button _btnNetToggle;

	private Button _btnDirect;

	private Button _btnMcastToggle;

	private Button _btnTestConn;

	private Button _btnDisconn;

	private Label _lblRigGrp;

	private Label _lblRigPath;

	private string _rigGrpCur = "0";

	private string _rigPathCur = "";

	private string _lblRigFreqCur = "0";

	private System.Windows.Forms.Timer _pttKeep;

	private volatile bool _pttKeepBusy;

	private bool _rigTunnelOn;

	private long _hzCache;

	private string _modeCache = "--";

	private long _latCache = -1L;

	private bool _linkCache;

	private long _spHzCache;

	private string _spModeCache = "--";

	private bool _spOnCache;

	private bool _chToggleGuard;

	private readonly object _pttReqLock = new object();

	private int _pttWant = int.MinValue;

	private int _pttWorkerBusy;

	private ProgressBar _prgOta;

	private Label _lblOtaInfo;

	private ListView _lstWifi;

	private TextBox _txtWifiPass;

	private TextBox _txtEthIp;

	private TextBox _txtEthGw;

	private TextBox _txtEthNm;

	private TextBox _txtEthDs;

	private Label _lblEthState;

	private YzRstHistory _lastRst;

	private const int OnvifClockQuietMinutes = 10;

	private readonly object _onvifGate = new object();

	private readonly Dictionary<string, OnvifClock> _onvifClockCache = new Dictionary<string, OnvifClock>();

	private readonly Dictionary<string, string> _onvifProfileCache = new Dictionary<string, string>();

	private const int PtzKindMove = 0;

	private const int PtzKindStop = 1;

	private const int PtzKindGetPresets = 2;

	private const int PtzKindGotoPreset = 3;

	private const int PtzKindSetPreset = 4;

	private readonly object _ptzGate = new object();

	private volatile bool _ptzBusy;

	private PtzJob _ptzPending;

	private System.Windows.Forms.Timer _ptzHold;

	private volatile bool _ptzHeld;

	private float _ptzHoldPan;

	private float _ptzHoldTilt;

	private float _ptzHoldZoom;

	private const int PollIntervalVisibleMs = 1000;

	private const int PollIntervalHiddenMs = 5000;

	private int _pollInFlight;

	private volatile bool _pollFast = true;

	private int _autoDiscoverGapMs = 20000;

	private string _discoverDiag = "";

	private bool _mcastRunning;

	private bool DirectReady
	{
		get
		{
			try
			{
				return _civ != null && _civ.IsOpen;
			}
			catch
			{
				return false;
			}
		}
	}

	private bool DirectLinkIsCom
	{
		get
		{
			if (_rbDirectCom != null)
			{
				return _rbDirectCom.Checked;
			}
			return false;
		}
	}

	private bool ChannelIsDirect => !string.Equals(_rigChannel, "board", StringComparison.OrdinalIgnoreCase);

	private static StatusDot MkDot(string caption)
	{
		StatusDot statusDot = new StatusDot();
		statusDot.Height = 24;
		statusDot.Margin = new Padding(4, 8, 4, 2);
		statusDot.Font = Theme.FSmall;
		statusDot.Set(DotState.Unknown, caption);
		return statusDot;
	}

	private static FlowLayoutPanel AddStatusRow(FlowLayoutPanel col, StatusDot dot, string groupTitle)
	{
		FlowLayoutPanel flowLayoutPanel = Row();
		flowLayoutPanel.WrapContents = false;
		Label label = Theme.SectionLabel(groupTitle);
		label.AutoSize = false;
		label.Height = 24;
		label.Margin = new Padding(0, 5, 0, 5);
		label.Font = Theme.FSmall;
		flowLayoutPanel.Controls.Add(label);
		dot.Height = 24;
		dot.Margin = new Padding(14, 5, 4, 5);
		dot.NoCaption();
		dot.FixWidth(28);
		flowLayoutPanel.Controls.Add(dot);
		SetFlowSize(col, flowLayoutPanel);
		col.Controls.Add(flowLayoutPanel);
		FitFlowRowHeight(flowLayoutPanel);
		return flowLayoutPanel;
	}

	public void DumpControlTree(string path)
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("ClientSize=" + base.ClientSize.Width + "x" + base.ClientSize.Height);
			string[] obj = new string[14]
			{
				"ThemeKey=",
				Theme.P.Key,
				" Light=",
				Theme.IsLight.ToString(),
				" Text=",
				null,
				null,
				null,
				null,
				null,
				null,
				null,
				null,
				null
			};
			Color color = Theme.P.Text;
			obj[5] = color.ToString();
			obj[6] = " TextDim=";
			color = Theme.P.TextDim;
			obj[7] = color.ToString();
			obj[8] = " UIFont=";
			obj[9] = Theme.FBody.Name;
			obj[10] = "/";
			obj[11] = Theme.FBody.SizeInPoints.ToString("0.#");
			obj[12] = "/";
			obj[13] = Theme.FBody.Style.ToString();
			stringBuilder.AppendLine(string.Concat(obj));
			stringBuilder.AppendLine("BackgroundImage=" + ((BackgroundImage == null) ? "null" : (BackgroundImage.Width + "x" + BackgroundImage.Height)) + " Layout=" + BackgroundImageLayout);
			DumpNode(this, this, 0, stringBuilder);
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("==== 文字超宽（可能被裁成半个字）====");
			DumpTextOverflow(this, stringBuilder);
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("==== 跨容器重叠（含 Visible=false 的状态文字按假定可见参与）====");
			stringBuilder.AppendLine("重叠数 = " + DumpCrossOverlap(this, stringBuilder));
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("==== 最小间距体检（相邻控件水平间距 < 10px）====");
			stringBuilder.AppendLine("紧贴数 = " + DumpTightSpacing(this, stringBuilder));
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("==== 状态灯—文字间距体检（要求 ≥ 12px）====");
			stringBuilder.AppendLine("灯字间距不足数 = " + DumpDotGaps(this, stringBuilder));
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("==== 按钮宽度体检（宽度必须 ≥ 实测文字宽 + 图标位）====");
			stringBuilder.AppendLine("按钮文字放不下数 = " + DumpButtonFit(this, stringBuilder));
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("==== 自绘单选/复选体检（圆点+间距+文字必须放得下）====");
			stringBuilder.AppendLine("单选错位数 = " + DumpToggleFit(this, stringBuilder));
			File.WriteAllText(path, stringBuilder.ToString(), Encoding.UTF8);
		}
		catch (Exception ex)
		{
			try
			{
				File.WriteAllText(path, "DUMP-ERROR: " + ex.GetType().Name + ": " + ex.Message + "\r\n" + ex.StackTrace, Encoding.UTF8);
			}
			catch
			{
			}
		}
	}

	private static int DumpCrossOverlap(Control root, StringBuilder sb)
	{
		List<Control> list = new List<Control>();
		CollectLeaves(root, list);
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			for (int j = i + 1; j < list.Count; j++)
			{
				Control control = list[i];
				Control control2 = list[j];
				if (control.Parent == control2.Parent || control.Parent == null || control2.Parent == null || IsUnder(control, control2.Parent) || IsUnder(control2, control.Parent) || !SameVisiblePage(control, control2))
				{
					continue;
				}
				Rectangle rectangle = control.Parent.RectangleToScreen(control.Bounds);
				Rectangle rect = control2.Parent.RectangleToScreen(control2.Bounds);
				if (rectangle.IntersectsWith(rect))
				{
					int num2 = Math.Min(rectangle.Right, rect.Right) - Math.Max(rectangle.Left, rect.Left);
					int num3 = Math.Min(rectangle.Bottom, rect.Bottom) - Math.Max(rectangle.Top, rect.Top);
					if (num2 >= 3 && num3 >= 3)
					{
						num++;
						sb.AppendLine("重叠 " + num2 + "x" + num3 + " : " + Describe(control) + "  <->  " + Describe(control2));
					}
				}
			}
		}
		if (num == 0)
		{
			sb.AppendLine("（无）");
		}
		return num;
	}

	private static int DumpTightSpacing(Control root, StringBuilder sb)
	{
		int hits = 0;
		TightWalk(root, sb, ref hits);
		if (hits == 0)
		{
			sb.AppendLine("（无）");
		}
		return hits;
	}

	private static void TightWalk(Control parent, StringBuilder sb, ref int hits)
	{
		if (parent == null)
		{
			return;
		}
		if (parent is FlowLayoutPanel flowLayoutPanel && AncestorsAllVisible(flowLayoutPanel))
		{
			List<Control> list = new List<Control>();
			foreach (Control control4 in flowLayoutPanel.Controls)
			{
				if (control4.Visible)
				{
					list.Add(control4);
				}
			}
			list.Sort((Control a, Control b) => a.Left.CompareTo(b.Left));
			for (int i = 0; i + 1 < list.Count; i++)
			{
				Control control2 = list[i];
				Control control3 = list[i + 1];
				int num = control3.Left - control2.Right;
				if (num >= 0 && num < 10 && !IsAllowedTight(control2, control3))
				{
					hits++;
					if (hits <= 10)
					{
						sb.AppendLine("紧贴 " + num + "px : " + Describe(control2) + "  <->  " + Describe(control3));
					}
				}
			}
		}
		foreach (Control control5 in parent.Controls)
		{
			TightWalk(control5, sb, ref hits);
		}
	}

	private static bool IsAllowedTight(Control a, Control b)
	{
		bool num = a is TrackBar;
		bool flag = b is TrackBar;
		if (num && b is Label)
		{
			return true;
		}
		if (flag && a is Label)
		{
			return true;
		}
		if (SameOwner(a, b))
		{
			return true;
		}
		return false;
	}

	private static bool SameOwner(Control a, Control b)
	{
		if (a.Parent != null && a.Parent == b.Parent)
		{
			if (!(a.Parent is NumericUpDown))
			{
				return a.Parent is UpDownBase;
			}
			return true;
		}
		return false;
	}

	private static int DumpDotGaps(Control root, StringBuilder sb)
	{
		int num = 0;
		List<Control> list = new List<Control>();
		CollectDots(root, list);
		foreach (Control item in list)
		{
			if (item.Parent == null)
			{
				continue;
			}
			int num2 = int.MaxValue;
			Control control = null;
			foreach (Control control2 in item.Parent.Controls)
			{
				if (control2 != item && control2.Visible && !string.IsNullOrEmpty(control2.Text))
				{
					int num3 = ((control2.Left < item.Left) ? (item.Left - control2.Right) : (control2.Left - item.Right));
					if (num3 < num2)
					{
						num2 = num3;
						control = control2;
					}
				}
			}
			if (control != null && num2 < 12)
			{
				num++;
				sb.AppendLine("灯字间距 " + num2 + "px (<12) : " + Describe(control) + "  <->  StatusDot");
			}
		}
		if (num == 0)
		{
			sb.AppendLine("（全部达标）");
		}
		return num;
	}

	private static void CollectDots(Control root, List<Control> outList)
	{
		if (root == null)
		{
			return;
		}
		foreach (Control control in root.Controls)
		{
			if (control is StatusDot && control.Visible && AncestorsAllVisible(control))
			{
				outList.Add(control);
			}
			CollectDots(control, outList);
		}
	}

	private static int DumpButtonFit(Control root, StringBuilder sb)
	{
		int num = 0;
		List<Control> list = new List<Control>();
		CollectButtons(root, list);
		foreach (Control item in list)
		{
			string text = item.Text;
			if (!string.IsNullOrEmpty(text))
			{
				Size size = TextRenderer.MeasureText(text, item.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
				int num2 = Math.Min(24, Math.Max(0, item.ClientSize.Height - 8)) + 12;
				bool flag = item is Button && item.ClientSize.Width - 7 - num2 - 5 < size.Width;
				int num3 = size.Width + ((!(item is Button)) ? 6 : (flag ? 10 : 46));
				if (item.Width < num3)
				{
					num++;
					sb.AppendLine("按钮过窄 " + item.Width + "px < 需要 " + num3 + "px : '" + text + "'");
				}
			}
		}
		if (num == 0)
		{
			sb.AppendLine("（全部达标）");
		}
		return num;
	}

	private static void CollectButtons(Control root, List<Control> outList)
	{
		if (root == null)
		{
			return;
		}
		foreach (Control control in root.Controls)
		{
			if (control is Button && control.Visible && AncestorsAllVisible(control))
			{
				outList.Add(control);
			}
			CollectButtons(control, outList);
		}
	}

	private static int DumpToggleFit(Control root, StringBuilder sb)
	{
		int num = 0;
		List<Control> list = new List<Control>();
		CollectToggles(root, list);
		foreach (Control item in list)
		{
			int num2 = 24 + TextRenderer.MeasureText(item.Text ?? "", item.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width + 4;
			if (item.Width < num2)
			{
				num++;
				sb.AppendLine("单选过窄 " + item.Width + "px < 需要 " + num2 + "px : '" + item.Text + "'（文字起点固定 " + 24 + "px）");
			}
			if (item.Parent == null)
			{
				continue;
			}
			foreach (Control control in item.Parent.Controls)
			{
				if (control != item && control.Visible)
				{
					int num3 = ((control.Left < item.Left) ? (item.Left - control.Right) : (control.Left - item.Right));
					if (num3 >= 0 && num3 < 10 && Math.Abs(control.Top - item.Top) <= 8)
					{
						num++;
						sb.AppendLine("单选间距 " + num3 + "px (<10) : '" + item.Text + "'  <->  " + Describe(control));
					}
				}
			}
		}
		if (num == 0)
		{
			sb.AppendLine("（全部达标：圆点与文字分开、间距 ≥10px）");
		}
		return num;
	}

	private static void CollectToggles(Control root, List<Control> outList)
	{
		if (root == null)
		{
			return;
		}
		foreach (Control control in root.Controls)
		{
			if ((control is RadioButton || control is CheckBox) && control.Visible && AncestorsAllVisible(control))
			{
				outList.Add(control);
			}
			CollectToggles(control, outList);
		}
	}

	private static string Describe(Control c)
	{
		string text = (c.Text ?? "").Replace("\r", " ").Replace("\n", " ");
		if (text.Length > 16)
		{
			text = text.Substring(0, 16) + "…";
		}
		string text2 = (c.Visible ? "" : "(隐藏,按假定可见计)");
		return c.GetType().Name + text2 + " '" + text + "'";
	}

	private static bool IsTextish(Control c)
	{
		if (c is Label || c is Button || c is TextBox || c is ComboBox || c is StatusDot || c is TrackBar || c is ListView || c is NumericUpDown || c is ProgressBar || c is RadioButton || c is CheckBox)
		{
			return true;
		}
		string name = c.GetType().Name;
		switch (name)
		{
		default:
			return name == "NeoGroupLabel";
		case "RigDisplay":
		case "CompassDial":
		case "ElevationGauge":
		case "StatusLamp":
		case "BarMeter":
			return true;
		}
	}

	private static void CollectLeaves(Control root, List<Control> outList)
	{
		if (root == null)
		{
			return;
		}
		foreach (Control control in root.Controls)
		{
			if (IsTextish(control) && AncestorsAllVisible(control))
			{
				outList.Add(control);
			}
			CollectLeaves(control, outList);
		}
	}

	private static bool AncestorsAllVisible(Control c)
	{
		for (Control control = c?.Parent; control != null; control = control.Parent)
		{
			if (!control.Visible)
			{
				return false;
			}
			if (control is Form)
			{
				break;
			}
		}
		return true;
	}

	private static bool IsUnder(Control c, Control anc)
	{
		for (Control control = c; control != null; control = control.Parent)
		{
			if (control == anc)
			{
				return true;
			}
		}
		return false;
	}

	private static bool SameVisiblePage(Control a, Control b)
	{
		return VisiblePageHost(a) == VisiblePageHost(b);
	}

	private static Control VisiblePageHost(Control c)
	{
		if (c == null)
		{
			return null;
		}
		List<Control> list = new List<Control>();
		for (Control control = c; control != null; control = control.Parent)
		{
			list.Add(control);
		}
		if (list.Count == 0)
		{
			return null;
		}
		int num = -1;
		for (int num2 = list.Count - 1; num2 >= 0; num2--)
		{
			Control control2 = list[num2];
			if (!(control2 is Form) && control2.Visible)
			{
				num = num2;
				break;
			}
		}
		if (num < 0)
		{
			return list[list.Count - 1];
		}
		int num3 = num;
		while (num3 - 1 >= 1)
		{
			Control control3 = list[num3 - 1];
			Control control4 = list[num3];
			if (!control3.Visible || (!(control4 is TableLayoutPanel) && !(control4 is FlowLayoutPanel) && !(control4.GetType() == typeof(Panel))) || control4.Controls.Count <= 1)
			{
				break;
			}
			num3--;
		}
		return list[num3];
	}

	private static void DumpTextOverflow(Control root, StringBuilder sb)
	{
		if (root == null)
		{
			return;
		}
		string text = root.Text;
		if (!string.IsNullOrEmpty(text) && root.Visible)
		{
			bool flag = false;
			if (root is Label label)
			{
				flag = label.AutoSize;
			}
			if (!flag)
			{
				Button button = root as Button;
				int num = root.ClientSize.Width - ((button != null) ? 46 : 6);
				Size size = TextRenderer.MeasureText(text, root.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
				if (button != null)
				{
					int num2 = Math.Min(24, Math.Max(0, root.ClientSize.Height - 8)) + 12;
					if (root.ClientSize.Width - 7 - num2 - 5 < size.Width)
					{
						num = root.ClientSize.Width - 10;
					}
				}
				if (size.Width > num && num > 0)
				{
					sb.AppendLine(root.GetType().Name + " 需要 " + size.Width + "px / 可用 " + num + "px   '" + text.Replace("\r", " ").Replace("\n", " ") + "'");
				}
			}
		}
		foreach (Control control in root.Controls)
		{
			DumpTextOverflow(control, sb);
		}
	}

	private static void DumpNode(Control c, Form f, int depth, StringBuilder sb)
	{
		if (c == null)
		{
			return;
		}
		Point point;
		try
		{
			point = f.PointToClient(c.PointToScreen(Point.Empty));
		}
		catch
		{
			point = c.Location;
		}
		string text = (c.Text ?? "").Replace("\r", " ").Replace("\n", " ");
		if (text.Length > 30)
		{
			text = text.Substring(0, 30) + "…";
		}
		bool flag = false;
		if (c.Parent != null)
		{
			Rectangle clientRectangle = c.Parent.ClientRectangle;
			Rectangle rect = new Rectangle(c.Left, c.Top, c.Width, c.Height);
			flag = !clientRectangle.IntersectsWith(rect);
		}
		string text2 = "";
		if (c.Parent != null && c.Visible)
		{
			int num = 0;
			foreach (Control control in c.Parent.Controls)
			{
				num++;
				if (control == c || !control.Visible)
				{
					continue;
				}
				Rectangle bounds = c.Bounds;
				Rectangle bounds2 = control.Bounds;
				if (bounds.IntersectsWith(bounds2))
				{
					int num2 = Math.Min(bounds.Right, bounds2.Right) - Math.Max(bounds.Left, bounds2.Left);
					int num3 = Math.Min(bounds.Bottom, bounds2.Bottom) - Math.Max(bounds.Top, bounds2.Top);
					if (num2 > 3 && num3 > 3)
					{
						text2 = text2 + ((text2.Length == 0) ? "" : ",") + num + "(" + num2 + "x" + num3 + ")";
					}
				}
			}
		}
		sb.Append(new string(' ', depth * 2)).Append(c.GetType().Name).Append(" vis=")
			.Append(c.Visible ? "1" : "0")
			.Append(" @")
			.Append(point.X)
			.Append(",")
			.Append(point.Y)
			.Append(" ")
			.Append(c.Width)
			.Append("x")
			.Append(c.Height);
		if (flag)
		{
			sb.Append("  <<CLIPPED-OUTSIDE-PARENT>>");
		}
		if (text2.Length > 0)
		{
			sb.Append("  <<OVERLAP ").Append(text2).Append(">>");
		}
		if (text.Length > 0)
		{
			sb.Append("  '").Append(text).Append("'");
		}
		sb.AppendLine();
		foreach (Control control2 in c.Controls)
		{
			DumpNode(control2, f, depth + 1, sb);
		}
	}

	private static string ReadYzVer()
	{
		try
		{
			Version version = Assembly.GetExecutingAssembly().GetName().Version;
			return "v" + version.Major + "." + version.Minor + "." + version.Build;
		}
		catch
		{
			return "v1.0.0";
		}
	}

	private static void SplitSys(string sys, out string ver, out string bld)
	{
		ver = null;
		bld = null;
		if (string.IsNullOrEmpty(sys))
		{
			return;
		}
		string[] array = sys.Split(';');
		for (int i = 0; i < array.Length; i++)
		{
			int num = array[i].IndexOf('=');
			if (num > 0)
			{
				string text = array[i].Substring(0, num).Trim();
				string text2 = array[i].Substring(num + 1).Trim();
				if (text == "ver" && ver == null)
				{
					ver = text2;
				}
				else if (text == "bld" && bld == null)
				{
					bld = text2;
				}
			}
		}
	}

	private void UpdateFirmwareLabels()
	{
		string text = FirmwareText(_lastStatus);
		if (text.Length == 0)
		{
			text = "固件：尚未读到（点「刷新 /sys」或「测试连接」）";
		}
		if (_lblFwRot != null)
		{
			_lblFwRot.Text = text;
			_lblFwRot.ForeColor = Theme.Accent;
		}
		if (_lblFwOta != null)
		{
			_lblFwOta.Text = text;
			_lblFwOta.ForeColor = Theme.Accent;
		}
	}

	private string FirmwareText(YzStatus st)
	{
		string text = st?.Ver;
		string fwBldCache = _fwBldCache;
		if (string.IsNullOrEmpty(text))
		{
			text = _fwVerCache;
		}
		if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(fwBldCache))
		{
			return "";
		}
		if (string.IsNullOrEmpty(fwBldCache))
		{
			return "固件 " + text;
		}
		if (string.IsNullOrEmpty(text))
		{
			return "固件 build " + fwBldCache;
		}
		return "固件 " + text + " / build " + fwBldCache;
	}

	public MainForm()
		: this(0)
	{
	}

	public MainForm(int startPage)
	{
		_startPage = startPage;
		Text = "YiZhou 控制中枢  " + YzAppVer;
		base.Width = 1280;
		base.Height = 800;
		MinimumSize = new Size(1024, 680);
		base.StartPosition = FormStartPosition.CenterScreen;
		_cfg = CameraStore.Load();
		string text = (_cfg.S3Host ?? "").Trim();
		if (S3Discovery.IsUsableIp(text) || S3Discovery.IsUsableHostName(text))
		{
			_s3.Host = text;
		}
		else
		{
			_s3.Host = "192.168.4.1";
		}
		_boardSeen = (_cfg.S3HostSeen ?? "").Trim();
		if (!string.IsNullOrEmpty(CameraStore.LastError))
		{
			string cfgWarn = CameraStore.LastError;
			RunBg(delegate
			{
				SetMsg("⚠ 配置告警：" + cfgWarn);
			});
		}
		BuildUi();
		// v1.5.1：监控页的控件与海康 SDK 都改为「第一次切到监控页」时才加载（见 OnMonitorPageEnter）。
		// 这里不再 LoadMonitorWindows() / UpdatePaneLabels()，启动阶段完全不碰 VideoSDK。
		SetupTray();
		_bgTimer = new System.Windows.Forms.Timer();
		_bgTimer.Interval = 60;
		_bgTimer.Tick += delegate
		{
			_bgTimer.Stop();
			LayoutBackdrop();
		};
		SetupTheme();
		base.Resize += delegate
		{
			if (_bgTimer != null)
			{
				_bgTimer.Stop();
				_bgTimer.Start();
			}
		};
		base.Load += delegate
		{
			LayoutBackdrop();
			if (!string.IsNullOrEmpty(DumpUiPath))
			{
				BeginInvoke((MethodInvoker)delegate
				{
					try
					{
						ShowNav(_startPage);
					}
					catch
					{
					}
					DumpControlTree(DumpUiPath);
				});
			}
			TakePriorityIfNeeded();
			RefreshSys();
			UpdateFirmwareLabels();
			UpdateSpeedEditLabels();
			// v1.5.1 启动减负：电台轮询/网络配置/以太网查询/组播设备枚举/OTA 门禁
			// 全部移到各自页签首次打开时（OnRadioPageEnter / OnSystemPageEnter），
			// 启动阶段只保留一条 /sys 读（后台线程，不阻塞界面）。
			SetMsg("就绪：主界面已可用（监控/电台/系统页首次点击时才加载对应资源）");
		};
		base.FormClosing += delegate(object s, FormClosingEventArgs e)
		{
			if (!_reallyExit && e.CloseReason == CloseReason.UserClosing)
			{
				e.Cancel = true;
				base.WindowState = FormWindowState.Minimized;
				SetMsg("已最小化到任务栏（程序未退出）；右键托盘图标 → 退出 可真正关闭");
				if (_tray != null && !_trayTipShown)
				{
					_trayTipShown = true;
					try
					{
						_tray.ShowBalloonTip(3500, "已最小化到任务栏", "程序仍在后台运行。要真正退出：右键任务栏右下角的托盘图标 → 退出（或用工具栏「退出」按钮）。", ToolTipIcon.Info);
					}
					catch
					{
					}
				}
			}
			else
			{
				Shutdown();
				if (_tray != null)
				{
					try
					{
						_tray.Visible = false;
						_tray.Dispose();
					}
					catch
					{
					}
					_tray = null;
				}
			}
		};
	}

	private IVideoEngine GetEngine(int pane, string url)
	{
		IVideoEngine videoEngine = CreateEngine(url);
		IVideoEngine videoEngine2 = ((pane == 0) ? _s1 : _s2);
		if (videoEngine2 != null && videoEngine2.GetType() == videoEngine.GetType())
		{
			return videoEngine2;
		}
		if (videoEngine2 != null)
		{
			try
			{
				videoEngine2.Dispose();
			}
			catch
			{
			}
		}
		videoEngine.Started += delegate
		{
			SetMsg("窗口" + (pane + 1) + " 已出图");
		};
		videoEngine.Failed += delegate(object s, string m)
		{
			SetMsg("窗口" + (pane + 1) + "：" + m);
		};
		if (pane == 0)
		{
			_s1 = videoEngine;
		}
		else
		{
			_s2 = videoEngine;
		}
		return videoEngine;
	}

	private static IVideoEngine CreateEngine(string url)
	{
		if (!string.IsNullOrEmpty(url) && url.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) && VlcEngine.Available)
		{
			return new VlcPreviewSession();
		}
		return new HikPreviewSession();
	}

	private void ReadProto()
	{
		RunBg(delegate
		{
			string text = _s3.Get("/proto");
			if (text == null)
			{
				SetMsgSafe("读取协议失败：" + _s3.LastError);
			}
			else
			{
				string s = "0";
				string[] array = text.Trim().Split(';');
				for (int i = 0; i < array.Length; i++)
				{
					if (array[i].StartsWith("p="))
					{
						s = array[i].Substring(2);
					}
				}
				int result = -1;
				int.TryParse(s, out result);
				int vv = result;
				string raw = text.Trim();
				UiInvoke(delegate
				{
					if (vv >= 0 && vv < _cboProto.Items.Count)
					{
						_cboProto.SelectedIndex = vv;
					}
					SetMsg("当前协议：" + _cboProto.Text + "（原始返回 " + raw + "）");
				});
			}
		});
	}

	private void ApplyProto()
	{
		if (_cboProto.SelectedIndex >= 0)
		{
			int idx = _cboProto.SelectedIndex;
			string name = _cboProto.Text;
			RunBg(delegate
			{
				string text = _s3.Get("/proto?p=" + idx);
				SetMsg((text != null && text.Trim() == "OK") ? ("协议已切换为 " + name + "（TCP 4533 立即生效）") : ("切换失败：" + (text ?? _s3.LastError)));
			});
		}
	}

	private void RebootDevice()
	{
		if (MessageBox.Show("确定要重启 S3 中转站吗？\r\n\r\n重启期间云台、电台、摄像头都会短暂断开（约 10 秒）。", "硬件重启确认", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
		{
			RunBg(delegate
			{
				string text = _s3.Get("/cmd?g=RB");
				SetMsg((text != null) ? "已下发硬件重启指令，设备将在 3 秒后重启" : ("重启指令失败：" + _s3.LastError));
			});
		}
	}

	private void StartMini()
	{
		if (_mini == null)
		{
			return;
		}
		int num = ((_cfg.Pane1 >= 0 && _cfg.Pane1 < _cfg.Cameras.Count) ? _cfg.Pane1 : ((_cfg.Cameras.Count <= 0) ? (-1) : 0));
		if (num < 0)
		{
			// v1.5.2：没有配置摄像头时，视频窗要给出**明确状态**（不能空白，也不能卡死）
			if (_lblMini != null)
			{
				_lblMini.Text = "第一画面：未配置 —— 请到「监控（摄像机）」页填地址+账号密码并播放一次";
				_lblMini.ForeColor = HudDraw.Warn;
			}
			if (_mini != null)
			{
				_mini.Invalidate();
			}
			SetMsg("还没有配置摄像头：请到「监控（摄像机）」页把窗口1/窗口2 的地址+账号密码添加一次");
			return;
		}
		CameraEntry cam = _cfg.Cameras[num];
		_lblMini.Text = "第一画面：" + cam.Name + " 解析取流地址…";
		int seq = ++_miniSeq;
		RunBg(delegate
		{
			string note;
			List<string> urls = BuildUrlCandidatesBg(cam, out note);
			if (note.Length > 0)
			{
				SetMsgSafe(note);
			}
			if (urls.Count == 0)
			{
				SetMsgSafe("第一画面：无法生成取流地址（检查 IP / XAddr）");
			}
			UiInvoke(delegate
			{
				if (_mini != null && seq == _miniSeq)
				{
					if (urls.Count == 0)
					{
						_lblMini.Text = "第一画面：无法生成取流地址";
					}
					else
					{
						StopMini();
						for (int i = 0; i < urls.Count; i++)
						{
							IVideoEngine videoEngine = CreateEngine(urls[i]);
							if (videoEngine.Start(urls[i], _mini.Handle))
							{
								_miniEngine = videoEngine;
								_lblMini.Text = "第一画面：" + cam.Name + "  [" + videoEngine.EngineName + "]";
								SetMsg("第一画面已启动（" + videoEngine.EngineName + "）");
								return;
							}
						}
						_lblMini.Text = "第一画面：启动失败";
						SetMsg("第一画面启动失败（检查摄像头账号密码）");
					}
				}
			});
		});
	}

	private void StopMini()
	{
		if (_miniEngine != null)
		{
			try
			{
				_miniEngine.Stop();
				_miniEngine.Dispose();
			}
			catch
			{
			}
			_miniEngine = null;
		}
		if (_lblMini != null)
		{
			_lblMini.Text = "第一画面：未启动";
			_lblMini.ForeColor = HudDraw.Cyan;
		}
		if (_mini != null)
		{
			_mini.Invalidate();
		}
		// v1.5.2：用户点「停止画面」后，若监控页两个窗口也没在播 → 释放 libvlc 实例
		// （只留 DLL 映射的代码页，托管/私有内存回到基线，实测 67MB → ~40MB 级）
		ReleaseVideoEnginesIfIdle();
	}

	/// <summary>没有任一路视频在播时，释放 libvlc / 海康 SDK 的运行实例（不卸载 DLL，DLL 由系统管理）。</summary>
	private void ReleaseVideoEnginesIfIdle()
	{
		try
		{
			if (_miniEngine != null || _s1 != null || _s2 != null)
			{
				return;
			}
			VlcEngine.Release();
			try
			{
				HikVideoSdk.Shutdown();
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	private void ShowNav(int i)
	{
		if (_pages != null && i >= 0 && i < _pages.Length)
		{
			int prev = _currentPage;
			if (prev != i)
			{
				LeavePage(prev);
			}
			EnsurePage(i);
			for (int j = 0; j < _pages.Length; j++)
			{
				_pages[j].Visible = j == i;
			}
			if (_hdr != null)
			{
				_hdr.Set(NavIcons[i], NavTitles[i], NavSubs[i]);
			}
			if (_rail != null && _rail.SelectedIndex != i)
			{
				_rail.SelectedIndex = i;
			}
			_currentPage = i;
			LayoutHost();
			EnterPage(i);
		}
	}

	/// <summary>
	/// v1.5.1：按需构建页签。页签 0 在启动时构建（主界面），其余三页首次访问才构建。
	/// 未构建的页签里所有控件引用都是 null —— 所有后台刷新方法都判断对应字段是否为 null
	/// 或用 _pageBuilt[x] 提前返回，因此不会出现 NullReferenceException。
	/// </summary>
	private void EnsurePage(int i)
	{
		if (i < 0 || i >= 5 || _pageBuilt[i])
		{
			return;
		}
		_pageBuilt[i] = true;
		Panel content = null;
		Stopwatch sw = Stopwatch.StartNew();
		_pages[i].SuspendLayout();
		try
		{
			switch (i)
			{
			case 0:
				content = BuildRotatorTab();
				break;
			case 1:
				content = BuildRadioTab();
				break;
			case 2:
				content = BuildMonitorTab();
				break;
			case 3:
				content = BuildRtspTab();
				break;
			case 4:
			{
				// v1.7：卫星追踪改成内置（不再启动 OrbitLink）；把云台客户端与电台链路交给追踪页
				content = TrackerPage.Build(_s3, CivForTracking, RigKindForTracking);
				break;
			}
			}
			if (content != null)
			{
				content.Dock = DockStyle.Fill;
				_pages[i].Controls.Add(content);
			}
		}
		catch (Exception ex)
		{
			// 失败时不要留下"空白且标记为已构建"的页：回滚标记，隔一个消息循环重试（最多 2 次）。
			// 实测这是 GDI+ 在鼠标消息上下文里设置 Font 时的暂时性 InvalidParameter，
			// 重试即可正常建出整页。
			_pageBuilt[i] = false;
			_pageBuildRetry[i]++;
			// 现场自愈：清掉可能已失效的缓存字体，下一次重试用全新 Font 实例
			try
			{
				Theme.HealFontCache();
			}
			catch
			{
			}
			try
			{
				System.IO.File.AppendAllText(
					System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yz-pagebuild.txt"),
					DateTime.Now.ToString("s") + " page=" + i + " try=" + _pageBuildRetry[i] + Environment.NewLine + ex + Environment.NewLine + Environment.NewLine);
			}
			catch
			{
			}
			if (_pageBuildRetry[i] <= 2)
			{
				SetMsg("页面构建遇到 GDI+ 暂时性失败，正在自动重试（第 " + _pageBuildRetry[i] + " 次）：" + ex.Message);
				BeginInvoke((MethodInvoker)delegate
				{
					try
					{
						if (_pages != null && i >= 0 && i < _pages.Length)
						{
							EnsurePage(i);
							if (_pageBuilt[i])
							{
								for (int j = 0; j < _pages.Length; j++)
								{
									_pages[j].Visible = j == _currentPage;
								}
								LayoutHost();
								EnterPage(i);
							}
						}
					}
					catch
					{
					}
				});
			}
			else
			{
				SetMsg("页面构建失败（已重试）：" + ex.Message);
			}
		}
		finally
		{
			_pages[i].ResumeLayout(performLayout: true);
			sw.Stop();
			_pageBuildMs[i] = (int)sw.ElapsedMilliseconds;
		}
		// FlowLayoutPanel 在构建期间内容宽度会变化，Windows 可能残留一条**多余的横向滚动条**；
		// 布局稳定后重新开关一次 AutoScroll 让它按当前内容重算（只在真正需要时才出现）。
		Panel built = _pages[i];
		MethodInvoker normalize = delegate
		{
			try
			{
				NormalizeScrollbars(built);
			}
			catch
			{
			}
		};
		if (base.IsHandleCreated)
		{
			BeginInvoke(normalize);
		}
		else
		{
			// 构造期（句柄还没建好）不能 BeginInvoke —— 等句柄创建后再做一次
			EventHandler once = null;
			once = delegate
			{
				base.HandleCreated -= once;
				try
				{
					BeginInvoke(normalize);
				}
				catch
				{
				}
			};
			base.HandleCreated += once;
		}
	}

	private static void NormalizeScrollbars(Control root)
	{
		if (root == null)
		{
			return;
		}
		foreach (Control c in root.Controls)
		{
			if (c is FlowLayoutPanel flp && flp.AutoScroll)
			{
				try
				{
					flp.AutoScroll = false;
					flp.AutoScroll = true;
					flp.AutoScrollMinSize = Size.Empty;
					// 关键：构建期的行宽是按"默认 560"算的，之后列宽确定（还多了竖向滚动条）
					// 不会自动回算 → 滚动范围里就多出 20 多像素，Windows 于是在底部画一条
					// 灰色横向滚动条。这里按当前真实宽度把每一行重新同步一次。
					foreach (Control ch in flp.Controls)
					{
						if (ch is FlowLayoutPanel)
						{
							SyncRowWidth(flp, ch);
						}
					}
					flp.PerformLayout();
					if (flp.HorizontalScroll.Visible)
					{
						flp.HorizontalScroll.Visible = false;
					}

				}
				catch
				{
				}
			}
			if (c.HasChildren)
			{
				NormalizeScrollbars(c);
			}
		}
	}

	/// <summary>进入某页时的惰性初始化（重资源只在这里加载）。</summary>
	private void EnterPage(int i)
	{
		try
		{
			switch (i)
			{
			case 0:
				OnRotatorPageEnter();
				break;
			case 1:
				OnRadioPageEnter();
				break;
			case 2:
				OnMonitorPageEnter();
				break;
			case 3:
				OnSystemPageEnter();
				break;
			}
		}
		catch (Exception ex)
		{
			SetMsg("打开第 " + (i + 1) + " 页时出错：" + ex.Message);
		}
	}

	/// <summary>离开某页时释放重资源（视频/音频/轮询），回到主界面不再占用。</summary>
	private void LeavePage(int i)
	{
		try
		{
			switch (i)
			{
			case 0:
				OnRotatorPageLeave();
				break;
			case 1:
				OnRadioPageLeave();
				break;
			case 2:
				OnMonitorPageLeave();
				break;
			}
		}
		catch
		{
		}
	}

	/// <summary>
	/// v1.5.2：离开云台页 → 停止「第一画面」并释放播放器。
	/// 只有监控页也在播（有 _s1/_s2）时才保留 libvlc 实例，否则 Release() 让内存回到基线。
	/// </summary>
	private void OnRotatorPageLeave()
	{
		try
		{
			StopMini();
		}
		catch
		{
		}
		try
		{
			if (_s1 == null && _s2 == null)
			{
				VlcEngine.Release();
			}
		}
		catch
		{
		}
	}

	private void OnRotatorPageEnter()
	{
		// 第一页常驻：状态轮询与诊断定时器本来就在跑，这里只做一次“补一次数据”，
		// 让从别的页切回来时数字立刻是新的（不必等下一个 1s/15s 周期）。
		RefreshSys();
		RefreshDiagNow();
		// v1.5.2：首次显示后布局会再跑 1~2 轮，横向滚动条可能在此时重新出现 —— 600ms 后统一收一次。
		try
		{
			System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
			t.Interval = 600;
			t.Tick += delegate
			{
				t.Stop();
				t.Dispose();
				KillHScroll(this);
			};
			t.Start();
		}
		catch
		{
		}
	}

	/// <summary>
	/// v1.5.1：主界面「高级诊断」折叠区开关。诊断控件一直在后台刷新（数值始终是最新的），
	/// 只是默认不显示；点一下就在这里展开，不占屏幕也不丢功能。
	/// </summary>
	private void ToggleAdv()
	{
		SetAdvOpen(_advCtrls != null && !_advOpen);
	}

	/// <summary>v1.5.1：电台页「高级链路 / 原始调试」折叠开关（默认收起，只留一套透传/直连）。</summary>
	private void SetRadAdvOpen(bool open)
	{
		_radAdvOpen = open;
		if (_radAdvCtrls != null)
		{
			foreach (Control c in _radAdvCtrls)
			{
				try
				{
					c.Visible = open;
				}
				catch
				{
				}
			}
		}
		if (_radAdvBtn != null)
		{
			_radAdvBtn.Text = open ? "▾ 收起高级链路（S3 板载中继 / 云台板串口 / 手动发帧）" : "▸ 高级链路 / 原始调试（S3 板载中继 · 云台板串口 · 手动发帧）";
		}
		if (_cboBaud != null && _cboBaud.Parent != null)
		{
			try
			{
				_cboBaud.Parent.PerformLayout();
			}
			catch
			{
			}
		}
		SetMsg(open ? "已展开高级链路设置（默认收起，避免与「本机直连」重复）" : "已收起高级链路设置：电台页只保留一套链路（本机直连 ZQWL）");
	}

	private void SetAdvOpen(bool open)
	{
		_advOpen = open;
		if (_advCtrls != null)
		{
			foreach (Control c in _advCtrls)
			{
				try
				{
					c.Visible = open;
				}
				catch
				{
				}
			}
		}
		if (_advBtn != null)
		{
			_advBtn.Text = open ? "▾ 收起高级功能（往返扫描 / 正北校零 / 翻转 / 协议 / DTrac / 看门狗 / 固件诊断）" : "▸ 高级功能 / 诊断（往返扫描 · 正北校零 · 翻转 · 协议 · DTrac · 看门狗）";
		}
		if (_ctrlCol != null)
		{
			try
			{
				_ctrlCol.PerformLayout();
				_ctrlCol.Invalidate(invalidateChildren: true);
			}
			catch
			{
			}
		}
		SetMsg(open ? "已展开高级诊断：固件诊断字段 / 看门狗两层 / ptrace / 复位待命 / 网络通路" : "已收起高级诊断（主界面只保留常用操作）");
	}

	private void OnRadioPageEnter()
	{
		// 电台页：进入才启动轮询定时器（离开即停，见 OnRadioPageLeave）
		if (_rigTimer != null && !_rigTimer.Enabled)
		{
			_rigTimer.Start();
		}
		if (_tRigLvl != null && !_tRigLvl.Enabled)
		{
			_tRigLvl.Start();
		}
		FillMcastDefaultsBg();
		RigReadNet();
		ApplyCivPollMs();
		RefreshChannelToggle();
		RefreshPauseButton();
		RefreshDirectDot();
		RigPollNow(verbose: false);
	}

	private void OnRadioPageLeave()
	{
		// 不在电台页时不轮询电台（省 HTTP/串口流量与 CPU）；PTT 保持定时器不动。
		if (_rigTimer != null)
		{
			_rigTimer.Stop();
		}
		if (_tRigLvl != null)
		{
			_tRigLvl.Stop();
		}
	}

	private void OnMonitorPageEnter()
	{
		// 监控页：第一次进入才加载海康 VideoSDK（v1.5.1 起不在启动时预热）
		if (!_monitorInited)
		{
			_monitorInited = true;
			LoadMonitorWindows();
			UpdatePaneLabels();
			RunBg(delegate
			{
				string err;
				bool ok;
				try
				{
					ok = HikVideoSdk.EnsureInit(out err);
				}
				catch (Exception ex)
				{
					ok = false;
					err = ex.Message;
				}
				if (!ok)
				{
					SetMsgSafe("海康视频 SDK 初始化失败：" + err + "（仅摄像头画面不可用，云台/电台不受影响）");
				}
				else
				{
					SetMsgSafe("监控页就绪：海康 SDK 已按需加载（启动时不再预加载）");
				}
			});
		}
		else
		{
			UpdateMonitorStatus();
		}
	}

	private void OnMonitorPageLeave()
	{
		// 离开监控页：停播并释放播放会话（VLC 只在这页用；mini 画面还在用就保留实例）
		try
		{
			StopPane(0);
		}
		catch
		{
		}
		try
		{
			StopPane(1);
		}
		catch
		{
		}
		try
		{
			if (_s1 != null)
			{
				_s1.Dispose();
				_s1 = null;
			}
			if (_s2 != null)
			{
				_s2.Dispose();
				_s2 = null;
			}
			ReleaseVideoEnginesIfIdle();
			SetMsg("已离开监控页：视频已停止、播放器已释放（下次进入再按需重建）");
		}
		catch
		{
		}
	}

	private void OnSystemPageEnter()
	{
		// 系统页：进入才读 /sys、/otas、复位历史（启动时不再做这些请求）
		RefreshSys();
		ReadOtasNow();
		EthQuery();
		RefreshResetTable();
	}

	private void SetupTheme()
	{
		if (_topBar is HudTopBar hudTopBar)
		{
			hudTopBar.ThemeToggle += delegate
			{
				ToggleTheme();
			};
		}
	}

	private void ToggleTheme()
	{
		bool flag = Theme.Toggle();
		Theme.Apply(this);
		Theme.ClearFontCache();
		if (_toolBar is TopStrip)
		{
			((TopStrip)_toolBar).RefreshTheme();
		}
		if (_status != null)
		{
			Theme.ApplyStatus(_status);
		}
		if (_status != null)
		{
			foreach (ToolStripItem item in _status.Items)
			{
				item.ForeColor = Theme.TextDim;
				item.Font = Theme.FSmall;
			}
			if (_lblS3 != null)
			{
				_lblS3.ForeColor = Theme.Accent;
			}
			if (_lblBoard != null)
			{
				_lblBoard.ForeColor = Theme.Accent;
			}
			if (_lblPrio != null)
			{
				_lblPrio.ForeColor = Theme.Ok;
			}
			if (_lblMsg != null)
			{
				_lblMsg.ForeColor = Theme.Text;
			}
		}
		Theme.ApplyTheme(this);
		Icon3D.ClearCache();
		// v1.5.1：主题色变了 → 丢弃 HudDraw 缓存的网格笔/暗角笔刷，按新配色重建
		HudDraw.ClearCache();
		Invalidate(invalidateChildren: true);
		LayoutBackdrop();
		SetMsg(flag ? "已切换到白天模式（选择已保存，下次启动仍为白天）" : "已切换到黑夜模式（选择已保存，下次启动仍为黑夜）");
	}

	private void LayoutBackdrop()
	{
		try
		{
			Size clientSize = base.ClientSize;
			if (clientSize.Width < 64 || clientSize.Height < 64 || (clientSize == _bgSize && _bgTheme == Theme.P.Key && _bgBmp != null))
			{
				return;
			}
			bool num = clientSize.Width >= 1100;
			Size size = (num ? new Size(clientSize.Width / 2, clientSize.Height) : new Size(clientSize.Width, clientSize.Height));
			if (num && (size.Width < 320 || size.Height < 200))
			{
				size = clientSize;
			}
			Bitmap bgBmp = _bgBmp;
			BackgroundImageLayout = ImageLayout.Stretch;
			_bgBmp = Backdrop.Make(size, Theme.P);
			BackgroundImage = _bgBmp;
			_bgSize = clientSize;
			_bgTheme = Theme.P.Key;
			if (bgBmp == null)
			{
				return;
			}
			try
			{
				bgBmp.Dispose();
			}
			catch
			{
			}
		}
		catch
		{
		}
	}

	private void ToggleFullScreen()
	{
		_fullScreen = !_fullScreen;
		if (_fullScreen)
		{
			_fsBorder = base.FormBorderStyle;
			_fsState = base.WindowState;
			_fsBounds = base.Bounds;
			// ★ v1.5.1 修正：全屏**绝不隐藏顶栏**。
			// 原实现把 _toolBar / _topBar 置 Visible=false，导致全屏后
			//「测试连接 / 重新发现板子 / 读取设备信息 / 全屏 / 退出」整条入口消失
			// （用户报的"顶栏被掩藏"就是这个原因）。顶栏是唯一连接入口，必须一直可见可点。
			if (_toolBar != null)
			{
				_toolBar.Visible = true;
			}
			if (_topBar != null)
			{
				_topBar.Visible = true;
			}
			base.FormBorderStyle = FormBorderStyle.None;
			base.WindowState = FormWindowState.Normal;
			base.TopMost = true;
			// 全屏矩形 = 当前显示器 Bounds（不是 WorkingArea，才真正盖住任务栏），并且**夹在屏幕内**：
			// 宽高不超过屏幕、四边不越界 → 顶栏不会被挤出可见区域。
			Rectangle screen = Screen.FromControl(this).Bounds;
			base.Bounds = ClampToScreen(screen, screen);
			SetMsg("已进入全屏模式（顶栏保留：测试连接 / 重新发现板子 / 读取设备信息 均可用）；按 F11 或 Esc 退出");
		}
		else
		{
			base.TopMost = false;
			base.WindowState = FormWindowState.Normal;
			base.FormBorderStyle = _fsBorder;
			base.Bounds = ClampToScreen(_fsBounds, Screen.FromControl(this).Bounds);
			base.WindowState = _fsState;
			if (_toolBar != null)
			{
				_toolBar.Visible = true;
			}
			if (_topBar != null)
			{
				_topBar.Visible = true;
			}
			SetMsg("已退出全屏模式（窗口尺寸与位置已恢复）");
		}
		LayoutHost();
		LayoutBackdrop();
	}

	/// <summary>把窗口矩形夹进屏幕：宽高不超过屏幕、四边不越界（防止顶栏被挤出可见区域）。</summary>
	private static Rectangle ClampToScreen(Rectangle r, Rectangle screen)
	{
		int w = Math.Min(Math.Max(r.Width, 320), screen.Width);
		int h = Math.Min(Math.Max(r.Height, 240), screen.Height);
		int x = Math.Min(Math.Max(r.X, screen.Left), screen.Right - w);
		int y = Math.Min(Math.Max(r.Y, screen.Top), screen.Bottom - h);
		return new Rectangle(x, y, w, h);
	}

	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		switch (keyData)
		{
		case Keys.F11:
			ToggleFullScreen();
			return true;
		case Keys.Escape:
			if (_fullScreen)
			{
				ToggleFullScreen();
				return true;
			}
			break;
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}

	private void LayoutHost()
	{
		if (_host == null || _hdr == null || _pages == null)
		{
			return;
		}
		int num = _host.ClientSize.Width;
		int num2 = _host.ClientSize.Height;
		if (num > 0 && num2 > 0)
		{
			for (int i = 0; i < _pages.Length; i++)
			{
				_pages[i].SetBounds(0, 0, num, Math.Max(40, num2));
			}
			// v1.5.2：页面尺寸变化后，FlowLayoutPanel 偶尔会重新画出一条"多余的横向滚动条"
			// （内容并没有真正超宽）；每次布局后统一收掉，避免截图/使用中被误认为内容被裁。
			KillHScroll(_host);
		}
	}

	private static void KillHScroll(Control root)
	{
		if (root == null)
		{
			return;
		}
		foreach (Control c in root.Controls)
		{
			// 注意：不只 FlowLayoutPanel —— 外层承载滚动的 Panel 也是 ScrollableControl
			if (c is ScrollableControl sc && sc.AutoScroll && sc.HorizontalScroll.Visible)
			{
				try
				{
					sc.HorizontalScroll.Visible = false;
				}
				catch
				{
				}
			}
			if (c.HasChildren)
			{
				KillHScroll(c);
			}
		}
	}

	private Control BuildInstrumentPanel()
	{
		// v1.5.1 二次精简：云台页右侧**只保留方位罗盘 + 俯仰仪表**。
		// 转速 / 倾角 ADC / 堆 / 融合来源 / 状态灯 / DTrac / 上电自检 这些调试信息不再上屏
		// （对象仍然创建、UpdateInstruments 仍然更新，只是不放进布局 → 不占位、不留空洞）。
		TableLayoutPanel obj = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			BackColor = Theme.Bg,
			ColumnCount = 1,
			RowCount = 2,
			RowStyles = 
			{
				new RowStyle(SizeType.Percent, 58f),
				new RowStyle(SizeType.Percent, 42f)
			}
		};
		_dAz = new CompassDial();
		_dAz.Dock = DockStyle.Fill;
		_dAz.Margin = new Padding(0, 0, 0, 4);
		_dAz.CenterDial = true;
		obj.Controls.Add(_dAz, 0, 0);
		_dEl = new ElevationGauge();
		_dEl.Dock = DockStyle.Fill;
		_dEl.Margin = new Padding(0, 0, 0, 4);
		obj.Controls.Add(_dEl, 0, 1);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 2,
			ColumnStyles = 
			{
				new ColumnStyle(SizeType.Percent, 50f),
				new ColumnStyle(SizeType.Percent, 50f)
			}
		};
		_lampRun = new StatusLamp();
		_lampRun.Dock = DockStyle.Fill;
		_lampRun.Margin = new Padding(0, 0, 3, 3);
		_lampSensor = new StatusLamp();
		_lampSensor.Dock = DockStyle.Fill;
		_lampSensor.Margin = new Padding(0, 0, 0, 3);
		_lampPrio = new StatusLamp();
		_lampPrio.Dock = DockStyle.Fill;
		_lampPrio.Margin = new Padding(0, 0, 3, 0);
		_lampTrack = new StatusLamp();
		_lampTrack.Dock = DockStyle.Fill;
		_lampTrack.Margin = new Padding(0);
		tableLayoutPanel.Controls.Add(_lampRun, 0, 0);
		tableLayoutPanel.Controls.Add(_lampSensor, 1, 0);
		tableLayoutPanel.Controls.Add(_lampPrio, 0, 1);
		tableLayoutPanel.Controls.Add(_lampTrack, 1, 1);
		// v1.5.1: obj.Controls.Add(tableLayoutPanel, 0, 2);	// （仪表已下架，保留对象供刷新逻辑使用）
		_lblDtracDial = new Label();
		_lblDtracDial.Dock = DockStyle.Fill;
		_lblDtracDial.TextAlign = ContentAlignment.MiddleLeft;
		_lblDtracDial.AutoEllipsis = true;
		Theme.StyleLabel(_lblDtracDial, dim: true);
		_lblDtracDial.Text = "DTrac：读取中…";
		// v1.5.1: obj.Controls.Add(_lblDtracDial, 0, 3);	// （仪表已下架，保留对象供刷新逻辑使用）
		_mSpeed = new BarMeter();
		_mSpeed.Dock = DockStyle.Fill;
		_mSpeed.Margin = new Padding(0, 0, 0, 4);
		// v1.5.1: obj.Controls.Add(_mSpeed, 0, 4);	// （仪表已下架，保留对象供刷新逻辑使用）
		_mAdc = new BarMeter();
		_mAdc.Dock = DockStyle.Fill;
		_mAdc.Margin = new Padding(0, 0, 0, 4);
		// v1.5.1: obj.Controls.Add(_mAdc, 0, 5);	// （仪表已下架，保留对象供刷新逻辑使用）
		_mHeap = new BarMeter();
		_mHeap.Dock = DockStyle.Fill;
		_mHeap.Margin = new Padding(0);
		// v1.5.1: obj.Controls.Add(_mHeap, 0, 6);	// （仪表已下架，保留对象供刷新逻辑使用）
		_lblElSrc = new Label();
		_lblElSrc.Dock = DockStyle.Fill;
		_lblElSrc.TextAlign = ContentAlignment.MiddleLeft;
		_lblElSrc.AutoEllipsis = true;
		Theme.StyleLabel(_lblElSrc, dim: false);
		_lblElSrc.Text = "融合来源：--";
		// v1.5.1: obj.Controls.Add(_lblElSrc, 0, 7);	// （仪表已下架，保留对象供刷新逻辑使用）
		_lblElCalc = new Label();
		_lblElCalc.Dock = DockStyle.Fill;
		_lblElCalc.TextAlign = ContentAlignment.MiddleLeft;
		_lblElCalc.AutoEllipsis = true;
		Theme.StyleLabel(_lblElCalc, dim: false);
		_lblElCalc.Text = "传感器 --° / 积分 --°";
		// v1.5.1: obj.Controls.Add(_lblElCalc, 0, 8);	// （仪表已下架，保留对象供刷新逻辑使用）
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 1,
			ColumnStyles = 
			{
				new ColumnStyle(SizeType.Percent, 100f),
				new ColumnStyle(SizeType.Absolute, 128f)
			}
		};
		_lblElDiff = new Label();
		_lblElDiff.Dock = DockStyle.Fill;
		_lblElDiff.TextAlign = ContentAlignment.MiddleLeft;
		_lblElDiff.AutoEllipsis = true;
		_lblElDiff.Margin = new Padding(0, 0, 8, 0);
		Theme.StyleLabel(_lblElDiff, dim: false);
		_lblElDiff.Text = "融合误差 --°";
		_chkSelfTest = new CheckBox();
		_chkSelfTest.Dock = DockStyle.Fill;
		_chkSelfTest.Text = "上电自检";
		_chkSelfTest.Checked = true;
		_chkSelfTest.ForeColor = Theme.TextDim;
		_chkSelfTest.BackColor = Color.Transparent;
		_chkSelfTest.FlatStyle = FlatStyle.Flat;
		_chkSelfTest.Font = Theme.FSmall;
		_chkSelfTest.CheckedChanged += delegate
		{
			ApplySelfTestFlag();
		};
		tableLayoutPanel2.Controls.Add(_lblElDiff, 0, 0);
		tableLayoutPanel2.Controls.Add(_chkSelfTest, 1, 0);
		// v1.5.1: obj.Controls.Add(tableLayoutPanel2, 0, 9);	// （仪表已下架，保留对象供刷新逻辑使用）
		return obj;
	}

	private void UpdateInstruments(YzStatus s)
	{
		if (s == null || _dAz == null)
		{
			return;
		}
		bool scanning = s.State == 4;
		_dAz.Set(s.Az, s.StateText, scanning);
		if (double.TryParse(_numAz.Text, out var result))
		{
			_dAz.SetTarget(result);
		}
		if (double.TryParse(_numEl.Text, out result))
		{
			_dEl.SetTarget(result);
		}
		string text = ((s.VState == 0) ? "停止" : ((s.VState == 1) ? "上行" : ((s.VState == 2) ? "下行" : ("状态" + s.VState))));
		_dEl.Set(s.TiltDeg, s.LimUp, s.LimDown, text);
		_lampRun.Set("云台状态", s.StateText, (s.State == 0) ? HudDraw.Green : HudDraw.Cyan);
		_lampSensor.Set("倾角传感器", (s.TiltDead == 1) ? "离线" : "在线", (s.TiltDead == 1) ? HudDraw.Danger : HudDraw.Green);
		_lampPrio.Set("控制权", (s.PrioIp == "0.0.0.0") ? "未限制" : s.PrioIp, (s.PrioIp == "0.0.0.0") ? HudDraw.Cyan : HudDraw.Green);
		_lampTrack.Set("追星客户端", (s.TrackOk > 0) ? "已连接" : "无", (s.TrackOk > 0) ? HudDraw.Green : Theme.TextDim);
		_mSpeed.Set("转速", " ms/°", s.MsPerDeg, 0.0, 400.0);
		_mAdc.Set("倾角 ADC（原始值）", "", s.TiltRaw, 0.0, 4095.0);
		_mAdc.SetNote((s.TiltDead == 1) ? "传感器离线" : "0 / 4095 = 两端极限", s.TiltDead == 1);
		_mHeap.Set("S3 可用堆", " B", s.Heap, 0.0, 262144.0);
		_mHeap.SetNote(s.Heap / 1024 + " KB", s.Heap < 40000);
		if (_lblElSrc != null)
		{
			string elSrcText = s.ElSrcText;
			_lblElSrc.Text = string.Format("融合来源：{0}（elsrc={1}）", elSrcText, (s.ElSrc.Length == 0) ? "?" : s.ElSrc);
			_lblElSrc.ForeColor = ((elSrcText == "融合") ? Theme.Ok : ((elSrcText == "传感器") ? HudDraw.Cyan : HudDraw.Warn));
		}
		if (_lblElCalc != null)
		{
			_lblElCalc.Text = $"传感器 {s.ElSen:0.0}° / 积分 {s.ElInt:0.0}°";
		}
		if (_lblElDiff != null)
		{
			bool flag = Math.Abs(s.ElDiff) > 5.0;
			_lblElDiff.Text = string.Format("融合误差 {0:0.0}°（eldiff）{1}", s.ElDiff, flag ? " ⚠" : "");
			_lblElDiff.ForeColor = (flag ? Theme.Danger : Theme.TextDim);
		}
		if (_chkSelfTest != null)
		{
			bool flag2 = s.SelfTest != 0;
			if (_chkSelfTest.Checked != flag2)
			{
				_selfTestSync = true;
				try
				{
					_chkSelfTest.Checked = flag2;
				}
				finally
				{
					_selfTestSync = false;
				}
			}
		}
		if (_lblSpeedNow != null)
		{
			_lblSpeedNow.Text = $"当前：{s.MsPerDeg} ms/° = {YzStatus.DegPerSec(s.MsPerDeg):0.0#} °/s";
		}
		if (_lblSpeedVNow != null)
		{
			_lblSpeedVNow.Text = $"当前：{s.MsPerDegV} ms/° = {YzStatus.DegPerSec(s.MsPerDegV):0.0#} °/s";
		}
		if (_lblAzEndpoint != null)
		{
			string text2 = YzAzPlan.EndpointHint(s.Az);
			if (text2.Length == 0)
			{
				double az = s.Az;
				double num = 360.0 - s.Az;
				text2 = $"端点：距下端 0° 还有 {az:0.#}°，距上端 360° 还有 {num:0.#}°（未在端点，两个方向都可走）";
				_lblAzEndpoint.ForeColor = Theme.TextDim;
			}
			else
			{
				_lblAzEndpoint.ForeColor = Theme.Warn;
			}
			_lblAzEndpoint.Text = text2;
		}
		UpdateAzPlan();
		if (_lblRec != null)
		{
			_lblRec.Text = YzRecDiag.Text(s.Rec, s.RecRst, -1L) + ((s.Rec == 1) ? "\u3000→ 点右边「解除待命」（本程序用 /cmd?g=R + 立刻 /cmd?g=S 收住）" : "");
			_lblRec.ForeColor = ((s.Rec == 1) ? Theme.Danger : Theme.TextDim);
		}
		if (_btnRecLeave != null)
		{
			_btnRecLeave.Enabled = s.Rec == 1;
		}
		try
		{
			if (_numUp != null && s.LimUp != 0)
			{
				decimal num2 = Math.Max(_numUp.Minimum, Math.Min(_numUp.Maximum, s.LimUp));
				if (_numUp.Value != num2)
				{
					_numUp.Value = num2;
				}
			}
			if (_numDown != null && s.LimDown != 0)
			{
				decimal num3 = Math.Max(_numDown.Minimum, Math.Min(_numDown.Maximum, s.LimDown));
				if (_numDown.Value != num3)
				{
					_numDown.Value = num3;
				}
			}
		}
		catch
		{
		}
		if (s.PTrace >= 0)
		{
			RefreshPtraceButton(s.PTrace != 0);
		}
		ApplyProtoDiag(_lastDiag);
	}

	private void BuildUi()
	{
		SuspendLayout();
		Theme.Apply(this);
		try
		{
			base.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		}
		catch
		{
		}
		Text = "YiZhou 控制中枢  " + YzAppVer + "   —   卫星追踪云台 · 电台控制 · 监控 · 系统";
		base.AutoScaleDimensions = new SizeF(96f, 96f);
		base.AutoScaleMode = AutoScaleMode.Dpi;
		base.ClientSize = new Size(1380, 880);
		// v1.5.1：最小尺寸按**实际屏幕**收敛（原来硬编码 1300×720，在 1280×720/高缩放的机器上
		// 窗口比屏幕还宽 → 右侧按钮被切掉）。这里保证最小尺寸永远不超过屏幕工作区。
		Rectangle workArea = SystemInformation.WorkingArea;
		MinimumSize = new Size(Math.Min(1300, Math.Max(880, workArea.Width - 40)), Math.Min(720, Math.Max(560, workArea.Height - 40)));
		base.StartPosition = FormStartPosition.CenterScreen;
		HudTopBar hudTopBar = (HudTopBar)(_topBar = new HudTopBar());
		hudTopBar.SetSubtitle("独立卫星追踪云台（S3 · HTTP） · 电台（HTTPS） · 监控画面（地址直填，自动挑模式）   |   S3 中转站按 IP 直连汇聚");
		base.Controls.Add(hudTopBar);
		TopStrip topStrip = (TopStrip)(_toolBar = new TopStrip());
		topStrip.Build(delegate
		{
			TestConnection();
		}, delegate
		{
			RediscoverBoard(userInitiated: true);
		}, delegate
		{
			DisconnectS3();
		}, delegate
		{
			// ★ v1.5.1：原来这里只 RefreshSys()，结果读到的东西只写进「系统」页的
			// 只读文本框 —— 在别的页签上等于什么都看不到（用户报的"结果被覆盖/显示不全"）。
			// 现在改成弹出独立的可滚动、可复制信息窗口，内容一次看全。
			ReadDeviceInfo();
		}, delegate
		{
			ToggleFullScreen();
		}, delegate
		{
			_reallyExit = true;
			Close();
		});
		_dotS3 = topStrip.S3Dot;
		_txtHost = topStrip.HostBox;
		_btnTestConn = topStrip.TestButton;
		_btnDisconn = topStrip.DisconnectButton;
		_btnRediscover = topStrip.RediscoverButton;
		_hostBoxSync = true;
		try
		{
			_txtHost.Text = _s3.Host;
		}
		finally
		{
			_hostBoxSync = false;
		}
		_txtHost.TextChanged += delegate
		{
			string text = (_txtHost.Text ?? "").Trim();
			if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
			{
				text = text.Substring(7);
			}
			if (text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
			{
				text = text.Substring(8);
			}
			int num = text.IndexOf('/');
			if (num >= 0)
			{
				text = text.Substring(0, num);
			}
			text = text.Trim();
			_s3.Host = text;
			if (!_hostBoxSync)
			{
				_manualHost = text;
				_cfg.S3Host = text;
				_cfg.S3HostSource = S3AddressSourceText.Short(S3AddressSource.Manual);
				_ipChangeNotified = false;
				RefreshBoardLabel();
			}
		};
		base.Controls.Add(topStrip);
		_status = new StatusStrip();
		_lblS3 = new ToolStripStatusLabel("S3：未连接");
		_lblAz = new ToolStripStatusLabel("方位 --");
		_lblPrio = new ToolStripStatusLabel("控制中枢--");
		_lblBoard = new ToolStripStatusLabel("板子：—（未发现）");
		_lblCam = new ToolStripStatusLabel("摄像头 0/0");
		_lblMsg = new ToolStripStatusLabel("");
		_dotS3Bar = new StatusDot();
		_dotS3Bar.Width = 22;
		_dotS3Bar.Height = 18;
		_dotS3Bar.Set(DotState.Unknown, "");
		_dotS3Bar.Visible = true;
		_lblMsg.Spring = true;
		_lblMsg.TextAlign = ContentAlignment.MiddleLeft;
		_status.Items.AddRange(new ToolStripItem[6] { _lblS3, _lblBoard, _lblAz, _lblPrio, _lblCam, _lblMsg });
		_status.Items.Add(new ToolStripControlHost(_dotS3Bar));
		Theme.ApplyStatus(_status);
		foreach (ToolStripItem item in _status.Items)
		{
			item.ForeColor = Theme.TextDim;
			item.Font = Theme.FSmall;
		}
		_lblS3.ForeColor = Theme.Accent;
		_lblPrio.ForeColor = Theme.Ok;
		_lblMsg.ForeColor = Theme.Text;
		if (_lblBoard != null)
		{
			_lblBoard.ForeColor = Theme.Accent;
			_lblBoard.ToolTipText = "当前使用的云台板地址及其来源（记忆 / 发现 / 手填 / 兜底 / 探测）。\n点顶部「重新发现板子」可再发一次 UDP 60059 广播查找（板子换网/IP 变了时用它）。";
		}
		base.Controls.Add(_status);
		_host = new Panel();
		_host.Dock = DockStyle.Fill;
		_host.BackColor = Theme.Bg;
		// ★ v1.5.1 性能：4 个页签**不再在启动时全部构建**。
		// 原来 BuildUi 里 new Panel[4]{ BuildRotatorTab(), BuildRadioTab(), BuildMonitorTab(), BuildRtspTab() }
		// 会一次性造出 1000+ 个控件（电台页/系统页各 300+，含 ListView/TextBox/TrackBar），
		// 启动慢、GDI/内存占用高，而用户 90% 时间只在第一页。
		// 现在每页只放一个空宿主，**首次 ShowNav(i) 时才构建**（EnsurePage）。
		_pages = new Panel[5];
		for (int i = 0; i < _pages.Length; i++)
		{
			Panel host = new Panel();
			host.Dock = DockStyle.None;
			host.Visible = false;
			host.BackColor = Theme.Bg;
			host.Padding = new Padding(12, 8, 12, 10);
			_pages[i] = host;
			_host.Controls.Add(host);
		}
		_hdr = new HudPageHeader();
		_host.Resize += delegate
		{
			LayoutHost();
		};
		NavRail rail = new NavRail();
		rail.Dock = DockStyle.Left;
		rail.Add(HudIcon.Satellite, "卫星追踪云台", "S3 · HTTP");
		rail.Add(HudIcon.Radio, "电台", "S3 · HTTPS");
		rail.Add(HudIcon.Camera, "监控（摄像机）", "地址直填 · 双窗口");
		rail.Add(HudIcon.Chip, "系统", "升级 · 信息");
		rail.Add(HudIcon.Satellite, "卫星追踪", "S3 · 4533");
		rail.SelectedIndexChanged += delegate
		{
			// v1.5.1：**不要**在鼠标消息里同步构建整页（会 new 上千个控件 + 设 Font）。
			// 实测这种 Windows 环境下，鼠标按下消息上下文里给新控件设 Font 可能抛
			// GDI+ InvalidParameter；投递到下一个消息循环再构建就完全正常，
			// 而且界面手感更顺（点击立即高亮，页面随后出现）。
			int idx = rail.SelectedIndex;
			if (!base.IsHandleCreated)
			{
				// 构造期（BuildUi 里设置 SelectedIndex）还没有窗口句柄，不能 BeginInvoke
				ShowNav(idx);
				return;
			}
			BeginInvoke((MethodInvoker)delegate
			{
				ShowNav(idx);
			});
		};
		_rail = rail;
		base.Controls.Add(rail);
		base.Controls.Add(_host);
		_host.BringToFront();
		ShowNav((_startPage >= 0 && _startPage < NavTitles.Length) ? _startPage : 0);
		topStrip.Dock = DockStyle.Top;
		topStrip.RefreshTheme();
		ResumeLayout(performLayout: true);
	}

	private void SetupTray()
	{
		try
		{
			_tray = new NotifyIcon();
			_tray.Icon = ((base.Icon != null) ? base.Icon : SystemIcons.Application);
			_tray.Text = "YiZhou 控制中枢（右键 → 退出）";
			ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
			contextMenuStrip.Items.Add("显示主界面", null, delegate
			{
				RestoreFromTray();
			});
			contextMenuStrip.Items.Add("最小化到任务栏", null, delegate
			{
				base.WindowState = FormWindowState.Minimized;
			});
			contextMenuStrip.Items.Add("关于本程序（版本 · 固件）", null, delegate
			{
				ShowAbout();
			});
			contextMenuStrip.Items.Add(new ToolStripSeparator());
			contextMenuStrip.Items.Add("退出（真正关闭程序）", null, delegate
			{
				_reallyExit = true;
				Close();
			});
			_tray.ContextMenuStrip = contextMenuStrip;
			_tray.DoubleClick += delegate
			{
				RestoreFromTray();
			};
			_tray.Visible = true;
		}
		catch
		{
		}
	}

	private void RestoreFromTray()
	{
		try
		{
			Show();
			if (base.WindowState == FormWindowState.Minimized)
			{
				base.WindowState = FormWindowState.Normal;
			}
			Activate();
			BringToFront();
		}
		catch
		{
		}
	}

	private void ShowAbout()
	{
		string text = FirmwareText(_lastStatus);
		if (text.Length == 0)
		{
			text = "固件：尚未读到（点「读取设备信息」或「测试连接」后即可显示）";
		}
		string text2 = "程序：YiZhou 控制中枢 " + YzAppVer + "\r\n（版本号来自程序集，与 YzControl.csproj 的 <Version> 单一来源）\r\n云台 / 电台 / 摄像头 上位机 · 仅本 PC 程序含摄像头监控\r\n\r\n" + text + "\r\n（固件 ver / bld 一律读自设备 /st、/sys，**不硬编码**）\r\n\r\nS3 设备：" + _s3.Host + "\r\n安全约定：默认只用 HTTP(80)，不主动连接 TCP 4533；\r\n启动不会自动抢占控制权（检测到 DTrac 在线时直接跳过）。" + (string.IsNullOrEmpty(CameraStore.LastError) ? "" : ("\r\n\r\n⚠ 配置告警（本次启动已自动回退到可用值）：\r\n" + CameraStore.LastError));
		MessageBox.Show(this, text2, "关于 YiZhou 控制中枢 " + YzAppVer, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
	}

	private new static ToolStripMenuItem Menu(string text, ToolStripItem[] items)
	{
		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(text);
		toolStripMenuItem.DropDownItems.AddRange(items);
		return toolStripMenuItem;
	}

	private static ToolStripMenuItem Item(string text, EventHandler h)
	{
		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(text);
		toolStripMenuItem.Click += h;
		return toolStripMenuItem;
	}

	private Panel BuildMonitorTab()
	{
		HudPanel obj = new HudPanel
		{
			Padding = new Padding(6)
		};
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			BackColor = Theme.Bg,
			ColumnCount = 1,
			RowCount = 4,
			RowStyles = 
			{
				new RowStyle(SizeType.Absolute, 42f),
				new RowStyle(SizeType.Absolute, 42f),
				new RowStyle(SizeType.Absolute, 40f),
				new RowStyle(SizeType.Percent, 100f)
			}
		};
		obj.Controls.Add(tableLayoutPanel);
		tableLayoutPanel.Controls.Add(BuildWindowBar(0), 0, 0);
		tableLayoutPanel.Controls.Add(BuildWindowBar(1), 0, 1);
		FlowLayoutPanel flowLayoutPanel = Row();
		flowLayoutPanel.Dock = DockStyle.Left;
		flowLayoutPanel.AutoSize = true;
		flowLayoutPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		flowLayoutPanel.WrapContents = false;
		flowLayoutPanel.Height = 36;
		flowLayoutPanel.MinimumSize = new Size(0, 36);
		flowLayoutPanel.Controls.Add(MkGroup("画面工具"));
		flowLayoutPanel.Controls.Add(SmallBtn("抓图(左)", delegate
		{
			Snap(0);
		}));
		flowLayoutPanel.Controls.Add(SmallBtn("抓图(右)", delegate
		{
			Snap(1);
		}));
		flowLayoutPanel.Controls.Add(SmallBtn("录像(左)", delegate
		{
			ToggleRecord(0);
		}));
		flowLayoutPanel.Controls.Add(SmallBtn("录像(右)", delegate
		{
			ToggleRecord(1);
		}));
		flowLayoutPanel.Controls.Add(SmallBtn("打开目录", delegate
		{
			OpenMediaDir();
		}));
		Label value = new Label
		{
			AutoSize = false,
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			Text = "地址填 rtsp://… 完整地址，或只填 IP（默认端口 554）→ 自动试 4 种模式",
			AutoEllipsis = true,
			ForeColor = Theme.TextDim,
			BackColor = Color.Transparent,
			Font = Theme.FSmall
		};
		Panel control = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = Theme.Bg,
			Controls = 
			{
				(Control)value,
				(Control)flowLayoutPanel
			}
		};
		tableLayoutPanel.Controls.Add(control, 0, 2);
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			BackColor = Theme.Bg,
			ColumnCount = 2,
			RowCount = 1,
			ColumnStyles = 
			{
				new ColumnStyle(SizeType.Percent, 50f),
				new ColumnStyle(SizeType.Percent, 50f)
			}
		};
		Panel control2 = NewPane(out _lblPane1, out _pane1, "窗口1");
		Panel control3 = NewPane(out _lblPane2, out _pane2, "窗口2");
		tableLayoutPanel2.Controls.Add(control2, 0, 0);
		tableLayoutPanel2.Controls.Add(control3, 1, 0);
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 0, 3);
		return obj;
	}

	private Control BuildWindowBar(int pane)
	{
		Panel bar = new Panel();
		bar.Dock = DockStyle.Fill;
		bar.BackColor = Theme.Bg;
		bar.Margin = new Padding(0);
		Label cap = MkFixedLabel("窗口" + (pane + 1), 60);
		cap.ForeColor = Theme.Accent;
		cap.Font = Theme.FHead;
		bar.Controls.Add(cap);
		Label la = MkFixedLabel("地址", 46);
		bar.Controls.Add(la);
		TextBox addr = new TextBox();
		Theme.StyleTextBox(addr);
		bar.Controls.Add(addr);
		Label lu = MkFixedLabel("用户名", 58);
		bar.Controls.Add(lu);
		TextBox user = new TextBox();
		Theme.StyleTextBox(user);
		bar.Controls.Add(user);
		Label lp = MkFixedLabel("密码", 48);
		bar.Controls.Add(lp);
		TextBox pass = new TextBox();
		pass.UseSystemPasswordChar = true;
		Theme.StyleTextBox(pass);
		bar.Controls.Add(pass);
		FlowLayoutPanel ops = new FlowLayoutPanel();
		ops.FlowDirection = FlowDirection.LeftToRight;
		ops.WrapContents = false;
		ops.BackColor = Color.Transparent;
		ops.Margin = new Padding(0);
		ops.Controls.Add(SmallBtn("▶ 添加并播放", delegate
		{
			AddWindowAndPlay(pane);
		}));
		ops.Controls.Add(SmallBtn("■ 停止", delegate
		{
			StopWindow(pane);
		}));
		bar.Controls.Add(ops);
		StatusDot statusDot = new StatusDot();
		statusDot.Width = 84;
		statusDot.Height = 26;
		statusDot.Margin = new Padding(0);
		statusDot.Set(DotState.Fail, "未播放", "窗口" + (pane + 1) + "：未在播放");
		statusDot.ShowCaption = true;
		bar.Controls.Add(statusDot);
		if (pane == 0)
		{
			_dotWin1 = statusDot;
		}
		else
		{
			_dotWin2 = statusDot;
		}
		_paneDot[pane] = DotState.Fail;
		Label label = new Label();
		label.AutoSize = false;
		label.TextAlign = ContentAlignment.MiddleLeft;
		label.Text = "";
		label.ForeColor = Theme.Text;
		label.Font = Theme.FSmall;
		label.Width = 1;
		label.Height = 1;
		label.Visible = false;
		bar.Controls.Add(label);
		MethodInvoker layout = delegate
		{
			int num = Math.Min(90, Math.Max(0, bar.ClientSize.Width - 1100));
			int num2 = 2;
			cap.SetBounds(num2, 9, 62, 22);
			num2 += 72;
			la.SetBounds(num2, 9, 46, 22);
			num2 += 58;
			addr.SetBounds(num2, 9, 250 + num, 23);
			num2 += 250 + num + 18;
			lu.SetBounds(num2, 9, 62, 22);
			num2 += 74;
			user.SetBounds(num2, 9, 92, 23);
			num2 += 108;
			lp.SetBounds(num2, 9, 46, 22);
			num2 += 58;
			pass.SetBounds(num2, 9, 92, 23);
			num2 += 112;
			int num3 = 250;
			ops.SetBounds(num2, 4, num3, 34);
			num2 += num3 + 12;
			if (_dotWin1 != null && _dotWin2 != null)
			{
				((pane == 0) ? _dotWin1 : _dotWin2).SetBounds(num2, 6, 96, 30);
				num2 += 96;
			}
		};
		layout();
		bar.Resize += delegate
		{
			layout();
		};
		if (pane == 0)
		{
			_txtAddr1 = addr;
			_txtUser1 = user;
			_txtPass1 = pass;
			_lblWinState1 = label;
		}
		else
		{
			_txtAddr2 = addr;
			_txtUser2 = user;
			_txtPass2 = pass;
			_lblWinState2 = label;
		}
		return bar;
	}

	private Panel NewPane(out Label caption, out Panel video, string def)
	{
		Panel obj = new Panel
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(2)
		};
		video = new Panel();
		video.Dock = DockStyle.Fill;
		video.BackColor = Color.Black;
		video.BorderStyle = BorderStyle.FixedSingle;
		caption = new Label();
		caption.Text = def;
		caption.ForeColor = Theme.Accent;
		caption.BackColor = Theme.SurfaceAlt;
		caption.Dock = DockStyle.Top;
		caption.Height = 22;
		caption.Font = Theme.FSmall;
		caption.TextAlign = ContentAlignment.MiddleLeft;
		obj.Controls.Add(video);
		obj.Controls.Add(caption);
		return obj;
	}

	private Control BuildPtzPad()
	{
		FlowLayoutPanel obj = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			Padding = new Padding(4)
		};
		_lblPtzInfo = new Label();
		_lblPtzInfo.Text = "摄像头自带云台 PTZ\r\n（走 ONVIF，与①卫星追踪云台无关）\r\n需摄像头开启 ONVIF 且账号有 PTZ 权限";
		_lblPtzInfo.Width = 220;
		_lblPtzInfo.Height = 80;
		_lblPtzInfo.TextAlign = ContentAlignment.MiddleLeft;
		obj.Controls.Add(_lblPtzInfo);
		obj.Controls.Add(PtzButton("↑", 0f, 0.6f, 0f));
		obj.Controls.Add(PtzButton("↓", 0f, -0.6f, 0f));
		obj.Controls.Add(PtzButton("←", -0.6f, 0f, 0f));
		obj.Controls.Add(PtzButton("→", 0.6f, 0f, 0f));
		obj.Controls.Add(PtzButton("变倍+", 0f, 0f, 0.5f));
		obj.Controls.Add(PtzButton("变倍-", 0f, 0f, -0.5f));
		obj.Controls.Add(PtzButton("停", 0f, 0f, 0f));
		_cboPreset = new ComboBox();
		Theme.StyleCombo(_cboPreset);
		_cboPreset.Width = 92;
		_cboPreset.DropDownStyle = ComboBoxStyle.DropDown;
		obj.Controls.Add(_cboPreset);
		obj.Controls.Add(Btn("调用", delegate
		{
			GotoPreset();
		}));
		obj.Controls.Add(Btn("设置", delegate
		{
			SetPreset();
		}));
		obj.Controls.Add(Btn("刷新预置位", delegate
		{
			LoadPresets();
		}));
		Button value = Btn("抓图(左)", delegate
		{
			Snap(0);
		});
		Button value2 = Btn("抓图(右)", delegate
		{
			Snap(1);
		});
		obj.Controls.Add(value);
		obj.Controls.Add(value2);
		obj.Controls.Add(Btn("录像(左)", delegate
		{
			ToggleRecord(0);
		}));
		obj.Controls.Add(Btn("录像(右)", delegate
		{
			ToggleRecord(1);
		}));
		return obj;
	}

	private Button PtzButton(string text, float pan, float tilt, float zoom)
	{
		Button b = Theme.MakeButton(text, null);
		b.Width = 62;
		b.Height = 40;
		if (text == "停")
		{
			b.ForeColor = Theme.Danger;
		}
		b.MouseDown += delegate
		{
			if (text == "停")
			{
				StopPtzHold(sendStop: false);
				PtzStop();
			}
			else
			{
				try
				{
					b.Capture = true;
				}
				catch
				{
				}
				StartPtzHold(pan, tilt, zoom);
			}
		};
		b.MouseUp += delegate
		{
			try
			{
				b.Capture = false;
			}
			catch
			{
			}
			if (text != "停")
			{
				StopPtzHold(sendStop: true);
			}
		};
		return b;
	}

	private static Button Btn(string text, EventHandler h)
	{
		Button button = Theme.MakeButton(text, h);
		button.Height = 34;
		button.Margin = new Padding(9, 3, 9, 3);
		return button;
	}

	private static Button SmallBtn(string text, EventHandler h)
	{
		Button button = Theme.MakeButton(text, h);
		button.Height = 30;
		button.Font = Theme.FCardSmall;
		button.Margin = new Padding(8, 2, 8, 2);
		int num = TextRenderer.MeasureText(text ?? "", button.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width + 46;
		if (button.Width < num)
		{
			button.Width = num;
		}
		return button;
	}

	private Panel BuildRotatorTab()
	{
		// v1.5.1 二次精简：云台页只保留"手动 / 定向 / 参数 / 标定"四组常用操作 + 罗盘/俯仰仪表；
		// 其余（往返扫描、正北校零、翻转、读标定、协议、DTrac、方位语义…）全部收进
		// 页面中部的「高级诊断 / 扩展功能」折叠区（代码与事件全保留，只是默认不可见）。
		List<Control> adv = new List<Control>();
		HudPanel hudPanel = new HudPanel();
		// v1.5.2：左列改成「外层 Panel 负责竖向滚动 + 内层 FlowLayoutPanel 按内容长高」。
		// 这样 FlowLayoutPanel 自己不开 AutoScroll，就**不可能**再画出那条多余的灰色横向滚动条
		//（它的内容其实从来没超宽，只是 ScrollableControl 的几像素误差）。
		Panel scrollHost = new Panel();
		scrollHost.Dock = DockStyle.Fill;
		scrollHost.AutoScroll = true;
		scrollHost.BackColor = Theme.Bg;
		FlowLayoutPanel f = new FlowLayoutPanel();
		f.Dock = DockStyle.Top;
		f.AutoSize = true;
		f.AutoSizeMode = AutoSizeMode.GrowAndShrink;
		f.FlowDirection = FlowDirection.TopDown;
		f.WrapContents = false;
		f.AutoScroll = false;
		f.Padding = new Padding(0, 2, 8, 2);
		// 加进本列并同时登记到"高级诊断"折叠区（默认不可见）
		Action<Control> advAny = delegate(Control c)
		{
			f.Controls.Add(c);
			adv.Add(c);
		};
		f.Controls.Add(MkGroup("手动"));
		FlowLayoutPanel flowLayoutPanel = Row();
		flowLayoutPanel.Controls.Add(Btn("◀ 向左", delegate
		{
			Act("L");
		}));
		flowLayoutPanel.Controls.Add(Btn("停止", delegate
		{
			Act("S");
		}));
		flowLayoutPanel.Controls.Add(Btn("向右 ▶", delegate
		{
			Act("R");
		}));
		f.Controls.Add(flowLayoutPanel);
		FlowLayoutPanel flowLayoutPanel2 = Row();
		flowLayoutPanel2.Controls.Add(Btn("▲ 仰角", delegate
		{
			Act("VU");
		}));
		flowLayoutPanel2.Controls.Add(Btn("俯仰停", delegate
		{
			Act("VS");
		}));
		flowLayoutPanel2.Controls.Add(Btn("▼ 俯角", delegate
		{
			Act("VD");
		}));
		f.Controls.Add(flowLayoutPanel2);
		FlowLayoutPanel flowLayoutPanel3 = Row();
		flowLayoutPanel3.Controls.Add(Btn("水平回零", delegate
		{
			Act("Z");
		}));
		flowLayoutPanel3.Controls.Add(Btn("俯仰回零", delegate
		{
			Act("VG0");
		}));
		Button btnScan = Btn("往返扫描", delegate
		{
			Act("A");
		});
		flowLayoutPanel3.Controls.Add(btnScan);
		adv.Add(btnScan);
		f.Controls.Add(flowLayoutPanel3);
		f.Controls.Add(MkGroup("定向转向（0~360° · 0=正北 ≠ 360=上端）"));
		FlowLayoutPanel flowLayoutPanel4 = Row();
		_numAz = Num(0m, 360m, 0m, 80);
		_numEl = Num(-70m, 70m, 0m, 80);
		_numAz.Height = 22;
		_numAz.Margin = new Padding(8, 8, 10, 2);
		_numEl.Height = 22;
		_numEl.Margin = new Padding(8, 8, 10, 2);
		_numAz.ValueChanged += delegate
		{
			UpdateAzPlan();
		};
		flowLayoutPanel4.Controls.Add(new Label
		{
			Text = "方位",
			Width = 52,
			Height = 20,
			TextAlign = ContentAlignment.MiddleLeft,
			Margin = new Padding(3, 9, 4, 2)
		});
		flowLayoutPanel4.Controls.Add(_numAz);
		flowLayoutPanel4.Controls.Add(new Label
		{
			Text = "俯仰",
			Width = 52,
			Height = 20,
			TextAlign = ContentAlignment.MiddleLeft,
			Margin = new Padding(3, 9, 4, 2)
		});
		flowLayoutPanel4.Controls.Add(_numEl);
		flowLayoutPanel4.Controls.Add(Btn("定位", delegate
		{
			GotoTarget();
		}));
		Button btnAzZero = Btn("◎ 正北校零", delegate
		{
			AzZero();
		});
		flowLayoutPanel4.Controls.Add(btnAzZero);
		adv.Add(btnAzZero);
		Button btnAzRead = Btn("读方位", delegate
		{
			AzRead();
		});
		flowLayoutPanel4.Controls.Add(btnAzRead);
		adv.Add(btnAzRead);
		f.Controls.Add(flowLayoutPanel4);
		f.Controls.Add(MkGroup("参数（写入 S3）"));
		FlowLayoutPanel flowLayoutPanel5 = Row();
		_numSpeed = Num(50m, 200m, 111m, 74);
		_numSpeed.Margin = new Padding(6, 3, 6, 3);
		flowLayoutPanel5.Controls.Add(new Label
		{
			Text = "水平 ms/°",
			AutoSize = true,
			Height = 20,
			Margin = new Padding(3, 9, 4, 2)
		});
		flowLayoutPanel5.Controls.Add(_numSpeed);
		_lblSpeedEdit = new Label();
		_lblSpeedEdit.AutoSize = true;
		_lblSpeedEdit.Height = 20;
		_lblSpeedEdit.Margin = new Padding(4, 9, 8, 2);
		Theme.StyleLabel(_lblSpeedEdit, dim: false);
		flowLayoutPanel5.Controls.Add(_lblSpeedEdit);
		_numSpeed.ValueChanged += delegate
		{
			UpdateSpeedEditLabels();
		};
		_lblSpeedNow = new Label();
		_lblSpeedNow.AutoSize = true;
		_lblSpeedNow.Height = 20;
		_lblSpeedNow.Margin = new Padding(3, 9, 8, 2);
		Theme.StyleLabel(_lblSpeedNow, dim: false);
		_lblSpeedNow.Text = "当前：--";
		flowLayoutPanel5.Controls.Add(_lblSpeedNow);
		Button btnSaveSpeed = Btn("保存水平速度", delegate
		{
			SaveSpeed();
		});
		flowLayoutPanel5.Controls.Add(btnSaveSpeed);
		adv.Add(btnSaveSpeed);
		f.Controls.Add(flowLayoutPanel5);
		FlowLayoutPanel flowLayoutPanel6 = Row();
		_numSpeedV = Num(200m, 5000m, 1000m, 82);
		_numSpeedV.Margin = new Padding(6, 3, 6, 3);
		flowLayoutPanel6.Controls.Add(new Label
		{
			Text = "俯仰 ms/°",
			AutoSize = true,
			Height = 20,
			Margin = new Padding(3, 9, 4, 2)
		});
		flowLayoutPanel6.Controls.Add(_numSpeedV);
		_lblSpeedVEdit = new Label();
		_lblSpeedVEdit.AutoSize = true;
		_lblSpeedVEdit.Height = 20;
		_lblSpeedVEdit.Margin = new Padding(4, 9, 8, 2);
		Theme.StyleLabel(_lblSpeedVEdit, dim: false);
		flowLayoutPanel6.Controls.Add(_lblSpeedVEdit);
		_numSpeedV.ValueChanged += delegate
		{
			UpdateSpeedEditLabels();
		};
		_lblSpeedVNow = new Label();
		_lblSpeedVNow.AutoSize = true;
		_lblSpeedVNow.Height = 20;
		_lblSpeedVNow.Margin = new Padding(3, 9, 8, 2);
		Theme.StyleLabel(_lblSpeedVNow, dim: false);
		_lblSpeedVNow.Text = "当前：--";
		flowLayoutPanel6.Controls.Add(_lblSpeedVNow);
		Button btnSaveSpeedV = Btn("保存俯仰速度", delegate
		{
			SaveSpeedV();
		});
		flowLayoutPanel6.Controls.Add(btnSaveSpeedV);
		adv.Add(btnSaveSpeedV);
		// 俯仰速度整行也收进高级区（保存参数按钮会一起写下去，功能不丢）
		adv.Add(flowLayoutPanel6);
		f.Controls.Add(flowLayoutPanel6);
		FlowLayoutPanel flowLayoutPanel7 = Row();
		_numUp = Num(10m, 90m, 70m, 70);
		_numUp.Margin = new Padding(6, 3, 6, 3);
		_numDown = Num(-90m, -10m, -70m, 70);
		_numDown.Margin = new Padding(6, 3, 6, 3);
		flowLayoutPanel7.Controls.Add(new Label
		{
			Text = "仰角上限",
			AutoSize = true,
			Height = 20,
			Margin = new Padding(8, 9, 4, 2)
		});
		flowLayoutPanel7.Controls.Add(_numUp);
		flowLayoutPanel7.Controls.Add(new Label
		{
			Text = "俯角下限",
			AutoSize = true,
			Height = 20,
			Margin = new Padding(8, 9, 4, 2)
		});
		flowLayoutPanel7.Controls.Add(_numDown);
		Button btnSaveLimits = Btn("保存限位", delegate
		{
			SaveLimits();
		});
		flowLayoutPanel7.Controls.Add(btnSaveLimits);
		adv.Add(btnSaveLimits);
		f.Controls.Add(flowLayoutPanel7);
		// ★ 二次精简：同一组参数只留一个保存入口（水平速度 + 俯仰速度 + 上下限位一起写）
		FlowLayoutPanel saveAllRow = Row();
		Button btnSaveAll = Btn("保存参数（速度 + 限位，写入 S3 掉电保存）", delegate
		{
			SaveAllParams();
		});
		btnSaveAll.Width = 340;
		saveAllRow.Controls.Add(btnSaveAll);
		SetFlowSize(f, saveAllRow);
		f.Controls.Add(saveAllRow);
		f.Controls.Add(MkGroup("标定 / 自检"));
		FlowLayoutPanel flowLayoutPanel8 = Row();
		flowLayoutPanel8.Controls.Add(Btn("传感器校零", delegate
		{
			Act("TZ");
		}));
		Button btnTf = Btn("倾角方向翻转", delegate
		{
			Act("TF");
		});
		flowLayoutPanel8.Controls.Add(btnTf);
		adv.Add(btnTf);
		Button btnGx = Btn("水平方向翻转", delegate
		{
			Act("GX");
		});
		flowLayoutPanel8.Controls.Add(btnGx);
		adv.Add(btnGx);
		f.Controls.Add(flowLayoutPanel8);
		FlowLayoutPanel flowLayoutPanel9 = Row();
		Button btnTcal = Btn("读标定值", delegate
		{
			ReadTcal();
		});
		flowLayoutPanel9.Controls.Add(btnTcal);
		adv.Add(btnTcal);
		flowLayoutPanel9.Controls.Add(Btn("关闭上电自检", delegate
		{
			RunBg(delegate
			{
				SetMsg(_s3.SetSelfTest(on: false) ? "已关闭上电自检（存 NVS sf=0，重启后不再自检）" : ("关闭上电自检失败：" + _s3.LastError));
			});
		}));
		flowLayoutPanel9.Controls.Add(Btn("开启上电自检", delegate
		{
			RunBg(delegate
			{
				SetMsg(_s3.SetSelfTest(on: true) ? "已开启上电自检（存 NVS sf=1；注意：会让水平轴转满一圈）" : ("开启上电自检失败：" + _s3.LastError));
			});
		}));
		f.Controls.Add(flowLayoutPanel9);
		FlowLayoutPanel flowLayoutPanel10 = Row();
		flowLayoutPanel10.Controls.Add(Btn("▶ 跑一圈（0→360→0）", delegate
		{
			RunSelfTest();
		}));
		f.Controls.Add(flowLayoutPanel10);
		advAny(MkGroup("控制权（优先客户端仲裁）"));
		FlowLayoutPanel flowLayoutPanel11 = Row();
		flowLayoutPanel11.Controls.Add(Btn("取得云台控制权", delegate
		{
			TakePriority(take: true);
		}));
		flowLayoutPanel11.Controls.Add(Btn("释放云台控制权", delegate
		{
			TakePriority(take: false);
		}));
		advAny(flowLayoutPanel11);
		advAny(MkGroup("通信协议 / 硬件重启"));
		FlowLayoutPanel flowLayoutPanel12 = Row();
		_cboProto = new ComboBox();
		_cboProto.Width = 150;
		_cboProto.DropDownStyle = ComboBoxStyle.DropDownList;
		_cboProto.Items.AddRange(new object[5] { "0 · EasyComm I/II", "1 · DTrac Rotor", "2 · rotctld", "3 · GS232B", "4 · PELCO-D" });
		_cboProto.SelectedIndex = 0;
		Theme.StyleCombo(_cboProto);
		flowLayoutPanel12.Controls.Add(new Label
		{
			Text = "协议",
			Width = 46,
			Height = 20,
			Margin = new Padding(3, 9, 4, 2)
		});
		flowLayoutPanel12.Controls.Add(_cboProto);
		flowLayoutPanel12.Controls.Add(Btn("读取协议", delegate
		{
			ReadProto();
		}));
		flowLayoutPanel12.Controls.Add(Btn("应用协议", delegate
		{
			ApplyProto();
		}));
		flowLayoutPanel12.Controls.Add(Btn("硬件重启 S3", delegate
		{
			RebootDevice();
		}));
		advAny(flowLayoutPanel12);
		advAny(MkGroup("DTrac 追星客户端"));
		FlowLayoutPanel flowLayoutPanel13 = Row();
		_lblDtracText = new Label();
		_lblDtracText.AutoSize = true;
		_lblDtracText.Height = 20;
		_lblDtracText.Margin = new Padding(3, 9, 8, 2);
		Theme.StyleLabel(_lblDtracText, dim: false);
		_lblDtracText.Text = "DTrac：尚未读取…";
		flowLayoutPanel13.Controls.Add(_lblDtracText);
		advAny(flowLayoutPanel13);
		FlowLayoutPanel flowLayoutPanel14 = Row();
		flowLayoutPanel14.Controls.Add(Btn("释放控制权（/prio?off=1）", delegate
		{
			ReleasePriorityFromDtrac();
		}));
		flowLayoutPanel14.Controls.Add(Btn("刷新 DTrac 状态", delegate
		{
			RunBg(delegate
			{
				DtracReadBg();
			});
		}));
		advAny(flowLayoutPanel14);
		FlowLayoutPanel flowLayoutPanel15 = Row();
		_chkAllow4533 = new CheckBox();
		_chkAllow4533.AutoSize = false;
		_chkAllow4533.Width = 400;
		_chkAllow4533.Height = 22;
		_chkAllow4533.Margin = new Padding(3, 8, 10, 2);
		_chkAllow4533.Text = "允许 4533 只读查询（不影响 DTrac）";
		_chkAllow4533.Checked = false;
		_chkAllow4533.ForeColor = Theme.TextDim;
		_chkAllow4533.BackColor = Color.Transparent;
		_chkAllow4533.FlatStyle = FlatStyle.Flat;
		_chkAllow4533.Font = Theme.FSmall;
		flowLayoutPanel15.Controls.Add(_chkAllow4533);
		_btnQuery4533 = Btn("只读查询 4533", delegate
		{
			Query4533Once();
		});
		_btnQuery4533.Enabled = false;
		_chkAllow4533.CheckedChanged += delegate
		{
			if (_btnQuery4533 != null)
			{
				_btnQuery4533.Enabled = _chkAllow4533.Checked;
			}
			if (_chkAllow4533.Checked)
			{
				Confirm4533OptIn();
			}
		};
		flowLayoutPanel15.Controls.Add(_btnQuery4533);
		advAny(flowLayoutPanel15);
		advAny(MkGroup("方位语义（不穿越端点）"));
		FlowLayoutPanel flowLayoutPanel16 = Row();
		_lblAzPlan = MkWrapLabel(520, bold: false);
		_lblAzPlan.Text = "定向预览：选好「方位」后这里显示方向、行程与预计耗时（长边会提示「将绕行 ≈NN s」）。";
		flowLayoutPanel16.Controls.Add(_lblAzPlan);
		advAny(flowLayoutPanel16);
		FlowLayoutPanel flowLayoutPanel17 = Row();
		_lblAzEndpoint = MkWrapLabel(520, bold: false);
		_lblAzEndpoint.Text = "端点提示：读一次状态后显示（固件只拦「朝端点外冲」，离开端点永不拦）。";
		flowLayoutPanel17.Controls.Add(_lblAzEndpoint);
		advAny(flowLayoutPanel17);
		f.Controls.Add(MkGroup("俯仰姿态归零（写 NVS）"));
		FlowLayoutPanel flowLayoutPanel18 = Row();
		_btnPitchZero = Btn("俯仰当前姿态归零（VZ）", delegate
		{
			PitchZeroNow();
		});
		flowLayoutPanel18.Controls.Add(_btnPitchZero);
		_lblPzNote = MkWrapLabel(380, bold: false);
		_lblPzNote.Text = "把「当前姿态」定义为 0°（va/elint 同圈回 0，/st 的 vz 计数 +1）。";
		flowLayoutPanel18.Controls.Add(_lblPzNote);
		adv.Add(_lblPzNote);
		f.Controls.Add(flowLayoutPanel18);
		// ★ v1.5.1 「主界面精简」：DTrac / 方位语义 / 俯仰归零保留在主界面（常用）；
		// 固件诊断 / 看门狗 / ptrace / 复位待命 / 网络通路这些长文本收进下面的折叠区，默认不展开。
		FlowLayoutPanel advToggleRow = Row();
		_advBtn = Btn("▸ 高级功能 / 诊断（往返扫描 · 正北校零 · 翻转 · 协议 · DTrac · 看门狗）", delegate
		{
			ToggleAdv();
		});
		_advBtn.Width = 440;
		advToggleRow.Controls.Add(_advBtn);
		SetFlowSize(f, advToggleRow);
		f.Controls.Add(advToggleRow);
		Action<Control> advAdd = advAny;
		FlowLayoutPanel flowLayoutPanel19 = Row();
		_btnPtrace = Btn("引脚时序日志 ptrace：读取中…", delegate
		{
			TogglePtrace();
		});
		flowLayoutPanel19.Controls.Add(_btnPtrace);
		Label label = MkWrapLabel(300, bold: false);
		label.Text = "开启后仰角 IO4/IO5 每次变化打一行串口日志（只走串口、不写 flash）。";
		flowLayoutPanel19.Controls.Add(label);
		advAdd(flowLayoutPanel19);
		FlowLayoutPanel flowLayoutPanel20 = Row();
		_lblRec = MkWrapLabel(430, bold: false);
		_lblRec.Text = "复位恢复待命：读取中…";
		flowLayoutPanel20.Controls.Add(_lblRec);
		_btnRecLeave = Btn("解除待命", delegate
		{
			RecLeaveStandby();
		});
		flowLayoutPanel20.Controls.Add(_btnRecLeave);
		advAdd(flowLayoutPanel20);
		advAdd(MkGroup("固件诊断（/sys，15 秒自动刷新）"));
		_lblDiagAz = MkWrapLabel(520, bold: false);
		_lblDiagAz.Text = "方位诊断（azblk/azblkn）：读取中…";
		_lblDiagPitch = MkWrapLabel(520, bold: false);
		_lblDiagPitch.Text = "俯仰输出（verr/vz/vrev）：读取中…";
		_lblDiagWdt = MkWrapLabel(520, bold: false);
		_lblDiagWdt.Text = "看门狗/BOD：读取中…";
		_lblDiagProto = MkWrapLabel(520, bold: false);
		_lblDiagProto.Text = "协议/跟踪（ec*/eclast）：读取中…";
		_lblDiagEl = MkWrapLabel(520, bold: false);
		_lblDiagEl.Text = "协议 EL 目标（elacc/elrej/elrc/ellast）：读取中…";
		_lblDiagNvs = MkWrapLabel(520, bold: false);
		_lblDiagNvs.Text = "NVS/门禁（nvs*/http*）：读取中…";
		FlowLayoutPanel flowLayoutPanel21 = Row();
		flowLayoutPanel21.Controls.Add(_lblDiagAz);
		advAdd(flowLayoutPanel21);
		FlowLayoutPanel flowLayoutPanel22 = Row();
		flowLayoutPanel22.Controls.Add(_lblDiagPitch);
		advAdd(flowLayoutPanel22);
		FlowLayoutPanel flowLayoutPanel23 = Row();
		flowLayoutPanel23.Controls.Add(_lblDiagWdt);
		advAdd(flowLayoutPanel23);
		FlowLayoutPanel flowLayoutPanel24 = Row();
		flowLayoutPanel24.Controls.Add(_lblDiagProto);
		advAdd(flowLayoutPanel24);
		FlowLayoutPanel flowLayoutPanel25 = Row();
		flowLayoutPanel25.Controls.Add(_lblDiagEl);
		advAdd(flowLayoutPanel25);
		FlowLayoutPanel flowLayoutPanel26 = Row();
		flowLayoutPanel26.Controls.Add(_lblDiagNvs);
		advAdd(flowLayoutPanel26);
		FlowLayoutPanel flowLayoutPanel27 = Row();
		flowLayoutPanel27.Controls.Add(Btn("刷新诊断 /sys", delegate
		{
			RefreshDiagNow();
		}));
		advAdd(flowLayoutPanel27);
		advAdd(MkGroup("看门狗（TWDT 15 s / RTC_WDT 30 s）"));
		_lblWdtL1 = MkWrapLabel(560, bold: false);
		_lblWdtL1.Text = "第 1 层（任务看门狗）：读取中…（wdtSub/wdtDeg/wdtRe/wdtIn/wdtFix/wdtAdd）";
		_lblWdtL2 = MkWrapLabel(560, bold: false);
		_lblWdtL2.Text = "第 2 层（硬件 RTC_WDT）：读取中…（rtcwOk/rtcwEn/rtcwStg0/rtcwTk/rtcwTkWant）";
		_lblWdtBoot = MkWrapLabel(560, bold: false);
		_lblWdtBoot.Text = "启动阶段：读取中…（bootSt=9 = 启动完成；<9 = 卡在第 N 阶段）";
		_lblWdtSil = MkWrapLabel(560, bold: false);
		_lblWdtSil.Text = "限频闸：读取中…（silN 非 0 = 有被抑制的重复日志；wto 应恒 0）";
		FlowLayoutPanel flowLayoutPanel28 = Row();
		flowLayoutPanel28.Controls.Add(_lblWdtL1);
		advAdd(flowLayoutPanel28);
		FlowLayoutPanel flowLayoutPanel29 = Row();
		flowLayoutPanel29.Controls.Add(_lblWdtL2);
		advAdd(flowLayoutPanel29);
		FlowLayoutPanel flowLayoutPanel30 = Row();
		flowLayoutPanel30.Controls.Add(_lblWdtBoot);
		advAdd(flowLayoutPanel30);
		FlowLayoutPanel flowLayoutPanel31 = Row();
		flowLayoutPanel31.Controls.Add(_lblWdtSil);
		advAdd(flowLayoutPanel31);
		FlowLayoutPanel flowLayoutPanel32 = Row();
		_btnWdtSelfTest = Btn("看门狗自测（3 档）", delegate
		{
			WdtSelfTest3();
		});
		flowLayoutPanel32.Controls.Add(_btnWdtSelfTest);
		flowLayoutPanel32.Controls.Add(new Label
		{
			Text = "A 档次数 N",
			AutoSize = true,
			Height = 20,
			Margin = new Padding(10, 9, 4, 2)
		});
		_numHangN = new NumericUpDown();
		_numHangN.Minimum = 1m;
		_numHangN.Maximum = 10m;
		_numHangN.Value = 3m;
		_numHangN.Width = 52;
		_numHangN.Height = 24;
		_numHangN.Margin = new Padding(2, 6, 6, 2);
		flowLayoutPanel32.Controls.Add(_numHangN);
		_btnWdtReadback = Btn("读 /set?wdt=（只读回读）", delegate
		{
			RefreshWdtReadback();
		});
		flowLayoutPanel32.Controls.Add(_btnWdtReadback);
		advAdd(flowLayoutPanel32);
		FlowLayoutPanel flowLayoutPanel33 = Row();
		_lblWdtTest = MkWrapLabel(560, bold: false);
		_lblWdtTest.Text = "自测：未开始。点左边按钮会**二次确认**后依次跑 C(5 s 不复位) → A(20 s ×N 约 15 s 复位) → B(40 s+noint 约 30 s 复位)，全程约 3~5 分钟；期间 HTTP 无响应属正常（就是在验证卡死）。";
		flowLayoutPanel33.Controls.Add(_lblWdtTest);
		advAdd(flowLayoutPanel33);
		FlowLayoutPanel flowLayoutPanel34 = Row();
		_lblNetPath = MkWrapLabel(560, bold: false);
		_lblNetPath.Text = "网络通路：读取中…（以太网 link=0 且无有线地址 ⇒ 板子当前只能走 WiFi）";
		flowLayoutPanel34.Controls.Add(_lblNetPath);
		advAdd(flowLayoutPanel34);
		_advCtrls = adv;
		foreach (Control c in adv)
		{
			c.Visible = false;
		}
		f.Controls.Add(MkGroup("固件（读自设备，非硬编码）"));
		FlowLayoutPanel flowLayoutPanel35 = Row();
		_lblFwRot = new Label();
		_lblFwRot.AutoSize = true;
		_lblFwRot.Height = 20;
		_lblFwRot.Margin = new Padding(3, 9, 8, 2);
		Theme.StyleLabel(_lblFwRot, dim: false);
		_lblFwRot.Text = "固件：读取中…";
		flowLayoutPanel35.Controls.Add(_lblFwRot);
		f.Controls.Add(flowLayoutPanel35);
		Label label2 = new Label();
		label2.AutoSize = true;
		label2.Height = 20;
		label2.Margin = new Padding(3, 6, 8, 2);
		Theme.StyleLabel(label2, dim: true);
		label2.Text = "（本列可上下滚动；上面图标栏可切换模块）";
		f.Controls.Add(label2);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		// v1.5.2：**恢复中间"第一画面（监控画面）"**，三列并排：
		// 左=操作列 520px（手动/定向/参数/标定），中=视频窗（自适应，窗口窄时先被压缩），右=罗盘+俯仰仪表 330px。
		// 左右两列是绝对宽度 → 中列无论怎么缩都**不会压到左右列**。
		tableLayoutPanel.ColumnCount = 3;
		tableLayoutPanel.RowCount = 1;
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 520f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330f));
		f.Dock = DockStyle.Fill;
		f.AutoScroll = true;
		f.Padding = new Padding(0, 0, 4, 0);
		_ctrlCol = f;
		scrollHost.Controls.Add(f);
		tableLayoutPanel.Controls.Add(scrollHost, 0, 0);
		f.SizeChanged += delegate
		{
			int num = Math.Max(200, f.ClientSize.Width - 24);
			foreach (Control control in f.Controls)
			{
				if (control is Label && !control.AutoSize && control.Width != num)
				{
					control.Width = num;
				}
			}
		};
		// v1.5.2：竖向滚动条出现/消失会改变 ClientSize（但不触发 SizeChanged），
		// 行宽不跟着更新就会多出一条横向滚动条。这里在 ClientSizeChanged 里重排行宽。
		f.ClientSizeChanged += delegate
		{
			try
			{
				int w = Math.Max(180, f.ClientSize.Width - 40);
				foreach (Control control2 in f.Controls)
				{
					// 行(FlowLayoutPanel) 与 分组标题(NeoGroupLabel/Label) 都可能比列宽宽出几个像素，
					// 只要有一个超宽，ScrollableControl 就会画横向滚动条 → 全部收回 w 以内。
					if (control2.Visible && control2.Width > w)
					{
						control2.Width = w;
					}
				}
				if (f.HorizontalScroll.Visible)
				{
					f.HorizontalScroll.Visible = false;
				}
			}
			catch
			{
			}
		};
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel();
		tableLayoutPanel2.Dock = DockStyle.Fill;
		tableLayoutPanel2.ColumnCount = 1;
		tableLayoutPanel2.RowCount = 3;
		tableLayoutPanel2.BackColor = Theme.Bg;
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
		tableLayoutPanel2.Padding = new Padding(6, 0, 6, 0);
		_lblMini = new Label();
		_lblMini.Dock = DockStyle.Fill;
		_lblMini.Text = "第一画面：未启动（点下方「▶ 启动画面」）";
		Theme.StyleLabel(_lblMini, dim: true);
		_lblMini.ForeColor = HudDraw.Cyan;
		tableLayoutPanel2.Controls.Add(_lblMini, 0, 0);
		_mini = new Panel();
		_mini.Dock = DockStyle.Fill;
		_mini.BackColor = Color.Black;
		_mini.BorderStyle = BorderStyle.FixedSingle;
		tableLayoutPanel2.Controls.Add(_mini, 0, 1);
		FlowLayoutPanel flowLayoutPanel36 = Row();
		flowLayoutPanel36.Dock = DockStyle.Fill;
		flowLayoutPanel36.Controls.Add(Btn("▶ 启动画面", delegate
		{
			StartMini();
		}));
		flowLayoutPanel36.Controls.Add(Btn("■ 停止画面", delegate
		{
			StopMini();
		}));
		tableLayoutPanel2.Controls.Add(flowLayoutPanel36, 0, 2);
		// v1.5.2：第一画面回到可见区域（中列）。**不会自动播放**：
		// 只有用户点「▶ 启动画面」才会 CreateEngine（首次才加载 VLC / 海康 VideoSDK）。
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 1, 0);
		tableLayoutPanel.Controls.Add(BuildInstrumentPanel(), 2, 0);
		hudPanel.Controls.Add(tableLayoutPanel);
		return hudPanel;
	}

	private static string GuessLocalIp()
	{
		return McastAudio.GuessLocalIp();
	}

	private Panel BuildRadioTab()
	{
		// v1.5.1 二次精简：透传/直连只保留**一套**（本机直连 ZQWL：IP + 端口 + 连接/断开 + 状态灯）。
		// 「路径 A：S3 板载中继」「云台板串口参数」「手动发帧」「组号说明」全部收进本页折叠区，
		// 控件与事件都还在（需要时一键展开），界面上不再出现第二套 IP/端口。
		List<Control> radAdv = new List<Control>();
		Action<Control> radHide = delegate(Control c)
		{
			c.Visible = false;
			radAdv.Add(c);
		};
		HudPanel hudPanel = new HudPanel();
		hudPanel.Padding = new Padding(10);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
		tableLayoutPanel.Dock = DockStyle.Fill;
		tableLayoutPanel.ColumnCount = 2;
		tableLayoutPanel.RowCount = 1;
		tableLayoutPanel.BackColor = Theme.Bg;
		tableLayoutPanel.Padding = new Padding(0);
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		Panel panel = new Panel();
		panel.Dock = DockStyle.Fill;
		panel.BackColor = Theme.Bg;
		panel.Margin = new Padding(0, 0, 4, 0);
		Panel panel2 = new Panel();
		panel2.Dock = DockStyle.Fill;
		panel2.BackColor = Theme.Bg;
		panel2.Margin = new Padding(4, 0, 0, 0);
		FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
		flowLayoutPanel.Dock = DockStyle.Fill;
		flowLayoutPanel.FlowDirection = FlowDirection.TopDown;
		flowLayoutPanel.WrapContents = false;
		flowLayoutPanel.AutoScroll = true;
		flowLayoutPanel.BackColor = Theme.Bg;
		flowLayoutPanel.Padding = new Padding(0, 2, 0, 2);
		panel.Controls.Add(flowLayoutPanel);
		FlowLayoutPanel flowLayoutPanel2 = new FlowLayoutPanel();
		flowLayoutPanel2.Dock = DockStyle.Fill;
		flowLayoutPanel2.FlowDirection = FlowDirection.TopDown;
		flowLayoutPanel2.WrapContents = false;
		flowLayoutPanel2.AutoScroll = true;
		flowLayoutPanel2.BackColor = Theme.Bg;
		flowLayoutPanel2.Padding = new Padding(0, 2, 0, 2);
		panel2.Controls.Add(flowLayoutPanel2);
		tableLayoutPanel.Controls.Add(panel, 0, 0);
		tableLayoutPanel.Controls.Add(panel2, 1, 0);
		hudPanel.Controls.Add(tableLayoutPanel);
		_rigDisplay = new RigDisplay();
		_rigDisplay.Dock = DockStyle.None;
		_rigDisplay.Height = 118;
		SetFlowSize(flowLayoutPanel, _rigDisplay);
		flowLayoutPanel.Controls.Add(_rigDisplay);
		_lblCivSrc = MkFixedValue(232);
		_lblCivTime = MkFixedValue(176);
		_lblCivSplit = MkFixedValue(56);
		_lblCivTx = MkFixedValue(150);
		_lblCivMode = MkFixedValue(76);
		AddGroup(flowLayoutPanel, "通道 · 更新 · 异频");
		FlowLayoutPanel flowLayoutPanel3 = Row();
		flowLayoutPanel3.Controls.Add(MkFixedLabel("通道来源", 76));
		flowLayoutPanel3.Controls.Add(_lblCivSrc);
		flowLayoutPanel3.Controls.Add(MkFixedLabel("最后更新", 76));
		flowLayoutPanel3.Controls.Add(_lblCivTime);
		AddRow(flowLayoutPanel, flowLayoutPanel3);
		FlowLayoutPanel flowLayoutPanel4 = Row();
		flowLayoutPanel4.Controls.Add(MkFixedLabel("SPLIT", 52));
		flowLayoutPanel4.Controls.Add(_lblCivSplit);
		flowLayoutPanel4.Controls.Add(MkFixedLabel("异频(TX)", 72));
		flowLayoutPanel4.Controls.Add(_lblCivTx);
		flowLayoutPanel4.Controls.Add(MkFixedLabel("模式", 44));
		flowLayoutPanel4.Controls.Add(_lblCivMode);
		AddRow(flowLayoutPanel, flowLayoutPanel4);
		FlowLayoutPanel flowLayoutPanel5 = Row();
		_lblCivMisc = MkFixedLabel("", 560);
		_lblCivMisc.AutoEllipsis = true;
		_lblCivMisc.ForeColor = Theme.TextDim;
		// v1.5.1：这一行是"直连读数 + 总线收帧"的超长单行文字（560px 放不下，会压到上一行的模式值）。
		// 同样的内容在右下「链路状态 / 原始回读」大框里完整显示 → 这里不再上屏（对象保留，刷新逻辑不变）。
		flowLayoutPanel5.Controls.Add(_lblCivMisc);
		AddGroup(flowLayoutPanel, "频率与模式");
		FlowLayoutPanel flowLayoutPanel6 = Row();
		_txtRigFreq = new TextBox();
		_txtRigFreq.Width = 96;
		_txtRigFreq.Text = "145.500";
		Theme.StyleTextBox(_txtRigFreq);
		_cboRigMode = new ComboBox();
		_cboRigMode.Width = 84;
		_cboRigMode.DropDownStyle = ComboBoxStyle.DropDownList;
		_cboRigMode.Items.AddRange(new object[9] { "USB", "LSB", "CW", "CW-R", "FM", "AM", "RTTY", "RTTY-R", "DATA" });
		_cboRigMode.SelectedIndex = 0;
		Theme.StyleCombo(_cboRigMode);
		_cboStep = new ComboBox();
		_cboStep.Width = 78;
		_cboStep.DropDownStyle = ComboBoxStyle.DropDownList;
		_cboStep.Items.AddRange(new object[6] { "1 kHz", "5 kHz", "10 kHz", "12.5 kHz", "25 kHz", "100 kHz" });
		_cboStep.SelectedIndex = 2;
		Theme.StyleCombo(_cboStep);
		flowLayoutPanel6.Controls.Add(MkFixedLabel("频率(MHz)", 84));
		flowLayoutPanel6.Controls.Add(_txtRigFreq);
		flowLayoutPanel6.Controls.Add(Btn("设置频率", delegate
		{
			RigSetFreq();
		}));
		flowLayoutPanel6.Controls.Add(MkFixedLabel("模式", 52));
		flowLayoutPanel6.Controls.Add(_cboRigMode);
		flowLayoutPanel6.Controls.Add(Btn("设置模式", delegate
		{
			RigSetMode();
		}));
		flowLayoutPanel6.Controls.Add(MkFixedLabel("步进", 52));
		flowLayoutPanel6.Controls.Add(_cboStep);
		flowLayoutPanel6.Controls.Add(Btn("◀ 下调", delegate
		{
			RigTune(-1);
		}));
		flowLayoutPanel6.Controls.Add(Btn("上调 ▶", delegate
		{
			RigTune(1);
		}));
		AddRow(flowLayoutPanel, flowLayoutPanel6);
		AddGroup(flowLayoutPanel, "发射控制");
		FlowLayoutPanel flowLayoutPanel7 = Row();
		Button bPtt = Theme.MakeButton("按住发射 PTT（HTTPS）", null);
		bPtt.Width = 210;
		bPtt.Height = 46;
		bPtt.MouseDown += delegate
		{
			try
			{
				bPtt.Capture = true;
			}
			catch
			{
			}
			RigPtt(on: true);
		};
		bPtt.MouseUp += delegate
		{
			try
			{
				bPtt.Capture = false;
			}
			catch
			{
			}
			RigPtt(on: false);
		};
		flowLayoutPanel7.Controls.Add(bPtt);
		flowLayoutPanel7.Controls.Add(Btn("强制接收", delegate
		{
			RigPtt(on: false);
		}));
		flowLayoutPanel7.Controls.Add(Btn("立即刷新", delegate
		{
			RigPollNow(verbose: true);
		}));
		string[] array = new string[4] { "USB", "LSB", "CW", "FM" };
		foreach (string text in array)
		{
			string mm = text;
			flowLayoutPanel7.Controls.Add(Btn(mm, delegate
			{
				_cboRigMode.SelectedItem = mm;
				RigSetMode();
			}));
		}
		AddRow(flowLayoutPanel, flowLayoutPanel7);
		AddGroup(flowLayoutPanel, "CI-V 快捷（直接发标准帧）");
		FlowLayoutPanel flowLayoutPanel8 = Row();
		flowLayoutPanel8.Controls.Add(Btn("VFO A", delegate
		{
			RigCiv("0700");
		}));
		flowLayoutPanel8.Controls.Add(Btn("VFO B", delegate
		{
			RigCiv("0701");
		}));
		flowLayoutPanel8.Controls.Add(Btn("SPLIT 开", delegate
		{
			RigCiv("0F01");
		}));
		flowLayoutPanel8.Controls.Add(Btn("SPLIT 关", delegate
		{
			RigCiv("0F00");
		}));
		flowLayoutPanel8.Controls.Add(Btn("RIT 清零", delegate
		{
			RigCiv("2100");
		}));
		flowLayoutPanel8.Controls.Add(Btn("功率 100%", delegate
		{
			RigCiv("140A0255");
		}));
		flowLayoutPanel8.Controls.Add(Btn("滤波 1", delegate
		{
			RigCiv("160201");
		}));
		flowLayoutPanel8.Controls.Add(Btn("滤波 2", delegate
		{
			RigCiv("160202");
		}));
		AddRow(flowLayoutPanel, flowLayoutPanel8);
		AddGroup(flowLayoutPanel, "音量与静噪");
		FlowLayoutPanel flowLayoutPanel9 = Row();
		flowLayoutPanel9.Controls.Add(MkFixedLabel("音量 AF", 66));
		_tbRigVol = MkLvlSlider();
		_tbRigVol.ValueChanged += delegate
		{
			RigLvlChanged(sql: false);
		};
		_tbRigVol.MouseDown += delegate
		{
			_rigVolDrag = true;
		};
		_tbRigVol.MouseUp += delegate
		{
			_rigVolDrag = false;
		};
		_tbRigVol.MouseCaptureChanged += delegate
		{
			_rigVolDrag = false;
		};
		flowLayoutPanel9.Controls.Add(_tbRigVol);
		_lblRigVolVal = MkLvlValue(52);
		flowLayoutPanel9.Controls.Add(_lblRigVolVal);
		flowLayoutPanel9.Controls.Add(MkFixedLabel("静噪 SQL", 78));
		_tbRigSql = MkLvlSlider();
		_tbRigSql.ValueChanged += delegate
		{
			RigLvlChanged(sql: true);
		};
		_tbRigSql.MouseDown += delegate
		{
			_rigSqlDrag = true;
		};
		_tbRigSql.MouseUp += delegate
		{
			_rigSqlDrag = false;
		};
		_tbRigSql.MouseCaptureChanged += delegate
		{
			_rigSqlDrag = false;
		};
		flowLayoutPanel9.Controls.Add(_tbRigSql);
		_lblRigSqlVal = MkLvlValue(52);
		flowLayoutPanel9.Controls.Add(_lblRigSqlVal);
		AddRow(flowLayoutPanel, flowLayoutPanel9);
		AddGroup(flowLayoutPanel, "信道模式与记忆频道");
		FlowLayoutPanel flowLayoutPanel10 = Row();
		flowLayoutPanel10.Controls.Add(MkFixedLabel("信道模式", 82));
		_btnRigVfo = Btn("VFO", delegate
		{
			RigSetVm(mem: false);
		});
		_btnRigVfo.Width = 86;
		_btnRigMem = Btn("MEM", delegate
		{
			RigSetVm(mem: true);
		});
		_btnRigMem.Width = 90;
		flowLayoutPanel10.Controls.Add(_btnRigVfo);
		flowLayoutPanel10.Controls.Add(_btnRigMem);
		flowLayoutPanel10.Controls.Add(MkFixedLabel("信道步进", 82));
		Button button = Btn("CH−", delegate
		{
			RigChStep(up: false);
		});
		button.Width = 86;
		Button button2 = Btn("CH+", delegate
		{
			RigChStep(up: true);
		});
		button2.Width = 86;
		flowLayoutPanel10.Controls.Add(button);
		flowLayoutPanel10.Controls.Add(button2);
		flowLayoutPanel10.Controls.Add(MkFixedLabel("当前", 46));
		_lblRigCh = MkLvlValue(52);
		flowLayoutPanel10.Controls.Add(_lblRigCh);
		AddRow(flowLayoutPanel, flowLayoutPanel10);
		AddGroup(flowLayoutPanel, "频道号直跳（1~99）");
		FlowLayoutPanel flowLayoutPanel11 = Row();
		flowLayoutPanel11.Controls.Add(MkFixedLabel("频道号", 66));
		_txtRigChN = new TextBox();
		_txtRigChN.Width = 56;
		_txtRigChN.MaxLength = 2;
		_txtRigChN.Text = "";
		_txtRigChN.TextAlign = HorizontalAlignment.Center;
		Theme.StyleTextBox(_txtRigChN);
		_txtRigChN.KeyDown += delegate(object s, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return)
			{
				e.SuppressKeyPress = true;
				RigChJump();
			}
		};
		flowLayoutPanel11.Controls.Add(_txtRigChN);
		Button button3 = Btn("跳转", delegate
		{
			RigChJump();
		});
		button3.Width = 84;
		flowLayoutPanel11.Controls.Add(button3);
		flowLayoutPanel11.Controls.Add(MkFixedLabel("（回车亦可）", 110));
		AddRow(flowLayoutPanel, flowLayoutPanel11);
		AddGroup(flowLayoutPanel, "存储组 / CI-V 通路");
		FlowLayoutPanel flowLayoutPanel12 = Row();
		flowLayoutPanel12.Controls.Add(MkFixedLabel("存储组", 66));
		for (int j = 1; j <= 5; j++)
		{
			int gg = j;
			Button button4 = Btn(((char)(65 + j - 1)).ToString(), delegate
			{
				RigSetGrp(gg);
			});
			button4.Width = 48;
			button4.Margin = new Padding(6, 3, 6, 3);
			_btnRigGrp[j - 1] = button4;
			flowLayoutPanel12.Controls.Add(button4);
		}
		_lblRigGrp = MkLvlValue(48);
		flowLayoutPanel12.Controls.Add(_lblRigGrp);
		flowLayoutPanel12.Controls.Add(MkFixedLabel("CI-V 通路", 82));
		_lblRigPath = MkLvlValue(96);
		flowLayoutPanel12.Controls.Add(_lblRigPath);
		AddRow(flowLayoutPanel, flowLayoutPanel12);
		FlowLayoutPanel flowLayoutPanel13 = Row();
		flowLayoutPanel13.Controls.Add(MkFixedLabel("提示：组号=最后设置值（电台无读组命令）；通路 tcp=透传已连", 560));
		radHide(flowLayoutPanel13);
		AddRow(flowLayoutPanel, flowLayoutPanel13);
		_tRigLvl = new System.Windows.Forms.Timer();
		// v1.5.1：滑块回写去抖 60 ms → 140 ms（发送阈值本身是 150 ms，60 ms 纯属空转）
		_tRigLvl.Interval = 140;
		_tRigLvl.Tick += delegate
		{
			RigLvlFlush();
		};
		_tRigLvl.Start();
		_cboBaud = new ComboBox();
		_cboBaud.Width = 82;
		_cboBaud.DropDownStyle = ComboBoxStyle.DropDownList;
		_cboBaud.Items.AddRange(new object[6] { "4800", "9600", "19200", "38400", "57600", "115200" });
		_cboBaud.SelectedIndex = 2;
		Theme.StyleCombo(_cboBaud);
		_txtCivAddr = new TextBox();
		_txtCivAddr.Width = 46;
		_txtCivAddr.Text = "88";
		Theme.StyleTextBox(_txtCivAddr);
		_txtTxDly = new TextBox();
		_txtTxDly.Width = 40;
		_txtTxDly.Text = "7";
		Theme.StyleTextBox(_txtTxDly);
		_txtRxDly = new TextBox();
		_txtRxDly.Width = 40;
		_txtRxDly.Text = "8";
		Theme.StyleTextBox(_txtRxDly);
		radHide(AddGroup(flowLayoutPanel2, "路径 A：S3 板载中继（经板子转发，非本机直连）"));
		FlowLayoutPanel flowLayoutPanel14 = Row();
		_txtNetIp = new TextBox();
		_txtNetIp.Width = 122;
		_txtNetIp.Text = "192.168.1.100";
		Theme.StyleTextBox(_txtNetIp);
		_txtNetPort = new TextBox();
		_txtNetPort.Width = 54;
		_txtNetPort.Text = "4001";
		Theme.StyleTextBox(_txtNetPort);
		_cboNetMode = new ComboBox();
		_cboNetMode.Width = 104;
		_cboNetMode.DropDownStyle = ComboBoxStyle.DropDownList;
		_cboNetMode.Items.AddRange(new object[2] { "监听(推荐)", "主动轮询" });
		_cboNetMode.SelectedIndex = 0;
		Theme.StyleCombo(_cboNetMode);
		_lblNetState = new Label();
		_lblNetState.AutoSize = false;
		_lblNetState.Width = 1;
		_lblNetState.Height = 1;
		_lblNetState.Visible = false;
		flowLayoutPanel14.Controls.Add(MkFixedLabel("网络透传", 78));
		flowLayoutPanel14.Controls.Add(MkFixedLabel("目标IP", 58));
		flowLayoutPanel14.Controls.Add(_txtNetIp);
		flowLayoutPanel14.Controls.Add(MkFixedLabel("端口", 44));
		flowLayoutPanel14.Controls.Add(_txtNetPort);
		flowLayoutPanel14.Controls.Add(MkFixedLabel("链路", 44));
		flowLayoutPanel14.Controls.Add(_cboNetMode);
		flowLayoutPanel14.Controls.Add(Btn("读取配置", delegate
		{
			RigReadNet();
		}));
		_btnNetToggle = Btn("▶ 启用透传", delegate
		{
			ToggleNetPassthrough();
		});
		_btnNetToggle.Width = 122;
		new ToolTip().SetToolTip(_btnNetToggle, "同一按钮按状态切换：未启用→应用并启用；已启用→停用并恢复本机控制");
		flowLayoutPanel14.Controls.Add(_btnNetToggle);
		radHide(flowLayoutPanel14);
		AddRow(flowLayoutPanel2, flowLayoutPanel14);
		AddGroup(flowLayoutPanel2, "电台数据通道（本机直连：ZQWL 网络 / 串口 COM）");
		FlowLayoutPanel flowLayoutPanel15 = Row();
		_tgRigChannel = MkToggle("① 直连 ZQWL（主用）", 12);
		_tgRigChannel.CheckedChanged += delegate
		{
			ApplyRigChannel();
		};
		flowLayoutPanel15.Controls.Add(_tgRigChannel);
		_btnCivReadSplit = Btn("读取异频(25 00)", delegate
		{
			RigReadSplitNow();
		});
		_btnCivReadSplit.Width = 146;
		new ToolTip().SetToolTip(_btnCivReadSplit, "立即用直连通道读：03 频率 / 04 模式 / 0F SPLIT / 25 00 异频(TX) 频率。\n该机不响应 1C 读异频（实测只回 1C 00 收发状态），必须走 25 00 ✓");
		flowLayoutPanel15.Controls.Add(_btnCivReadSplit);
		AddRow(flowLayoutPanel2, flowLayoutPanel15);
		FlowLayoutPanel flowLayoutPanel16 = Row();
		flowLayoutPanel16.Controls.Add(MkFixedLabel("本机直连 IP", 86));
		_txtDirectIp = new TextBox();
		_txtDirectIp.Width = 116;
		_txtDirectIp.Text = "172.16.1.123";
		Theme.StyleTextBox(_txtDirectIp);
		_txtDirectIp.TextChanged += delegate
		{
			SaveDirectCfg();
		};
		flowLayoutPanel16.Controls.Add(_txtDirectIp);
		flowLayoutPanel16.Controls.Add(MkFixedLabel("端口", 44));
		_txtDirectPort = new TextBox();
		_txtDirectPort.Width = 58;
		_txtDirectPort.Text = "4001";
		_txtDirectPort.TextAlign = HorizontalAlignment.Center;
		Theme.StyleTextBox(_txtDirectPort);
		_txtDirectPort.TextChanged += delegate
		{
			SaveDirectCfg();
		};
		flowLayoutPanel16.Controls.Add(_txtDirectPort);
		AddRow(flowLayoutPanel2, flowLayoutPanel16);
		FlowLayoutPanel flowLayoutPanel17 = Row();
		flowLayoutPanel17.Controls.Add(MkFixedLabel("轮询周期", 76));
		_cboCivPoll = new ComboBox();
		_cboCivPoll.DropDownStyle = ComboBoxStyle.DropDownList;
		_cboCivPoll.Width = 84;
		_cboCivPoll.Items.AddRange(new object[3] { "1 秒", "2 秒", "5 秒" });
		_cboCivPoll.SelectedIndex = 1;
		_cboCivPoll.SelectedIndexChanged += delegate
		{
			ApplyCivPollMs();
		};
		Theme.StyleCombo(_cboCivPoll);
		flowLayoutPanel17.Controls.Add(_cboCivPoll);
		_btnCivPause = Btn("⏸ 暂停轮询", delegate
		{
			ToggleRigPause();
		});
		_btnCivPause.Width = 122;
		new ToolTip().SetToolTip(_btnCivPause, "同一按钮按状态切换：轮询中→暂停；已暂停→恢复（暂停时不发任何 CI-V 命令）");
		flowLayoutPanel17.Controls.Add(_btnCivPause);
		AddRow(flowLayoutPanel2, flowLayoutPanel17);
		FlowLayoutPanel flowLayoutPanel18 = Row();
		_tgLegacy1C = MkToggle("1C 兼容探测", 10);
		new ToolTip().SetToolTip(_tgLegacy1C, "默认关。开启后读异频会先试 1C 01：本机实测它回 NG(FA)、1C 00 只回 8 字节收发状态帧\n（都是**残缺/不可用**），识别到就**自动回退 25 00** 并在日志里写明 —— 不会用 1C 当数据源 ✗");
		_tgLegacy1C.CheckedChanged += delegate
		{
			SaveDirectCfg();
		};
		flowLayoutPanel18.Controls.Add(_tgLegacy1C);
		_tgDeepVfoB = MkToggle("深读 VFO-B(07+03)", 10);
		new ToolTip().SetToolTip(_tgDeepVfoB, "默认关。25 00 读不到时才用：07 01(切 VFO-B) → 03(读频率) → 07 00(还原 VFO-A) → 复读 03 校验。\n⚠ 这是**写**命令（会短暂切换 VFO）；只读验证请保持关闭");
		_tgDeepVfoB.CheckedChanged += delegate
		{
			SaveDirectCfg();
		};
		flowLayoutPanel18.Controls.Add(_tgDeepVfoB);
		_tgReadSmeter = MkToggle("读 S 表(15 02)", 10);
		_tgReadSmeter.CheckedChanged += delegate
		{
			SaveDirectCfg();
		};
		flowLayoutPanel18.Controls.Add(_tgReadSmeter);
		AddRow(flowLayoutPanel2, flowLayoutPanel18);
		FlowLayoutPanel flowLayoutPanel19 = Row();
		flowLayoutPanel19.Controls.Add(MkFixedLabel("链路方式", 82));
		_rbDirectTcp = new NeoToggle();
		_rbDirectTcp.Text = "网络(TCP)";
		_rbDirectTcp.Checked = true;
		_rbDirectTcp.AutoSize = false;
		_rbDirectTcp.Height = 28;
		_rbDirectTcp.Margin = new Padding(2, 4, 12, 4);
		_rbDirectTcp.CheckedChanged += delegate
		{
			if (_rbDirectTcp.Checked)
			{
				ApplyDirectLinkMode("tcp");
			}
		};
		Theme.StyleToggle(_rbDirectTcp);
		_rbDirectTcp.Width = Theme.ToggleWidth(_rbDirectTcp) + 20;
		flowLayoutPanel19.Controls.Add(_rbDirectTcp);
		_rbDirectCom = new NeoToggle();
		_rbDirectCom.Text = "串口(COM)";
		_rbDirectCom.AutoSize = false;
		_rbDirectCom.Height = 28;
		_rbDirectCom.Margin = new Padding(2, 4, 14, 4);
		_rbDirectCom.CheckedChanged += delegate
		{
			if (_rbDirectCom.Checked)
			{
				ApplyDirectLinkMode("com");
			}
		};
		Theme.StyleToggle(_rbDirectCom);
		_rbDirectCom.Width = Theme.ToggleWidth(_rbDirectCom) + 20;
		EventHandler value = delegate
		{
			try
			{
				if (_rbDirectTcp.IsHandleCreated)
				{
					_rbDirectTcp.Width = Theme.ToggleWidth(_rbDirectTcp) + 20;
					_rbDirectCom.Width = Theme.ToggleWidth(_rbDirectCom) + 20;
				}
			}
			catch
			{
			}
		};
		_rbDirectTcp.HandleCreated += value;
		_rbDirectCom.HandleCreated += value;
		flowLayoutPanel19.Controls.Add(_rbDirectCom);
		_btnDirect = Btn("▶ 连接直连", delegate
		{
			RigDirectToggle();
		});
		_btnDirect.Width = 138;
		new ToolTip().SetToolTip(_btnDirect, "同一按钮按状态切换：未连接→按所选链路建立；已连接→断开并释放");
		flowLayoutPanel19.Controls.Add(_btnDirect);
		AddRow(flowLayoutPanel2, flowLayoutPanel19);
		_rowDirectCom = Row();
		_rowDirectCom.Controls.Add(MkFixedLabel("COM 口", 62));
		_cboDirectCom = new ComboBox();
		_cboDirectCom.DropDownStyle = ComboBoxStyle.DropDown;
		_cboDirectCom.Width = 110;
		Theme.StyleCombo(_cboDirectCom);
		_rowDirectCom.Controls.Add(_cboDirectCom);
		Button button5 = Btn("↻ 刷新串口", delegate
		{
			RefreshComPorts();
			SetMsg("已刷新串口列表：" + DescribeComPorts());
		});
		button5.Width = 118;
		_rowDirectCom.Controls.Add(button5);
		_rowDirectCom.Controls.Add(MkFixedLabel("（枚举本机串口，可手填 COM12）", 250));
		AddRow(flowLayoutPanel2, _rowDirectCom);
		_rowDirectCom2 = Row();
		_rowDirectCom2.Controls.Add(MkFixedLabel("本机COM波特率", 100));
		_cboDirectBaud = new ComboBox();
		_cboDirectBaud.DropDownStyle = ComboBoxStyle.DropDownList;
		_cboDirectBaud.Width = 92;
		_cboDirectBaud.Items.AddRange(new object[6] { "4800", "9600", "19200", "38400", "57600", "115200" });
		_cboDirectBaud.SelectedIndex = 2;
		Theme.StyleCombo(_cboDirectBaud);
		_rowDirectCom2.Controls.Add(_cboDirectBaud);
		_rowDirectCom2.Controls.Add(MkFixedLabel("本机CI-V地址", 96));
		_txtDirectAddr = new TextBox();
		_txtDirectAddr.Width = 52;
		_txtDirectAddr.MaxLength = 2;
		_txtDirectAddr.Text = "88";
		_txtDirectAddr.TextAlign = HorizontalAlignment.Center;
		Theme.StyleTextBox(_txtDirectAddr);
		_txtDirectAddr.TextChanged += delegate
		{
			SaveDirectCfg();
		};
		_rowDirectCom2.Controls.Add(_txtDirectAddr);
		_rowDirectCom2.Controls.Add(MkFixedLabel("hex，默认 88 = IC-7100", 200));
		AddRow(flowLayoutPanel2, _rowDirectCom2);
		_cboDirectCom.TextChanged += delegate
		{
			SaveDirectCfg();
		};
		_cboDirectBaud.SelectedIndexChanged += delegate
		{
			SaveDirectCfg();
		};
		radHide(AddGroup(flowLayoutPanel2, "云台板串口参数（下发到 S3，非本机 COM）"));
		FlowLayoutPanel flowLayoutPanel20 = Row();
		flowLayoutPanel20.Controls.Add(MkFixedLabel("云台板波特率", 104));
		flowLayoutPanel20.Controls.Add(_cboBaud);
		flowLayoutPanel20.Controls.Add(MkFixedLabel("云台板 CI-V 地址", 116));
		flowLayoutPanel20.Controls.Add(_txtCivAddr);
		flowLayoutPanel20.Controls.Add(MkFixedLabel("TX延时", 62));
		flowLayoutPanel20.Controls.Add(_txtTxDly);
		flowLayoutPanel20.Controls.Add(MkFixedLabel("RX延时", 62));
		flowLayoutPanel20.Controls.Add(_txtRxDly);
		radHide(flowLayoutPanel20);
		AddRow(flowLayoutPanel2, flowLayoutPanel20);
		FlowLayoutPanel flowLayoutPanel21 = Row();
		Button button6 = Btn("应用到云台板（/rset）", delegate
		{
			RigSetSerial();
		});
		button6.Width = 180;
		new ToolTip().SetToolTip(button6, "把这组参数写入 S3（/rset?baud=&addr=&tx=&rx=），改的是云台板连电台的 UART；不影响本机 COM 直连");
		flowLayoutPanel21.Controls.Add(button6);
		flowLayoutPanel21.Controls.Add(MkFixedLabel("只影响 S3 连电台的串口，不影响本机直连 COM", 340));
		radHide(flowLayoutPanel21);
		AddRow(flowLayoutPanel2, flowLayoutPanel21);
		_lblDirect = new Label();
		_lblDirect.AutoSize = false;
		_lblDirect.Width = 1;
		_lblDirect.Height = 1;
		_lblDirect.Visible = false;
		_lblDirect.Text = "直连：未连接";
		_dotNet = MkDot("透传");
		_dotDirect = MkDot("直连");
		AddStatusRow(flowLayoutPanel2, _dotNet, "透传状态");
		FlowLayoutPanel directRow = AddStatusRow(flowLayoutPanel2, _dotDirect, "直连状态");
		_lblDirectTxt = MkFixedLabel("未连接", 430);
		_lblDirectTxt.AutoEllipsis = true;
		directRow.Controls.Add(_lblDirectTxt);
		FitFlowRowHeight(directRow);
		FlowLayoutPanel flowLayoutPanel22 = Row();
		_txtSendHex = new TextBox();
		_txtSendHex.Width = 300;
		_txtSendHex.Text = "FE FE 88 E0 03";
		Theme.StyleTextBox(_txtSendHex);
		flowLayoutPanel22.Controls.Add(MkFixedLabel("手动发帧", 78));
		flowLayoutPanel22.Controls.Add(_txtSendHex);
		flowLayoutPanel22.Controls.Add(Btn("发送 CI-V 帧", delegate
		{
			RigSendHex();
		}));
		radHide(flowLayoutPanel22);
		AddRow(flowLayoutPanel2, flowLayoutPanel22);
		FlowLayoutPanel flowLayoutPanel23 = Row();
		_txtMcGroup = new TextBox();
		_txtMcGroup.Width = 126;
		_txtMcGroup.Text = "239.255.42.99";
		Theme.StyleTextBox(_txtMcGroup);
		_txtMcPort = new TextBox();
		_txtMcPort.Width = 56;
		_txtMcPort.Text = "5004";
		Theme.StyleTextBox(_txtMcPort);
		_txtMcIface = new TextBox();
		_txtMcIface.Width = 108;
		Theme.StyleTextBox(_txtMcIface);
		_lblMcState = new Label();
		_lblMcState.Width = 300;
		_lblMcState.Height = 24;
		_lblMcState.AutoSize = false;
		_lblMcState.Visible = false;
		_lblMcState.TextAlign = ContentAlignment.MiddleLeft;
		_lblMcState.Text = "组播音频：未开始";
		_cboMcDev = new ComboBox();
		_cboMcDev.DropDownStyle = ComboBoxStyle.DropDownList;
		_cboMcDev.Width = 130;
		try
		{
			_cboMcDev.Items.Add("系统默认");
		}
		catch
		{
		}
		_cboMcDev.SelectedIndex = 0;
		flowLayoutPanel23.Controls.Add(MkFixedLabel("组播", 44));
		flowLayoutPanel23.Controls.Add(MkFixedLabel("组播地址", 68));
		flowLayoutPanel23.Controls.Add(_txtMcGroup);
		flowLayoutPanel23.Controls.Add(MkFixedLabel("端口", 44));
		flowLayoutPanel23.Controls.Add(_txtMcPort);
		flowLayoutPanel23.Controls.Add(MkFixedLabel("网卡", 44));
		flowLayoutPanel23.Controls.Add(_txtMcIface);
		flowLayoutPanel23.Controls.Add(MkFixedLabel("输出", 44));
		flowLayoutPanel23.Controls.Add(_cboMcDev);
		_btnMcastToggle = Btn("▶ 开始接收", delegate
		{
			ToggleMcast();
		});
		_btnMcastToggle.Width = 118;
		flowLayoutPanel23.Controls.Add(_btnMcastToggle);
		flowLayoutPanel23.Controls.Add(Btn("\ud83d\udd0a 测试声卡", delegate
		{
			McastToneTest();
		}));
		flowLayoutPanel23.Controls.Add(_lblMcState);
		AddRow(flowLayoutPanel2, flowLayoutPanel23);
		_dotMcast = MkDot("未收包");
		AddStatusRow(flowLayoutPanel2, _dotMcast, "组播状态");
		AddGroup(flowLayoutPanel2, "链路状态 / 原始回读");
		_txtRadioStatus = new TextBox();
		_txtRadioStatus.Multiline = true;
		_txtRadioStatus.ReadOnly = true;
		_txtRadioStatus.ScrollBars = ScrollBars.Vertical;
		_txtRadioStatus.WordWrap = true;
		_txtRadioStatus.Dock = DockStyle.Fill;
		Theme.StyleTextBox(_txtRadioStatus);
		_txtRadioStatus.Font = Theme.FMonoF(9.5f, FontStyle.Bold);
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel();
		tableLayoutPanel2.Dock = DockStyle.Fill;
		tableLayoutPanel2.ColumnCount = 2;
		tableLayoutPanel2.RowCount = 1;
		tableLayoutPanel2.BackColor = Theme.Bg;
		tableLayoutPanel2.MinimumSize = new Size(0, 150);
		tableLayoutPanel2.Height = 150;
		// v1.5.1：左列 430px 会把右侧「原始回读」文本框挤到 114px 宽（几乎没有可读宽度），改成 300px
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		TableLayoutPanel tableLayoutPanel3 = new TableLayoutPanel();
		tableLayoutPanel3.Dock = DockStyle.Fill;
		tableLayoutPanel3.ColumnCount = 2;
		tableLayoutPanel3.RowCount = 2;
		tableLayoutPanel3.BackColor = Theme.Bg;
		tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		tableLayoutPanel3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
		tableLayoutPanel3.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
		tableLayoutPanel3.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
		_lampHttps = MkLamp();
		_lampSerial = MkLamp();
		_lampNet = MkLamp();
		_lampTcp = MkLamp();
		tableLayoutPanel3.Controls.Add(_lampHttps, 0, 0);
		tableLayoutPanel3.Controls.Add(_lampSerial, 1, 0);
		tableLayoutPanel3.Controls.Add(_lampNet, 0, 1);
		tableLayoutPanel3.Controls.Add(_lampTcp, 1, 1);
		tableLayoutPanel2.Controls.Add(tableLayoutPanel3, 0, 0);
		tableLayoutPanel2.Controls.Add(_txtRadioStatus, 1, 0);
		AddRow(flowLayoutPanel2, tableLayoutPanel2);
		// 折叠开关（放在最底部，点开才出现 S3 板载中继 / 云台板串口 / 手动发帧）
		FlowLayoutPanel radAdvToggleRow = Row();
		_radAdvBtn = Btn("▸ 高级链路 / 原始调试（S3 板载中继 · 云台板串口 · 手动发帧）", delegate
		{
			SetRadAdvOpen(!_radAdvOpen);
		});
		_radAdvBtn.Width = 560;
		radAdvToggleRow.Controls.Add(_radAdvBtn);
		AddRow(flowLayoutPanel2, radAdvToggleRow);
		_radAdvCtrls = radAdv;
		LoadDirectCfg();
		_rigTimer = new System.Windows.Forms.Timer();
		_rigTimer.Interval = ClampPollMs(_rigPollMs);
		_rigTimer.Tick += delegate
		{
			RigPollAuto(verbose: false);
		};
		_rigTimer.Start();
		return hudPanel;
	}

	private StatusLamp MkLamp()
	{
		return new StatusLamp
		{
			Dock = DockStyle.Fill,
			Margin = new Padding(0, 0, 3, 3)
		};
	}

	private void RigTune(int dir)
	{
		long num = 10000L;
		string text = _cboStep.SelectedItem as string;
		if (!string.IsNullOrEmpty(text) && double.TryParse(text.Replace(" kHz", ""), out var result))
		{
			num = (long)(result * 1000.0);
		}
		if (!long.TryParse(_lblRigFreqCur, out var result2) || result2 <= 0)
		{
			SetMsg("还没读到当前频率，请先点“立即刷新”");
			return;
		}
		long num2 = result2 + dir * num;
		if (num2 < 30000)
		{
			num2 = 30000L;
		}
		long nextHz = num2;
		long stepK = num;
		RunBg(delegate
		{
			string rig = _s3.GetRig("/rf?f=" + nextHz);
			SetMsg((rig != null && rig.Trim() == "OK") ? ("已调谐到 " + ((double)nextHz / 1000000.0).ToString("0.00000") + " MHz（步进 " + stepK / 1000 + " kHz）") : ("调谐失败：" + (rig ?? _s3.LastError)));
			RigPoll(verbose: false);
		});
	}

	private void RigSetSerial()
	{
		if (_cboBaud.SelectedItem == null || !int.TryParse(_cboBaud.SelectedItem.ToString(), out var result))
		{
			result = 19200;
		}
		int num = 136;
		try
		{
			num = Convert.ToInt32(_txtCivAddr.Text.Trim(), 16);
		}
		catch
		{
			num = 136;
		}
		int num2 = 7;
		int num3 = 8;
		if (int.TryParse(_txtTxDly.Text.Trim(), out var result2) && result2 >= 0 && result2 <= 200)
		{
			num2 = result2;
		}
		if (int.TryParse(_txtRxDly.Text.Trim(), out result2) && result2 >= 0 && result2 <= 200)
		{
			num3 = result2;
		}
		string url = "/rset?baud=" + result + "&addr=" + num.ToString("X") + "&tx=" + num2 + "&rx=" + num3;
		int b2 = result;
		int a2 = num;
		int t2 = num2;
		int r2 = num3;
		RunBg(delegate
		{
			string rig = _s3.GetRig(url);
			SetMsg((rig != null && rig.Trim().StartsWith("OK")) ? ("串口参数已应用：baud=" + b2 + " addr=0x" + a2.ToString("X") + " tx=" + t2 + " rx=" + r2) : ("串口参数失败：" + (rig ?? _s3.LastError)));
			RigPoll(verbose: false);
		});
	}

	private void RigApplyNet(bool on)
	{
		string text = _txtNetIp.Text.Trim();
		int port = 4001;
		int.TryParse(_txtNetPort.Text.Trim(), out port);
		int num = ((_cboNetMode == null || _cboNetMode.SelectedIndex != 1) ? 1 : 0);
		string url = "/rnet?on=" + (on ? "1" : "0") + "&ip=" + Uri.EscapeDataString(text) + "&port=" + port + "&ps=" + num;
		string ip2 = text;
		RunBg(delegate
		{
			string rig = _s3.GetRig(url);
			if (rig == null)
			{
				SetMsg("网络透传设置失败：" + _s3.LastError);
			}
			else
			{
				string text2 = rig.Trim();
				if (!text2.StartsWith("OK"))
				{
					SetMsg("网络透传设置失败：" + text2 + ((text2.IndexOf("ERR:ip", StringComparison.Ordinal) >= 0) ? "（目标 IP 格式不对，应填 IPv4 地址（例如 x.x.x.x））" : ""));
				}
				else
				{
					SetMsg(on ? ("网络透传已启用 → " + ip2 + ":" + port + "（" + text2 + "）") : ("网络透传已停用（" + text2 + "）"));
				}
			}
			RigReadNet();
		});
	}

	private void RigReadNet()
	{
		RunBg(delegate
		{
			RigReadNetBg();
		});
	}

	private void RigReadNetBg()
	{
		string rig = _s3.GetRig("/rnet");
		if (rig == null)
		{
			string err = _s3.LastError;
			UiInvoke(delegate
			{
				if (_lblNetState != null)
				{
					_lblNetState.Text = "读取失败：" + err;
				}
			});
			return;
		}
		string obj = rig.Trim();
		string ip = "";
		string port = "";
		string ps = "";
		string on = "";
		string st = "";
		string[] array = obj.Split(';');
		for (int i = 0; i < array.Length; i++)
		{
			int num = array[i].IndexOf('=');
			if (num > 0)
			{
				string text = array[i].Substring(0, num);
				string text2 = array[i].Substring(num + 1);
				switch (text)
				{
				case "ip":
					ip = text2;
					break;
				case "port":
					port = text2;
					break;
				case "ps":
					ps = text2;
					break;
				case "on":
					on = text2;
					break;
				case "st":
					st = text2;
					break;
				}
			}
		}
		string text3 = "未启用";
		if (on == "1")
		{
			text3 = ((st == "1") ? "连接中" : ((st == "2") ? "已连接" : ((st == "3") ? "失败" : ("状态" + st))));
		}
		string stTxt2 = text3;
		UiInvoke(delegate
		{
			if (_txtNetIp != null && ip.Length > 0)
			{
				_txtNetIp.Text = ip;
			}
			if (_txtNetPort != null && port.Length > 0 && port != "0")
			{
				_txtNetPort.Text = port;
			}
			if (_cboNetMode != null && ps.Length > 0)
			{
				_cboNetMode.SelectedIndex = ((ps == "0") ? 1 : 0);
			}
			if (_lblNetState != null)
			{
				_lblNetState.Text = "透传：" + stTxt2;
				_lblNetState.ForeColor = ((on == "1" && st == "2") ? HudDraw.Green : ((on == "1") ? HudDraw.Warn : Theme.TextDim));
			}
		});
	}

	private void RigSendHex()
	{
		string text = _txtSendHex.Text.Replace(" ", "").Replace("-", "").Trim();
		if (text.Length < 2 || text.Length % 2 != 0 || !IsHexString(text))
		{
			SetMsg("十六进制格式不对（只能是 0-9/A-F，示例：FE FE 88 E0 03）");
			return;
		}
		string full = (text.ToUpperInvariant().StartsWith("FEFE") ? text : (CivHead() + text + "FD"));
		string hex2 = text;
		RunBg(delegate
		{
			if (!DirectReady)
			{
				RigDirectConnect(silent: true);
			}
			if (DirectSend(full))
			{
				SetMsg("已直连发送帧：" + full);
			}
			else
			{
				string rig = _s3.GetRig("/rsend?hex=" + full);
				SetMsg((rig != null && rig.Trim().StartsWith("OK")) ? ("已发送帧：" + hex2) : ("发送失败：" + (rig ?? _s3.LastError)));
			}
		});
	}

	private void RigPollAuto(bool verbose)
	{
		if (_rigPollPaused)
		{
			return;
		}
		_rigFellBack = false;
		if (!ChannelIsDirect)
		{
			RigPoll(verbose);
		}
		else
		{
			if (Interlocked.CompareExchange(ref _civPollStart, 1, 0) != 0)
			{
				return;
			}
			RunBg(delegate
			{
				try
				{
					RigPollDirectBg(verbose);
				}
				finally
				{
					Interlocked.Exchange(ref _civPollStart, 0);
				}
			});
		}
	}

	private void RigPollNow(bool verbose)
	{
		_rigFellBack = false;
		if (!ChannelIsDirect)
		{
			RigPoll(verbose);
			return;
		}
		if (Interlocked.CompareExchange(ref _civPollStart, 1, 0) != 0)
		{
			SetMsg("直连正在读上一轮，请稍候…");
			return;
		}
		RunBg(delegate
		{
			try
			{
				RigPollDirectBg(verbose);
			}
			finally
			{
				Interlocked.Exchange(ref _civPollStart, 0);
			}
		});
	}

	private void RigPollDirectBg(bool verbose)
	{
		try
		{
			if (_civ == null)
			{
				lock (_civGate)
				{
					if (_civ == null)
					{
						_civ = new CivLink();
					}
				}
			}
			AttachCivState();
			_civ.RadioAddr = ReadDirectAddrSafe();
			_civ.ControllerAddr = 224;
			_civ.DefaultTimeoutMs = 900;
			_civ.DefaultRetries = 1;
			if (!_civ.IsOpen)
			{
				bool flag;
				string err;
				if (DirectLinkIsCom)
				{
					int result = 19200;
					if (_cboDirectBaud != null && _cboDirectBaud.SelectedItem != null)
					{
						int.TryParse(_cboDirectBaud.SelectedItem.ToString(), out result);
					}
					string portName = ((_cboDirectCom == null) ? "" : (_cboDirectCom.Text ?? "").Trim().ToUpperInvariant());
					flag = _civ.ConnectCom(portName, result, out err);
				}
				else
				{
					string host = ((_txtDirectIp == null) ? "" : (_txtDirectIp.Text ?? "").Trim());
					int port = DirectPortValue();
					flag = _civ.ConnectTcp(host, port, 2500, out err);
				}
				UiInvoke(delegate
				{
					RefreshDirectDot();
				});
				if (!flag)
				{
					_civPollFail++;
					FallBackToBoard("直连连不上（" + err + "）", verbose);
					return;
				}
			}
			if (_civPoller == null)
			{
				_civPoller = new CivPoller(_civ);
			}
			SyncPollerOptions();
			RigSnapshot rigSnap = _rigSnap;
			RigSnapshot snap = _civPoller.Poll(rigSnap);
			if (!snap.Ok)
			{
				_civPollFail++;
				UiInvoke(delegate
				{
					RefreshDirectDot();
				});
				FallBackToBoard("直连读电台失败：" + snap.Error, verbose);
				return;
			}
			_civPollOk++;
			_directRxHex = _civ.LastRxHex ?? "";
			string[] recent = _civ.RecentFrames;
			long rx = _civ.RxFrames;
			long foreign = _civ.ForeignFrames;
			long ng = _civ.NgReplies;
			long to = _civ.Timeouts;
			string lastTx = _civ.LastTxHex ?? "";
			UiInvoke(delegate
			{
				ApplyDirectStatus(snap, recent, rx, foreign, ng, to, lastTx);
			});
			if (verbose)
			{
				SetMsgSafe("直连读电台完成：" + snap.Log);
			}
		}
		catch (Exception ex)
		{
			_civPollFail++;
			FallBackToBoard("直连读电台异常：" + ex.GetType().Name + "：" + ex.Message, verbose);
		}
	}

	private void FallBackToBoard(string why, bool verbose)
	{
		_rigFellBack = true;
		UiInvoke(delegate
		{
			if (_lampHttps != null)
			{
				_lampHttps.Set("电台通道", "直连失败 → 回退经板子", HudDraw.Warn);
			}
			if (_lblCivSrc != null)
			{
				_lblCivSrc.Text = "回退：经板子 HTTP";
				_lblCivSrc.ForeColor = HudDraw.Warn;
			}
			if (_lblCivMisc != null)
			{
				_lblCivMisc.Text = "直连未读数：" + why;
			}
			SetMsgSafe("⚠ " + why + " → 已回退经板子 HTTP（/rst2）；点「连接直连」或「立即刷新」可重试直连");
		});
		RigPoll(verbose: false);
	}

	private int DirectPortValue()
	{
		int result = 4001;
		if (_txtDirectPort != null && int.TryParse((_txtDirectPort.Text ?? "").Trim(), out var result2) && result2 > 0 && result2 < 65536)
		{
			result = result2;
		}
		return result;
	}

	private byte ReadDirectAddrSafe()
	{
		if (!ReadDirectAddr(out var addr, out var _))
		{
			return 136;
		}
		return addr;
	}

	/// <summary>v1.7：卫星追踪页取当前 CI-V 直连链路（未建立时返回 null，界面提示先连电台）。</summary>
	private CivLink CivForTracking()
	{
		lock (_civGate)
		{
			return _civ;
		}
	}

	/// <summary>v1.7：按 CI-V 地址判断机型（0xA4=IC-705，0x88=IC-7100）。</summary>
	private YzControl.Sat.RigKind RigKindForTracking()
	{
		byte addr = ReadDirectAddrSafe();
		if (addr == 0xA4) return YzControl.Sat.RigKind.Ic705;
		return YzControl.Sat.RigKind.Ic7100;
	}

	private void ApplyDirectStatus(RigSnapshot s, string[] recent, long rx, long foreign, long ng, long timeouts, string lastTx)
	{
		if (!_pageBuilt[1])
		{
			return;
		}
		_rigSnap = s;
		_rigLastUpdate = s.At;
		_rigFellBack = false;
		if (_lblCivSrc != null)
		{
			_lblCivSrc.Text = s.Channel;
			_lblCivSrc.ForeColor = HudDraw.Cyan;
		}
		if (_lblCivTime != null)
		{
			_lblCivTime.Text = s.At.ToString("HH:mm:ss") + "  " + s.RttMs + "ms";
			_lblCivTime.ForeColor = HudDraw.Cyan;
		}
		_hzCache = s.Hz;
		_modeCache = s.Mode;
		_spOnCache = s.SplitOn;
		_spHzCache = (s.HasTx ? s.TxHz : 0);
		_spModeCache = "--";
		_latCache = s.RttMs;
		_linkCache = true;
		_lblRigFreqCur = s.Hz.ToString();
		if (_lblCivSplit != null)
		{
			_lblCivSplit.Text = ((!s.SplitKnown) ? "--" : (s.SplitOn ? "开" : "关"));
			_lblCivSplit.ForeColor = ((s.SplitKnown && s.SplitOn) ? HudDraw.Warn : HudDraw.Cyan);
		}
		if (_lblCivTx != null)
		{
			_lblCivTx.Text = (s.HasTx ? (((double)s.TxHz / 1000000.0).ToString("0.00000") + " MHz") : "--");
			_lblCivTx.ForeColor = (s.HasTx ? HudDraw.Cyan : Theme.TextDim);
		}
		if (_lblCivMode != null)
		{
			_lblCivMode.Text = s.Mode;
		}
		string text = "直连读数：" + s.Log;
		if (s.TxSource.Length > 0)
		{
			text = text + " ｜ 异频来源 " + s.TxSource;
		}
		if (s.Smeter >= 0)
		{
			text = text + " ｜ S表 " + s.Smeter;
		}
		text += " ｜ 收发状态：直连不读（该机 1C 不可靠），以本机 PTT 操作为准";
		if (_lblCivMisc != null)
		{
			_lblCivMisc.Text = text;
		}
		if (_rigDisplay != null)
		{
			_rigDisplay.Set(s.Hz, s.Mode, _rigPttOn, s.RttMs, link: true, s.HasTx ? s.TxHz : 0, "--", s.SplitOn, "直连 " + (DirectLinkIsCom ? "COM" : DirectTargetText()) + (s.SplitOn ? ("    SPLIT 开 · TX " + (s.HasTx ? (((double)s.TxHz / 1000000.0).ToString("0.00000") + " MHz") : "--")) : "") + "    直连轮询 " + _rigPollMs + " ms" + (_rigPollPaused ? "（已暂停）" : ""));
		}
		if (_lampHttps != null)
		{
			_lampHttps.Set("电台通道", "直连 CI-V", HudDraw.Green);
		}
		if (_lampSerial != null)
		{
			_lampSerial.Set("串口链路", "在线", HudDraw.Green);
		}
		if (_lampNet != null)
		{
			_lampNet.Set("网络透传", _rigTunnelOn ? "已连接" : "未启用", _rigTunnelOn ? HudDraw.Green : Theme.TextDim);
		}
		if (_txtRadioStatus != null)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append("通道来源：").Append(s.Channel).Append("\r\n");
			stringBuilder.Append("最后更新：").Append(s.At.ToString("HH:mm:ss.fff")).Append("   往返 ")
				.Append(s.RttMs)
				.Append(" ms\r\n");
			stringBuilder.Append("VFO 频率：").Append(((double)s.Hz / 1000000.0).ToString("0.00000")).Append(" MHz   模式：")
				.Append(s.Mode)
				.Append("\r\n");
			stringBuilder.Append("SPLIT   ：").Append((!s.SplitKnown) ? "--" : (s.SplitOn ? "开" : "关")).Append("   异频(TX)：")
				.Append(s.HasTx ? (((double)s.TxHz / 1000000.0).ToString("0.00000") + " MHz") : "--")
				.Append("   来源：")
				.Append((s.TxSource.Length > 0) ? s.TxSource : "--")
				.Append("\r\n");
			if (s.Smeter >= 0)
			{
				stringBuilder.Append("S 表    ：").Append(s.Smeter).Append("（0000=S0、0120=S9、0241=S9+60dB）\r\n");
			}
			stringBuilder.Append("本轮：").Append(s.Log).Append("\r\n");
			if (lastTx.Length > 0)
			{
				stringBuilder.Append("最近发帧（写通道）：").Append(lastTx).Append("\r\n");
			}
			stringBuilder.Append("统计：成功 ").Append(_civPollOk).Append(" 次 / 失败 ")
				.Append(_civPollFail)
				.Append(" 次");
			stringBuilder.Append("   总线上收到 ").Append(rx).Append(" 帧（其中非本机应答 ")
				.Append(foreign)
				.Append(" 帧，已按 FE FE <本机E0> <电台88> 过滤）  NG ")
				.Append(ng)
				.Append(" 次  超时 ")
				.Append(timeouts)
				.Append(" 次");
			stringBuilder.Append("\r\n");
			stringBuilder.Append("\r\n最近总线上收到的帧（含自己的回显与别的控制器，最能说明链路真况）：\r\n");
			for (int i = 0; i < recent.Length && i < 12; i++)
			{
				stringBuilder.Append("  ").Append(recent[i]).Append("\r\n");
			}
			stringBuilder.Append("\r\n说明：直连 = 本机 TCP 直连 ZQWL 串口服务器，自己成帧发 03/04/0F/25 00（只读）；").Append("此时**不访问云台板**（云台板保持被动，不抢 CI-V 总线）。直连失败才回退经板子 /rst2。");
			_txtRadioStatus.Text = stringBuilder.ToString();
		}
	}

	private void RigReadSplitNow()
	{
		if (!ChannelIsDirect)
		{
			SetMsg("当前通道是「经板子 HTTP」：异频频率来自 /rst2 的 sp=/spn=；要本机直读请切到「① 直连 ZQWL」");
		}
		RigPollNow(verbose: true);
	}

	private void RigPoll(bool verbose)
	{
		if (_rigPolling || _lampHttps == null)
		{
			return;
		}
		_rigPolling = true;
		ThreadPool.QueueUserWorkItem(delegate
		{
			string txt = _s3.GetRig("/rst2" + (verbose ? "?rd=1" : ""));
			string err = _s3.LastError;
			UiInvoke(delegate
			{
				try
				{
					ApplyRigStatus(txt, err, verbose);
				}
				catch (Exception ex)
				{
					SetMsgSafe("电台状态刷新异常：" + ex.Message);
				}
				finally
				{
					_rigPolling = false;
				}
			});
			if (!base.IsHandleCreated || base.IsDisposed)
			{
				_rigPolling = false;
			}
		});
	}

	private void ApplyRigStatus(string txt, string err, bool verbose)
	{
		if (!_pageBuilt[1])
		{
			return;
		}
		if (txt != null)
		{
			_rigFail = 0;
			try
			{
				if (_rigTimer != null && _rigTimer.Interval != _rigPollMs)
				{
					_rigTimer.Interval = _rigPollMs;
				}
			}
			catch
			{
			}
		}
		if (txt == null)
		{
			if (_lampHttps != null)
			{
				_lampHttps.Set("电台通道", _s3.RigTransport + "（读不到电台）", HudDraw.Danger);
			}
			_rigFail++;
			try
			{
				if (_rigTimer != null)
				{
					int num = Math.Max(1000, _rigPollMs);
					_rigTimer.Interval = ((_rigFail <= 3) ? num : _rigPollMs);
				}
			}
			catch
			{
			}
			if (_rigDisplay != null && _rigFail >= 3)
			{
				_rigDisplay.Set(0L, "--", tx: false, -1L, link: false);
			}
			if (_txtRadioStatus != null)
			{
				_txtRadioStatus.Text = "读取电台信息失败。\r\n实际通道：" + _s3.RigTransport + "\r\n错误：" + (err ?? "(未知)") + "\r\n说明：软件会先试 HTTPS(443)，失败自动回退 HTTP(80)；两者都失败说明设备不可达。";
			}
			if (verbose)
			{
				SetMsg("读取电台失败：" + err);
			}
			return;
		}
		string text = "";
		string text2 = "";
		string text3 = "";
		string text4 = "";
		string text5 = "";
		string text6 = "";
		string text7 = "";
		string text8 = "";
		string s = "";
		string text9 = "";
		string text10 = "";
		string text11 = "";
		string text12 = "";
		string vol = "";
		string sql = "";
		string vm = "";
		string ch = "";
		string grp = "";
		string text13 = "";
		string[] array = txt.Split(';');
		for (int i = 0; i < array.Length; i++)
		{
			int num2 = array[i].IndexOf('=');
			if (num2 > 0)
			{
				string text14 = array[i].Substring(0, num2);
				string text15 = array[i].Substring(num2 + 1);
				switch (text14)
				{
				case "f":
					text = text15;
					break;
				case "m":
					text2 = text15;
					break;
				case "ptt":
					text3 = text15;
					break;
				case "on":
					text4 = text15;
					break;
				case "seen":
					text5 = text15;
					break;
				case "net":
					text6 = text15;
					break;
				case "tcp":
					text7 = text15;
					break;
				case "lat":
					text8 = text15;
					break;
				case "sp":
					s = text15;
					break;
				case "spm":
					text9 = text15;
					break;
				case "spn":
					text10 = text15;
					break;
				case "baud":
					text11 = text15;
					break;
				case "addr":
					text12 = text15;
					break;
				case "vol":
					vol = text15;
					break;
				case "sql":
					sql = text15;
					break;
				case "vm":
					vm = text15;
					break;
				case "ch":
					ch = text15;
					break;
				case "grp":
					grp = text15;
					break;
				case "path":
					text13 = text15;
					break;
				}
			}
		}
		if (text.Length == 0 && text4.Length == 0 && text5.Length == 0)
		{
			if (_lampHttps != null)
			{
				_lampHttps.Set("电台通道", _s3.RigTransport + "（响应格式异常）", HudDraw.Danger);
			}
			if (_txtRadioStatus != null)
			{
				_txtRadioStatus.Text = "电台状态响应格式异常（HTTP 200 但不是 /rst2 状态串）。\r\n实际通道：" + _s3.RigTransport + "\r\n前 200 字符：" + ((txt.Length > 200) ? txt.Substring(0, 200) : txt) + "\r\n说明：请确认地址栏填的是 S3 的 IP（当前 " + _s3.Host + "），且 80/443 未被其它设备占用。";
			}
			if (verbose)
			{
				SetMsg("电台状态响应格式异常：请检查设备地址");
			}
			return;
		}
		long result = 0L;
		long.TryParse(text, out result);
		_lblRigFreqCur = result.ToString();
		string text16 = ((string.IsNullOrEmpty(text2) || text2 == "---") ? "--" : text2);
		bool flag = (_rigPttOn = text3 == "1");
		_lampHttps.Set("电台通道", (ChannelIsDirect ? "回退经板子 " : "经板子 ") + _s3.RigTransport, (_s3.RigTransport == "HTTPS") ? HudDraw.Green : HudDraw.Warn);
		long result2 = 0L;
		long.TryParse(text5, out result2);
		bool flag2 = text4 == "1" || (result2 >= 0 && result2 < 8);
		if (_rigDisplay != null)
		{
			long result3 = 0L;
			long.TryParse(s, out result3);
			if (result3 < 30000)
			{
				result3 = 0L;
			}
			bool flag3 = text10 == "1";
			_hzCache = result;
			_modeCache = text16;
			_linkCache = flag2;
			_spHzCache = result3;
			_spOnCache = flag3;
			_spModeCache = ((string.IsNullOrEmpty(text9) || text9 == "---") ? "--" : text9);
			_rigTunnelOn = text6 == "1" || text6 == "2";
			if (long.TryParse(text8, out var result4))
			{
				_latCache = result4;
			}
			_rigDisplay.Set(result, text16, flag, _latCache, flag2, result3, _spModeCache, flag3, "串口 " + text11 + " / 地址 0x" + text12 + "    透传 " + ((text6 == "2") ? "已连接(占用链路,本机不能发射)" : ((text6 == "1") ? "连接中" : "未启用")) + "    在线 " + text5 + " s" + (flag3 ? ("    SPLIT 开 · TX " + ((result3 > 0) ? (((double)result3 / 1000000.0).ToString("0.00000") + " MHz") : "--") + ((string.IsNullOrEmpty(_spModeCache) || _spModeCache == "--") ? "" : (" " + _spModeCache))) : ""));
		}
		_rigLastUpdate = DateTime.Now;
		_rigSnap = new RigSnapshot
		{
			Ok = true,
			Hz = result,
			Mode = text16,
			SplitKnown = (text10.Length > 0),
			SplitOn = (text10 == "1"),
			TxHz = ParseLongSafe(s),
			TxSource = "经板子 /rst2(sp)",
			At = _rigLastUpdate,
			Channel = (ChannelIsDirect ? "回退：经板子 " : "经板子 ") + _s3.RigTransport
		};
		if (_lblCivSrc != null)
		{
			_lblCivSrc.Text = _rigSnap.Channel;
			_lblCivSrc.ForeColor = (ChannelIsDirect ? HudDraw.Warn : HudDraw.Cyan);
		}
		if (_lblCivTime != null)
		{
			_lblCivTime.Text = _rigLastUpdate.ToString("HH:mm:ss") + "  lat=" + text8;
			_lblCivTime.ForeColor = HudDraw.Cyan;
		}
		if (_lblCivSplit != null)
		{
			_lblCivSplit.Text = ((!_rigSnap.SplitKnown) ? "--" : (_rigSnap.SplitOn ? "开" : "关"));
			_lblCivSplit.ForeColor = (_rigSnap.SplitOn ? HudDraw.Warn : HudDraw.Cyan);
		}
		if (_lblCivTx != null)
		{
			_lblCivTx.Text = (_rigSnap.HasTx ? (((double)_rigSnap.TxHz / 1000000.0).ToString("0.00000") + " MHz") : "--");
			_lblCivTx.ForeColor = (_rigSnap.HasTx ? HudDraw.Cyan : Theme.TextDim);
		}
		if (_lblCivMode != null)
		{
			_lblCivMode.Text = text16;
		}
		if (_lblCivMisc != null)
		{
			_lblCivMisc.Text = "经板子 /rst2（sp=异频频率, spn=SPLIT 开关） ｜ 收发 ptt=" + (flag ? "发射" : "接收") + " ｜ CI-V 通路 " + ((text13.Length > 0) ? text13 : "--") + (_rigFellBack ? " ｜ ⚠ 本轮是直连失败后的回退（直连通道本轮未读到）" : "");
		}
		_lampSerial.Set("串口链路", flag2 ? "在线" : ("未见电台 " + result2 + "s"), flag2 ? HudDraw.Green : HudDraw.Warn);
		string value = "未启用";
		Color color = Theme.TextDim;
		switch (text6)
		{
		case "1":
			value = "连接中";
			color = HudDraw.Warn;
			break;
		case "2":
			value = "已连接";
			color = HudDraw.Green;
			break;
		case "3":
		case "4":
			value = "失败/已停";
			color = HudDraw.Danger;
			break;
		}
		_lampNet.Set("网络透传", value, color);
		bool on = text6 == "1" || text6 == "2";
		switch (text6)
		{
		case "1":
			SetDotTip(_dotNet, DotState.Busy, "连接中", "网络透传：连接中（net=1）");
			break;
		case "2":
			SetDotTip(_dotNet, DotState.Ok, "已连接", "网络透传：已连接（net=2，占用链路，本机不能发射）");
			break;
		case "3":
		case "4":
			SetDotTip(_dotNet, DotState.Fail, "未连接", "网络透传：连接失败/已停止（net=" + text6 + "）");
			break;
		default:
			SetDotTip(_dotNet, DotState.Off, "未启用", "网络透传：未启用（net=" + text6 + "）");
			break;
		}
		RefreshNetButton(on);
		RefreshDirectButton();
		_lampTcp.Set("rigctld 4532", (text7 == "1") ? "已连接" : "未连接", (text7 == "1") ? HudDraw.Green : Theme.TextDim);
		ApplyRigLevels(vol, sql, vm, ch, grp, text13);
		_txtRadioStatus.Text = "HTTPS 端点：https://" + _s3.Host + "/rst2\r\n原始状态：" + txt + "\r\n时延 lat=" + text8 + " ms     收发 ptt=" + (flag ? "发射" : "接收") + "     CI-V 通路 path=" + ((text13.Length > 0) ? text13 : "--") + "\r\n提示：控制走 HTTPS（/rf 设频率、/rm 设模式、/rptt 收发、/rlvl 音量静噪、/rvm 信道/VFO、/rch 频道±与跳转、/rgrp 存储组 A~E）；网页版含 Web Serial，仅 Chrome/Edge 可用，本控制台不依赖它。";
		if (verbose)
		{
			SetMsg("电台状态已刷新（HTTPS）");
		}
	}

	private static TrackBar MkLvlSlider()
	{
		return new TrackBar
		{
			Minimum = 0,
			Maximum = 255,
			Value = 0,
			TickStyle = TickStyle.None,
			AutoSize = false,
			Height = 26,
			Width = 152,
			BackColor = Theme.SurfaceAlt,
			Margin = new Padding(2, 8, 2, 2)
		};
	}

	private static Label MkLvlValue(int w)
	{
		return new Label
		{
			Width = Math.Max(w, TextRenderer.MeasureText("--", Theme.FMono).Width + 8),
			Height = 24,
			Text = "--",
			TextAlign = ContentAlignment.MiddleCenter,
			ForeColor = HudDraw.Cyan,
			Font = Theme.FMono,
			Margin = new Padding(2, 8, 6, 2)
		};
	}

	private static Label MkFixedValue(int w)
	{
		return new Label
		{
			AutoSize = false,
			Text = "--",
			Width = Math.Max(w, 60),
			Height = 24,
			TextAlign = ContentAlignment.MiddleLeft,
			ForeColor = HudDraw.Cyan,
			Font = Theme.FMono,
			BackColor = Color.Transparent,
			Margin = new Padding(2, 8, 10, 2)
		};
	}

	private static NeoToggle MkToggle(string text, int marginRight)
	{
		NeoToggle t = new NeoToggle();
		t.Text = text;
		t.AutoSize = false;
		t.Height = 28;
		t.Margin = new Padding(2, 4, marginRight, 4);
		Theme.StyleToggle(t);
		t.Width = Theme.ToggleWidth(t) + 24;
		EventHandler value = delegate
		{
			try
			{
				t.Width = Theme.ToggleWidth(t) + 24;
			}
			catch
			{
			}
		};
		t.HandleCreated += value;
		return t;
	}

	private static Label MkFixedLabel(string text, int w)
	{
		return new Label
		{
			AutoSize = false,
			Text = text,
			Width = Math.Max(w, TextRenderer.MeasureText(text ?? "", Theme.FBody).Width + 8),
			Height = 20,
			TextAlign = ContentAlignment.MiddleLeft,
			ForeColor = Theme.TextDim,
			BackColor = Color.Transparent,
			Margin = new Padding(4, 9, 14, 2)
		};
	}

	private static Label MkWrapLabel(int maxWidth, bool bold)
	{
		Label obj = new Label
		{
			AutoSize = true,
			MaximumSize = new Size(maxWidth, 0),
			Margin = new Padding(3, 6, 10, 2),
			TextAlign = ContentAlignment.MiddleLeft
		};
		Theme.StyleLabel(obj, bold);
		return obj;
	}

	private static Color SevColor(YzSev sev)
	{
		return sev switch
		{
			YzSev.Bad => Theme.Danger, 
			YzSev.Warn => Theme.Warn, 
			_ => Theme.TextDim, 
		};
	}

	private void RigLvlChanged(bool sql)
	{
		TrackBar trackBar = (sql ? _tbRigSql : _tbRigVol);
		Label label = (sql ? _lblRigSqlVal : _lblRigVolVal);
		if (trackBar == null || _rigLvlSync)
		{
			return;
		}
		int value = trackBar.Value;
		if (label != null)
		{
			label.Text = value.ToString();
		}
		int tickCount = Environment.TickCount;
		if (sql)
		{
			if (tickCount - _rigSqlAt >= 150)
			{
				_rigSqlAt = tickCount;
				_rigSqlPending = -1;
				RigSendLevel(sql: true, value);
			}
			else
			{
				_rigSqlPending = value;
			}
		}
		else if (tickCount - _rigVolAt >= 150)
		{
			_rigVolAt = tickCount;
			_rigVolPending = -1;
			RigSendLevel(sql: false, value);
		}
		else
		{
			_rigVolPending = value;
		}
	}

	private void RigLvlFlush()
	{
		int tickCount = Environment.TickCount;
		if (_rigVolPending >= 0 && tickCount - _rigVolAt >= 150)
		{
			int rigVolPending = _rigVolPending;
			_rigVolPending = -1;
			_rigVolAt = tickCount;
			RigSendLevel(sql: false, rigVolPending);
		}
		if (_rigSqlPending >= 0 && tickCount - _rigSqlAt >= 150)
		{
			int rigSqlPending = _rigSqlPending;
			_rigSqlPending = -1;
			_rigSqlAt = tickCount;
			RigSendLevel(sql: true, rigSqlPending);
		}
	}

	private bool RigPathOk()
	{
		if (_rigPathCur.Length != 0 && !(_rigPathCur == "tcp"))
		{
			return _rigPathCur == "uart";
		}
		return true;
	}

	private string RigPathWarn()
	{
		if (!RigPathOk())
		{
			return "（⚠ CI-V 通路 " + _rigPathCur + "：命令可能未送达电台）";
		}
		return "";
	}

	private void RigSendLevel(bool sql, int v)
	{
		string url = "/rlvl?lvl=" + (sql ? "sql" : "vol") + "&v=" + v;
		string what = (sql ? ("静噪 SQL " + v) : ("音量 AF " + v));
		ThreadPool.QueueUserWorkItem(delegate
		{
			string text = _s3.Get(url);
			bool flag = text?.Trim().StartsWith("OK") ?? false;
			SetMsgSafe(flag ? ("已设置" + what + RigPathWarn()) : (what + " 设置失败：" + (text ?? _s3.LastError)));
		});
	}

	private void RigSetVm(bool mem)
	{
		_rigVmCur = (mem ? "mem" : "vfo");
		RigPaintVm();
		string url = "/rvm?m=" + (mem ? "mem" : "vfo");
		ThreadPool.QueueUserWorkItem(delegate
		{
			string text = _s3.Get(url);
			bool flag = text?.Trim().StartsWith("OK") ?? false;
			SetMsgSafe(flag ? ("已切" + (mem ? "记忆(MEM)" : "VFO") + "模式" + RigPathWarn()) : ("切换失败：" + (text ?? _s3.LastError)));
		});
	}

	private void RigChStep(bool up)
	{
		string url = "/rch?d=" + (up ? "up" : "dn");
		ThreadPool.QueueUserWorkItem(delegate
		{
			string text = _s3.Get(url);
			if (text == null)
			{
				SetMsgSafe("频道切换失败：" + _s3.LastError);
			}
			else
			{
				string text2 = text.Trim();
				if (!text2.StartsWith("OK"))
				{
					SetMsgSafe("频道切换失败：" + text2);
				}
				else
				{
					int num = text2.IndexOf("ch=", StringComparison.Ordinal);
					string chTxt = ((num >= 0) ? text2.Substring(num + 3).Trim() : "");
					UiInvoke(delegate
					{
						if (chTxt.Length > 0)
						{
							_rigChCur = chTxt;
						}
						RigPaintCh();
					});
					SetMsgSafe("记忆频道 " + ((chTxt.Length > 0) ? chTxt : "--") + RigPathWarn());
				}
			}
		});
	}

	private void RigChJump()
	{
		if (_txtRigChN == null || !int.TryParse((_txtRigChN.Text ?? "").Trim(), out var i) || i < 1 || i > 99)
		{
			SetMsg("频道号需 1~99（请输入后点“跳转”或回车）");
			return;
		}
		string url = "/rch?n=" + i;
		ThreadPool.QueueUserWorkItem(delegate
		{
			string text = _s3.Get(url);
			if (text == null)
			{
				SetMsgSafe("频道跳转失败：" + _s3.LastError);
			}
			else
			{
				string text2 = text.Trim();
				if (!text2.StartsWith("OK"))
				{
					SetMsgSafe("频道跳转失败：" + text2);
				}
				else
				{
					int num = text2.IndexOf("ch=", StringComparison.Ordinal);
					string text3 = ((num >= 0) ? text2.Substring(num + 3).Trim() : "");
					if (text3.Length == 0)
					{
						text3 = i.ToString();
					}
					string show = text3;
					UiInvoke(delegate
					{
						_rigChCur = show;
						RigPaintCh();
					});
					SetMsgSafe("已跳转记忆频道 " + show + RigPathWarn());
				}
			}
		});
	}

	private void ApplyRigLevels(string vol, string sql, string vm, string ch, string grp, string path)
	{
		int result;
		if (!_rigVolDrag)
		{
			if (int.TryParse(vol, out result) && result >= 0)
			{
				if (_tbRigVol != null)
				{
					_rigLvlSync = true;
					try
					{
						_tbRigVol.Value = Math.Min(255, Math.Max(0, result));
					}
					finally
					{
						_rigLvlSync = false;
					}
				}
				if (_lblRigVolVal != null)
				{
					_lblRigVolVal.Text = result.ToString();
				}
			}
			else if (_lblRigVolVal != null)
			{
				_lblRigVolVal.Text = "--";
			}
		}
		if (!_rigSqlDrag)
		{
			if (int.TryParse(sql, out result) && result >= 0)
			{
				if (_tbRigSql != null)
				{
					_rigLvlSync = true;
					try
					{
						_tbRigSql.Value = Math.Min(255, Math.Max(0, result));
					}
					finally
					{
						_rigLvlSync = false;
					}
				}
				if (_lblRigSqlVal != null)
				{
					_lblRigSqlVal.Text = result.ToString();
				}
			}
			else if (_lblRigSqlVal != null)
			{
				_lblRigSqlVal.Text = "--";
			}
		}
		if (vm == "vfo" || vm == "mem")
		{
			_rigVmCur = vm;
			RigPaintVm();
		}
		if (int.TryParse(ch, out result) && result > 0)
		{
			_rigChCur = result.ToString();
		}
		RigPaintCh();
		if (int.TryParse(grp, out result) && result >= 1 && result <= 5)
		{
			_rigGrpCur = result.ToString();
		}
		else
		{
			_rigGrpCur = "0";
		}
		RigPaintGrp();
		_rigPathCur = (path ?? "").Trim();
		if (_lblRigPath != null)
		{
			_lblRigPath.Text = ((_rigPathCur.Length > 0) ? _rigPathCur : "--");
			bool flag = _rigPathCur == "tcp" || _rigPathCur == "uart";
			_lblRigPath.ForeColor = (flag ? HudDraw.Cyan : HudDraw.Danger);
		}
	}

	private void RigSetGrp(int g)
	{
		if (g < 1 || g > 5)
		{
			return;
		}
		string url = "/rgrp?g=" + g;
		ThreadPool.QueueUserWorkItem(delegate
		{
			string text = _s3.Get(url);
			if (text == null)
			{
				SetMsgSafe("存储组切换失败：" + _s3.LastError);
			}
			else
			{
				string text2 = text.Trim();
				if (!text2.StartsWith("OK"))
				{
					SetMsgSafe("存储组切换失败：" + text2);
				}
				else
				{
					string text3 = ((char)(65 + g - 1)).ToString();
					UiInvoke(delegate
					{
						_rigGrpCur = g.ToString();
						RigPaintGrp();
					});
					SetMsgSafe("已切存储组 " + text3 + "（显示为最后设置值；电台无读组命令）" + RigPathWarn());
				}
			}
		});
	}

	private void RigPaintGrp()
	{
		int.TryParse(_rigGrpCur, out var result);
		for (int i = 0; i < 5; i++)
		{
			if (_btnRigGrp[i] != null)
			{
				SetVmHighlight(_btnRigGrp[i], i + 1 == result);
			}
		}
		if (_lblRigGrp != null)
		{
			_lblRigGrp.Text = ((result >= 1 && result <= 5) ? ((char)(65 + result - 1)).ToString() : "--");
		}
	}

	private void RigPaintVm()
	{
		SetVmHighlight(_btnRigVfo, _rigVmCur == "vfo");
		SetVmHighlight(_btnRigMem, _rigVmCur == "mem");
	}

	private static void SetVmHighlight(Button b, bool on)
	{
		if (b is TechButton techButton)
		{
			techButton.Highlight = on;
		}
	}

	private void RigPaintCh()
	{
		if (_lblRigCh != null)
		{
			_lblRigCh.Text = ((int.TryParse(_rigChCur, out var result) && result > 0) ? result.ToString() : "--");
		}
	}

	private void UiInvoke(MethodInvoker a)
	{
		try
		{
			if (base.IsHandleCreated && !base.IsDisposed)
			{
				BeginInvoke(a);
			}
		}
		catch
		{
		}
	}

	private void RigSetFreq()
	{
		if (!double.TryParse(_txtRigFreq.Text.Trim(), out var result) || result <= 0.0)
		{
			SetMsg("频率格式不对，请填 MHz，例如 145.500");
			return;
		}
		long num = (long)Math.Round(result * 1000000.0);
		if (num < 30000)
		{
			num = (long)Math.Round(result);
		}
		if (num < 30000 || num > 2000000000)
		{
			SetMsg("频率超出允许范围（0.03 ~ 2000 MHz）：请填 MHz（如 145.500）或完整 Hz（如 145500000）");
			return;
		}
		long hz2 = num;
		RunBg(delegate
		{
			if (!DirectReady)
			{
				RigDirectConnect(silent: true);
			}
			if (DirectFreq(hz2))
			{
				SetMsg("已设置频率 " + ((double)hz2 / 1000000.0).ToString("0.00000") + " MHz（直连 " + DirectTargetText() + "，本机直发电台）");
				RigPoll(verbose: false);
			}
			else
			{
				string rig = _s3.GetRig("/rf?f=" + hz2);
				SetMsg((rig != null && rig.Trim() == "OK") ? ("已设置频率 " + ((double)hz2 / 1000000.0).ToString("0.00000") + " MHz（HTTPS）") : ("设置频率失败：" + (rig ?? _s3.LastError)));
				RigPoll(verbose: false);
			}
		});
	}

	private void RigSetMode()
	{
		string i = _cboRigMode.SelectedItem as string;
		if (string.IsNullOrEmpty(i))
		{
			return;
		}
		RunBg(delegate
		{
			if (!DirectReady)
			{
				RigDirectConnect(silent: true);
			}
			if (DirectMode(i))
			{
				SetMsg("已设置模式 " + i + "（直连，经透传通道）");
				RigPoll(verbose: false);
			}
			else
			{
				string rig = _s3.GetRig("/rm?m=" + i);
				SetMsg((rig != null && rig.Trim() == "OK") ? ("已设置模式 " + i + "（HTTPS）") : ("设置模式失败：" + (rig ?? _s3.LastError)));
				RigPoll(verbose: false);
			}
		});
	}

	private void SetMsgSafe(string m)
	{
		try
		{
			if (base.IsHandleCreated && !base.IsDisposed)
			{
				BeginInvoke((MethodInvoker)delegate
				{
					SetMsg(m);
				});
			}
		}
		catch
		{
		}
	}

	private void StartPttKeep()
	{
		if (_pttKeep == null)
		{
			_pttKeep = new System.Windows.Forms.Timer();
			_pttKeep.Interval = 900;
			_pttKeep.Tick += delegate
			{
				if (!_rigPttOn)
				{
					_pttKeep.Stop();
				}
				else if (!_pttKeepBusy)
				{
					_pttKeepBusy = true;
					ThreadPool.QueueUserWorkItem(delegate
					{
						try
						{
							_s3.GetRig("/rptt?x=1");
						}
						catch
						{
						}
						finally
						{
							_pttKeepBusy = false;
						}
					});
				}
			};
		}
		_pttKeep.Start();
	}

	private void StopPttKeep()
	{
		if (_pttKeep != null)
		{
			_pttKeep.Stop();
		}
	}

	private static bool IsHexString(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return false;
		}
		foreach (char c in s)
		{
			if ((c < '0' || c > '9') && (c < 'a' || c > 'f') && (c < 'A' || c > 'F'))
			{
				return false;
			}
		}
		return true;
	}

	private void RigCiv(string payload)
	{
		string hex = CivHead() + payload + "FD";
		string full = (hex.ToUpperInvariant().StartsWith("FEFE") ? hex : (CivHead() + hex + "FD"));
		RunBg(delegate
		{
			if (DirectReady && DirectSend(hex))
			{
				SetMsg("已直连发送 CI-V 帧：" + hex);
			}
			else
			{
				if (!DirectReady)
				{
					RigDirectConnect(silent: true);
				}
				if (DirectSend(full))
				{
					SetMsg("已直连发送帧：" + full);
				}
				else
				{
					string rig = _s3.GetRig("/rsend?hex=" + full);
					SetMsg((rig != null && rig.Trim().StartsWith("OK")) ? ("已发送 CI-V 帧：" + hex) : ("发送失败：" + (rig ?? _s3.LastError)));
				}
			}
		});
	}

	private void RigDropTunnel()
	{
		RunBg(delegate
		{
			string rig = _s3.GetRig("/rnet?on=0");
			SetMsg((rig != null) ? "已停用透传，CI-V 链路已切回本机串口（PTT 可用）" : ("停用失败：" + _s3.LastError));
			RigReadNet();
			RigPoll(verbose: true);
		});
	}

	private void ApplyDirectLinkMode(string mode)
	{
		bool flag = string.Equals(mode, "com", StringComparison.OrdinalIgnoreCase);
		if (_rowDirectCom != null)
		{
			_rowDirectCom.Visible = flag;
		}
		if (_btnDirect != null)
		{
			_btnDirect.Enabled = !DirectReady;
		}
		if (_rowDirectCom != null)
		{
			FitFlowRowHeight(_rowDirectCom);
		}
		SaveDirectCfg();
	}

	private void SaveDirectCfg()
	{
		try
		{
			_cfg.DirectLink = (DirectLinkIsCom ? "com" : "tcp");
			if (_cboDirectCom != null)
			{
				_cfg.DirectCom = _cboDirectCom.Text.Trim();
			}
			if (_cboDirectBaud != null && _cboDirectBaud.SelectedItem != null && int.TryParse(_cboDirectBaud.SelectedItem.ToString(), out var result))
			{
				_cfg.DirectBaud = result;
			}
			if (_txtDirectAddr != null)
			{
				_cfg.DirectCivAddr = _txtDirectAddr.Text.Trim().ToUpperInvariant();
			}
			_cfg.RigChannel = _rigChannel;
			if (_txtDirectIp != null)
			{
				_cfg.DirectIp = _txtDirectIp.Text.Trim();
			}
			if (_txtDirectPort != null && int.TryParse(_txtDirectPort.Text.Trim(), out var result2) && result2 > 0 && result2 < 65536)
			{
				_cfg.DirectPort = result2;
			}
			_cfg.CivPollMs = _rigPollMs;
			_cfg.CivPaused = _rigPollPaused;
			if (_tgLegacy1C != null)
			{
				_cfg.CivLegacy1C = _tgLegacy1C.Checked;
			}
			if (_tgDeepVfoB != null)
			{
				_cfg.CivDeepVfoB = _tgDeepVfoB.Checked;
			}
			if (_tgReadSmeter != null)
			{
				_cfg.CivReadSmeter = _tgReadSmeter.Checked;
			}
			SaveConfig();
		}
		catch
		{
		}
	}

	private void LoadDirectCfg()
	{
		try
		{
			bool flag = string.Equals(_cfg.DirectLink, "com", StringComparison.OrdinalIgnoreCase);
			if (_rbDirectCom != null)
			{
				_rbDirectCom.Checked = flag;
			}
			if (_rbDirectTcp != null)
			{
				_rbDirectTcp.Checked = !flag;
			}
			if (_cboDirectCom != null)
			{
				RefreshComPorts();
				string text = (string.IsNullOrEmpty(_cfg.DirectCom) ? "COM2" : _cfg.DirectCom);
				if (_cboDirectCom.Items.Contains(text))
				{
					_cboDirectCom.SelectedItem = text;
				}
				else
				{
					_cboDirectCom.Text = text;
				}
			}
			if (_cboDirectBaud != null)
			{
				string text2 = ((_cfg.DirectBaud > 0) ? _cfg.DirectBaud.ToString() : "19200");
				if (_cboDirectBaud.Items.Contains(text2))
				{
					_cboDirectBaud.SelectedItem = text2;
				}
				else
				{
					_cboDirectBaud.SelectedIndex = 2;
				}
			}
			if (_txtDirectAddr != null)
			{
				string text3 = (string.IsNullOrEmpty(_cfg.DirectCivAddr) ? "88" : _cfg.DirectCivAddr.ToUpperInvariant());
				_txtDirectAddr.Text = text3;
			}
			_rigChannel = (string.Equals(_cfg.RigChannel, "board", StringComparison.OrdinalIgnoreCase) ? "board" : "direct");
			if (_txtDirectIp != null)
			{
				_txtDirectIp.Text = (string.IsNullOrEmpty(_cfg.DirectIp) ? "172.16.1.123" : _cfg.DirectIp.Trim());
			}
			if (_txtDirectPort != null)
			{
				int num = ((_cfg.DirectPort > 0 && _cfg.DirectPort < 65536) ? _cfg.DirectPort : 4001);
				_txtDirectPort.Text = num.ToString();
			}
			_rigPollMs = ClampPollMs(_cfg.CivPollMs);
			if (_cboCivPoll != null)
			{
				_cboCivPoll.SelectedIndex = ((_rigPollMs > 1000) ? ((_rigPollMs <= 2000) ? 1 : 2) : 0);
			}
			_rigPollPaused = _cfg.CivPaused;
			if (_tgLegacy1C != null)
			{
				_tgLegacy1C.Checked = _cfg.CivLegacy1C;
			}
			if (_tgDeepVfoB != null)
			{
				_tgDeepVfoB.Checked = _cfg.CivDeepVfoB;
			}
			if (_tgReadSmeter != null)
			{
				_tgReadSmeter.Checked = _cfg.CivReadSmeter;
			}
			RefreshChannelToggle();
			RefreshPauseButton();
			ApplyCivPollMs();
			ApplyDirectLinkMode(flag ? "com" : "tcp");
		}
		catch
		{
		}
	}

	private static int ClampPollMs(int ms)
	{
		if (ms < 1000)
		{
			return 1000;
		}
		if (ms > 60000)
		{
			return 60000;
		}
		return ms;
	}

	private void RefreshChannelToggle()
	{
		if (_tgRigChannel == null)
		{
			return;
		}
		bool channelIsDirect = ChannelIsDirect;
		string text = (channelIsDirect ? ("① 直连 ZQWL（主用） " + DirectTargetText()) : "② 经板子 HTTP（回退）");
		if (_tgRigChannel.Text != text)
		{
			_tgRigChannel.Text = text;
		}
		try
		{
			_tgRigChannel.Width = Theme.ToggleWidth(_tgRigChannel) + 24;
		}
		catch
		{
		}
		if (_tgRigChannel.Checked == channelIsDirect)
		{
			return;
		}
		_chToggleGuard = true;
		try
		{
			_tgRigChannel.Checked = channelIsDirect;
		}
		finally
		{
			_chToggleGuard = false;
		}
	}

	private void ApplyRigChannel()
	{
		if (_chToggleGuard)
		{
			return;
		}
		_rigChannel = (_tgRigChannel.Checked ? "direct" : "board");
		RefreshChannelToggle();
		SaveDirectCfg();
		if (ChannelIsDirect)
		{
			SetMsg("电台通道 = ① 直连 " + DirectTargetText() + "（本机自己读 CI-V；云台板保持被动，不抢总线）");
			RigPollNow(verbose: true);
			return;
		}
		RunBg(delegate
		{
			RigDirectDisconnect();
		});
		SetMsg("电台通道 = ② 经板子 HTTP（沿用既有 /rst2；直连链路已断开）");
		RigPollNow(verbose: true);
	}

	private string DirectTargetText()
	{
		string text = ((_txtDirectIp == null) ? "" : (_txtDirectIp.Text ?? "").Trim());
		string text2 = ((_txtDirectPort == null) ? "" : (_txtDirectPort.Text ?? "").Trim());
		if (text.Length == 0)
		{
			return "(未填 IP)";
		}
		return text + ":" + ((text2.Length == 0) ? "4001" : text2);
	}

	private void ApplyCivPollMs()
	{
		try
		{
			int ms = 2000;
			if (_cboCivPoll != null && _cboCivPoll.SelectedIndex == 0)
			{
				ms = 1000;
			}
			else if (_cboCivPoll != null && _cboCivPoll.SelectedIndex == 2)
			{
				ms = 5000;
			}
			_rigPollMs = ClampPollMs(ms);
			if (_rigTimer != null)
			{
				_rigTimer.Interval = _rigPollMs;
			}
			SaveDirectCfg();
		}
		catch
		{
		}
	}

	private void ToggleRigPause()
	{
		_rigPollPaused = !_rigPollPaused;
		RefreshPauseButton();
		SaveDirectCfg();
		SetMsg(_rigPollPaused ? "已暂停电台轮询（不再发任何 CI-V/HTTP 请求；「立即刷新」仍可手动读一次）" : ("已恢复电台轮询（直连通道周期 " + _rigPollMs + " ms）"));
	}

	private void RefreshPauseButton()
	{
		if (_btnCivPause != null)
		{
			_btnCivPause.Text = (_rigPollPaused ? "▶ 恢复轮询" : "⏸ 暂停轮询");
		}
	}

	private bool RigDirectConnect(bool silent)
	{
		if (_civ == null)
		{
			lock (_civGate)
			{
				if (_civ == null)
				{
					_civ = new CivLink();
				}
			}
			AttachCivState();
		}
		// v1.6.0：用户点「连接直连」= 手动重试，先清掉自动重连的退避窗口
		_civ.ResetLink();
		if (!ReadDirectAddr(out var addr, out var err))
		{
			if (!silent)
			{
				SetMsg("直连失败：" + err);
			}
			SetDotTip(_dotDirect, DotState.Fail, "地址码非法", err);
			return false;
		}
		_civ.RadioAddr = addr;
		_civ.ControllerAddr = 224;
		_civ.DefaultTimeoutMs = 900;
		_civ.DefaultRetries = 1;
		bool flag;
		string err2;
		if (DirectLinkIsCom)
		{
			int result = 19200;
			if (_cboDirectBaud != null && _cboDirectBaud.SelectedItem != null)
			{
				int.TryParse(_cboDirectBaud.SelectedItem.ToString(), out result);
			}
			string portName = ((_cboDirectCom == null) ? "" : (_cboDirectCom.Text ?? "").Trim().ToUpperInvariant());
			flag = _civ.ConnectCom(portName, result, out err2);
		}
		else
		{
			int port = 4001;
			if (_txtDirectPort != null && int.TryParse((_txtDirectPort.Text ?? "").Trim(), out var result2) && result2 > 0 && result2 < 65536)
			{
				port = result2;
			}
			string host = ((_txtDirectIp == null) ? "" : (_txtDirectIp.Text ?? "").Trim());
			flag = _civ.ConnectTcp(host, port, 2500, out err2);
		}
		if (flag)
		{
			_civPoller = new CivPoller(_civ);
			SyncPollerOptions();
			SaveDirectCfg();
			if (!silent)
			{
				SetMsg("直连电台已建立：" + _civ.Describe() + "（帧不经云台板中转）");
			}
		}
		else
		{
			if (!silent)
			{
				SetMsg("直连失败：" + err2);
			}
			SetDotTip(_dotDirect, DotState.Fail, "连接失败", "直连失败：" + err2);
		}
		UiInvoke(delegate
		{
			RefreshDirectDot();
			RefreshChannelToggle();
		});
		return flag;
	}

	private void SyncPollerOptions()
	{
		if (_civPoller != null)
		{
			_civPoller.TimeoutMs = 900;
			_civPoller.Retries = 1;
			_civPoller.Legacy1C = _tgLegacy1C != null && _tgLegacy1C.Checked;
			_civPoller.DeepVfoB = _tgDeepVfoB != null && _tgDeepVfoB.Checked;
			_civPoller.ReadSmeter = _tgReadSmeter != null && _tgReadSmeter.Checked;
			_civPoller.ReadPtt1C = false;
		}
	}

	private void RigDirectDisconnect()
	{
		try
		{
			if (_civ != null)
			{
				_civ.Close();
			}
		}
		catch
		{
		}
		_civPoller = null;
		UiInvoke(delegate
		{
			RefreshDirectDot();
		});
	}

	private void RefreshComPorts()
	{
		if (_cboDirectCom == null)
		{
			return;
		}
		string text = _cboDirectCom.Text;
		try
		{
			string[] portNames = SerialPort.GetPortNames();
			Array.Sort(portNames, delegate(string a, string b)
			{
				int num = ParseComNumber(a);
				int num2 = ParseComNumber(b);
				return (num == num2) ? string.CompareOrdinal(a, b) : num.CompareTo(num2);
			});
			_cboDirectCom.Items.Clear();
			string[] array = portNames;
			foreach (string item in array)
			{
				_cboDirectCom.Items.Add(item);
			}
		}
		catch
		{
		}
		if (!string.IsNullOrEmpty(text))
		{
			if (_cboDirectCom.Items.Contains(text))
			{
				_cboDirectCom.SelectedItem = text;
			}
			else
			{
				_cboDirectCom.Text = text;
			}
		}
		else if (_cboDirectCom.Items.Count > 0)
		{
			_cboDirectCom.SelectedIndex = 0;
		}
	}

	private static int ParseComNumber(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return int.MaxValue;
		}
		if (!int.TryParse(name.Substring(3), out var result))
		{
			return int.MaxValue;
		}
		return result;
	}

	private static long ParseLongSafe(string s)
	{
		if (!string.IsNullOrEmpty(s) && long.TryParse(s.Trim(), out var result) && result >= 30000)
		{
			return result;
		}
		return 0L;
	}

	private static string DescribeComPorts()
	{
		try
		{
			string[] portNames = SerialPort.GetPortNames();
			return (portNames.Length == 0) ? "未发现串口" : string.Join(", ", portNames);
		}
		catch (Exception ex)
		{
			return "枚举失败：" + ex.Message;
		}
	}

	private bool ReadDirectAddr(out byte addr, out string err)
	{
		addr = 136;
		err = null;
		string text = ((_txtDirectAddr == null) ? "" : (_txtDirectAddr.Text ?? "").Trim());
		if (text.Length == 0)
		{
			err = "CI-V 地址码为空（应为 1~2 位十六进制，默认 88）";
			return false;
		}
		if (text.Length > 2)
		{
			err = "CI-V 地址码过长：'" + text + "'（只允许 1~2 位十六进制，如 88 / E0 / A4）";
			return false;
		}
		foreach (char c in text)
		{
			if ((c < '0' || c > '9') && (c < 'a' || c > 'f') && (c < 'A' || c > 'F'))
			{
				err = "CI-V 地址码含非法字符：'" + text + "'（只允许 0-9 / A-F）";
				return false;
			}
		}
		try
		{
			addr = Convert.ToByte(text, 16);
		}
		catch
		{
			err = "CI-V 地址码无法解析：'" + text + "'";
			return false;
		}
		return true;
	}

	private void RigDirectToggle()
	{
		bool wasReady = DirectReady;
		bool wasCom = DirectLinkIsCom;
		RunBg(delegate
		{
			if (wasReady)
			{
				RigDirectDisconnect();
				SetMsg(wasCom ? "已断开直连（串口已释放）" : "已断开直连通道；读电台改走经板子 HTTP");
			}
			else
			{
				RigDirectConnect(silent: false);
			}
		});
	}

	private void RefreshDirectDot()
	{
		if (_dotDirect == null)
		{
			return;
		}
		if (DirectReady)
		{
			string text = ((_civ == null) ? "已连接" : _civ.Describe());
			SetDotTip(_dotDirect, DotState.Ok, "已连接", "直连通道：已连接（" + text + "，CI-V 帧不经云台板中转）");
			if (_lblDirect != null)
			{
				_lblDirect.Text = "直连：已连接";
				_lblDirect.ForeColor = Theme.Ok;
			}
			if (_lblDirectTxt != null)
			{
				_lblDirectTxt.Text = "已连接  " + DirectTargetText() + (DirectLinkIsCom ? "（串口）" : "（TCP）");
				_lblDirectTxt.ForeColor = Theme.Ok;
			}
		}
		else
		{
			SetDotTip(_dotDirect, DotState.Fail, "未连接", "直连通道：未连接（点「连接直连」按所选链路建立；未连接时读电台自动走经板子 HTTP）");
			if (_lblDirect != null)
			{
				_lblDirect.Text = "直连：未连接";
				_lblDirect.ForeColor = Theme.TextDim;
			}
			if (_lblDirectTxt != null)
			{
				_lblDirectTxt.Text = "未连接  （目标 " + DirectTargetText() + "，点「连接直连」启用）";
				_lblDirectTxt.ForeColor = Theme.TextDim;
			}
		}
		RefreshDirectButton();
		RefreshChannelToggle();
	}

	/// <summary>
	/// v1.6.0：把 CivLink 的状态机接到「直连状态」状态位（只换文案/颜色/提示，不动布局与尺寸）。
	/// 事件来自后台线程，这里统一 UiInvoke 回主线程。
	/// </summary>
	private void AttachCivState()
	{
		try
		{
			if (_civ == null || _civStateHooked)
			{
				return;
			}
			_civStateHooked = true;
			_civ.StateChanged += OnCivLinkState;
		}
		catch
		{
		}
	}

	/// <summary>云台链路状态 → 顶栏 S3 状态位（Ready/Connecting/Backoff/Failed 直接映射）。</summary>
	private void OnS3LinkState(LinkState s)
	{
		UiInvoke(delegate
		{
			try
			{
				string reason = _s3.LinkStateReason;
				switch (s)
				{
				case LinkState.Ready:
					SetS3Dot(DotState.Ok, "已连接");
					break;
				case LinkState.Connecting:
					SetS3Dot(DotState.Busy, "连接中");
					break;
				case LinkState.Backoff:
					SetS3Dot(DotState.Busy, "重连中");
					break;
				case LinkState.Failed:
					SetS3Dot(DotState.Fail, "已断开");
					break;
				default:
					SetS3Dot(DotState.Unknown, "未连接");
					break;
				}
				if (_dotS3 != null && reason.Length > 0)
				{
					_dotS3.Tip = "云台链路 " + LinkStateText.Zh(s) + " · " + reason;
				}
			}
			catch
			{
			}
		});
	}

	/// <summary>电台直连链路状态 → 「直连」状态位（只改状态点，不改任何控件布局）。</summary>
	private void OnCivLinkState(LinkState s)
	{
		UiInvoke(delegate
		{
			try
			{
				if (_dotDirect == null || _civ == null)
				{
					return;
				}
				string cap;
				DotState st;
				switch (s)
				{
				case LinkState.Ready:
					cap = "已连接";
					st = DotState.Ok;
					break;
				case LinkState.Connecting:
					cap = "连接中";
					st = DotState.Busy;
					break;
				case LinkState.Backoff:
					cap = "重连中";
					st = DotState.Busy;
					break;
				case LinkState.Failed:
					cap = "已断开";
					st = DotState.Fail;
					break;
				default:
					cap = "未连接";
					st = DotState.Off;
					break;
				}
				string reason = _civ.StateReason;
				SetDotTip(_dotDirect, st, cap, "直连通道 " + LinkStateText.Zh(s) + (reason.Length > 0 ? ("：" + reason) : ""));
			}
			catch
			{
			}
		});
	}

	private string DescribeDirectLink()
	{
		if (_civ != null && _civ.IsOpen)
		{
			return _civ.Describe();
		}
		if (DirectLinkIsCom)
		{
			return "串口 " + ((_cboDirectCom == null) ? "" : _cboDirectCom.Text) + " @" + ((_cboDirectBaud == null || _cboDirectBaud.SelectedItem == null) ? "" : _cboDirectBaud.SelectedItem.ToString());
		}
		return "TCP " + DirectTargetText();
	}

	private bool DirectSend(string hex)
	{
		if (!DirectReady)
		{
			return false;
		}
		if (_civ.SendHex(hex, out var err))
		{
			return true;
		}
		SetMsg("直连发送失败（" + DescribeDirectLink() + "）：" + err);
		UiInvoke(delegate
		{
			RefreshDirectDot();
		});
		return false;
	}

	private string CivHead()
	{
		return "FEFE" + DirectAddrHex() + "E0";
	}

	private string DirectAddrHex()
	{
		if (ReadDirectAddr(out var addr, out var _))
		{
			return addr.ToString("X2");
		}
		return "88";
	}

	private static string CivFreqBcd(long hz)
	{
		long num = hz;
		StringBuilder stringBuilder = new StringBuilder(10);
		for (int i = 0; i < 5; i++)
		{
			int num2 = (int)(num % 10);
			num /= 10;
			int num3 = (int)(num % 10);
			num /= 10;
			stringBuilder.Append((num2 | (num3 << 4)).ToString("X2"));
		}
		return stringBuilder.ToString();
	}

	private bool DirectPtt(bool on)
	{
		if (!DirectReady)
		{
			return false;
		}
		return DirectSend(CivHead() + "1C00" + (on ? "01" : "00") + "FD");
	}

	private bool DirectFreq(long hz)
	{
		if (!DirectReady)
		{
			return false;
		}
		return DirectSend(CivHead() + "05" + CivFreqBcd(hz) + "FD");
	}

	private bool DirectMode(string mode)
	{
		if (!DirectReady)
		{
			return false;
		}
		string text = "0100";
		string text2 = (mode ?? "").Trim().ToUpperInvariant();
		return DirectSend(CivHead() + "06" + text2 switch
		{
			"USB" => "01", 
			"LSB" => "00", 
			"CW" => "03", 
			"CW-R" => "07", 
			"FM" => "05", 
			"AM" => "02", 
			"RTTY" => "04", 
			"RTTY-R" => "08", 
			"DATA" => "01", 
			_ => "01", 
		} + "FD");
	}

	private void RigPttViaBest(bool on)
	{
		if (!DirectPtt(on))
		{
			_s3.GetRig("/rptt?x=" + (on ? "1" : "0"));
		}
	}

	private void PaintRigPtt()
	{
		if (_rigDisplay != null)
		{
			_rigDisplay.Set(_hzCache, _modeCache, _rigPttOn, _latCache, _linkCache, _spHzCache, _spModeCache, _spOnCache, null);
		}
	}

	private void PttWorker()
	{
		try
		{
			while (true)
			{
				int pttWant;
				lock (_pttReqLock)
				{
					pttWant = _pttWant;
					_pttWant = int.MinValue;
					if (pttWant == int.MinValue)
					{
						Interlocked.Exchange(ref _pttWorkerBusy, 0);
						lock (_pttReqLock)
						{
							if (_pttWant == int.MinValue)
							{
								break;
							}
						}
						continue;
					}
				}
				string rig = _s3.GetRig("/rptt?x=" + ((pttWant == 1) ? "1" : "0"));
				if (rig != null && rig.Trim().StartsWith("OK"))
				{
					UiInvoke(delegate
					{
						PaintRigPtt();
					});
					continue;
				}
				_rigPttOn = false;
				UiInvoke(delegate
				{
					try
					{
						StopPttKeep();
					}
					catch
					{
					}
					PaintRigPtt();
				});
				SetMsgSafe("PTT 未确认（界面已回到收信）：" + (rig ?? _s3.LastError));
			}
		}
		finally
		{
			Interlocked.Exchange(ref _pttWorkerBusy, 0);
		}
	}

	private void RigPtt(bool on)
	{
		_rigPttOn = on;
		if (on)
		{
			StartPttKeep();
		}
		else
		{
			StopPttKeep();
		}
		lock (_pttReqLock)
		{
			_pttWant = (on ? 1 : 0);
		}
		if (Interlocked.CompareExchange(ref _pttWorkerBusy, 1, 0) == 0)
		{
			ThreadPool.QueueUserWorkItem(delegate
			{
				PttWorker();
			});
		}
		SetMsg(on ? "PTT：发射请求已发出（等设备确认）" : "PTT：接收");
	}

	private Panel BuildRtspTab()
	{
		HudPanel obj = new HudPanel
		{
			Padding = new Padding(8)
		};
		Panel panel = new Panel
		{
			Dock = DockStyle.Fill,
			AutoScroll = true,
			BackColor = Theme.Bg
		};
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			ColumnCount = 1,
			RowCount = 3,
			BackColor = Theme.Bg,
			Height = 1026,
			RowStyles = 
			{
				// v1.5.1：系统页把「关键信息」放到最上面（一进页就能看到，不用滚），
				// OTA / 网络两段功能块排到下面。
				new RowStyle(SizeType.Absolute, 300f),
				new RowStyle(SizeType.Absolute, 396f),
				new RowStyle(SizeType.Absolute, 330f)
			}
		};
		TableLayoutPanel tableLayoutPanel2 = new TableLayoutPanel();
		tableLayoutPanel2.Dock = DockStyle.Fill;
		tableLayoutPanel2.ColumnCount = 3;
		tableLayoutPanel2.RowCount = 7;
		tableLayoutPanel2.BackColor = Theme.Bg;
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 62f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 50f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
		tableLayoutPanel2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		tableLayoutPanel2.Controls.Add(SectionLabel("S3 固件升级（OTA · 在软件内直接上传）"), 0, 0);
		tableLayoutPanel2.SetColumnSpan(tableLayoutPanel2.GetControlFromPosition(0, 0), 3);
		Label label = new Label
		{
			Dock = DockStyle.Fill,
			Text = "点“选择固件并升级…”选中编译好的 main.bin，软件直接 POST 到设备 /update（字段名 update）并显示进度。\r\n· ★ v1.4.2 新增「一键 OTA」：先读 /otas 判网段闸门 → 必要时自动 POST /wifi 修好 → 再上传 → 报 bld 变化。\r\n· 升级中请勿断电、勿关闭本程序；成功后设备自动重启（约 10 秒）。\r\n· 升级前建议先停止电台发射，并关闭其他正在访问设备的页面。"
		};
		Theme.StyleLabel(label, dim: true);
		tableLayoutPanel2.Controls.Add(label, 0, 1);
		tableLayoutPanel2.SetColumnSpan(label, 3);
		FlowLayoutPanel flowLayoutPanel = Row();
		flowLayoutPanel.Dock = DockStyle.Fill;
		flowLayoutPanel.Controls.Add(Btn("选择固件并升级…", delegate
		{
			OtaPickAndUpload();
		}));
		flowLayoutPanel.Controls.Add(Btn("打开网页版升级页", delegate
		{
			OpenOtaPage();
		}));
		_lblFwOta = new Label();
		_lblFwOta.AutoSize = true;
		_lblFwOta.Height = 24;
		_lblFwOta.Margin = new Padding(6, 8, 8, 2);
		_lblFwOta.TextAlign = ContentAlignment.MiddleLeft;
		Theme.StyleLabel(_lblFwOta, dim: false);
		_lblFwOta.Text = "固件：读取中…";
		flowLayoutPanel.Controls.Add(_lblFwOta);
		FitFlowRowHeight(flowLayoutPanel);
		_lblOtaInfo = new Label();
		_lblOtaInfo.Width = 150;
		_lblOtaInfo.Height = 26;
		_lblOtaInfo.Margin = new Padding(3, 8, 4, 2);
		_lblOtaInfo.TextAlign = ContentAlignment.MiddleLeft;
		_lblOtaInfo.Text = "未开始升级";
		Theme.StyleLabel(_lblOtaInfo, dim: false);
		flowLayoutPanel.Controls.Add(_lblOtaInfo);
		tableLayoutPanel2.Controls.Add(flowLayoutPanel, 0, 2);
		tableLayoutPanel2.SetColumnSpan(flowLayoutPanel, 3);
		FlowLayoutPanel flowLayoutPanel2 = Row();
		flowLayoutPanel2.Dock = DockStyle.Fill;
		flowLayoutPanel2.Controls.Add(Btn("① 读取 /otas（门禁）", delegate
		{
			ReadOtasNow();
		}));
		_lblOtas = MkWrapLabel(700, bold: false);
		_lblOtas.Text = "OTA 门禁：点「① 读取 /otas」查看 令牌闸门 / 同网段闸门 / STA 与有线地址。";
		flowLayoutPanel2.Controls.Add(_lblOtas);
		tableLayoutPanel2.Controls.Add(flowLayoutPanel2, 0, 3);
		tableLayoutPanel2.SetColumnSpan(flowLayoutPanel2, 3);
		FlowLayoutPanel flowLayoutPanel3 = Row();
		flowLayoutPanel3.Dock = DockStyle.Fill;
		_btnOtaOneKey = Btn("② 一键 OTA（自动修复网段闸门）", delegate
		{
			OtaOneKey();
		});
		flowLayoutPanel3.Controls.Add(_btnOtaOneKey);
		flowLayoutPanel3.Controls.Add(new Label
		{
			Text = "OTA 配网 SSID",
			AutoSize = true,
			Height = 20,
			Margin = new Padding(8, 9, 4, 2)
		});
		_txtOtaSsid = new TextBox();
		_txtOtaSsid.Width = 150;
		Theme.StyleTextBox(_txtOtaSsid);
		flowLayoutPanel3.Controls.Add(_txtOtaSsid);
		flowLayoutPanel3.Controls.Add(new Label
		{
			Text = "密码",
			AutoSize = true,
			Height = 20,
			Margin = new Padding(6, 9, 4, 2)
		});
		_txtOtaPass = new TextBox();
		_txtOtaPass.Width = 130;
		_txtOtaPass.UseSystemPasswordChar = true;
		Theme.StyleTextBox(_txtOtaPass);
		flowLayoutPanel3.Controls.Add(_txtOtaPass);
		Label label2 = MkWrapLabel(360, bold: false);
		label2.Text = "（仅在点了「② 一键 OTA」且 /otas 判定会被拦时，才会 POST /wifi 配网；不填就不配网）";
		flowLayoutPanel3.Controls.Add(label2);
		tableLayoutPanel2.Controls.Add(flowLayoutPanel3, 0, 4);
		tableLayoutPanel2.SetColumnSpan(flowLayoutPanel3, 3);
		_prgOta = new ProgressBar();
		_prgOta.Dock = DockStyle.Fill;
		_prgOta.Minimum = 0;
		_prgOta.Maximum = 100;
		Panel panel2 = new Panel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(0, 4, 0, 0)
		};
		_prgOta.Dock = DockStyle.Fill;
		panel2.Controls.Add(_prgOta);
		tableLayoutPanel2.Controls.Add(panel2, 0, 5);
		tableLayoutPanel2.SetColumnSpan(panel2, 3);
		TextBox textBox = new TextBox
		{
			Dock = DockStyle.Fill,
			Multiline = true,
			ReadOnly = true,
			ScrollBars = ScrollBars.Vertical,
			WordWrap = true
		};
		Theme.StyleTextBox(textBox);
		textBox.Font = Theme.FUi(9.5f, FontStyle.Bold);
		textBox.Text = "升级说明：\r\n· 分区方案 16M/app3M_fat9M_16MB（两个 3MB OTA 分区），单次固件需小于约 3MB。\r\n· 编译产物：.build\\main.bin（约 1.7MB）。\r\n· ★ 固件对 /update 有**网段闸门**：上位机必须与板子的无线 STA 或**有线**地址同一 /24；\r\n  被拒时正文是 `403 ERR:ota-subnet` —— 点「② 一键 OTA」会自动配网修好再上传。\r\n· 固件还会对「真正开始上传」做 3 秒限速（429 ERR:ota-ratelimit）—— 失败等 3 秒重试即可。\r\n· 上传失败并提示分区余量不足时，请改用 USB 串口整片烧录。\r\n· 本页只处理 S3 自身；摄像头/监控相关内容全部在“监控（摄像机）”页。";
		tableLayoutPanel2.Controls.Add(textBox, 0, 6);
		tableLayoutPanel2.SetColumnSpan(textBox, 3);
		// （顺序在下面统一 Add：0=关键信息 1=OTA 2=网络）
		TableLayoutPanel tableLayoutPanel3 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 2,
			RowCount = 1,
			BackColor = Theme.Bg,
			ColumnStyles = 
			{
				new ColumnStyle(SizeType.Percent, 55f),
				new ColumnStyle(SizeType.Percent, 45f)
			},
			Padding = new Padding(0, 6, 0, 0)
		};
		TableLayoutPanel tableLayoutPanel4 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 4,
			BackColor = Theme.Bg,
			RowStyles = 
			{
				new RowStyle(SizeType.Absolute, 30f),
				new RowStyle(SizeType.Absolute, 42f),
				new RowStyle(SizeType.Percent, 100f),
				new RowStyle(SizeType.Absolute, 42f)
			},
			Padding = new Padding(0, 0, 10, 0),
			Controls = { 
			{
				(Control)SectionLabel("网络 · WiFi 扫描与配网"),
				0,
				0
			} }
		};
		FlowLayoutPanel flowLayoutPanel4 = Row();
		flowLayoutPanel4.Dock = DockStyle.Fill;
		flowLayoutPanel4.Controls.Add(Btn("扫描 WiFi 并推荐", delegate
		{
			WifiScan();
		}));
		flowLayoutPanel4.Controls.Add(Btn("连接选中的 WiFi", delegate
		{
			WifiConnect();
		}));
		tableLayoutPanel4.Controls.Add(flowLayoutPanel4, 0, 1);
		_lstWifi = new ListView();
		_lstWifi.Dock = DockStyle.Fill;
		_lstWifi.View = View.Details;
		_lstWifi.FullRowSelect = true;
		_lstWifi.GridLines = true;
		_lstWifi.Columns.Add("推荐", 48);
		_lstWifi.Columns.Add("WiFi 名称(SSID)", 240);
		_lstWifi.Columns.Add("信号", 80);
		_lstWifi.Columns.Add("加密", 80);
		Theme.StyleListView(_lstWifi);
		tableLayoutPanel4.Controls.Add(_lstWifi, 0, 2);
		FlowLayoutPanel flowLayoutPanel5 = Row();
		flowLayoutPanel5.Dock = DockStyle.Fill;
		_txtWifiPass = new TextBox();
		_txtWifiPass.Width = 200;
		_txtWifiPass.UseSystemPasswordChar = true;
		Theme.StyleTextBox(_txtWifiPass);
		flowLayoutPanel5.Controls.Add(new Label
		{
			Text = "WiFi 密码",
			AutoSize = true,
			Height = 20,
			Margin = new Padding(3, 9, 4, 2)
		});
		flowLayoutPanel5.Controls.Add(_txtWifiPass);
		flowLayoutPanel5.Controls.Add(Btn("连接", delegate
		{
			WifiConnect();
		}));
		tableLayoutPanel4.Controls.Add(flowLayoutPanel5, 0, 3);
		tableLayoutPanel3.Controls.Add(tableLayoutPanel4, 0, 0);
		TableLayoutPanel tableLayoutPanel5 = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			ColumnCount = 1,
			RowCount = 5,
			BackColor = Theme.Bg,
			RowStyles = 
			{
				new RowStyle(SizeType.Absolute, 30f),
				new RowStyle(SizeType.Absolute, 42f),
				new RowStyle(SizeType.Absolute, 42f),
				new RowStyle(SizeType.Absolute, 42f),
				new RowStyle(SizeType.Percent, 100f)
			},
			Controls = { 
			{
				(Control)SectionLabel("网络 · 以太网（DHCP / 固定 IP）"),
				0,
				0
			} }
		};
		FlowLayoutPanel flowLayoutPanel6 = Row();
		flowLayoutPanel6.Dock = DockStyle.Fill;
		flowLayoutPanel6.Controls.Add(Btn("切到 DHCP（自动获取）", delegate
		{
			EthDhcp();
		}));
		flowLayoutPanel6.Controls.Add(Btn("读取当前配置", delegate
		{
			EthQuery();
		}));
		tableLayoutPanel5.Controls.Add(flowLayoutPanel6, 0, 1);
		_lblEthState = new Label();
		_lblEthState.Dock = DockStyle.Fill;
		_lblEthState.Text = "（未读取）";
		Theme.StyleLabel(_lblEthState, dim: false);
		tableLayoutPanel5.Controls.Add(_lblEthState, 0, 2);
		_txtEthIp = new TextBox();
		_txtEthIp.Width = 118;
		_txtEthIp.Text = "172.16.1.122";
		Theme.StyleTextBox(_txtEthIp);
		_txtEthGw = new TextBox();
		_txtEthGw.Width = 118;
		_txtEthGw.Text = "172.16.1.1";
		Theme.StyleTextBox(_txtEthGw);
		_txtEthNm = new TextBox();
		_txtEthNm.Width = 118;
		_txtEthNm.Text = "255.255.255.0";
		Theme.StyleTextBox(_txtEthNm);
		_txtEthDs = new TextBox();
		_txtEthDs.Width = 118;
		_txtEthDs.Text = "172.16.1.253";
		Theme.StyleTextBox(_txtEthDs);
		FlowLayoutPanel flowLayoutPanel7 = Row();
		flowLayoutPanel7.Dock = DockStyle.Fill;
		flowLayoutPanel7.Controls.Add(new Label
		{
			Text = "固定IP",
			AutoSize = true,
			Height = 20,
			Margin = new Padding(3, 9, 4, 2)
		});
		flowLayoutPanel7.Controls.Add(_txtEthIp);
		flowLayoutPanel7.Controls.Add(new Label
		{
			Text = "掩码",
			AutoSize = true,
			Height = 20,
			Margin = new Padding(3, 9, 4, 2)
		});
		flowLayoutPanel7.Controls.Add(_txtEthNm);
		tableLayoutPanel5.Controls.Add(flowLayoutPanel7, 0, 3);
		FlowLayoutPanel flowLayoutPanel8 = Row();
		flowLayoutPanel8.Dock = DockStyle.Fill;
		flowLayoutPanel8.Controls.Add(new Label
		{
			Text = "网关",
			AutoSize = true,
			Height = 20,
			Margin = new Padding(3, 9, 4, 2)
		});
		flowLayoutPanel8.Controls.Add(_txtEthGw);
		flowLayoutPanel8.Controls.Add(new Label
		{
			Text = "DNS",
			AutoSize = true,
			Height = 20,
			Margin = new Padding(3, 9, 4, 2)
		});
		flowLayoutPanel8.Controls.Add(_txtEthDs);
		flowLayoutPanel8.Controls.Add(Btn("应用固定 IP", delegate
		{
			EthStatic();
		}));
		tableLayoutPanel5.Controls.Add(flowLayoutPanel8, 0, 4);
		tableLayoutPanel3.Controls.Add(tableLayoutPanel5, 1, 0);
		tableLayoutPanel.Controls.Add(BuildSystemTab(), 0, 0);
		tableLayoutPanel.Controls.Add(tableLayoutPanel2, 0, 1);
		tableLayoutPanel.Controls.Add(tableLayoutPanel3, 0, 2);
		panel.Controls.Add(tableLayoutPanel);
		obj.Controls.Add(panel);
		return obj;
	}

	private void RunBg(Action work)
	{
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				work();
			}
			catch (Exception ex)
			{
				SetMsgSafe("后台任务异常：" + ex.Message);
			}
		});
	}

	private void WifiScan()
	{
		if (_lstWifi == null)
		{
			return;
		}
		SetMsg("正在扫描 WiFi（最多 6 秒，界面不卡）…");
		_lstWifi.Items.Clear();
		RunBg(delegate
		{
			string text = HttpGetPlain("/scan", 12000);
			string err = _s3.LastError;
			List<string[]> list = null;
			if (text != null)
			{
				list = new List<string[]>();
				string[] array = text.Split(';');
				for (int i = 0; i < array.Length; i++)
				{
					if (array[i].Length != 0)
					{
						string[] array2 = array[i].Split('|');
						if (array2.Length >= 2)
						{
							list.Add(array2);
						}
					}
				}
				list.Sort(delegate(string[] a, string[] b)
				{
					int result = 0;
					int result2 = 0;
					int.TryParse(a[1], out result);
					int.TryParse(b[1], out result2);
					return result2.CompareTo(result);
				});
			}
			List<string[]> res = list;
			UiInvoke(delegate
			{
				if (_lstWifi != null)
				{
					if (res == null)
					{
						SetMsg("WiFi 扫描失败：" + err);
					}
					else
					{
						for (int j = 0; j < res.Count; j++)
						{
							string[] array3 = res[j];
							ListViewItem listViewItem = new ListViewItem((j == 0) ? "★推荐" : "")
							{
								SubItems = 
								{
									array3[0],
									array3[1] + " dBm",
									(array3.Length > 2 && array3[2] == "0") ? "开放" : "加密"
								},
								Tag = array3[0]
							};
							if (j == 0)
							{
								listViewItem.BackColor = Color.FromArgb(30, 60, 60);
							}
							_lstWifi.Items.Add(listViewItem);
						}
						SetMsg("扫描完成：共 " + res.Count + " 个 WiFi，信号最强已标为推荐（双击可填入 SSID）");
					}
				}
			});
		});
	}

	private void WifiConnect()
	{
		string text = null;
		if (_lstWifi != null && _lstWifi.SelectedItems.Count > 0)
		{
			text = _lstWifi.SelectedItems[0].Tag as string;
		}
		if (string.IsNullOrEmpty(text) && _lstWifi != null && _lstWifi.Items.Count > 0)
		{
			text = _lstWifi.Items[0].Tag as string;
		}
		if (string.IsNullOrEmpty(text))
		{
			SetMsg("请先扫描 WiFi 并选中一个");
			return;
		}
		string pass = ((_txtWifiPass != null) ? _txtWifiPass.Text : "");
		string ssid2 = text;
		SetMsg("正在提交 WiFi 切换（最长 25 秒，设备会断开原连接；界面不卡）…");
		RunBg(delegate
		{
			string text2 = HttpGetPlain("/wifi?s=" + Uri.EscapeDataString(ssid2) + "&p=" + Uri.EscapeDataString(pass), 25000);
			string lastError = _s3.LastError;
			SetMsgSafe((text2 == null) ? ("WiFi 连接失败：" + lastError) : ("WiFi 切换已提交：" + ssid2 + " → " + text2.Trim()));
		});
	}

	private void EthDhcp()
	{
		SetMsg("正在切换以太网为 DHCP…");
		RunBg(delegate
		{
			string text = HttpGetPlain("/eth?dhcp=1", 10000);
			string lastError = _s3.LastError;
			SetMsgSafe((text != null && text.Trim() == "OK") ? "以太网已切到 DHCP" : ("切换失败：" + (text ?? lastError)));
			EthQueryBg();
		});
	}

	private void EthStatic()
	{
		string ip = _txtEthIp.Text.Trim();
		string gw = _txtEthGw.Text.Trim();
		string nm = _txtEthNm.Text.Trim();
		string ds = _txtEthDs.Text.Trim();
		SetMsg("正在应用固定 IP…");
		RunBg(delegate
		{
			string path = "/eth?ip=" + ip + "&gw=" + gw + "&nm=" + nm + "&ds=" + ds;
			string text = HttpGetPlain(path, 10000);
			string lastError = _s3.LastError;
			SetMsgSafe((text != null && text.Trim() == "OK") ? "固定 IP 已应用" : ("应用失败：" + (text ?? lastError)));
			EthQueryBg();
		});
	}

	private void EthQuery()
	{
		RunBg(delegate
		{
			EthQueryBg();
		});
	}

	private void EthQueryBg()
	{
		string text = HttpGetPlain("/eth", 6000);
		string err = _s3.LastError;
		if (text == null)
		{
			UiInvoke(delegate
			{
				if (_lblEthState != null)
				{
					_lblEthState.Text = "读取失败：" + err;
				}
			});
			return;
		}
		string txt = text.Trim();
		string[] array = txt.Split(';');
		string ip = null;
		string gw = null;
		string nm = null;
		string ds = null;
		for (int i = 0; i < array.Length; i++)
		{
			int num = array[i].IndexOf('=');
			if (num <= 0)
			{
				continue;
			}
			string text2 = array[i].Substring(0, num);
			string text3 = array[i].Substring(num + 1);
			if (text3.Length != 0 && !(text3 == "0.0.0.0"))
			{
				switch (text2)
				{
				case "ip":
					ip = text3;
					break;
				case "gw":
					gw = text3;
					break;
				case "nm":
					nm = text3;
					break;
				case "ds":
					ds = text3;
					break;
				}
			}
		}
		UiInvoke(delegate
		{
			if (_lblEthState != null)
			{
				_lblEthState.Text = txt;
			}
			if (ip != null && _txtEthIp != null)
			{
				_txtEthIp.Text = ip;
			}
			if (gw != null && _txtEthGw != null)
			{
				_txtEthGw.Text = gw;
			}
			if (nm != null && _txtEthNm != null)
			{
				_txtEthNm.Text = nm;
			}
			if (ds != null && _txtEthDs != null)
			{
				_txtEthDs.Text = ds;
			}
		});
	}

	private string HttpGetPlain(string path, int timeoutMs)
	{
		try
		{
			HttpWebRequest obj = (HttpWebRequest)WebRequest.Create("http://" + _s3.Host + path);
			obj.Method = "GET";
			obj.Timeout = timeoutMs;
			obj.ReadWriteTimeout = timeoutMs;
			obj.KeepAlive = false;
			obj.Proxy = null;
			using HttpWebResponse httpWebResponse = (HttpWebResponse)obj.GetResponse();
			using StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream(), Encoding.UTF8);
			return streamReader.ReadToEnd();
		}
		catch (Exception ex)
		{
			_s3.LastError = ex.Message;
			return null;
		}
	}

	private void OtaPickAndUpload()
	{
		using OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Title = "选择 S3 固件文件（.bin）";
		openFileDialog.Filter = "固件文件 (*.bin)|*.bin|所有文件 (*.*)|*.*";
		openFileDialog.InitialDirectory = Environment.CurrentDirectory;
		if (openFileDialog.ShowDialog(this) == DialogResult.OK)
		{
			OtaUpload(openFileDialog.FileName);
		}
	}

	private void OtaUpload(string path)
	{
		FileInfo fileInfo;
		try
		{
			fileInfo = new FileInfo(path);
		}
		catch (Exception ex)
		{
			SetMsg("固件文件无法读取：" + ex.Message);
			return;
		}
		if (!fileInfo.Exists)
		{
			SetMsg("固件文件不存在");
			return;
		}
		SetOtaProgress(0, "正在上传 " + fileInfo.Name + "（" + fileInfo.Length / 1024 + " KB）…");
		SetMsg("OTA：开始上传 " + fileInfo.Name);
		try
		{
			if (_rigTimer != null)
			{
				_rigTimer.Stop();
			}
		}
		catch
		{
		}
		try
		{
			if (_pttKeep != null)
			{
				_pttKeep.Stop();
			}
		}
		catch
		{
		}
		try
		{
			if (_trackTimer != null)
			{
				_trackTimer.Stop();
			}
		}
		catch
		{
		}
		try
		{
			if (_diagTimer != null)
			{
				_diagTimer.Stop();
			}
		}
		catch
		{
		}
		ThreadPool.QueueUserWorkItem(delegate
		{
			string err;
			string b0;
			string b1;
			bool accepted = _s3.OtaPush(path, delegate(int pct, string txt)
			{
				SetOtaProgress(pct, txt);
			}, out err, out b0, out b1);
			UiInvoke(delegate
			{
				try
				{
					if (_rigTimer != null)
					{
						_rigTimer.Start();
					}
				}
				catch
				{
				}
				try
				{
					if (_trackTimer != null)
					{
						_trackTimer.Start();
					}
				}
				catch
				{
				}
				try
				{
					if (_diagTimer != null)
					{
						_diagTimer.Start();
					}
				}
				catch
				{
				}
				if (!accepted || err != null)
				{
					string text = ((err == null) ? "上传未完成" : err);
					SetOtaProgress(0, "升级失败：" + text);
					SetMsg("OTA 失败：" + text + "（轮询已恢复）");
				}
				else if (b1 == null)
				{
					SetOtaProgress(100, "上传成功（HTTP 200），但读不到 /sys 的 bld —— 设备可能仍在重启，请稍后点「刷新 /sys」确认。");
					SetMsg("OTA：上传完成但未能确认 bld（旧 bld=" + (b0 ?? "?") + "）");
				}
				else if (b0 != null && b1 == b0)
				{
					SetOtaProgress(0, "升级未生效：bld 仍是 " + b1 + "（与上传前相同）——请重试或改用 USB 串口整片烧录。");
					SetMsg("OTA 未切换：bld 仍为 " + b1);
				}
				else
				{
					SetOtaProgress(100, string.Format("✔ 升级成功：bld {0} → {1}（设备已重启，轮询已恢复）", b0 ?? "?", b1));
					SetMsg(string.Format("✔ OTA 成功：bld {0} → {1}", b0 ?? "?", b1));
					RefreshSys();
					DiagReadBg();
				}
			});
		});
	}

	private void SetOtaProgress(int pct, string text)
	{
		if (_prgOta == null || _lblOtaInfo == null)
		{
			return;
		}
		if (base.InvokeRequired)
		{
			BeginInvoke((MethodInvoker)delegate
			{
				try
				{
					SetOtaProgress(pct, text);
				}
				catch
				{
				}
			});
			return;
		}
		if (pct < 0)
		{
			pct = 0;
		}
		if (pct > 100)
		{
			pct = 100;
		}
		_prgOta.Value = pct;
		_lblOtaInfo.Text = text;
	}

	private void OpenOtaPage()
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "http://" + _s3.Host + "/update",
				UseShellExecute = true
			});
			SetMsg("已打开网页版升级页（备用方式）");
		}
		catch (Exception ex)
		{
			SetMsg("打开失败：" + ex.Message);
		}
	}

	private Panel BuildSystemTab()
	{
		// ★ v1.5.1「系统页精简」：原来这一页是一屏堆满的东西——
		//   6 个按钮 + 复位统计 + DTrac + 复位历史表(ListView 7 列) + 诊断长文本 + /sys 长文本，
		//   其中 /sys 和诊断文本各是一个几百行的只读文本框，整页永远是"满的"。
		//   现在：页面只留【4 个按钮 + 3 行关键信息 + DTrac 一行】，
		//   所有冗长内容（完整 /sys、固件诊断全文、复位历史表）收进「详情…」「复位历史…」弹窗，
		//   弹窗内可滚动、可一键复制（见 ShowSysDetailDialog / ShowResetHistoryDialog / ShowInfoDialog）。
		HudPanel obj = new HudPanel
		{
			Dock = DockStyle.Fill,
			Padding = new Padding(6)
		};
		_rstRowStyle = new RowStyle(SizeType.Absolute, 0f);
		TableLayoutPanel tableLayoutPanel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			BackColor = Theme.Bg,
			ColumnCount = 1,
			RowCount = 5,
			RowStyles = 
			{
				new RowStyle(SizeType.Absolute, 44f),
				new RowStyle(SizeType.Absolute, 88f),
				_rstRowStyle,
				new RowStyle(SizeType.Absolute, 26f),
				new RowStyle(SizeType.Percent, 100f)
			}
		};
		obj.Controls.Add(tableLayoutPanel);
		FlowLayoutPanel flowLayoutPanel = Row();
		flowLayoutPanel.Dock = DockStyle.Fill;
		flowLayoutPanel.AutoSize = false;
		flowLayoutPanel.Height = 40;
		flowLayoutPanel.MinimumSize = new Size(0, 40);
		flowLayoutPanel.Controls.Add(Btn("刷新 /sys", delegate
		{
			RefreshSys();
		}));
		flowLayoutPanel.Controls.Add(Btn("详情（完整 /sys 与诊断）…", delegate
		{
			ShowSysDetailDialog();
		}));
		flowLayoutPanel.Controls.Add(Btn("复位历史…", delegate
		{
			OpenResetHistory();
		}));
		flowLayoutPanel.Controls.Add(Btn("打开配置目录", delegate
		{
			OpenConfigDir();
		}));
		tableLayoutPanel.Controls.Add(flowLayoutPanel, 0, 0);
		_lblSysBrief = new Label();
		_lblSysBrief.Dock = DockStyle.Fill;
		_lblSysBrief.TextAlign = ContentAlignment.TopLeft;
		_lblSysBrief.AutoEllipsis = false;
		Theme.StyleLabel(_lblSysBrief, dim: false);
		_lblSysBrief.Font = Theme.FMonoF(9.5f, FontStyle.Bold);
		_lblSysBrief.Text = "关键信息：读取中…（点「刷新 /sys」或等自动刷新）";
		tableLayoutPanel.Controls.Add(_lblSysBrief, 0, 1);
		// 最近 3 条复位记录（其余进「复位历史…」弹窗，避免长列表把页面堆满）
		_lblRstTop3 = new Label();
		_lblRstTop3.Dock = DockStyle.Fill;
		_lblRstTop3.TextAlign = ContentAlignment.TopLeft;
		_lblRstTop3.AutoEllipsis = false;
		Theme.StyleLabel(_lblRstTop3, dim: true);
		_lblRstTop3.Font = Theme.FMonoF(9f, FontStyle.Regular);
		_lblRstTop3.Text = "复位原因（最近 3 条）：点「复位历史…」读取";
		tableLayoutPanel.Controls.Add(_lblRstTop3, 0, 2);
		_lblDtracSys = new Label();
		_lblDtracSys.Dock = DockStyle.Fill;
		_lblDtracSys.TextAlign = ContentAlignment.MiddleLeft;
		_lblDtracSys.AutoEllipsis = true;
		Theme.StyleLabel(_lblDtracSys, dim: true);
		_lblDtracSys.Text = "DTrac：读取中…";
		tableLayoutPanel.Controls.Add(_lblDtracSys, 0, 3);
		Label hint = new Label();
		hint.Dock = DockStyle.Fill;
		hint.TextAlign = ContentAlignment.TopLeft;
		Theme.StyleLabel(hint, dim: true);
		hint.Text = "本页只显示关键信息：设备地址 / 连接 / 固件与构建 / 复位原因 / 可用堆 / 网络通路。\r\n"
			+ "· 完整 /sys 原文、固件诊断（azblk/verr/wdtAdd/ec*/el*/nvs*）、门禁与看门狗回读 → 点「详情（完整 /sys 与诊断）…」\r\n"
			+ "· 复位历史表（panic/brownout/wdt…） → 点「复位历史…」\r\n"
			+ "· 两个弹窗都可以 Ctrl+A / Ctrl+C 或点「复制全部」整段复制，窗口可拖大、可滚动\r\n"
			+ "· 固件升级 OTA / WiFi 配网 / 以太网固定 IP 在上半部分；摄像头相关内容全部在「监控（摄像机）」页";
		tableLayoutPanel.Controls.Add(hint, 0, 4);
		// 下面这些控件仍然保留为字段（后台刷新逻辑不用改），但**不再挂到页面上**：
		// 它们只做数据缓存，展示交给弹窗里的新控件。
		_lblRstInfo = new Label();
		Theme.StyleLabel(_lblRstInfo, dim: true);
		_lblRstInfo.Text = "复位统计：未读取";
		_txtSys = new TextBox();
		_txtSys.Multiline = true;
		_txtSys.ReadOnly = true;
		_txtSys.ScrollBars = ScrollBars.Vertical;
		_txtSys.WordWrap = true;
		Theme.StyleTextBox(_txtSys);
		_txtSys.Font = Theme.FMonoF(9.5f, FontStyle.Bold);
		_txtDiag = new TextBox();
		_txtDiag.Multiline = true;
		_txtDiag.ReadOnly = true;
		_txtDiag.ScrollBars = ScrollBars.Vertical;
		_txtDiag.WordWrap = true;
		Theme.StyleTextBox(_txtDiag);
		_txtDiag.Font = Theme.FMonoF(9.5f, FontStyle.Regular);
		return obj;
	}

	/// <summary>
	/// v1.5.1：可滚动 / 可复制的信息弹窗。
	/// 用途：顶部「读取设备信息」、系统页「详情」、「复位历史」，以及任何需要完整显示长文本的地方。
	/// 尺寸按当前屏幕工作区收敛，保证整窗在屏幕内、内容不被裁掉。
	/// </summary>
	private void ShowInfoDialog(string title, string text)
	{
		Rectangle wa = Screen.FromControl(this).WorkingArea;
		Form dlg = new Form();
		try
		{
			dlg.Text = title + " — YiZhou 控制中枢 " + YzAppVer;
			dlg.StartPosition = FormStartPosition.CenterParent;
			dlg.MinimizeBox = false;
			dlg.MaximizeBox = true;
			dlg.ShowInTaskbar = false;
			dlg.BackColor = Theme.Bg;
			dlg.ForeColor = Theme.Text;
			dlg.Font = Theme.FUi(9.5f, FontStyle.Regular);
			// 全屏（TopMost=true）时弹窗也要在上面，否则会被主窗口盖住
			dlg.TopMost = base.TopMost;
			dlg.ClientSize = new Size(Math.Min(1000, Math.Max(560, wa.Width - 160)), Math.Min(700, Math.Max(380, wa.Height - 160)));
			dlg.MinimumSize = new Size(520, 320);
			Panel bottom = new Panel();
			bottom.Dock = DockStyle.Bottom;
			bottom.Height = 46;
			bottom.BackColor = Theme.Bg;
			FlowLayoutPanel btns = new FlowLayoutPanel();
			btns.Dock = DockStyle.Fill;
			btns.FlowDirection = FlowDirection.RightToLeft;
			btns.WrapContents = false;
			btns.Padding = new Padding(8, 6, 10, 6);
			btns.BackColor = Theme.Bg;
			Button btnClose = Theme.MakeButton("关闭", delegate
			{
				dlg.Close();
			});
			btnClose.Width = 110;
			btns.Controls.Add(btnClose);
			// 用 RichTextBox 而不是 TextBox：只读多行 TextBox 在部分 Windows 上会被系统
			// 强制回白底黑字（背景色设置被忽略）；RichTextBox 会老实按主题上色，
			// 同时仍支持 Ctrl+A / Ctrl+C / 滚动 / 自动换行。
			RichTextBox box = new RichTextBox();
			box.Multiline = true;
			box.ReadOnly = true;
			box.DetectUrls = false;
			box.ScrollBars = RichTextBoxScrollBars.Vertical;
			box.WordWrap = true;
			box.Dock = DockStyle.Fill;
			box.Text = text ?? "";
			box.BackColor = Theme.P.Light ? Color.FromArgb(250, 252, 255) : Color.FromArgb(8, 13, 22);
			box.ForeColor = Theme.Text;
			box.BorderStyle = BorderStyle.FixedSingle;
			box.Font = Theme.FMonoF(10f, FontStyle.Regular);
			Button btnCopy = Theme.MakeButton("复制全部", delegate
			{
				try
				{
					Clipboard.SetText(box.Text ?? "");
					SetMsg("已复制到剪贴板（" + (box.Text ?? "").Length + " 字符）");
				}
				catch (Exception ex)
				{
					SetMsg("复制失败：" + ex.Message);
				}
			});
			btnCopy.Width = 120;
			btns.Controls.Add(btnCopy);
			Button btnWrap = Theme.MakeButton("自动换行：开", null);
			btnWrap.Width = 132;
			btnWrap.Click += delegate
			{
				box.WordWrap = !box.WordWrap;
				box.ScrollBars = box.WordWrap ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.Both;
				btnWrap.Text = box.WordWrap ? "自动换行：开" : "自动换行：关";
			};
			btns.Controls.Add(btnWrap);
			bottom.Controls.Add(btns);
			dlg.Controls.Add(box);
			dlg.Controls.Add(bottom);
			dlg.CancelButton = btnClose;
			// 保证弹窗完全落在屏幕工作区内（避免在 150% 缩放/小屏上被裁掉）
			dlg.Shown += delegate
			{
				try
				{
					dlg.Bounds = ClampToScreen(dlg.Bounds, Screen.FromControl(dlg).WorkingArea);
					box.BackColor = Theme.P.Light ? Color.FromArgb(250, 252, 255) : Color.FromArgb(8, 13, 22);
					box.ForeColor = Theme.Text;
					box.SelectionStart = 0;
					box.SelectionLength = 0;
				}
				catch
				{
				}
			};
			dlg.ShowDialog(this);
		}
		finally
		{
			dlg.Dispose();
		}
	}

	/// <summary>顶部「读取设备信息」：后台读 /sys + /st，然后整窗显示（完整、可滚动、可复制）。</summary>
	private void ReadDeviceInfo()
	{
		SetMsg("正在读取设备信息（/sys + /st）…");
		RunBg(delegate
		{
			string sys = _s3.GetSys();
			string errSys = _s3.LastError;
			YzStatus st = _s3.GetStatus();
			string errSt = _s3.LastError;
			UiInvoke(delegate
			{
				_lastSysText = sys;
				if (st != null)
				{
					_lastStatus = st;
				}
				UpdateFirmwareLabels();
				UpdateSysBrief();
				if (_txtSys != null)
				{
					_txtSys.Text = FormatSysText(sys);
				}
				StringBuilder sb = new StringBuilder();
				sb.AppendLine("读取时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				sb.AppendLine("设备地址：" + _s3.Host + "（" + (string.IsNullOrEmpty(_cfg.S3HostSource) ? "未知来源" : _cfg.S3HostSource) + "）");
				sb.AppendLine();
				sb.AppendLine("========== 关键信息 ==========");
				sb.AppendLine(SysBriefText());
				sb.AppendLine("========== /sys 原始（每项一行） ==========");
				sb.AppendLine((sys == null) ? ("读取失败：" + errSys) : FormatSysText(sys));
				sb.AppendLine();
				sb.AppendLine("========== /st 当前状态 ==========");
				sb.AppendLine((st == null) ? ("读取失败：" + errSt) : st.ToString());
				ShowInfoDialog("读取设备信息", sb.ToString());
			});
		});
	}

	private static string FormatSysText(string sys)
	{
		if (string.IsNullOrEmpty(sys))
		{
			return "";
		}
		return sys.Replace(";", ";\r\n").TrimEnd();
	}

	/// <summary>系统页「详情」：完整 /sys 原文 + 固件诊断全文（都放进同一个可复制弹窗）。</summary>
	private void ShowSysDetailDialog()
	{
		string sys = FormatSysText(_lastSysText);
		if (sys.Length == 0 && _txtSys != null)
		{
			sys = _txtSys.Text;
		}
		string diag = _lastDiagText;
		if (string.IsNullOrEmpty(diag) && _txtDiag != null)
		{
			diag = _txtDiag.Text;
		}
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("========== 关键信息 ==========");
		sb.AppendLine(SysBriefText());
		sb.AppendLine("========== 完整 /sys ==========");
		sb.AppendLine((sys.Length == 0) ? "（尚未读取：先点「刷新 /sys」）" : sys);
		sb.AppendLine();
		sb.AppendLine("========== 固件诊断（/sys 详细字段） ==========");
		sb.AppendLine(string.IsNullOrEmpty(diag) ? "（尚未读取：先点「刷新 /sys」）" : diag);
		ShowInfoDialog("系统详情", sb.ToString());
	}

	/// <summary>打开复位历史弹窗；缓存为空就先异步读一次再弹（不让用户看到空窗）。</summary>
	private void OpenResetHistory()
	{
		if (_lastRst != null)
		{
			ShowResetHistoryDialog();
			return;
		}
		SetMsg("正在读取复位历史 /rst?n=16 …");
		RunBg(delegate
		{
			YzRstHistory h = _s3.GetRstHistory(16);
			string err = _s3.LastError;
			UiInvoke(delegate
			{
				ApplyResetTable(h, err);
				ShowResetHistoryDialog();
			});
		});
	}

	/// <summary>系统页「复位历史」：用当前缓存（_lastRst）生成表格弹窗，可全选复制。</summary>
	private void ShowResetHistoryDialog()
	{
		YzRstHistory h = _lastRst;
		if (h == null)
		{
			ShowInfoDialog("复位历史", "尚未读取到复位历史。\r\n\r\n请稍等 1~2 秒后再点一次「复位历史…」（正在从设备读取 /rst?n=16）。");
			return;
		}
		Rectangle wa = Screen.FromControl(this).WorkingArea;
		Form dlg = new Form();
		try
		{
			dlg.Text = "复位历史（/rst?n=16） — 累计 " + h.Total + " 次";
			dlg.StartPosition = FormStartPosition.CenterParent;
			dlg.MinimizeBox = false;
			dlg.ShowInTaskbar = false;
			dlg.BackColor = Theme.Bg;
			dlg.ForeColor = Theme.Text;
			dlg.Font = Theme.FUi(9.5f, FontStyle.Regular);
			dlg.TopMost = base.TopMost;
			dlg.ClientSize = new Size(Math.Min(1040, Math.Max(640, wa.Width - 200)), Math.Min(620, Math.Max(380, wa.Height - 200)));
			dlg.MinimumSize = new Size(560, 320);
			Label sum = new Label();
			sum.Dock = DockStyle.Top;
			sum.Height = 46;
			sum.TextAlign = ContentAlignment.MiddleLeft;
			Theme.StyleLabel(sum, dim: false);
			sum.Text = "累计 " + h.Total + " 次：" + h.Summary + "\r\n最近一次：" + h.LastLine;
			ListView lv = new ListView();
			lv.Dock = DockStyle.Fill;
			lv.View = View.Details;
			lv.FullRowSelect = true;
			lv.GridLines = true;
			lv.MultiSelect = true;
			lv.Columns.Add("类型", 76);
			lv.Columns.Add("中文含义", 210);
			lv.Columns.Add("崩溃面包屑", 190);
			lv.Columns.Add("上次运行(s)", 84);
			lv.Columns.Add("最低堆(B)", 84);
			lv.Columns.Add("loop峰值(ms)", 94);
			lv.Columns.Add("峰值crumb", 76);
			Theme.StyleListView(lv);
			FillResetList(lv, h);
			Panel bottom = new Panel();
			bottom.Dock = DockStyle.Bottom;
			bottom.Height = 46;
			FlowLayoutPanel btns = new FlowLayoutPanel();
			btns.Dock = DockStyle.Fill;
			btns.FlowDirection = FlowDirection.RightToLeft;
			btns.WrapContents = false;
			btns.Padding = new Padding(8, 6, 10, 6);
			Button btnClose = Theme.MakeButton("关闭", delegate
			{
				dlg.Close();
			});
			btnClose.Width = 110;
			btns.Controls.Add(btnClose);
			Button btnCopy = Theme.MakeButton("复制为文本", delegate
			{
				try
				{
					Clipboard.SetText(ResetListText(h));
					SetMsg("已复制复位历史文本到剪贴板");
				}
				catch (Exception ex)
				{
					SetMsg("复制失败：" + ex.Message);
				}
			});
			btnCopy.Width = 130;
			btns.Controls.Add(btnCopy);
			Button btnReload = Theme.MakeButton("重新读取", delegate
			{
				dlg.Close();
				_lastRst = null;
				OpenResetHistory();
			});
			btnReload.Width = 120;
			btns.Controls.Add(btnReload);
			bottom.Controls.Add(btns);
			dlg.Controls.Add(lv);
			dlg.Controls.Add(bottom);
			dlg.Controls.Add(sum);
			dlg.CancelButton = btnClose;
			dlg.Shown += delegate
			{
				try
				{
					dlg.Bounds = ClampToScreen(dlg.Bounds, Screen.FromControl(dlg).WorkingArea);
				}
				catch
				{
				}
			};
			dlg.ShowDialog(this);
		}
		finally
		{
			dlg.Dispose();
		}
	}

	private static void FillResetList(ListView lv, YzRstHistory h)
	{
		lv.BeginUpdate();
		try
		{
			lv.Items.Clear();
			for (int i = h.Entries.Count - 1; i >= 0; i--)
			{
				YzRstEntry e = h.Entries[i];
				ListViewItem it = new ListViewItem((e.Name == "") ? "?" : e.Name);
				it.SubItems.Add(e.TypeText);
				it.SubItems.Add(e.CrumbText);
				it.SubItems.Add(e.UpSec.ToString());
				it.SubItems.Add(e.HeapMin.ToString());
				it.SubItems.Add(e.LoopMax.ToString());
				it.SubItems.Add((e.PeakCrumb == 0L) ? "-" : e.PeakCrumb.ToString());
				if (e.Name == "panic")
				{
					it.ForeColor = Theme.Danger;
				}
				else if (e.Name == "brownout" || e.Name == "brown" || e.Name == "taskwdt" || e.Name == "intwdt" || e.Name == "wdt")
				{
					it.ForeColor = Theme.Warn;
				}
				lv.Items.Add(it);
			}
		}
		finally
		{
			lv.EndUpdate();
		}
	}

	private static string ResetListText(YzRstHistory h)
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("复位历史（累计 " + h.Total + " 次）：" + h.Summary);
		sb.AppendLine("最近一次：" + h.LastLine);
		sb.AppendLine("类型\t中文含义\t崩溃面包屑\t上次运行(s)\t最低堆(B)\tloop峰值(ms)\t峰值crumb");
		for (int i = h.Entries.Count - 1; i >= 0; i--)
		{
			YzRstEntry e = h.Entries[i];
			sb.AppendLine(string.Join("\t", (e.Name == "") ? "?" : e.Name, e.TypeText, e.CrumbText, e.UpSec.ToString(), e.HeapMin.ToString(), e.LoopMax.ToString(), (e.PeakCrumb == 0L) ? "-" : e.PeakCrumb.ToString()));
		}
		return sb.ToString();
	}

	/// <summary>系统页「关键信息」三行摘要（连接 / 地址 / 固件 / 复位 / 堆 / 路由）。</summary>
	private string SysBriefText()
	{
		YzStatus st = _lastStatus;
		YzRstHistory rst = _lastRst;
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("设备：" + (string.IsNullOrEmpty(_s3.Host) ? "（未设置）" : (_s3.Host + ":80"))
			+ "（" + (string.IsNullOrEmpty(_cfg.S3HostSource) ? "未知来源" : _cfg.S3HostSource) + "）"
			+ "  ·  " + ((st == null) ? "连接：未连接" : "连接：正常")
			+ "  ·  心跳：" + ((_boardSeen.Length > 0) ? ("最后成功 " + _boardSeen) : "尚未成功")
			+ "  ·  " + ((_lastDiag != null) ? ("运行 " + _lastDiag.UpSec + " s") : "运行 --"));
		sb.AppendLine("固件：" + ((FirmwareText(st).Length == 0) ? "未读到" : FirmwareText(st).Replace("固件 ", ""))
			+ "  ·  " + ((st == null) ? "堆 --" : ("可用堆 " + st.Heap + " B（" + (st.Heap / 1024) + " KB）"))
			+ "  ·  复位：" + ((rst == null) ? "未读取（点「复位历史…」）" : (rst.Summary + " / 累计 " + rst.Total + " 次")));
		sb.AppendLine("网络：" + ((st == null) ? "未连接" : ("WiFi " + st.Ip + " · 以太网 " + st.EthIp))
			+ ((_lastEth != null && st != null) ? ("  ·  " + _lastEth.Text(st.Ip)) : ""));
		sb.Append("通路：S3 HTTP:80（/st 轮询 " + Math.Max(1, _pollFast ? 1 : 5) + " s）"
			+ "  ·  电台通道 " + (string.IsNullOrEmpty(_s3.RigTransport) ? "HTTP" : _s3.RigTransport)
			+ "  ·  电台 " + (ChannelIsDirect ? ("直连 " + DirectTargetText()) : "经板子 /rst2")
			+ "  ·  板载中继 " + ((_s3 == null) ? "--" : (_rigChannel == "board" ? "启用中" : "未启用")));
		return sb.ToString();
	}

	/// <summary>系统页只显示最近 3 条复位记录（完整表在「复位历史…」弹窗里）。</summary>
	private void UpdateResetTop3(YzRstHistory h)
	{
		if (_lblRstTop3 == null)
		{
			return;
		}
		if (h == null || h.Entries == null || h.Entries.Count == 0)
		{
			_lblRstTop3.Text = "复位原因（最近 3 条）：暂无记录";
			_lblRstTop3.Visible = false;
			if (_rstRowStyle != null)
			{
				_rstRowStyle.Height = 0f;
			}
			return;
		}
		_lblRstTop3.Visible = true;
		if (_rstRowStyle != null)
		{
			_rstRowStyle.Height = 78f;
		}
		StringBuilder sb = new StringBuilder();
		sb.Append("复位原因（最近 3 条，累计 " + h.Total + " 次）：");
		int n = 0;
		for (int i = h.Entries.Count - 1; i >= 0 && n < 3; i--, n++)
		{
			YzRstEntry e = h.Entries[i];
			sb.AppendLine();
			sb.Append("  " + (n + 1) + ") " + ((e.Name == "") ? "?" : e.Name) + " · " + e.TypeText
				+ " · 面包屑 " + ((e.CrumbText == "") ? "-" : e.CrumbText)
				+ " · 运行 " + e.UpSec + " s · 最低堆 " + e.HeapMin + " B");
		}
		_lblRstTop3.Text = sb.ToString();
		_lblRstTop3.ForeColor = (h.Panic > 0) ? Theme.Danger : ((h.Brownout > 0 || h.TaskWdt > 0 || h.IntWdt > 0 || h.Wdt > 0) ? Theme.Warn : Theme.TextDim);
	}

	private void UpdateSysBrief()
	{
		if (_lblSysBrief == null)
		{
			return;
		}
		try
		{
			_lblSysBrief.Text = SysBriefText();
		}
		catch
		{
		}
	}

	private static Label L(string text, int x, int y)
	{
		Label label = new Label();
		label.Text = text;
		label.SetBounds(x, y + 3, 44, 20);
		Theme.StyleLabel(label, dim: true);
		return label;
	}

	private static TextBox T(int x, int y, int w)
	{
		TextBox textBox = new TextBox();
		textBox.SetBounds(x, y, w, 23);
		Theme.StyleTextBox(textBox);
		return textBox;
	}

	private static FlowLayoutPanel Row()
	{
		return new FlowLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = AutoSizeMode.GrowAndShrink,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = true,
			BackColor = Color.Transparent,
			Padding = new Padding(0),
			Margin = new Padding(0, 2, 0, 12),
			MinimumSize = new Size(0, 42)
		};
	}

	private static Label MkGroup(string text)
	{
		Label label = Theme.SectionLabel(text);
		if (label.Width < 240)
		{
			label.Width = 240;
		}
		return label;
	}

	private static Label AddGroup(FlowLayoutPanel col, string text)
	{
		Label label = Theme.SectionLabel(text);
		label.AutoSize = false;
		label.Height = 26;
		label.Margin = new Padding(0, 12, 0, 8);
		SetFlowSize(col, label);
		col.Controls.Add(label);
		return label;
	}

	private static void AddRow(FlowLayoutPanel col, Control row)
	{
		SetFlowSize(col, row);
		col.Controls.Add(row);
		FitFlowRowHeight(row);
	}

	internal static void FitFlowRowHeight(Control row)
	{
		if (row == null)
		{
			return;
		}
		row.Height = CalcFlowRowHeight(row);
		EventHandler fix = delegate
		{
			ApplyRowHeight(row);
		};
		row.ParentChanged += delegate(object s, EventArgs e)
		{
			Control control = (Control)s;
			if (control.Parent != null)
			{
				control.Parent.SizeChanged -= fix;
				control.Parent.SizeChanged += fix;
			}
			ApplyRowHeight(row);
		};
		if (row.Parent != null)
		{
			row.Parent.SizeChanged -= fix;
			row.Parent.SizeChanged += fix;
		}
		ApplyRowHeight(row);
	}

	private static void ApplyRowHeight(Control row)
	{
		if (row == null)
		{
			return;
		}
		Form form = row.FindForm();
		if (form != null && form.IsHandleCreated)
		{
			try
			{
				form.BeginInvoke((MethodInvoker)delegate
				{
					if (row != null && !row.IsDisposed)
					{
						int num2 = CalcFlowRowHeight(row);
						if (row.Height != num2)
						{
							row.Height = num2;
						}
						Form form2 = row.FindForm();
						if (form2 != null && form2.IsHandleCreated)
						{
							try
							{
								form2.BeginInvoke((MethodInvoker)delegate
								{
									EnforceMinGap(row);
								});
								return;
							}
							catch
							{
							}
						}
						EnforceMinGap(row);
					}
				});
				return;
			}
			catch
			{
			}
		}
		int num = CalcFlowRowHeight(row);
		if (row.Height != num)
		{
			row.Height = num;
		}
	}

	private static void EnforceMinGap(Control row)
	{
		if (!(row is FlowLayoutPanel flowLayoutPanel) || flowLayoutPanel.Controls.Count < 2)
		{
			return;
		}
		List<Control> list = new List<Control>();
		foreach (Control control4 in flowLayoutPanel.Controls)
		{
			if (control4.Visible)
			{
				list.Add(control4);
			}
		}
		list.Sort(delegate(Control a, Control b)
		{
			int num2 = a.Top.CompareTo(b.Top);
			return (num2 == 0) ? a.Left.CompareTo(b.Left) : num2;
		});
		for (int i = 0; i + 1 < list.Count; i++)
		{
			Control control2 = list[i];
			Control control3 = list[i + 1];
			if (Math.Abs(control2.Top - control3.Top) <= 8 && !IsAllowedTight(control2, control3))
			{
				int num = control3.Left - control2.Right;
				if (num >= 0 && num < 10)
				{
					control3.Left = control2.Right + 10;
				}
			}
		}
	}

	private static int CalcFlowRowHeight(Control row)
	{
		if (!(row is FlowLayoutPanel flowLayoutPanel))
		{
			return row.Height;
		}
		int num = ((flowLayoutPanel.Parent != null) ? (flowLayoutPanel.Parent.ClientSize.Width - 10) : flowLayoutPanel.ClientSize.Width);
		if (num < 120)
		{
			num = flowLayoutPanel.ClientSize.Width;
		}
		if (num < 60)
		{
			return 44;
		}
		int num2 = flowLayoutPanel.Padding.Left;
		int num3 = 1;
		int num4 = 0;
		int num5 = 0;
		foreach (Control control in flowLayoutPanel.Controls)
		{
			if (control.Visible)
			{
				int num6 = control.Width + control.Margin.Horizontal;
				if (num2 + num6 > num + 1 && num2 > flowLayoutPanel.Padding.Left)
				{
					num3++;
					num2 = flowLayoutPanel.Padding.Left;
					num5 = 0;
				}
				num2 += num6;
				int num7 = control.Height + control.Margin.Vertical;
				if (num7 > num5)
				{
					num5 = num7;
				}
				if (num5 > num4)
				{
					num4 = num5;
				}
			}
		}
		int num8 = num3 * num4 + flowLayoutPanel.Padding.Vertical;
		if (num8 >= 40)
		{
			return num8;
		}
		return 40;
	}

	private static void SetFlowSize(FlowLayoutPanel col, Control c)
	{
		c.AutoSize = false;
		int num = ((col != null && col.ClientSize.Width > 400) ? (col.ClientSize.Width - 10) : 560);
		if (c.Width < num)
		{
			c.Width = num;
		}
		if (col == null)
		{
			return;
		}
		col.Resize += delegate
		{
			SyncRowWidth(col, c);
		};
		col.Layout += delegate
		{
			SyncRowWidth(col, c);
		};
		c.ParentChanged += delegate
		{
			Form form = col.FindForm();
			if (form != null && form.IsHandleCreated)
			{
				try
				{
					form.BeginInvoke((MethodInvoker)delegate
					{
						SyncRowWidth(col, c);
					});
					return;
				}
				catch
				{
				}
			}
			SyncRowWidth(col, c);
		};
	}

	private static void SyncRowWidth(FlowLayoutPanel col, Control c)
	{
		if (col == null || c == null || c.IsDisposed)
		{
			return;
		}
		// v1.5.1：留 22px 余量（原来 -10，正好卡在边界上 → FlowLayoutPanel 会多出一条
		// 灰色的横向滚动条；内容其实根本没超宽）
		int num = col.ClientSize.Width - 40;
		if (num > 160)
		{
			if (c.Width != num)
			{
				c.Width = num;
			}
			int num2 = CalcFlowRowHeight(c);
			if (c.Height != num2)
			{
				c.Height = num2;
			}
			EnforceMinGap(c);
		}
	}

	private static void ApplyFlowWidth(Control c)
	{
		if (c != null && c.Parent != null)
		{
			int num = c.Parent.ClientSize.Width - 10;
			if (num > 160 && c.Width != num)
			{
				c.Width = num;
			}
		}
	}

	private static Label SectionLabel(string text)
	{
		return Theme.SectionLabel("▌ " + text);
	}

	private static NumericUpDown Num(decimal min, decimal max, decimal val, int width)
	{
		return new NumericUpDown
		{
			Minimum = min,
			Maximum = max,
			Value = val,
			Width = width
		};
	}

	private void Act(string g)
	{
		RunBg(delegate
		{
			if (_s3.CmdEx(g, out var code, out var _, out var note))
			{
				SetMsg("已发送 /cmd?g=" + g + "（HTTP 200 OK）");
			}
			else if (code == 409)
			{
				SetMsgSafe("⚠ 指令被固件保护性拒绝（HTTP 409）：" + note + "\u3000→ 已刷新 azblk 诊断");
				DiagReadBg();
			}
			else
			{
				SetMsgSafe("指令失败：" + note);
			}
		});
	}

	private void GotoTarget()
	{
		double az = (double)_numAz.Value;
		double el = (double)_numEl.Value;
		string plan = AzPlanText(az);
		RunBg(delegate
		{
			if (_s3.GotoEx(az, el, out var code, out var _, out var note))
			{
				SetMsgSafe($"✔ 已下发定向：方位 {az:0.#}°（原样，不折叠）／俯仰 {el:0.#}°\u3000{plan}");
			}
			else
			{
				SetMsgSafe("✘ 定向失败（HTTP " + code + "）：" + note);
			}
		});
	}

	private string AzPlanText(double tgt)
	{
		if (_lastStatus == null)
		{
			return "（方位预览：等第一次状态读回后再显示；0 与 360 是两个不同目标）";
		}
		return YzAzPlan.Compute(_lastStatus.Az, tgt, _lastStatus.MsPerDeg, _lastStatus.NoWrap).Hint;
	}

	private void UpdateAzPlan()
	{
		if (_lblAzPlan == null)
		{
			return;
		}
		try
		{
			double tgt = (double)_numAz.Value;
			string text = AzPlanText(tgt);
			if (text != _lblAzPlan.Text)
			{
				_lblAzPlan.Text = text;
			}
			YzAzPlan.Plan plan = ((_lastStatus == null) ? null : YzAzPlan.Compute(_lastStatus.Az, tgt, _lastStatus.MsPerDeg, _lastStatus.NoWrap));
			_lblAzPlan.ForeColor = ((plan != null && plan.LongWay) ? Theme.Warn : HudDraw.Cyan);
		}
		catch
		{
		}
	}

	/// <summary>
	/// v1.5.1：云台页只保留一个「保存参数」按钮 —— 一次把
	/// 水平速度(mpd) / 俯仰速度(vmpd) / 仰角上限 / 俯角下限 全写进 S3（NVS，掉电保存）。
	/// 三个旧的单参数保存按钮仍保留在「高级诊断」里（功能完全没丢）。
	/// </summary>
	private void SaveAllParams()
	{
		int mpd = (int)_numSpeed.Value;
		int mpdV = (int)_numSpeedV.Value;
		int up = (int)_numUp.Value;
		int down = (int)_numDown.Value;
		SetMsg("正在写入参数（水平/俯仰速度 + 上下限位）…");
		RunBg(delegate
		{
			bool a = _s3.SetSpeed(mpd);
			bool b = _s3.SetSpeedV(mpdV);
			bool c = _s3.SetLimits(up, down);
			if (a && b && c)
			{
				SetMsgSafe($"参数已全部写入 S3：水平 {mpd} ms/°({YzStatus.DegPerSec(mpd):0.0#}°/s) · 俯仰 {mpdV} ms/°({YzStatus.DegPerSec(mpdV):0.0#}°/s) · 限位 上 {up}° / 下 {down}°（NVS 掉电保存）");
			}
			else
			{
				SetMsgSafe("参数写入未全部成功：水平 " + (a ? "OK" : "失败") + " / 俯仰 " + (b ? "OK" : "失败") + " / 限位 " + (c ? "OK" : "失败") + " —— " + _s3.LastError);
			}
		});
	}

	private void SaveSpeed()
	{
		int i = (int)_numSpeed.Value;
		RunBg(delegate
		{
			if (!_s3.SetSpeed(i))
			{
				SetMsg("保存水平速度失败：" + _s3.LastError);
			}
			else
			{
				SetMsg($"水平速度已保存：{i} ms/° = {YzStatus.DegPerSec(i):0.0#} °/s（NVS `mpd`，掉电保存）");
			}
		});
	}

	private void UpdateSpeedEditLabels()
	{
		if (_lblSpeedEdit != null && _numSpeed != null)
		{
			int num = (int)_numSpeed.Value;
			_lblSpeedEdit.Text = $"= {YzStatus.DegPerSec(num):0.0#} °/s";
		}
		if (_lblSpeedVEdit != null && _numSpeedV != null)
		{
			int num2 = (int)_numSpeedV.Value;
			_lblSpeedVEdit.Text = $"= {YzStatus.DegPerSec(num2):0.0#} °/s";
		}
	}

	private void SaveSpeedV()
	{
		int k2 = (int)_numSpeedV.Value;
		RunBg(delegate
		{
			if (!_s3.SetSpeedV(k2))
			{
				SetMsg("保存俯仰速度失败：" + _s3.LastError);
			}
			else
			{
				SetMsg($"俯仰速度已保存：{k2} ms/° = {YzStatus.DegPerSec(k2):0.0#} °/s（NVS `vmpd`，与水平 k 完全独立）");
			}
		});
	}

	private void ApplySelfTestFlag()
	{
		if (_selfTestSync || _chkSelfTest == null)
		{
			return;
		}
		bool on = _chkSelfTest.Checked;
		RunBg(delegate
		{
			if (!_s3.SetSelfTest(on))
			{
				SetMsgSafe("设置上电自检失败：" + _s3.LastError);
				UiInvoke(delegate
				{
					_selfTestSync = true;
					try
					{
						_chkSelfTest.Checked = !on;
					}
					finally
					{
						_selfTestSync = false;
					}
				});
			}
			else
			{
				SetMsgSafe(on ? "上电自检已开启（sf=1，存 NVS；下次上电水平轴会转满一圈后归零）" : "上电自检已关闭（sf=0，存 NVS；下次上电水平轴保持静止）");
			}
		});
	}

	private void RunSelfTest()
	{
		SetMsg("正在下发“跑一圈”…");
		RunBg(delegate
		{
			string note;
			bool flag = _s3.SelfTestRun(out note);
			SetMsgSafe((flag ? "✔ " : "✘ ") + note);
		});
	}

	private void DtracReadBg()
	{
		YzTrackSnapshot t = _s3.GetTrackCli();
		string err = _s3.LastError;
		UiInvoke(delegate
		{
			ApplyDtrac(t, err);
		});
	}

	private void ApplyDtrac(YzTrackSnapshot t, string err)
	{
		if (_lblDtracText == null && _lblDtracSys == null && _lblDtracDial == null)
		{
			return;
		}
		string text;
		Color foreColor;
		if (t == null)
		{
			text = "DTrac：读取失败（" + err + "）";
			foreColor = Theme.Danger;
		}
		else
		{
			string text2 = string.Format("仲裁 arb={0}\u3000·\u3000被拒指令数 rej={1}\u3000·\u3000槽上限 max={2}", (t.Arb != 0) ? "开" : "关", t.Rejected, t.MaxSlots);
			if (t.OnlineCount == 0)
			{
				text = "DTrac：无客户端在线\u3000·\u3000" + text2;
				foreColor = Theme.TextDim;
			}
			else
			{
				StringBuilder stringBuilder = new StringBuilder();
				for (int i = 0; i < t.Slots.Count; i++)
				{
					YzTrackCli yzTrackCli = t.Slots[i];
					if (yzTrackCli.On)
					{
						if (stringBuilder.Length > 0)
						{
							stringBuilder.Append("  |  ");
						}
						stringBuilder.Append(string.Format("槽{0} {1} {2}", yzTrackCli.Slot, yzTrackCli.Ip, yzTrackCli.Last ? "在线" : "在线·静默"));
						stringBuilder.Append($"（最后数据 {yzTrackCli.ActAgeSec} 秒前，已连 {yzTrackCli.ConnAgeSec} 秒）");
						if (yzTrackCli.Ctrl)
						{
							stringBuilder.Append(" ←控制权");
						}
					}
				}
				string text3 = ((t.CtrlSlot > 0) ? $"控制权：槽{t.CtrlSlot} {t.CtrlIp}" : ("控制权：未分配（ctrl_ip=" + t.CtrlIp + "）"));
				text = "DTrac：" + stringBuilder.ToString() + "\u3000·\u3000" + text3 + "\u3000·\u3000" + text2;
				foreColor = Theme.Ok;
			}
		}
		if (_lblDtracText != null)
		{
			_lblDtracText.Text = text;
			_lblDtracText.ForeColor = foreColor;
		}
		if (_lblDtracDial != null)
		{
			_lblDtracDial.Text = text;
			_lblDtracDial.ForeColor = foreColor;
		}
		if (_lblDtracSys != null)
		{
			_lblDtracSys.Text = text;
			_lblDtracSys.ForeColor = foreColor;
		}
		if (t != null && t.OnlineCount > 0)
		{
			SetDotTip(_dotNet, DotState.Ok, "DTrac 在线 " + t.OnlineCount, "追星客户端（TCP 4533）：" + t.OnlineCount + " 台在线\n" + t.Raw.Trim());
		}
	}

	private void ReleasePriorityFromDtrac()
	{
		if (MessageBox.Show(this, "释放云台控制权（/prio?off=1）？\r\n\r\n· 只改「谁能下发运动指令」，**不会断开任何 4533 连接**（DTrac 仍在线）；\r\n· 释放后控制权自动回到「最早接入者」（通常就是 DTrac）。", "释放控制权", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
		{
			return;
		}
		SetMsg("正在释放控制权 /prio?off=1 …");
		RunBg(delegate
		{
			bool flag = _s3.ReleasePriority();
			SetMsgSafe(flag ? "✔ 已释放云台控制权（/prio?off=1）——控制权回到最早接入者，连接全部保持" : ("✘ 释放控制权失败：" + _s3.LastError));
			if (flag)
			{
				DtracReadBg();
			}
		});
	}

	private void Confirm4533OptIn()
	{
		if (MessageBox.Show(this, "4533 只读查询 = 作为**非控制者**接入，只发查询、不发任何运动指令，用完立刻断开。\r\n\r\n⚠ 必须知道的风险：固件对 4533 的规则是「同源 IP 的新连接会替换它自己的旧会话」。\r\n   DTrac 与本上位机若在**同一台电脑**（源 IP 相同），这次查询可能把 DTrac 的会话顶掉一次\r\n   （DTrac 会掉线并自行重连；不会改变云台角度）。\r\n\r\n建议：仅在确实需要确认 4533 是否活着时勾选；平时保持关闭（默认只用 HTTP）。\r\n\r\n确定允许本次会话使用 4533 只读查询吗？", "4533 只读查询 —— 风险确认", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
		{
			_chkAllow4533.Checked = false;
		}
	}

	private void Query4533Once()
	{
		if (_chkAllow4533 == null || !_chkAllow4533.Checked)
		{
			SetMsg("4533 只读查询未启用：请先勾选「允许 4533 只读查询」（默认只用 HTTP）");
			return;
		}
		int proto = ((_lastStatus != null) ? _lastStatus.Proto : 0);
		SetMsg("正在做一次 4533 只读查询（只发查询、用完即断）…");
		RunBg(delegate
		{
			string note;
			string text = _s3.ReadOnlyQuery4533(proto, out note);
			SetMsgSafe(((text != null) ? "✔ " : "✘ ") + note);
		});
	}

	private void RefreshResetDiag()
	{
		RefreshResetTable();
	}

	private void ApplyResetDiag(YzResetStat st, string err)
	{
		if (st == null)
		{
			ApplyResetTable(null, err);
			return;
		}
		YzRstHistory yzRstHistory = new YzRstHistory();
		yzRstHistory.Panic = st.Panic;
		yzRstHistory.TaskWdt = st.TaskWdt;
		yzRstHistory.IntWdt = st.IntWdt;
		yzRstHistory.Wdt = st.Wdt;
		yzRstHistory.Brownout = st.Brownout;
		yzRstHistory.PowerOn = st.PowerOn;
		yzRstHistory.Sw = st.Sw;
		yzRstHistory.ExtRst = st.ExtRst;
		yzRstHistory.Total = st.Total;
		ApplyResetTable(yzRstHistory, err);
	}

	private void SaveLimits()
	{
		int up = (int)_numUp.Value;
		int down = (int)_numDown.Value;
		RunBg(delegate
		{
			if (!_s3.SetLimits(up, down))
			{
				SetMsg("保存限位失败");
			}
			else
			{
				SetMsg($"限位已保存：上 {up}° / 下 {down}°（NVS，掉电保存）");
			}
		});
	}

	private void ReadTcal()
	{
		RunBg(delegate
		{
			string tcal = _s3.GetTcal();
			SetMsg((tcal == null) ? ("读取标定失败：" + _s3.LastError) : ("标定值 " + tcal));
		});
	}

	private void PitchZeroNow()
	{
		if (MessageBox.Show(this, "把云台【俯仰当前姿态】定义为 0°？\r\n\r\n· **任意姿态都可用**（不需要先机械回水平）\r\n· **立即生效**：当圈 `va` 与 `elint` 回 0（已在机架水平位时就是「把当前当作零点」）\r\n· **写 NVS**（掉电不丢）；固件 `/st` 的 `vz` 计数 +1 可回读核对\r\n· 走的命令 = `/cmd?g=VZ`（与网页「传感器校零」同一条路径）", "俯仰姿态归零", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
		{
			return;
		}
		SetMsg("正在下发「俯仰姿态归零」/cmd?g=VZ …");
		RunBg(delegate
		{
			string note;
			bool flag = _s3.PitchZero(out note);
			SetMsgSafe((flag ? "✔ " : "✘ ") + note);
			if (flag)
			{
				if (_lblPzNote != null)
				{
					_lblPzNote.Text = "上次结果：" + note;
					_lblPzNote.ForeColor = Theme.Ok;
				}
				DiagReadBg();
			}
			else if (_lblPzNote != null)
			{
				_lblPzNote.Text = "上次结果：" + note;
				_lblPzNote.ForeColor = Theme.Danger;
			}
		});
	}

	private void TogglePtrace()
	{
		bool flag = false;
		bool flag2 = false;
		if (_lastStatus != null && _lastStatus.PTrace >= 0)
		{
			flag = _lastStatus.PTrace != 0;
			flag2 = true;
		}
		else if (_lastDiag != null && _lastDiag.PTrace >= 0)
		{
			flag = _lastDiag.PTrace != 0;
			flag2 = true;
		}
		if (!flag2)
		{
			SetMsg("ptrace：还读不到设备当前值（先点「刷新诊断 /sys」或等一次状态轮询），本次不下发");
			return;
		}
		bool want = !flag;
		SetMsg("正在下发 ptrace=" + (want ? 1 : 0) + " …");
		RunBg(delegate
		{
			string note;
			bool flag3 = _s3.SetPtrace(want, out note);
			SetMsgSafe((flag3 ? "✔ " : "✘ ") + note);
			if (flag3)
			{
				UiInvoke(delegate
				{
					RefreshPtraceButton(want);
				});
				DiagReadBg();
			}
		});
	}

	private void RefreshPtraceButton(bool on)
	{
		if (_btnPtrace != null)
		{
			string text = (on ? "引脚时序日志 ptrace：开（点此关闭）" : "引脚时序日志 ptrace：关（点此开启）");
			if (_btnPtrace.Text != text)
			{
				_btnPtrace.Text = text;
			}
			_btnPtrace.ForeColor = (on ? Theme.Warn : Theme.Text);
		}
	}

	private void RecLeaveStandby()
	{
		RunBg(delegate
		{
			int code;
			string body;
			string note;
			bool flag = _s3.CmdEx("R", out code, out body, out note);
			if (flag || code == 409)
			{
				SetMsgSafe(flag ? "✔ 已发出一条安全运动指令（/cmd?g=R）解除待命；随后立即停机（/cmd?g=S）" : "✔ 待命已解除（点动被端点保护拒绝 409 也照样解除——固件在到限判定前就调 yzRecLeaveStandby）；随后立即停机");
				try
				{
					_s3.Cmd("S");
				}
				catch
				{
				}
				DiagReadBg();
			}
			else
			{
				SetMsgSafe("解除待命失败（HTTP " + code + "）：" + note);
			}
		});
	}

	private void RefreshDiagNow()
	{
		SetMsg("正在读取固件诊断 /sys …");
		DiagReadBg();
	}

	private void DiagReadBg()
	{
		if (Interlocked.CompareExchange(ref _diagBusy, 1, 0) != 0)
		{
			return;
		}
		RunBg(delegate
		{
			try
			{
				YzSysSnapshot s = _s3.GetSysSnapshot();
				string err = _s3.LastError;
				YzEthInfo yzEthInfo = null;
				if (s != null)
				{
					S3Discovery.RememberBoardIp(s.StaIp);
					S3Discovery.RememberBoardIp(s.EthIp);
					if (YzOtaGate.IsZeroIp(s.EthIp))
					{
						yzEthInfo = _s3.GetEthInfo();
					}
				}
				YzEthInfo fe = yzEthInfo;
				UiInvoke(delegate
				{
					_lastEth = fe;
					ApplyDiag(s, err);
				});
			}
			finally
			{
				Interlocked.Exchange(ref _diagBusy, 0);
			}
		});
	}

	private void ApplyDiag(YzSysSnapshot s, string err)
	{
		if (s == null)
		{
			string text = "固件诊断：读取失败（" + err + "）";
			if (_lblDiagAz != null)
			{
				_lblDiagAz.Text = text;
				_lblDiagAz.ForeColor = Theme.Danger;
			}
			if (_txtDiag != null)
			{
				_txtDiag.Text = text;
			}
			return;
		}
		if (_lblDiagAz != null)
		{
			_lblDiagAz.Text = $"方位诊断：azblk={s.AzBlk}（{s.AzBlkName}）· azblkn={s.AzBlkCnt}（累计被拦次数）· 手动全停保持 msh={s.Msh} ms";
			_lblDiagAz.ForeColor = ((s.AzBlk == 0) ? Theme.TextDim : ((s.AzBlk == 3) ? Theme.Danger : Theme.Warn));
		}
		if (_lblDiagPitch != null)
		{
			long num = ((_lastStatus != null) ? _lastStatus.PitchZeroCnt : 0);
			string text2 = ((_lastStatus != null) ? _lastStatus.VRev.ToString(CultureInfo.InvariantCulture) : "?");
			_lblDiagPitch.Text = string.Format("俯仰输出：verr={0}（两线同高/输出异常，应恒 0）· vz={1}（姿态归零生效次数，源 /st）· vrev={2}（c30 起恒 1）· 待命 rec={3}（{4}）· ptrace={5}", s.VErr, num, text2, s.Rec, (s.RecName.Length == 0) ? "-" : s.RecName, s.PTrace);
			_lblDiagPitch.ForeColor = ((s.VErr != 0L) ? Theme.Danger : Theme.TextDim);
		}
		if (_lblDiagWdt != null)
		{
			bool flag = s.WdtAdd == -9999;
			bool flag2 = !flag && s.WdtAdd != 0;
			_lblDiagWdt.Text = string.Format("看门狗：wdtFeed={0}（递增）· wdtAdd={1}（{2}）· loopmax={3} ms · BOD 档 bod={4}/硬件 bodHw={5}/出厂 bodPre={6} · 已低于门槛 bodDet={7} · bodErr={8}（{9}）", s.WdtFeed, s.WdtAdd, flag ? "未上报（该固件早于 c31）" : (flag2 ? "⚠ 非 0：喂狗任务订阅失败" : "0 正常"), s.LoopMaxMs, s.Bod, s.BodHw, s.BodPre, s.BodDet, s.BodErr, YzFw.BodErrName(s.BodErr));
			_lblDiagWdt.ForeColor = ((flag2 || s.BodDet == 1 || s.VErr != 0L) ? Theme.Warn : Theme.TextDim);
		}
		YzWdtView yzWdtView = YzWdt.Build(s);
		if (_lblWdtL1 != null)
		{
			_lblWdtL1.Text = "第 1 层（任务看门狗 TWDT 15 s）：" + yzWdtView.L1;
			_lblWdtL1.ForeColor = SevColor(yzWdtView.L1Sev);
		}
		if (_lblWdtL2 != null)
		{
			string text3 = ((!yzWdtView.Layer2Unarmed || yzWdtView.L2.Contains("第 2 层未武装")) ? "" : "\u3000⚠⚠ **第 2 层未武装**（en/STAGE0/rtcwOk 未同时满足）—— 不要离开现场");
			_lblWdtL2.Text = "第 2 层（硬件 RTC_WDT 30 s）：" + yzWdtView.L2 + text3;
			_lblWdtL2.ForeColor = SevColor(yzWdtView.L2Sev);
		}
		if (_lblWdtBoot != null)
		{
			_lblWdtBoot.Text = "启动阶段：" + yzWdtView.Boot;
			_lblWdtBoot.ForeColor = SevColor(yzWdtView.BootSev);
		}
		if (_lblWdtSil != null)
		{
			_lblWdtSil.Text = "限频闸：" + yzWdtView.Sil;
			_lblWdtSil.ForeColor = SevColor(yzWdtView.SilSev);
		}
		if (_lblNetPath != null)
		{
			YzEthInfo lastEth = _lastEth;
			if (lastEth == null)
			{
				if (YzOtaGate.IsZeroIp(s.EthIp))
				{
					_lblNetPath.Text = string.Format("网络通路：板子**没有有线地址**（/sys eip={0}）⇒ 当前走 WiFi（ip={1}）；精确的链路位（link=）在 /eth —— 点「刷新诊断 /sys」会顺便读一次（只读）", (s.EthIp.Length == 0) ? "?" : s.EthIp, (s.StaIp.Length == 0) ? "?" : s.StaIp);
					_lblNetPath.ForeColor = Theme.Warn;
				}
				else
				{
					_lblNetPath.Text = $"网络通路：有线 ip={s.EthIp}\u3000无线 ip={s.StaIp}（读 /eth 可看 link/rx/tx）";
					_lblNetPath.ForeColor = Theme.TextDim;
				}
			}
			else
			{
				_lblNetPath.Text = "网络通路：" + lastEth.Text(s.StaIp);
				_lblNetPath.ForeColor = ((lastEth.LinkDown && lastEth.NoIp) ? Theme.Warn : ((lastEth.Link == 1) ? Theme.Ok : Theme.TextDim));
			}
		}
		ApplyProtoDiag(s);
		if (_lblDiagNvs != null)
		{
			_lblDiagNvs.Text = string.Format("NVS 写入：ok={0}/err={1}/排队={2} · 落盘 flushN={3}/写出={4} · 错误日志=\"{5}\"\u3000门禁：httpRl={6} tlsRl={7} httpLh={8} tlsLh={9} rlWin={10}ms rlip80={11}", s.NvsWrOk, s.NvsWrErr, s.NvsWrN, s.NvsFlushN, s.NvsFlushW, (s.NvsErrLog.Length == 0) ? "-" : s.NvsErrLog, s.HttpRl, s.TlsRl, s.HttpLh, s.TlsLh, s.RlWin, (s.Rlip80.Length == 0) ? "-" : s.Rlip80);
			_lblDiagNvs.ForeColor = ((s.NvsWrErr > 0) ? Theme.Warn : Theme.TextDim);
		}
		string text4 = (_lastDiagText = BuildDiagText(s));
		if (_txtDiag != null)
		{
			_txtDiag.Text = text4;
		}
		if (s.PTrace >= 0)
		{
			RefreshPtraceButton(s.PTrace != 0);
		}
		_lastDiag = s;
		if (_btnRecLeave != null)
		{
			_btnRecLeave.Enabled = s.Rec == 1;
		}
		UpdateSysBrief();
	}

	private void ApplyProtoDiag(YzSysSnapshot s)
	{
		YzStatus lastStatus = _lastStatus;
		long num = lastStatus?.EcOk ?? s?.EcOk ?? 0;
		long num2 = lastStatus?.EcBad ?? s?.EcBad ?? 0;
		long num3 = lastStatus?.EcRange ?? s?.EcRange ?? 0;
		long num4 = lastStatus?.EcPrio ?? s?.EcPrio ?? 0;
		string text = ((lastStatus != null) ? lastStatus.EcLast : ((s == null) ? "" : s.EcLast));
		if (_lblDiagProto != null)
		{
			_lblDiagProto.Text = string.Format("协议/跟踪：ecok={0}（到达执行器的行数）· ecbad={1}（无法识别）· ecrange={2}（参数非法已回 ERR）· ecprio={3}（被控制权仲裁拒绝）· eclast=\"{4}\"（板子收到的最后一条原文）· 跟踪客户端 tk={5}", num, num2, num3, num4, (text.Length == 0 || text == "-") ? "（还没收到任何行）" : text, lastStatus?.TrackOk ?? (-1));
			_lblDiagProto.ForeColor = ((num == 0L) ? Theme.Warn : Theme.TextDim);
		}
		if (_lblDiagEl != null)
		{
			long num5 = lastStatus?.ElAcc ?? s?.ElAcc ?? 0;
			long num6 = lastStatus?.ElRej ?? s?.ElRej ?? 0;
			int num7 = lastStatus?.ElRc ?? s?.ElRc ?? (-1);
			long num8 = lastStatus?.ElLastX10 ?? s?.ElLastX10 ?? (-32768);
			_lblDiagEl.Text = string.Format("协议 EL 目标：elacc={0}（受理）· elrej={1}（未受理）· elrc={2}（{3}）· ellast={4}（最近受理目标）", num5, num6, num7, YzFw.ElRejName(num7), (num8 <= -32768) ? "从未" : (((double)num8 / 10.0).ToString("0.0", CultureInfo.InvariantCulture) + "°"));
			_lblDiagEl.ForeColor = ((num6 > 0 && num5 == 0L) ? Theme.Warn : Theme.TextDim);
		}
	}

	private static string ElLastText(YzSysSnapshot s)
	{
		long l = s.GetL("ellast", -32768L);
		if (l > -32768)
		{
			return ((double)l / 10.0).ToString("0.0", CultureInfo.InvariantCulture) + "°";
		}
		return "从未";
	}

	private string NetPathDiagText(YzSysSnapshot s)
	{
		if (s == null)
		{
			return "（无 /sys 数据）";
		}
		if (_lastEth != null)
		{
			return _lastEth.Text(s.StaIp);
		}
		if (YzOtaGate.IsZeroIp(s.EthIp))
		{
			return string.Format("板子**没有有线地址**（/sys eip={0}）⇒ 当前走 WiFi（ip={1}）；精确链路位（link=）在 /eth —— 点「刷新诊断 /sys」会顺带只读读一次", (s.EthIp.Length == 0) ? "?" : s.EthIp, (s.StaIp.Length == 0) ? "?" : s.StaIp);
		}
		return $"有线 ip={s.EthIp}（/sys eip）· 无线 ip={s.StaIp}（/sys ip）";
	}

	private void RefreshWdtReadback()
	{
		SetMsg("正在只读回读 /set?wdt=（固件语义：不带值时只回读，不修改任何设置）…");
		RunBg(delegate
		{
			YzSysSnapshot w = _s3.GetWdtReadback();
			string err = _s3.LastError;
			UiInvoke(delegate
			{
				ApplyWdtReadback(w, err);
			});
		});
	}

	private void ApplyWdtReadback(YzSysSnapshot w, string err)
	{
		if (w == null)
		{
			SetMsg("读 /set?wdt= 失败：" + err);
			if (_lblWdtTest != null)
			{
				_lblWdtTest.Text = "读 /set?wdt= 失败：" + err;
				_lblWdtTest.ForeColor = Theme.Danger;
			}
			return;
		}
		YzWdtView yzWdtView = YzWdt.Build(w);
		if (_lblWdtL1 != null)
		{
			_lblWdtL1.Text = "第 1 层（任务看门狗 TWDT 15 s）：" + yzWdtView.L1;
			_lblWdtL1.ForeColor = SevColor(yzWdtView.L1Sev);
		}
		if (_lblWdtL2 != null)
		{
			string text = ((!yzWdtView.Layer2Unarmed || yzWdtView.L2.Contains("第 2 层未武装")) ? "" : "\u3000⚠⚠ **第 2 层未武装**（en/STAGE0/rtcwOk 未同时满足）—— 不要离开现场");
			_lblWdtL2.Text = "第 2 层（硬件 RTC_WDT 30 s）：" + yzWdtView.L2 + text;
			_lblWdtL2.ForeColor = SevColor(yzWdtView.L2Sev);
		}
		if (_lblWdtBoot != null)
		{
			_lblWdtBoot.Text = "启动阶段：" + yzWdtView.Boot;
			_lblWdtBoot.ForeColor = SevColor(yzWdtView.BootSev);
		}
		if (_lblWdtSil != null)
		{
			_lblWdtSil.Text = "限频闸：" + yzWdtView.Sil;
			_lblWdtSil.ForeColor = SevColor(yzWdtView.SilSev);
		}
		SetMsg("✔ 已只读回读 /set?wdt=（含 ticks/寄存器当场回读）");
	}

	private void WdtSelfTest3()
	{
		if (Interlocked.CompareExchange(ref _wdtTestBusy, 1, 0) != 0)
		{
			SetMsg("看门狗自测正在进行中，请等它跑完（云台页「自测：」那行会一直显示进度）。");
			return;
		}
		int n = 3;
		bool flag = false;
		try
		{
			try
			{
				if (_numHangN != null)
				{
					n = (int)_numHangN.Value;
				}
			}
			catch
			{
				n = 3;
			}
			YzHangStage[] stages = YzWdtSelfTest.Stages(n);
			int num = 0;
			YzHangStage[] array = stages;
			foreach (YzHangStage yzHangStage in array)
			{
				num += YzWdtSelfTest.WatchWindowSec(yzHangStage) * yzHangStage.Repeat;
			}
			string text = YzWdt.ExpectAdvisory(_lastDiag);
			string text2 = "即将对真板子执行「看门狗自测（3 档）」：\r\n\r\n  C 档：/set?hang=5（不关中断）\u3000\u3000期望：不复位，hangDone +1（证明「合法停顿不误触发」）\r\n  A 档：/set?hang=20 × " + n + "\u3000\u3000期望：每次约 15 秒后自动重启（第 1 层任务看门狗 TWDT）\r\n  B 档：/set?hang=40&noint=1 × 1\u3000期望：约 30 秒后自动重启（第 2 层硬件 RTC_WDT）\r\n\r\n【当前设备的档位前提（读自最近一次 /sys）】\r\n" + text + "\r\n⚠ 必须知道的风险：\r\n  · 这是故意让板子的主循环卡死（自测目的本身）：期间 HTTP/网页/DTrac 全部无响应；\r\n  · 期间板子不会执行任何运动指令，双轴保持当前状态（不会自己转）；\r\n  · 若两层看门狗都没生效，板子会一直卡死，只能到现场断电重启 —— 这正是要验证的点；\r\n  · 全程约 " + (num / 60 + 1) + " 分钟，请确认现场无人依赖云台、且你能到现场断电。\r\n\r\n第 1/2 次确认：确定开始吗？";
			if (MessageBox.Show(this, text2, "看门狗自测 —— 二次确认（第 1/2 次）", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2) != DialogResult.OK)
			{
				SetMsg("已取消看门狗自测（第 1 次确认未通过）");
				return;
			}
			string text3 = "第 2/2 次确认（最后一次）：\r\n\r\n请逐条确认：\r\n  ① 现场没有正在进行的重要跟踪 / 拍摄；\r\n  ② 我接受「期间云台不响应任何指令、必要时需要现场断电重启」；\r\n  ③ 我就在现场，或能立刻赶到现场。\r\n\r\n点「确定」立即开始（先 C 档 → 再 A 档 → 最后 B 档，全程自动，中途不可暂停）。";
			if (MessageBox.Show(this, text3, "看门狗自测 —— 二次确认（第 2/2 次）", MessageBoxButtons.OKCancel, MessageBoxIcon.Hand, MessageBoxDefaultButton.Button2) != DialogResult.OK)
			{
				SetMsg("已取消看门狗自测（第 2 次确认未通过）");
				return;
			}
			_wdtTestActive = true;
			_wdtTestLog = "";
			if (_btnWdtSelfTest != null)
			{
				_btnWdtSelfTest.Enabled = false;
			}
			if (_btnWdtReadback != null)
			{
				_btnWdtReadback.Enabled = false;
			}
			StopTimersForWdtTest();
			// v1.5.1：自测进度写在折叠区里，开测前先自动展开，保证用户看得见
			SetAdvOpen(open: true);
			WdtUi("已二次确认，开始执行（C → A → B），请不要关闭本程序；期间 HTTP 无响应属正常。", Theme.Warn);
			SetMsg("看门狗自测进行中（C/A/B 三档）…");
			RunBg(delegate
			{
				WdtSelfTestBg(stages);
			});
			flag = true;
		}
		catch (Exception ex)
		{
			_wdtTestActive = false;
			if (_btnWdtSelfTest != null)
			{
				_btnWdtSelfTest.Enabled = true;
			}
			if (_btnWdtReadback != null)
			{
				_btnWdtReadback.Enabled = true;
			}
			ResumeTimersForWdtTest();
			SetMsg("看门狗自测启动失败：" + ex.Message + "（已恢复界面状态，未向设备发出任何注入）");
		}
		finally
		{
			if (!flag)
			{
				Interlocked.Exchange(ref _wdtTestBusy, 0);
			}
		}
	}

	private void ResetWdtTestBusy()
	{
		Interlocked.Exchange(ref _wdtTestBusy, 0);
	}

	private void StopTimersForWdtTest()
	{
		try
		{
			if (_diagTimer != null)
			{
				_diagTimer.Stop();
			}
		}
		catch
		{
		}
		try
		{
			if (_trackTimer != null)
			{
				_trackTimer.Stop();
			}
		}
		catch
		{
		}
		try
		{
			if (_rigTimer != null)
			{
				_rigTimer.Stop();
			}
		}
		catch
		{
		}
	}

	private void ResumeTimersForWdtTest()
	{
		if (!_running)
		{
			return;
		}
		try
		{
			if (_diagTimer != null)
			{
				_diagTimer.Start();
			}
		}
		catch
		{
		}
		try
		{
			if (_trackTimer != null)
			{
				_trackTimer.Start();
			}
		}
		catch
		{
		}
		try
		{
			if (_rigTimer != null)
			{
				_rigTimer.Start();
			}
		}
		catch
		{
		}
	}

	private void WdtSelfTestBg(YzHangStage[] stages)
	{
		StringBuilder stringBuilder = new StringBuilder();
		int passCnt = 0;
		int failCnt = 0;
		try
		{
			stringBuilder.AppendLine("看门狗自测（3 档）开始：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\u3000目标 " + _s3.Host);
			stringBuilder.AppendLine("档位定义 = 固件 /set?hang=<秒>&noint=0|1（main.cpp:3291-3315）；判定只用 /sys 的真值（rstSeq / rst / wdtWhy / hangDone），不做任何折算");
			stringBuilder.AppendLine("【自测开始时的档位前提（读自最近一次 /sys 快照）】");
			stringBuilder.Append(YzWdt.ExpectAdvisory(_lastDiag));
			stringBuilder.AppendLine("等待在飞的只读请求落地（最多 4 s）：" + WdtDrainPolls(4000) + " ms");
			for (int i = 0; i < stages.Length; i++)
			{
				if (!_running)
				{
					break;
				}
				YzHangStage yzHangStage = stages[i];
				if (!YzWdtSelfTest.QueryAllowed(yzHangStage, out var why))
				{
					stringBuilder.AppendLine();
					stringBuilder.AppendLine("[跳过] " + yzHangStage.Title + " —— " + why);
					WdtUi("跳过 " + yzHangStage.Key + " 档：" + why, Theme.Warn);
					continue;
				}
				for (int j = 1; j <= yzHangStage.Repeat; j++)
				{
					if (!_running)
					{
						break;
					}
					string text2 = yzHangStage.Title + ((yzHangStage.Repeat > 1) ? ("（第 " + j + "/" + yzHangStage.Repeat + " 次）") : "");
					stringBuilder.AppendLine();
					stringBuilder.AppendLine("---- " + text2 + " ----");
					stringBuilder.AppendLine("期望：" + yzHangStage.ExpectText);
					WdtUi("进行中：" + text2 + " —— 等待设备可达（/sys）…", Theme.Accent);
					long num = WdtWaitReachable(90);
					if (!_running)
					{
						break;
					}
					stringBuilder.AppendLine("注入前等待设备可达：" + ((double)num / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s");
					YzSysSnapshot yzSysSnapshot = WdtReadSys(3);
					if (yzSysSnapshot == null)
					{
						failCnt++;
						stringBuilder.AppendLine("[失败] 注入前读不到 /sys（" + _s3.LastError + "）—— 设备不可达，本档无法执行");
						stringBuilder.AppendLine("判定：✗ 本档未执行（设备不可达）");
						WdtUi("✗ " + yzHangStage.Key + " 档：" + text2 + " —— 注入前读不到 /sys（设备不可达）", Theme.Danger);
						continue;
					}
					int rstSeq = yzSysSnapshot.RstSeq;
					long hangDone = yzSysSnapshot.HangDone;
					stringBuilder.AppendLine("注入前 /sys：rstSeq=" + rstSeq + " hangN=" + yzSysSnapshot.HangN + " hangDone=" + hangDone + " rst=" + ((yzSysSnapshot.Rst.Length == 0) ? "?" : yzSysSnapshot.Rst) + " wdtWhy=" + yzSysSnapshot.WdtWhy + " bootSt=" + yzSysSnapshot.BootSt + " wdtSub=" + yzSysSnapshot.WdtSub + " wdtDeg=" + yzSysSnapshot.WdtDeg + " rtcwOk=" + yzSysSnapshot.RtcwOk + " rtcwTk=" + yzSysSnapshot.RtcwTk + "/" + yzSysSnapshot.RtcwTkWant);
					stringBuilder.AppendLine("注入前 /sys 原文：" + yzSysSnapshot.Raw);
					Stopwatch stopwatch = Stopwatch.StartNew();
					int code;
					string body;
					string note;
					bool flag = _s3.HangInject(yzHangStage.Seconds, yzHangStage.NoInt, out code, out body, out note);
					stringBuilder.AppendLine("下发 " + yzHangStage.Query + " ⇒ HTTP " + code + "：" + ((body.Length > 0) ? body : note));
					if (!flag)
					{
						failCnt++;
						stopwatch.Stop();
						stringBuilder.AppendLine("判定：✗ 注入未被受理（" + note + "）");
						WdtUi("✗ " + yzHangStage.Key + " 档：" + text2 + " —— 注入未被受理：" + note, Theme.Danger);
						continue;
					}
					int num2 = YzWdtSelfTest.WatchWindowSec(yzHangStage);
					long num3 = (long)num2 * 1000L;
					YzSysSnapshot yzSysSnapshot2 = null;
					while (_running && stopwatch.ElapsedMilliseconds < num3)
					{
						WdtWait(1200);
						if (!_running)
						{
							break;
						}
						YzSysSnapshot yzSysSnapshot3 = WdtReadSys(1);
						if (yzSysSnapshot3 != null)
						{
							if (yzSysSnapshot3.RstSeq != rstSeq)
							{
								yzSysSnapshot2 = yzSysSnapshot3;
								break;
							}
							if (yzHangStage.ExpectResetAfterSec == 0 && stopwatch.ElapsedMilliseconds >= (long)(yzHangStage.Seconds + 3) * 1000L)
							{
								yzSysSnapshot2 = yzSysSnapshot3;
								break;
							}
						}
						WdtUi("进行中：" + text2 + " —— 已 " + stopwatch.ElapsedMilliseconds / 1000 + " s（上限 " + num2 + " s）；期望：" + yzHangStage.ExpectText, Theme.Accent);
					}
					if (yzSysSnapshot2 == null)
					{
						yzSysSnapshot2 = WdtReadSys(2);
					}
					stopwatch.Stop();
					long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
					bool pass;
					string text3;
					if (yzSysSnapshot2 == null)
					{
						pass = false;
						text3 = "✗ 无法确认：观察窗口 " + num2 + " s 内一直读不到 /sys —— 板子可能压根没回来（永久卡死 ⇒ 需现场断电），也可能只是起网慢；请立刻人工确认";
					}
					else
					{
						text3 = YzWdtSelfTest.Judge(yzHangStage, rstSeq, yzSysSnapshot2.RstSeq, elapsedMilliseconds, yzSysSnapshot2.Rst, yzSysSnapshot2.WdtWhy, out pass);
						stringBuilder.AppendLine("注入后 /sys：rstSeq=" + yzSysSnapshot2.RstSeq + "（Δ" + (yzSysSnapshot2.RstSeq - rstSeq) + "） rst=" + ((yzSysSnapshot2.Rst.Length == 0) ? "?" : yzSysSnapshot2.Rst) + " wdtWhy=" + yzSysSnapshot2.WdtWhy + " hangN=" + yzSysSnapshot2.HangN + " hangDone=" + yzSysSnapshot2.HangDone + " bootSt=" + yzSysSnapshot2.BootSt + " wdtSub=" + yzSysSnapshot2.WdtSub + " rtcwOk=" + yzSysSnapshot2.RtcwOk + " rtcwTk=" + yzSysSnapshot2.RtcwTk + "/" + yzSysSnapshot2.RtcwTkWant);
						stringBuilder.AppendLine("注入后 /sys 原文：" + yzSysSnapshot2.Raw);
						if (yzHangStage.ExpectResetAfterSec == 0)
						{
							long num4 = yzSysSnapshot2.HangDone - hangDone;
							text3 += ((num4 >= 1) ? ("；hangDone " + hangDone + " → " + yzSysSnapshot2.HangDone + "（+1 ✓ 与固件语义一致）") : ("；⚠ hangDone 没有 +1（" + hangDone + " → " + yzSysSnapshot2.HangDone + "）—— 与固件「注入跑完 ⇒ hangDone+1」不符，需复核"));
						}
					}
					if (pass)
					{
						passCnt++;
					}
					else
					{
						failCnt++;
					}
					stringBuilder.AppendLine("耗时：" + ((double)elapsedMilliseconds / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s");
					stringBuilder.AppendLine("判定：" + text3);
					WdtUi((pass ? "✔ " : "✗ ") + yzHangStage.Key + " 档" + ((yzHangStage.Repeat > 1) ? (" 第 " + j + " 次") : "") + "：" + text3, pass ? Theme.Ok : Theme.Danger);
				}
			}
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("===== 自测结束 " + DateTime.Now.ToString("HH:mm:ss") + "：符合预期 " + passCnt + " 档 / 不符合 " + failCnt + " 档 =====");
		}
		catch (Exception ex)
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("[异常] 自测中断：" + ex.GetType().Name + "：" + ex.Message);
			failCnt++;
		}
		finally
		{
			string text = stringBuilder.ToString();
			_wdtTestActive = false;
			UiInvoke(delegate
			{
				_wdtTestLog = text;
				if (_txtDiag != null)
				{
					string text4 = ((_lastDiagText.Length > 0) ? (_lastDiagText + "\r\n") : "");
					_txtDiag.Text = text4 + text;
				}
				if (_btnWdtSelfTest != null)
				{
					_btnWdtSelfTest.Enabled = true;
				}
				if (_btnWdtReadback != null)
				{
					_btnWdtReadback.Enabled = true;
				}
				ResumeTimersForWdtTest();
				SetMsg((failCnt == 0) ? ("✔ 看门狗自测全部符合预期（" + passCnt + " 档）；全过程已贴进诊断文本，可点「复制诊断文本」") : ("⚠ 看门狗自测有 " + failCnt + " 档不符合预期 —— 详见系统页诊断文本（点「复制诊断文本」）"));
				if (_lblWdtTest != null && failCnt > 0)
				{
					_lblWdtTest.ForeColor = Theme.Danger;
				}
			});
			Interlocked.Exchange(ref _wdtTestBusy, 0);
			DiagReadBg();
		}
	}

	private YzSysSnapshot WdtReadSys(int tries)
	{
		int httpTimeoutMs = _s3.HttpTimeoutMs;
		try
		{
			_s3.HttpTimeoutMs = 1500;
			int num = ((tries < 1) ? 1 : tries);
			for (int i = 0; i < num; i++)
			{
				YzSysSnapshot sysSnapshot = _s3.GetSysSnapshot();
				if (sysSnapshot != null)
				{
					return sysSnapshot;
				}
				if (i + 1 < num)
				{
					WdtWait(400);
				}
			}
			return null;
		}
		finally
		{
			_s3.HttpTimeoutMs = httpTimeoutMs;
		}
	}

	private long WdtDrainPolls(int maxMs)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		int num = 0;
		while (stopwatch.ElapsedMilliseconds < maxMs && _running)
		{
			Thread.Sleep(120);
			num = ((Interlocked.CompareExchange(ref _pollInFlight, 0, 0) == 0 && Interlocked.CompareExchange(ref _diagBusy, 0, 0) == 0) ? (num + 1) : 0);
			if (num >= 2)
			{
				break;
			}
		}
		stopwatch.Stop();
		return stopwatch.ElapsedMilliseconds;
	}

	private long WdtWaitReachable(int maxSec)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		while (_running && stopwatch.ElapsedMilliseconds < (long)maxSec * 1000L && WdtReadSys(1) == null)
		{
			WdtWait(700);
		}
		stopwatch.Stop();
		return stopwatch.ElapsedMilliseconds;
	}

	private void WdtWait(int ms)
	{
		int num = ms;
		while (num > 0 && _running)
		{
			int num2 = ((num > 200) ? 200 : num);
			Thread.Sleep(num2);
			num -= num2;
		}
	}

	private void WdtUi(string text, Color c)
	{
		if (!base.IsHandleCreated || base.IsDisposed)
		{
			return;
		}
		try
		{
			BeginInvoke((MethodInvoker)delegate
			{
				if (_lblWdtTest != null)
				{
					_lblWdtTest.Text = "自测：" + text;
					_lblWdtTest.ForeColor = c;
				}
			});
		}
		catch
		{
		}
	}

	private string BuildDiagText(YzSysSnapshot s)
	{
		if (s == null)
		{
			return "（无 /sys 数据）";
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("=== 固件诊断（读自设备 /sys，非硬编码）===");
		stringBuilder.AppendLine($"固件 {s.Ver} · build {s.Bld} · 已运行 {s.UpSec} 秒 · 堆 {s.Heap} B（最低 {s.HeapMin} B）· loop峰值 {s.LoopMaxMs} ms");
		stringBuilder.AppendLine($"崩溃面包屑 crumb={s.Crumb}（{s.CrumbName}）· 复位序列 rstSeq={s.RstSeq} · 本次是否崩溃 panic={s.LastWasPanic}");
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("【方位】" + $"azblk={s.AzBlk}（{s.AzBlkName}）azblkn={s.AzBlkCnt}\u3000手动全停保持 msh={s.Msh} ms");
		stringBuilder.AppendLine("【俯仰】（vz/vrev 源 /st，verr 源 /sys）" + string.Format("verr={0}（应恒 0）vz={1} vrev={2} ptrace={3}", s.VErr, (_lastStatus != null) ? _lastStatus.PitchZeroCnt : 0, (_lastStatus != null) ? _lastStatus.VRev.ToString(CultureInfo.InvariantCulture) : "?", s.PTrace));
		stringBuilder.AppendLine("【复位恢复待命】" + string.Format("rec={0} recrst={1}（{2}）recn={3} recName={4}", s.Rec, s.RecRst, YzFw.RecRstName(s.RecRst), s.RecN, (s.RecName.Length == 0) ? "-" : s.RecName));
		stringBuilder.AppendLine("【看门狗】" + $"wdtFeed={s.WdtFeed} wdtAdd={s.WdtAdd}（0=订阅成功）");
		stringBuilder.AppendLine("【BOD 欠压】" + $"bod={s.Bod} bodHw={s.BodHw} bodPre={s.BodPre} bodWr={s.BodWr} bodClamp={s.BodClamp} bodErr={s.BodErr}（{YzFw.BodErrName(s.BodErr)}）bodDet={s.BodDet}（1=当前电压已低于门槛）");
		stringBuilder.AppendLine("【协议/跟踪】（来源 /st）" + $"ecok={((_lastStatus != null) ? _lastStatus.EcOk : s.EcOk)} ecbad={((_lastStatus != null) ? _lastStatus.EcBad : s.EcBad)} ecrange={((_lastStatus != null) ? _lastStatus.EcRange : s.EcRange)} ecprio={((_lastStatus != null) ? _lastStatus.EcPrio : s.EcPrio)}");
		string text = ((_lastStatus != null) ? _lastStatus.EcLast : s.EcLast);
		stringBuilder.AppendLine("【板子收到的最后一条原文】eclast = " + ((text.Length == 0 || text == "-") ? "(还没收到任何行)" : text));
		stringBuilder.AppendLine("【协议 EL 目标】（来源 /st）" + string.Format("elacc={0} elrej={1} elrc={2}（{3}）ellast={4}", (_lastStatus != null) ? _lastStatus.ElAcc : s.ElAcc, (_lastStatus != null) ? _lastStatus.ElRej : s.ElRej, (_lastStatus != null) ? _lastStatus.ElRc : s.ElRc, YzFw.ElRejName((_lastStatus != null) ? _lastStatus.ElRc : s.ElRc), (((_lastStatus != null) ? _lastStatus.ElLastX10 : s.ElLastX10) <= -32768) ? "从未" : (((double)((_lastStatus != null) ? _lastStatus.ElLastX10 : s.ElLastX10) / 10.0).ToString("0.0", CultureInfo.InvariantCulture) + "°")));
		stringBuilder.AppendLine("【NVS】" + string.Format("nvsWrOk={0} nvsWrErr={1} nvsWrN={2} nvsFlushN={3} nvsFlushW={4} nvsErrLog=\"{5}\"", s.NvsWrOk, s.NvsWrErr, s.NvsWrN, s.NvsFlushN, s.NvsFlushW, (s.NvsErrLog.Length == 0) ? "-" : s.NvsErrLog));
		stringBuilder.AppendLine("【Web 门禁】" + string.Format("httpRl={0} tlsRl={1} httpRlS={2} tlsRlS={3} httpLh={4} tlsLh={5} httpAct={6} tlsAct={7} rlWin={8} rlBase={9} rlEx={10} rlip80={11} rlip443={12}", s.HttpRl, s.TlsRl, s.HttpRlS, s.TlsRlS, s.HttpLh, s.TlsLh, s.HttpAct, s.TlsAct, s.RlWin, s.Get("rlBase", "?"), s.Get("rlEx", "?"), (s.Rlip80.Length == 0) ? "-" : s.Rlip80, s.Get("rlip443", "-")));
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("=== 看门狗 / 启动阶段（固件 c37/c38 新字段；来源 /sys）===");
		stringBuilder.Append(YzWdt.Build(s).Block());
		stringBuilder.AppendLine("【网络通路】" + NetPathDiagText(s));
		if (_wdtTestLog.Length > 0)
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("=== 看门狗自测（3 档）全过程（含每一步的 /sys 回读）===");
			stringBuilder.Append(_wdtTestLog);
		}
		if (_lastRst != null)
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("=== 复位历史（/rst?n=16 表格化）===");
			stringBuilder.AppendLine(_lastRst.TableText());
		}
		else
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("（复位历史：点「复位诊断 /rst?n=8」或云台页「复位历史表」读取）");
		}
		return stringBuilder.ToString();
	}

	private void RefreshResetTable()
	{
		SetMsg("正在读取复位历史 /rst?n=16 …");
		RunBg(delegate
		{
			YzRstHistory h = _s3.GetRstHistory(16);
			string err = _s3.LastError;
			UiInvoke(delegate
			{
				ApplyResetTable(h, err);
			});
		});
	}

	private void ApplyResetTable(YzRstHistory h, string err)
	{
		_lastRst = h;
		if (h == null)
		{
			if (_lblRstInfo != null)
			{
				_lblRstInfo.Text = "复位历史：读取失败（" + err + "）";
				_lblRstInfo.ForeColor = Theme.Danger;
			}
			return;
		}
		if (_lblRstInfo != null)
		{
			_lblRstInfo.Text = $"复位历史（累计 {h.Total} 次）：{h.Summary}\u3000·\u3000{h.LastLine}";
			_lblRstInfo.ForeColor = ((h.Panic > 0) ? Theme.Danger : ((h.Brownout > 0 || h.TaskWdt > 0 || h.IntWdt > 0 || h.Wdt > 0) ? Theme.Warn : Theme.Ok));
		}
		SetMsg($"复位历史：累计 {h.Total} 次 · {h.Summary} · {h.LastLine}");
		UpdateResetTop3(h);
		UpdateSysBrief();
	}

	private void OtaOneKey()
	{
		string ssid = ((_txtOtaSsid == null) ? "" : _txtOtaSsid.Text.Trim());
		string pass = ((_txtOtaPass == null) ? "" : _txtOtaPass.Text);
		SetMsg("一键 OTA：正在读 /otas 判断网段闸门…");
		if (_btnOtaOneKey != null)
		{
			_btnOtaOneKey.Enabled = false;
		}
		RunBg(delegate
		{
			try
			{
				YzOtasInfo o = _s3.GetOtas();
				string err = _s3.LastError;
				string pcIp = S3Client.LocalIpFor(_s3.Host);
				UiInvoke(delegate
				{
					ApplyOtas(o, err, pcIp);
				});
				YzOtaGate.Decision decision = YzOtaGate.Decide(pcIp, o);
				if (!decision.Allowed && decision.TokenNeeded)
				{
					SetMsgSafe("一键 OTA 中止：" + decision.Why + " —— " + decision.Hint);
				}
				else
				{
					if (!decision.Allowed && decision.NeedWifiFix)
					{
						if (ssid.Length == 0)
						{
							SetMsgSafe("一键 OTA：需要先配网让板子的无线 STA 与上位机同 /24，请在「OTA 配网凭据」里填 SSID/密码后重试。（判定：本机 " + pcIp + " 与 STA " + ((o == null) ? "?" : o.Ip) + " / 有线 " + ((o == null) ? "?" : o.EthIp) + " 都不同网段）");
							return;
						}
						SetMsgSafe("一键 OTA：网段闸门会拦住上传 → 先 POST /wifi 配网（SSID=" + ssid + "），最多等 25 秒…");
						string note;
						bool num = _s3.WifiConnect(ssid, pass, out note);
						SetMsgSafe("一键 OTA 配网：" + note);
						if (!num && note.IndexOf("配网成功", StringComparison.Ordinal) < 0)
						{
							SetMsgSafe("一键 OTA 中止：配网未成功（" + note + "）。可改用「打开网页版升级页」或 USB 整包烧录。");
							return;
						}
						bool flag = false;
						for (int i = 0; i < 15; i++)
						{
							Thread.Sleep(2000);
							YzOtasInfo o2 = _s3.GetOtas();
							YzOtaGate.Decision decision2 = YzOtaGate.Decide(pcIp, o2);
							UiInvoke(delegate
							{
								ApplyOtas(o2, "", pcIp);
							});
							if (decision2.Allowed)
							{
								flag = true;
								SetMsgSafe("一键 OTA：网段闸门已放行（STA " + ((o2 == null) ? "?" : o2.Ip) + " 与 " + pcIp + " 同网段）");
								break;
							}
						}
						if (!flag)
						{
							SetMsgSafe("一键 OTA 中止：配网后仍未拿到同网段地址（可再点一次「① 读取 /otas」看实际值）。");
							return;
						}
					}
					else
					{
						if (!decision.Allowed)
						{
							SetMsgSafe("一键 OTA 中止：" + decision.Why + " —— " + decision.Hint);
							return;
						}
						SetMsgSafe("一键 OTA：门禁已放行（" + decision.Why + "）");
					}
					string path = null;
					InvokeUi(delegate
					{
						using OpenFileDialog openFileDialog = new OpenFileDialog();
						openFileDialog.Title = "选择要上传的固件（.bin）——一键 OTA 已确认网段闸门放行";
						openFileDialog.Filter = "固件文件 (*.bin)|*.bin|所有文件 (*.*)|*.*";
						openFileDialog.InitialDirectory = Environment.CurrentDirectory;
						if (openFileDialog.ShowDialog(this) == DialogResult.OK)
						{
							path = openFileDialog.FileName;
						}
					});
					if (string.IsNullOrEmpty(path))
					{
						SetMsgSafe("一键 OTA：已取消（没有选文件）——门禁检查已通过，可随时再点。");
					}
					else
					{
						OtaUpload(path);
					}
				}
			}
			catch (Exception ex)
			{
				SetMsgSafe("一键 OTA 异常：" + ex.Message);
			}
			finally
			{
				UiInvoke(delegate
				{
					if (_btnOtaOneKey != null)
					{
						_btnOtaOneKey.Enabled = true;
					}
				});
			}
		});
	}

	private void ApplyOtas(YzOtasInfo o, string err, string pcIp)
	{
		if (_lblOtas != null)
		{
			if (o == null)
			{
				_lblOtas.Text = "OTA 门禁：读取失败（" + err + "）";
				_lblOtas.ForeColor = Theme.Danger;
				return;
			}
			YzOtaGate.Decision decision = YzOtaGate.Decide(pcIp, o);
			_lblOtas.Text = "OTA 门禁：" + o.Summary + "\u3000本机 " + (pcIp ?? "?") + " ⇒ " + decision.Why;
			_lblOtas.ForeColor = (decision.Allowed ? Theme.Ok : (decision.TokenNeeded ? Theme.Danger : Theme.Warn));
		}
	}

	private void CopyDiagText()
	{
		try
		{
			string text = _lastDiagText;
			if (string.IsNullOrEmpty(text) && _txtDiag != null)
			{
				text = _txtDiag.Text;
			}
			if (string.IsNullOrEmpty(text))
			{
				SetMsg("还没有诊断文本：请先点「刷新诊断（新字段）」");
				return;
			}
			if (_wdtTestLog.Length > 0 && text.IndexOf(_wdtTestLog, StringComparison.Ordinal) < 0)
			{
				text = text + "\r\n=== 看门狗自测（3 档）全过程（含每一步的 /sys 回读）===\r\n" + _wdtTestLog;
			}
			Clipboard.SetText(text);
			SetMsg("✔ 诊断文本已复制到剪贴板（" + text.Length + " 字符）");
		}
		catch (Exception ex)
		{
			SetMsg("复制失败：" + ex.Message);
		}
	}

	private void ReadOtasNow()
	{
		string pcIp = null;
		RunBg(delegate
		{
			YzOtasInfo o = _s3.GetOtas();
			string err = _s3.LastError;
			if (pcIp == null)
			{
				pcIp = S3Client.LocalIpFor(_s3.Host);
			}
			string ip = pcIp;
			UiInvoke(delegate
			{
				ApplyOtas(o, err, ip);
			});
		});
	}

	private void McastStart()
	{
		int num = 5004;
		if (!int.TryParse(_txtMcPort.Text.Trim(), out var result) || result <= 0 || result > 65535)
		{
			SetMsg("组播端口不合法（1~65535）");
			return;
		}
		num = result;
		string text = _txtMcGroup.Text.Trim();
		string iface = _txtMcIface.Text.Trim();
		try
		{
			if (_mcast == null)
			{
				_mcast = new McastAudio();
				_mcast.Stats += delegate(McastAudio a)
				{
					string txt = a.Text;
					long num2 = (_mcastPkts = ParsePkts(txt));
					DotState st = ((num2 > 0) ? DotState.Ok : DotState.Busy);
					string cap = ((num2 > 0) ? ("收包 " + num2) : "等待收包");
					UiInvoke(delegate
					{
						try
						{
							if (_lblMcState != null)
							{
								_lblMcState.Text = "组播音频：" + txt;
							}
							SetDotTip(_dotMcast, st, cap, "组播音频：" + txt);
						}
						catch
						{
						}
					});
				};
				_mcast.Error += delegate(string m)
				{
					UiInvoke(delegate
					{
						SetDot(_dotMcast, DotState.Fail, "接收失败");
					});
					SetMsgSafe("组播音频：" + m);
				};
			}
			_mcast.VolumePercent = 100;
			_mcast.WaveOutDeviceId = McDeviceIndex();
			if (_mcast.Start(text, num, iface))
			{
				_mcastPkts = 0L;
				SetDotTip(_dotMcast, DotState.Busy, "已接收", "组播音频：已开始接收，尚未收到包");
				RefreshMcastButton(running: true);
				SetMsg("组播音频已开始：" + text + ":" + num + " @" + _mcast.Iface + "（输出设备：" + ((_cboMcDev != null && _cboMcDev.SelectedIndex >= 0) ? _cboMcDev.Text : "系统默认") + "）");
			}
			else
			{
				SetDot(_dotMcast, DotState.Fail, "未接收");
				RefreshMcastButton(running: false);
				SetMsg("组播音频启动失败（见上方提示）");
			}
		}
		catch (Exception ex)
		{
			SetDot(_dotMcast, DotState.Fail, "启动异常");
			SetMsg("组播音频启动异常：" + ex.Message);
		}
	}

	private static long ParsePkts(string txt)
	{
		if (string.IsNullOrEmpty(txt))
		{
			return 0L;
		}
		int num = txt.IndexOf("pkts", StringComparison.OrdinalIgnoreCase);
		if (num < 0)
		{
			return 0L;
		}
		int num2 = txt.IndexOf('=', num);
		if (num2 < 0)
		{
			return 0L;
		}
		int i = num2 + 1;
		long num3 = 0L;
		for (; i < txt.Length && txt[i] >= '0' && txt[i] <= '9'; i++)
		{
			num3 = num3 * 10 + (txt[i] - 48);
		}
		return num3;
	}

	private void McastStop()
	{
		try
		{
			if (_mcast != null)
			{
				_mcast.Stop();
				// v1.5.1：停止后连对象一起释放（WaveOut 设备句柄/缓冲线程不常驻），
				// 下次点「开始接收」会重新 new（McastStart 里已有惰性创建）
				_mcast.Dispose();
				_mcast = null;
			}
		}
		catch
		{
		}
		_mcastPkts = 0L;
		SetDot(_dotMcast, DotState.Fail, "未接收");
		RefreshMcastButton(running: false);
		if (_lblMcState != null)
		{
			_lblMcState.Text = "组播音频：已停止";
		}
		SetMsg("组播音频已停止");
	}

	private void FillMcastDefaultsBg()
	{
		RunBg(delegate
		{
			string text = "";
			string[] array = null;
			try
			{
				text = GuessLocalIp();
			}
			catch
			{
			}
			try
			{
				array = McastAudio.ListDevices();
			}
			catch
			{
			}
			string ip2 = text;
			string[] devs2 = array;
			UiInvoke(delegate
			{
				if (_txtMcIface != null && _txtMcIface.Text.Length == 0 && !string.IsNullOrEmpty(ip2))
				{
					_txtMcIface.Text = ip2;
				}
				if (_cboMcDev != null && devs2 != null && devs2.Length != 0)
				{
					_cboMcDev.Items.Clear();
					ComboBox.ObjectCollection items = _cboMcDev.Items;
					object[] items2 = devs2;
					items.AddRange(items2);
					_cboMcDev.SelectedIndex = 0;
				}
			});
		});
	}

	private int McDeviceIndex()
	{
		try
		{
			return (_cboMcDev != null && _cboMcDev.SelectedIndex > 0) ? (_cboMcDev.SelectedIndex - 1) : (-1);
		}
		catch
		{
			return -1;
		}
	}

	private void McastToneTest()
	{
		int dev = McDeviceIndex();
		RunBg(delegate
		{
			string msgSafe;
			try
			{
				using WaveOutPlayer waveOutPlayer = new WaveOutPlayer(48000, 1, 16, 60, dev);
				long num = waveOutPlayer.PlayedBytes();
				byte[] array = new byte[1920];
				int num2 = 0;
				DateTime now = DateTime.Now;
				while ((DateTime.Now - now).TotalSeconds < 1.5)
				{
					for (int i = 0; i < array.Length; i += 2)
					{
						short num3 = (short)(9000.0 * Math.Sin(Math.PI * 2000.0 * (double)num2 / 48000.0));
						num2++;
						array[i] = (byte)((uint)num3 & 0xFFu);
						array[i + 1] = (byte)((uint)(num3 >> 8) & 0xFFu);
					}
					waveOutPlayer.Submit(array);
				}
				Thread.Sleep(350);
				long num4 = waveOutPlayer.PlayedBytes() - num;
				msgSafe = "声卡自测（设备 " + ((dev < 0) ? "系统默认" : dev.ToString()) + "）：消费 " + num4 + " 字节（约 " + ((double)num4 / 96.0).ToString("0") + " ms 音频），写入失败 " + waveOutPlayer.FailCount + ((num4 > 10000) ? "  → 该设备正常出声 ✓（听不到就查系统音量/静音/默认设备）" : "  → 该设备**没有在渲染** ✗ 请换一个输出设备再试");
			}
			catch (Exception ex)
			{
				msgSafe = "声卡自测失败：" + ex.Message;
			}
			SetMsgSafe(msgSafe);
		});
	}

	private void AzZero()
	{
		if (MessageBox.Show("把云台【当前实际朝向】定义为正北 0°？\r\n\r\n· 请先确认天线现在**真正指向正北**（用指南针/已知方位核对）\r\n· 校零后 0°=正北、90°=正东、180°=正南、270°=正西，360° 与 0° 为同一位置\r\n· 立即写入设备 NVS，掉电不丢失", "正北校零", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
		{
			return;
		}
		RunBg(delegate
		{
			string text = _s3.Get("/az?zero=1");
			if (text != null && text.Trim().StartsWith("OK"))
			{
				SetMsgSafe("正北校零完成：" + text.Trim() + "（当前朝向=0°=正北，已存 NVS）");
				try
				{
					RefreshSys();
					return;
				}
				catch
				{
					return;
				}
			}
			SetMsgSafe("正北校零失败：" + (text ?? _s3.LastError));
		});
	}

	private void AzRead()
	{
		RunBg(delegate
		{
			string text = _s3.Get("/az");
			SetMsgSafe((text == null) ? ("读取方位失败：" + _s3.LastError) : ("方位 " + text.Trim()));
		});
	}

	private void AzSet(double deg)
	{
		if (deg < 0.0 || deg > 360.0)
		{
			SetMsg("方位角必须在 0~360 之间");
			return;
		}
		RunBg(delegate
		{
			string text = _s3.Get("/az?set=" + deg.ToString("0.##", CultureInfo.InvariantCulture));
			SetMsgSafe((text == null) ? ("写入方位失败：" + _s3.LastError) : ("已写入方位 " + deg.ToString("0.##") + "°（" + text.Trim() + "）"));
		});
	}

	private void TakePriorityIfNeeded()
	{
		if (!_cfg.AutoTakePriority)
		{
			return;
		}
		RunBg(delegate
		{
			YzTrackSnapshot trackCli = _s3.GetTrackCli();
			if (trackCli == null)
			{
				SetMsgSafe("已跳过「启动自动取得控制权」：/trackcli 读不到，无法确认 4533 上有没有客户端在用——按安全优先**放弃**自动取权（需要接管请手动点「取得云台控制权」）。");
			}
			else
			{
				int onlineCount = trackCli.OnlineCount;
				if (onlineCount > 0)
				{
					SetMsgSafe($"已跳过「启动自动取得控制权」：检测到 {onlineCount} 个 4533 客户端在线（DTrac 正在使用）——保持其控制权不受影响；本机仅做只读观测。需要接管请手动点「取得云台控制权」。");
				}
				else
				{
					TakePriority(take: true);
				}
			}
		});
	}

	private void TakePriority(bool take)
	{
		RunBg(delegate
		{
			if (take)
			{
				string text = S3Client.LocalIpFor(_s3.Host);
				if (string.IsNullOrEmpty(text))
				{
					SetMsg("无法确定本机 IP，未取得控制权");
				}
				else if (_s3.SetPriority(text))
				{
					SetMsg("已取得云台控制权（/prio?ip=" + text + "）——注意：DTrac 的运动指令此后会被固件拒绝，现场如需 DTrac 控制请点「释放控制权」。");
				}
				else
				{
					SetMsg("取得控制权失败：" + _s3.LastError);
				}
			}
			else if (_s3.ReleasePriority())
			{
				SetMsg("已释放云台控制权（/prio?off=1）：控制权回到「首个接入者」（DTrac），连接全部保持 ✓");
			}
			else
			{
				SetMsg("释放控制权失败：" + _s3.LastError);
			}
		});
	}

	private void CopyLocalIp()
	{
		RunBg(delegate
		{
			string ip = S3Client.LocalIpFor(_s3.Host);
			if (!string.IsNullOrEmpty(ip))
			{
				UiInvoke(delegate
				{
					try
					{
						Clipboard.SetText(ip);
					}
					catch
					{
					}
					SetMsg("已复制本机 IP：" + ip);
				});
			}
		});
	}

	private void AddWindowAndPlay(int pane)
	{
		TextBox textBox = ((pane == 0) ? _txtAddr1 : _txtAddr2);
		TextBox textBox2 = ((pane == 0) ? _txtUser1 : _txtUser2);
		TextBox textBox3 = ((pane == 0) ? _txtPass1 : _txtPass2);
		string addr = ((textBox == null) ? "" : textBox.Text.Trim());
		string user = ((textBox2 == null) ? "" : textBox2.Text.Trim());
		string pass = ((textBox3 == null) ? "" : textBox3.Text);
		if (addr.Length == 0)
		{
			SetMsg("请先填写窗口" + (pane + 1) + " 的地址（rtsp://… 完整地址，或只填 IP）");
			SetWinState(pane, "未填地址");
			_paneDot[pane] = DotState.Fail;
			SetDot((pane == 0) ? _dotWin1 : _dotWin2, DotState.Fail, "无地址");
			return;
		}
		SaveWindowCfg(pane, addr, user, pass);
		StopPane(pane);
		int seq = ++_paneStartSeq[pane];
		SetWinState(pane, "连接中…");
		_paneDot[pane] = DotState.Busy;
		SetDot((pane == 0) ? _dotWin1 : _dotWin2, DotState.Busy, "连接中");
		Label label = ((pane == 0) ? _lblPane1 : _lblPane2);
		if (label != null)
		{
			label.Text = "窗口" + (pane + 1) + "：" + addr + "  连接中…";
		}
		SetMsg("窗口" + (pane + 1) + " 正在后台连接（界面不卡）…");
		RunBg(delegate
		{
			StartPaneAutoBg(pane, addr, user, pass, seq);
		});
	}

	private void StopWindow(int pane)
	{
		_paneStartSeq[pane]++;
		StopPane(pane);
		SetWinState(pane, (WindowAddrText(pane).Length == 0) ? "未配置" : "已停止");
		SetMsg("窗口" + (pane + 1) + " 已停止");
	}

	private void StartPaneAutoBg(int pane, string addr, string user, string pass, int seq)
	{
		if (RtspUrlBuilder.IsUrl(addr))
		{
			SetWinStateSafe(pane, "播放中…");
			SetMsgSafe("窗口" + (pane + 1) + "：按完整地址播放…");
			if (TryPlayBg(pane, addr, 15000, seq))
			{
				DonePane(pane, seq, "完整地址", addr, null);
			}
			else
			{
				FailPane(pane, seq, "完整地址播放失败（核对地址 / 账号密码 / 网络）");
			}
			return;
		}
		ParseHostPort(addr, out var host, out var port);
		if (host.Length == 0)
		{
			FailPane(pane, seq, "地址格式不正确：" + addr);
			return;
		}
		List<string> list = RtspUrlBuilder.Candidates("auto", host, port, user, pass, 1, main: true);
		if (list == null)
		{
			list = new List<string>();
		}
		while (list.Count > 5)
		{
			list.RemoveAt(list.Count - 1);
		}
		string text = RtspUrlBuilder.BuildRtsp(host, port, user, pass, "/");
		if (!string.IsNullOrEmpty(text) && !list.Contains(text))
		{
			list.Add(text);
		}
		if (list.Count == 0)
		{
			FailPane(pane, seq, "无法生成取流地址（核对 IP）");
			return;
		}
		bool[] array = new bool[list.Count];
		bool flag = false;
		bool flag2 = false;
		for (int i = 0; i < list.Count; i++)
		{
			if (seq != _paneStartSeq[pane])
			{
				return;
			}
			string text2 = PaneModeName(i);
			SetWinStateSafe(pane, "检测 " + text2 + " 模式…");
			RtspProbeResult rtspProbeResult = RtspProbe.Probe(host, port, RtspPathOf(list[i]), user, pass, 1800);
			array[i] = rtspProbeResult.Usable;
			flag |= rtspProbeResult.Usable;
			flag2 |= rtspProbeResult.Reachable;
		}
		if (!flag2)
		{
			FailPane(pane, seq, "主机/" + port + " 端口不可达（核对 IP 与网络）");
			return;
		}
		bool flag3 = flag;
		for (int j = 0; j < 2 && !(j == 1 && flag3); j++)
		{
			for (int k = 0; k < list.Count; k++)
			{
				if (seq != _paneStartSeq[pane])
				{
					return;
				}
				if (!flag3 || j == 0 == array[k])
				{
					string text3 = PaneModeName(k);
					SetWinStateSafe(pane, "试播 " + text3 + " 模式…");
					SetMsgSafe("窗口" + (pane + 1) + "：正在用 " + text3 + " 模式试播…");
					if (TryPlayBg(pane, list[k], (j == 0) ? 9000 : 5000, seq))
					{
						DonePane(pane, seq, text3, list[k], PaneModeVendor(k));
						return;
					}
				}
			}
		}
		FailPane(pane, seq, "四种模式都没能出画面（核对账号密码 / 编码格式 / 网络）");
	}

	private bool TryPlayBg(int pane, string url, int waitMs, int seq)
	{
		if (seq != _paneStartSeq[pane])
		{
			return false;
		}
		Panel host = ((pane == 0) ? _pane1 : _pane2);
		if (host == null)
		{
			return false;
		}
		bool started = false;
		InvokeUi(delegate
		{
			try
			{
				IVideoEngine engine = GetEngine(pane, url);
				started = engine.Start(url, host.Handle);
			}
			catch
			{
				started = false;
			}
		});
		if (!started)
		{
			return false;
		}
		IVideoEngine videoEngine = ((pane == 0) ? _s1 : _s2);
		if (videoEngine == null)
		{
			return false;
		}
		DateTime dateTime = DateTime.Now.AddMilliseconds(waitMs);
		while (DateTime.Now < dateTime)
		{
			if (seq != _paneStartSeq[pane])
			{
				return false;
			}
			if (videoEngine.IsPlaying)
			{
				return true;
			}
			Thread.Sleep(150);
		}
		return false;
	}

	private void DonePane(int pane, int seq, string mode, string url, string vendor)
	{
		UiInvoke(delegate
		{
			if (seq == _paneStartSeq[pane])
			{
				Label label = ((pane == 0) ? _lblPane1 : _lblPane2);
				if (label != null)
				{
					label.Text = "窗口" + (pane + 1) + "：" + mode + " 模式  " + url;
				}
				SetWinState(pane, "已用 " + mode + " 模式连接");
				SaveWindowVendor(pane, vendor);
				RefreshPaneDot(pane);
				SetMsg("窗口" + (pane + 1) + " 已用 " + mode + " 模式出图：" + url);
			}
		});
	}

	private void FailPane(int pane, int seq, string why)
	{
		UiInvoke(delegate
		{
			if (seq == _paneStartSeq[pane])
			{
				SetWinState(pane, why);
				_paneDot[pane] = DotState.Fail;
				SetDotTip((pane == 0) ? _dotWin1 : _dotWin2, DotState.Fail, "未播放", "窗口" + (pane + 1) + "：未在播放（已停止或连接失败）");
				SetMsg("窗口" + (pane + 1) + "：" + why + (VlcEngine.Available ? "" : "  ⚠ 未找到 libvlc.dll：通用 RTSP 无法播放，请把 VLC 运行库放到程序目录。"));
			}
		});
	}

	private static string PaneModeName(int i)
	{
		return i switch
		{
			2 => "大华", 
			1 => "宇视", 
			0 => "海康", 
			_ => "通用", 
		};
	}

	private static string PaneModeVendor(int i)
	{
		return i switch
		{
			2 => "dahua", 
			1 => "uniview", 
			0 => "hik", 
			_ => "generic", 
		};
	}

	private void SaveWindowVendor(int pane, string vendor)
	{
		if (!string.IsNullOrEmpty(vendor) && _cfg.Cameras.Count > pane && !(_cfg.Cameras[pane].Vendor == vendor))
		{
			_cfg.Cameras[pane].Vendor = vendor;
			SaveConfig();
		}
	}

	private static void ParseHostPort(string addr, out string host, out int port)
	{
		host = (addr ?? "").Trim();
		port = 554;
		int num = host.IndexOf("://", StringComparison.Ordinal);
		if (num > 0)
		{
			host = host.Substring(num + 3);
		}
		int num2 = host.IndexOf('@');
		if (num2 >= 0)
		{
			host = host.Substring(num2 + 1);
		}
		int num3 = host.IndexOf('/');
		if (num3 >= 0)
		{
			host = host.Substring(0, num3);
		}
		int num4 = host.LastIndexOf(':');
		if (num4 > 0 && int.TryParse(host.Substring(num4 + 1), out var result) && result > 0 && result < 65536)
		{
			port = result;
			host = host.Substring(0, num4);
		}
		host = host.Trim();
	}

	private static string RtspPathOf(string url)
	{
		try
		{
			string text = url.Substring(url.IndexOf("://", StringComparison.Ordinal) + 3);
			int num = text.IndexOf('@');
			if (num >= 0)
			{
				text = text.Substring(num + 1);
			}
			int num2 = text.IndexOf('/');
			return (num2 >= 0) ? text.Substring(num2) : "/";
		}
		catch
		{
			return "/";
		}
	}

	private void InvokeUi(Action a)
	{
		try
		{
			if (base.IsHandleCreated && !base.IsDisposed && base.InvokeRequired)
			{
				Invoke((MethodInvoker)delegate
				{
					a();
				});
			}
			else
			{
				a();
			}
		}
		catch
		{
		}
	}

	private List<string> BuildUrlCandidatesBg(CameraEntry cam, out string note)
	{
		note = "";
		List<string> list = new List<string>();
		if (!string.IsNullOrEmpty(cam.Url))
		{
			list.Add(cam.Url);
			return list;
		}
		if (cam.Vendor == "onvif")
		{
			try
			{
				OnvifClient onvifClient = NewOnvifBg(cam);
				List<OnvifProfile> profiles = onvifClient.GetProfiles();
				if (profiles != null && profiles.Count > 0)
				{
					string streamUri = onvifClient.GetStreamUri(profiles[0].Token);
					if (!string.IsNullOrEmpty(streamUri))
					{
						list.Add(streamUri);
					}
				}
				if (list.Count == 0)
				{
					note = "ONVIF 取流地址失败：" + onvifClient.LastError + "（回退通用模板）";
				}
				SaveOnvifClock(cam, onvifClient);
			}
			catch (Exception ex)
			{
				note = "ONVIF 异常：" + ex.Message;
			}
		}
		List<string> list2 = RtspUrlBuilder.Candidates(cam.Vendor, cam.Ip, cam.Port, cam.User, cam.Password, cam.Channel, cam.MainStream);
		if (list2 != null)
		{
			list.AddRange(list2);
		}
		return list;
	}

	private void StopPane(int pane)
	{
		IVideoEngine videoEngine = ((pane == 0) ? _s1 : _s2);
		Label label = ((pane == 0) ? _lblPane1 : _lblPane2);
		videoEngine?.Stop();
		string text = WindowAddrText(pane);
		if (label != null)
		{
			label.Text = "窗口" + (pane + 1) + "：" + ((text.Length == 0) ? "未配置" : text);
		}
		_paneDot[pane] = DotState.Fail;
		SetDot((pane == 0) ? _dotWin1 : _dotWin2, DotState.Fail, "未播放");
		if (_pane1 != null)
		{
			_pane1.Invalidate();
		}
		if (_pane2 != null)
		{
			_pane2.Invalidate();
		}
	}

	private void Snap(int pane)
	{
		IVideoEngine videoEngine = ((pane == 0) ? _s1 : _s2);
		if (videoEngine == null)
		{
			SetMsg("窗口" + (pane + 1) + " 尚未启动预览");
			return;
		}
		string text = Path.Combine(MediaDir(), string.Format(arg2: (videoEngine.EngineName == "libvlc") ? ".png" : ".jpg", format: "snap-p{0}-{1:yyyyMMdd-HHmmss}{2}", arg0: pane + 1, arg1: DateTime.Now));
		if (videoEngine.Snap(text))
		{
			SetMsg("已抓图：" + text);
		}
		else
		{
			SetMsg("抓图失败：" + videoEngine.LastError);
		}
	}

	private void SetRecIndicator(int pane, bool on)
	{
		Label label = ((pane == 0) ? _lblPane1 : _lblPane2);
		if (label == null)
		{
			return;
		}
		string text = label.Text;
		if (on)
		{
			if (!text.Contains("● REC"))
			{
				label.Text = text + "    ● REC（录像中）";
			}
			label.ForeColor = Color.FromArgb(255, 80, 80);
			label.BackColor = Color.FromArgb(90, 20, 10);
		}
		else
		{
			label.Text = text.Replace("    ● REC（录像中）", "");
			label.ForeColor = HudDraw.Cyan;
			label.BackColor = Theme.SurfaceAlt;
		}
	}

	private void ToggleRecord(int pane)
	{
		IVideoEngine videoEngine = ((pane == 0) ? _s1 : _s2);
		if (videoEngine == null)
		{
			SetMsg("窗口" + (pane + 1) + " 尚未启动预览");
			return;
		}
		if (videoEngine.IsRecording)
		{
			videoEngine.StopRecord();
			SetRecIndicator(pane, on: false);
			SetMsg("窗口" + (pane + 1) + " 录像已停止");
			return;
		}
		string arg = ((videoEngine.EngineName == "libvlc") ? ".ts" : ".mp4");
		string text = Path.Combine(MediaDir(), $"rec-p{pane + 1}-{DateTime.Now:yyyyMMdd-HHmmss}{arg}");
		if (videoEngine.StartRecord(text))
		{
			SetRecIndicator(pane, on: true);
			SetMsg("窗口" + (pane + 1) + " 开始录像：" + text);
		}
		else
		{
			SetMsg("录像失败：" + videoEngine.LastError);
		}
	}

	private static string MediaDir()
	{
		string obj = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "YzControl");
		Directory.CreateDirectory(obj);
		return obj;
	}

	private void OpenMediaDir()
	{
		try
		{
			Process.Start("explorer.exe", MediaDir());
		}
		catch
		{
		}
	}

	private void OpenConfigDir()
	{
		try
		{
			Directory.CreateDirectory(CameraStore.ConfigDir);
			Process.Start("explorer.exe", CameraStore.ConfigDir);
		}
		catch
		{
		}
	}

	private static string CamKey(CameraEntry c)
	{
		if (c == null)
		{
			return "";
		}
		return (string.IsNullOrEmpty(c.XAddr) ? ("http://" + c.Ip + "/onvif/device_service") : c.XAddr) + "|" + c.User;
	}

	private OnvifClient NewOnvifBg(CameraEntry c)
	{
		OnvifClient onvifClient = new OnvifClient(string.IsNullOrEmpty(c.XAddr) ? ("http://" + c.Ip + "/onvif/device_service") : c.XAddr, c.User, c.Password);
		OnvifClock value;
		lock (_onvifGate)
		{
			_onvifClockCache.TryGetValue(CamKey(c), out value);
		}
		if (value != null && DateTime.UtcNow - value.SyncedUtc < TimeSpan.FromMinutes(10.0))
		{
			onvifClient.SetClockOffset(value.Offset);
		}
		return onvifClient;
	}

	private void SaveOnvifClock(CameraEntry c, OnvifClient oc)
	{
		if (c == null || oc == null || !oc.ClockMeasured)
		{
			return;
		}
		OnvifClock onvifClock = new OnvifClock();
		onvifClock.Offset = oc.ClockOffset;
		onvifClock.SyncedUtc = DateTime.UtcNow;
		lock (_onvifGate)
		{
			_onvifClockCache[CamKey(c)] = onvifClock;
		}
	}

	private CameraEntry PtzTarget()
	{
		int num = ((_cfg.Pane1 >= 0) ? _cfg.Pane1 : _cfg.Pane2);
		if (num < 0 || num >= _cfg.Cameras.Count)
		{
			return null;
		}
		return _cfg.Cameras[num];
	}

	private void PtzPost(PtzJob job)
	{
		bool flag;
		lock (_ptzGate)
		{
			_ptzPending = job;
			flag = !_ptzBusy;
			if (flag)
			{
				_ptzBusy = true;
			}
		}
		if (flag)
		{
			RunBg(delegate
			{
				PtzPump();
			});
		}
	}

	private void PtzPump()
	{
		while (true)
		{
			PtzJob ptzPending;
			lock (_ptzGate)
			{
				ptzPending = _ptzPending;
				_ptzPending = null;
				if (ptzPending == null)
				{
					_ptzBusy = false;
					break;
				}
			}
			try
			{
				PtzRunJob(ptzPending);
			}
			catch (Exception ex)
			{
				SetMsgSafe("PTZ 异常：" + ex.Message);
			}
		}
	}

	private void PtzRunJob(PtzJob job)
	{
		if (job.Kind == 0 && !_ptzHeld)
		{
			return;
		}
		CameraEntry cam = job.Cam;
		if (cam == null)
		{
			SetMsgSafe("PTZ：没有可用摄像头（请先选中一台或指定窗口）");
			return;
		}
		string profileToken;
		string err;
		OnvifClient onvifClient = MakeOnvifBg(cam, out profileToken, out err);
		if (onvifClient == null)
		{
			SetMsgSafe("ONVIF 取 Profile 失败：" + err);
			return;
		}
		if (job.Kind == 1)
		{
			onvifClient.PtzStop(profileToken);
		}
		else if (job.Kind == 0)
		{
			if (!onvifClient.PtzContinuousMove(profileToken, job.Pan, job.Tilt, job.Zoom))
			{
				SetMsgSafe("PTZ 失败：" + onvifClient.LastError + "（确认摄像头已开启 ONVIF 且账号有 PTZ 权限）");
			}
		}
		else if (job.Kind == 3)
		{
			if (!onvifClient.GotoPreset(profileToken, job.PresetText))
			{
				SetMsgSafe("调用预置位失败：" + onvifClient.LastError);
			}
		}
		else if (job.Kind == 4)
		{
			string text = onvifClient.SetPreset(profileToken, job.PresetText);
			if (text == null)
			{
				SetMsgSafe("设置预置位失败：" + onvifClient.LastError);
			}
			else
			{
				SetMsgSafe("预置位已保存：" + job.PresetText + " (token=" + text + ")");
				UiInvoke(delegate
				{
					LoadPresets();
				});
			}
		}
		else if (job.Kind == 2)
		{
			List<string> presets = onvifClient.GetPresets(profileToken);
			List<string> res = ((presets == null) ? new List<string>() : presets);
			UiInvoke(delegate
			{
				if (_cboPreset != null)
				{
					_cboPreset.Items.Clear();
					for (int i = 0; i < res.Count; i++)
					{
						_cboPreset.Items.Add(res[i]);
					}
					SetMsg("预置位 " + res.Count + " 个");
				}
			});
		}
		SaveOnvifClock(cam, onvifClient);
	}

	private OnvifClient MakeOnvifBg(CameraEntry c, out string profileToken, out string err)
	{
		profileToken = null;
		err = "";
		if (c == null)
		{
			err = "没有可用摄像头";
			return null;
		}
		OnvifClient onvifClient = NewOnvifBg(c);
		string key = CamKey(c);
		string value = null;
		lock (_onvifGate)
		{
			_onvifProfileCache.TryGetValue(key, out value);
		}
		if (string.IsNullOrEmpty(value))
		{
			List<OnvifProfile> profiles = onvifClient.GetProfiles();
			if (profiles == null || profiles.Count == 0)
			{
				err = onvifClient.LastError;
				SaveOnvifClock(c, onvifClient);
				return null;
			}
			value = profiles[0].Token;
			lock (_onvifGate)
			{
				_onvifProfileCache[key] = value;
			}
		}
		profileToken = value;
		return onvifClient;
	}

	private void PtzMove(float pan, float tilt, float zoom)
	{
		PtzJob ptzJob = new PtzJob();
		ptzJob.Kind = 0;
		ptzJob.Pan = pan;
		ptzJob.Tilt = tilt;
		ptzJob.Zoom = zoom;
		ptzJob.Cam = PtzTarget();
		if (ptzJob.Cam == null)
		{
			SetMsg("PTZ：请先选中一台摄像头，或先指定窗口1/窗口2");
		}
		else
		{
			PtzPost(ptzJob);
		}
	}

	private void PtzStop()
	{
		PtzJob ptzJob = new PtzJob();
		ptzJob.Kind = 1;
		ptzJob.Cam = PtzTarget();
		if (ptzJob.Cam != null)
		{
			PtzPost(ptzJob);
		}
	}

	private void StartPtzHold(float pan, float tilt, float zoom)
	{
		_ptzHoldPan = pan;
		_ptzHoldTilt = tilt;
		_ptzHoldZoom = zoom;
		_ptzHeld = true;
		PtzMove(pan, tilt, zoom);
		if (_ptzHold == null)
		{
			_ptzHold = new System.Windows.Forms.Timer();
			_ptzHold.Interval = 700;
			_ptzHold.Tick += delegate
			{
				if (!_ptzHeld)
				{
					_ptzHold.Stop();
				}
				else
				{
					PtzJob ptzJob = new PtzJob
					{
						Kind = 0,
						Pan = _ptzHoldPan,
						Tilt = _ptzHoldTilt,
						Zoom = _ptzHoldZoom,
						Cam = PtzTarget()
					};
					if (ptzJob.Cam == null)
					{
						_ptzHold.Stop();
					}
					else
					{
						PtzPost(ptzJob);
					}
				}
			};
		}
		_ptzHold.Stop();
		_ptzHold.Start();
	}

	private void StopPtzHold(bool sendStop)
	{
		_ptzHeld = false;
		if (_ptzHold != null)
		{
			_ptzHold.Stop();
		}
		if (sendStop)
		{
			PtzStop();
		}
	}

	private void LoadPresets()
	{
		PtzJob ptzJob = new PtzJob();
		ptzJob.Kind = 2;
		ptzJob.Cam = PtzTarget();
		if (ptzJob.Cam != null)
		{
			PtzPost(ptzJob);
		}
	}

	private void GotoPreset()
	{
		if (_cboPreset.Text.Length != 0)
		{
			PtzJob ptzJob = new PtzJob();
			ptzJob.Kind = 3;
			ptzJob.PresetText = _cboPreset.Text;
			ptzJob.Cam = PtzTarget();
			if (ptzJob.Cam != null)
			{
				PtzPost(ptzJob);
			}
		}
	}

	private void SetPreset()
	{
		string text = _cboPreset.Text.Trim();
		if (text.Length == 0)
		{
			SetMsg("请先填写预置位名称");
			return;
		}
		PtzJob ptzJob = new PtzJob();
		ptzJob.Kind = 4;
		ptzJob.PresetText = text;
		ptzJob.Cam = PtzTarget();
		if (ptzJob.Cam != null)
		{
			PtzPost(ptzJob);
		}
	}

	private void LoadMonitorWindows()
	{
		CameraEntry c = ((_cfg.Cameras.Count > 0) ? _cfg.Cameras[0] : null);
		CameraEntry c2 = ((_cfg.Cameras.Count > 1) ? _cfg.Cameras[1] : null);
		FillWindowInputs(0, c);
		FillWindowInputs(1, c2);
		if (EntryAddr(c).Length == 0)
		{
			_cfg.Pane1 = -1;
		}
		if (EntryAddr(c2).Length == 0)
		{
			_cfg.Pane2 = -1;
		}
		UpdateMonitorStatus();
	}

	private void FillWindowInputs(int pane, CameraEntry c)
	{
		string text = EntryAddr(c);
		string text2 = ((c != null && !string.IsNullOrEmpty(c.User)) ? c.User : "admin");
		string text3 = ((c != null) ? (c.Password ?? "") : "");
		TextBox textBox = ((pane == 0) ? _txtAddr1 : _txtAddr2);
		TextBox textBox2 = ((pane == 0) ? _txtUser1 : _txtUser2);
		TextBox textBox3 = ((pane == 0) ? _txtPass1 : _txtPass2);
		if (textBox != null)
		{
			textBox.Text = text;
		}
		if (textBox2 != null)
		{
			textBox2.Text = text2;
		}
		if (textBox3 != null)
		{
			textBox3.Text = text3;
		}
		SetWinState(pane, (text.Length == 0) ? "未配置" : "未启动");
	}

	private static string EntryAddr(CameraEntry c)
	{
		if (c == null)
		{
			return "";
		}
		if (RtspUrlBuilder.IsUrl(c.Url))
		{
			return c.Url.Trim();
		}
		if (string.IsNullOrEmpty(c.Ip))
		{
			return "";
		}
		if (c.Port <= 0 || c.Port == 554)
		{
			return c.Ip.Trim();
		}
		return c.Ip.Trim() + ":" + c.Port;
	}

	private string WindowAddrText(int pane)
	{
		TextBox textBox = ((pane == 0) ? _txtAddr1 : _txtAddr2);
		if (textBox != null)
		{
			return textBox.Text.Trim();
		}
		if (pane >= _cfg.Cameras.Count)
		{
			return "";
		}
		return EntryAddr(_cfg.Cameras[pane]);
	}

	private void SaveWindowCfg(int pane, string addr, string user, string pass)
	{
		while (_cfg.Cameras.Count < 2)
		{
			_cfg.Cameras.Add(new CameraEntry());
		}
		CameraEntry cameraEntry = _cfg.Cameras[pane];
		cameraEntry.Name = addr;
		cameraEntry.User = user;
		cameraEntry.Password = pass;
		cameraEntry.Channel = 1;
		cameraEntry.MainStream = true;
		if (RtspUrlBuilder.IsUrl(addr))
		{
			cameraEntry.Url = addr;
		}
		else
		{
			cameraEntry.Url = "";
			ParseHostPort(addr, out var host, out var port);
			cameraEntry.Ip = host;
			cameraEntry.Port = port;
		}
		if (pane == 0)
		{
			_cfg.Pane1 = 0;
		}
		else
		{
			_cfg.Pane2 = 1;
		}
		SaveConfig();
		UpdateMonitorStatus();
	}

	private void SetWinState(int pane, string text)
	{
		Label label = ((pane == 0) ? _lblWinState1 : _lblWinState2);
		if (label != null)
		{
			label.Text = text;
		}
		StatusDot statusDot = ((pane == 0) ? _dotWin1 : _dotWin2);
		if (statusDot != null)
		{
			statusDot.Tip = "窗口" + (pane + 1) + "：" + text;
		}
	}

	private void SetWinStateSafe(int pane, string text)
	{
		UiInvoke(delegate
		{
			SetWinState(pane, text);
		});
	}

	private void UpdateMonitorStatus()
	{
		if (_lblCam != null)
		{
			_lblCam.Text = "监控 窗1：" + ShortAddr(WindowAddrText(0)) + "  窗2：" + ShortAddr(WindowAddrText(1));
		}
	}

	private static string ShortAddr(string a)
	{
		a = (a ?? "").Trim();
		if (a.Length == 0)
		{
			return "未配置";
		}
		if (a.Length <= 26)
		{
			return a;
		}
		return a.Substring(0, 26) + "…";
	}

	private void SaveConfig()
	{
		string text = (_s3.Host ?? "").Trim();
		if (text.Length > 0)
		{
			_cfg.S3Host = text;
		}
		CameraConfig cameraConfig = new CameraConfig();
		cameraConfig.S3Host = _cfg.S3Host;
		cameraConfig.S3HostSource = _cfg.S3HostSource;
		cameraConfig.S3HostSeen = _cfg.S3HostSeen;
		cameraConfig.Pane1 = _cfg.Pane1;
		cameraConfig.Pane2 = _cfg.Pane2;
		cameraConfig.AutoTakePriority = _cfg.AutoTakePriority;
		cameraConfig.DirectLink = _cfg.DirectLink;
		cameraConfig.DirectCom = _cfg.DirectCom;
		cameraConfig.DirectBaud = _cfg.DirectBaud;
		cameraConfig.DirectCivAddr = _cfg.DirectCivAddr;
		cameraConfig.RigChannel = _cfg.RigChannel;
		cameraConfig.DirectIp = _cfg.DirectIp;
		cameraConfig.DirectIpSource = _cfg.DirectIpSource;
		cameraConfig.DirectIpSeen = _cfg.DirectIpSeen;
		cameraConfig.DirectPort = _cfg.DirectPort;
		cameraConfig.CivPollMs = _cfg.CivPollMs;
		cameraConfig.CivPaused = _cfg.CivPaused;
		cameraConfig.CivLegacy1C = _cfg.CivLegacy1C;
		cameraConfig.CivDeepVfoB = _cfg.CivDeepVfoB;
		cameraConfig.CivReadSmeter = _cfg.CivReadSmeter;
		for (int i = 0; i < _cfg.Cameras.Count; i++)
		{
			if (_cfg.Cameras[i] != null)
			{
				cameraConfig.Cameras.Add(_cfg.Cameras[i].Clone());
			}
		}
		if (!CameraStore.Save(cameraConfig))
		{
			SetMsg("配置保存失败（" + CameraStore.ConfigPath + "）");
		}
		RefreshBoardLabel();
	}

	private void UpdatePaneLabels()
	{
		string text = WindowAddrText(0);
		string text2 = WindowAddrText(1);
		if (_lblPane1 != null)
		{
			_lblPane1.Text = "窗口1：" + ((text.Length == 0) ? "未配置" : text);
		}
		if (_lblPane2 != null)
		{
			_lblPane2.Text = "窗口2：" + ((text2.Length == 0) ? "未配置" : text2);
		}
	}

	protected override void OnShown(EventArgs e)
	{
		base.OnShown(e);
		_running = true;
		// v1.6.0：连接层状态机的唯一 UI 接线点（只改状态灯/提示文案，不动任何布局）
		try
		{
			_s3.LinkStateChanged += OnS3LinkState;
		}
		catch
		{
		}
		_poll = new Thread(PollLoop);
		_poll.IsBackground = true;
		_poll.Start();
		RefreshBoardLabel();
		StartupDiscover();
		_trackTimer = new System.Windows.Forms.Timer();
		// v1.5.1 降频：DTrac 在线状态 3 s → 5 s（它变化很慢，3 s 一次 HTTP 纯属浪费）
		_trackTimer.Interval = 5000;
		_trackTimer.Tick += delegate
		{
			if (_currentPage > 0)
			{
				return;
			}
			RunBg(delegate
			{
				DtracReadBg();
			});
		};
		_trackTimer.Start();
		RunBg(delegate
		{
			DtracReadBg();
		});
		_diagTimer = new System.Windows.Forms.Timer();
		// v1.5.1 降频：固件诊断 10 s → 15 s（只用于人工排障的字段）
		_diagTimer.Interval = 15000;
		_diagTimer.Tick += delegate
		{
			if (_currentPage > 0)
			{
				return;
			}
			DiagReadBg();
		};
		_diagTimer.Start();
		DiagReadBg();
		UpdatePollRate();
		base.Resize += delegate
		{
			UpdatePollRate();
		};
		base.VisibleChanged += delegate
		{
			UpdatePollRate();
		};
		// v1.5.1：海康 VideoSDK / VLC / 组播设备枚举都不在启动时执行了。
		// 摄像头 SDK 只在第一次切到「监控（摄像机）」页时加载（OnMonitorPageEnter）。
	}

	private void PollLoop()
	{
		while (_running)
		{
			if (_wdtTestActive)
			{
				Thread.Sleep(200);
				continue;
			}
			if (Interlocked.CompareExchange(ref _pollInFlight, 1, 0) != 0)
			{
				Thread.Sleep(50);
				continue;
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			try
			{
				YzStatus s = _s3.GetStatus();
				_lastStatus = s;
				if (base.IsHandleCreated && !base.IsDisposed)
				{
					BeginInvoke((MethodInvoker)delegate
					{
						ApplyStatus(s);
					});
				}
			}
			catch
			{
			}
			finally
			{
				Interlocked.Exchange(ref _pollInFlight, 0);
				stopwatch.Stop();
			}
			int num = (_pollFast ? 1000 : 5000) - (int)stopwatch.ElapsedMilliseconds;
			if (num > 50)
			{
				Thread.Sleep(num);
			}
		}
	}

	private void UpdatePollRate()
	{
		bool pollFast = base.Visible && base.WindowState != FormWindowState.Minimized;
		_pollFast = pollFast;
	}

	private void ApplyStatus(YzStatus s)
	{
		if (s == null)
		{
			_lblS3.Text = "S3：未连接 (" + _s3.Host + ")";
			_lblS3.ForeColor = Theme.Danger;
			_lastS3Err = _s3.LastError ?? "";
			// v1.6.0：链路层在退避窗口里时，状态位跟随状态机显示「重连中」（黄），
			// 详细原因（还有多久重试 / 失败几次）放 tooltip；没进退避就是硬失败，仍显示红色 + 分类文案。
			if (_s3.LinkInBackoff)
			{
				SetS3Dot(DotState.Busy, "重连中");
				if (_dotS3 != null)
				{
					_dotS3.Tip = "云台链路退避中 · " + _lastS3Err;
				}
			}
			else
			{
				SetS3Dot(DotState.Fail, (_lastS3Err.Length > 0) ? _lastS3Err : "连接失败");
			}
			_s3FailStreak++;
			if (!_wdtTestActive && (_s3FailStreak == 1 || _s3FailStreak == 3))
			{
				MaybeAutoDiscover("连接失败");
			}
			// v1.6.0：追踪页复用本循环的 /st 结果（不再自己 1s 打一次 /st，避免触发固件限流）
			TrackerPage.PushStatus(online: false, trackOk: 0, az: 0.0, tiltDeg: 0, state: -1, ssid: "", err: _lastS3Err);
			return;
		}
		S3Discovery.RememberBoardIp(s.Ip);
		S3Discovery.RememberBoardIp(s.EthIp);
		_s3FailStreak = 0;
		_autoDiscoverGapMs = 20000;
		RecordBoardSuccess();
		SetS3Dot(DotState.Ok, "已连接");
		_lblS3.ForeColor = Theme.Accent;
		string text = FirmwareText(s);
		_lblS3.Text = $"S3：{_s3.Host}  {((text.Length > 0) ? text : s.Ver)}";
		_lblAz.Text = string.Format("方位 {0:0.#}° / 倾角 {1}° {2}{3}", s.Az, s.TiltDeg, s.StateText, (s.TiltDead == 1) ? " · 倾角传感器离线" : "");
		_lblPrio.Text = ((s.PrioIp == "0.0.0.0") ? "控制权：未限制（任意客户端）" : ("控制权：" + s.PrioIp));
		_lblS3.ToolTipText = "WiFi " + s.Ip + "  以太网 " + s.EthIp + "  dd" + s.Heap + "\n" + text + "\n融合来源 " + s.ElSrcText + "（elsrc=" + s.ElSrc + "，eldiff=" + s.ElDiff.ToString("0.0") + "°）\n水平 " + s.MsPerDeg + " ms/°（" + YzStatus.DegPerSec(s.MsPerDeg).ToString("0.0#") + " °/s）  俯仰 " + s.MsPerDegV + " ms/°（" + YzStatus.DegPerSec(s.MsPerDegV).ToString("0.0#") + " °/s）";
		UpdateInstruments(s);
		UpdateFirmwareLabels();
		RefreshBoardLabel();
		// v1.6.0：追踪页复用本循环的 /st 结果（tk / a / va / s 直接取自同一份快照）
		TrackerPage.PushStatus(online: true, trackOk: s.TrackOk, az: s.Az, tiltDeg: s.TiltDeg, state: s.State, ssid: s.Ssid, err: null);
	}

	private void RecordBoardSuccess()
	{
		try
		{
			string text = DateTime.Now.ToString("HH:mm:ss");
			if (_boardSeen != text)
			{
				_boardSeen = text;
				_cfg.S3HostSeen = text;
			}
			_ipChangeNotified = false;
		}
		catch
		{
		}
	}

	private void RefreshBoardLabel()
	{
		if (_lblBoard != null)
		{
			string text = (_s3.Host ?? "").Trim();
			string text2 = (string.IsNullOrEmpty(_cfg.S3HostSource) ? "未知" : _cfg.S3HostSource);
			_lblBoard.Text = "板子：" + ((text.Length == 0) ? "—" : text) + "（" + text2 + "）" + ((_boardSeen.Length > 0) ? (" · 最后成功 " + _boardSeen) : " · 尚未成功");
			_lblBoard.ToolTipText = "当前板子 " + ((text.Length == 0) ? "（空）" : text) + "\n来源：" + text2 + "（记忆=上次成功地址 / 发现=UDP 60059 广播 / 手填=用户输入 / 兜底=热点 192.168.4.1 / 探测=同网段 TCP 探测）\n最后一次成功读到设备状态：" + ((_boardSeen.Length > 0) ? _boardSeen : "从未") + ((_discoverDiag.Length > 0) ? ("\n最近一次发现：" + _discoverDiag) : "");
		}
	}

	private void MaybeAutoDiscover(string why)
	{
		if (_discoverBusy != 0)
		{
			return;
		}
		if (_lastAutoDiscoverTicks != 0L)
		{
			int num = Environment.TickCount - (int)_lastAutoDiscoverTicks;
			if (num >= 0 && num < _autoDiscoverGapMs)
			{
				return;
			}
		}
		if (_autoDiscoverGapMs < 160000)
		{
			_autoDiscoverGapMs *= 2;
		}
		RediscoverBoard(userInitiated: false);
	}

	private void RediscoverBoard(bool userInitiated)
	{
		RediscoverBoard(userInitiated, userInitiated);
	}

	private void RediscoverBoard(bool userInitiated, bool forceSearch)
	{
		if (Interlocked.CompareExchange(ref _discoverBusy, 1, 0) != 0)
		{
			SetMsg("正在发现板子，请稍候…（同一时刻只发一轮广播）");
			return;
		}
		_lastAutoDiscoverTicks = Environment.TickCount;
		if (userInitiated)
		{
			_autoDiscoverGapMs = 20000;
			if (_btnRediscover != null)
			{
				_btnRediscover.Enabled = false;
			}
			SetMsg("正在发现板子（UDP 60059 广播 " + 800 + "ms）…");
		}
		else
		{
			SetMsg("连接失败 → 正在自动重新发现板子（UDP 60059）…");
		}
		RunBg(delegate
		{
			try
			{
				ResolveApplyAndReport(userInitiated, forceSearch);
			}
			catch (Exception ex)
			{
				SetMsgSafe("发现异常：" + ex.Message);
			}
			finally
			{
				Interlocked.Exchange(ref _discoverBusy, 0);
				UiInvoke(delegate
				{
					if (_btnRediscover != null)
					{
						_btnRediscover.Enabled = true;
					}
				});
			}
		});
	}

	private void StartupDiscover()
	{
		if (Interlocked.CompareExchange(ref _discoverBusy, 1, 0) != 0)
		{
			return;
		}
		_lastAutoDiscoverTicks = Environment.TickCount;
		RunBg(delegate
		{
			try
			{
				ResolveApplyAndReport(verbose: false);
			}
			catch (Exception ex)
			{
				SetMsgSafe("启动发现异常：" + ex.Message);
			}
			finally
			{
				Interlocked.Exchange(ref _discoverBusy, 0);
			}
		});
	}

	private void ResolveApplyAndReport(bool verbose)
	{
		ResolveApplyAndReport(verbose, forceSearch: false);
	}

	private void ResolveApplyAndReport(bool verbose, bool forceSearch)
	{
		string text = (_cfg.S3Host ?? "").Trim();
		if (!S3Discovery.IsUsableIp(text) && !S3Discovery.IsUsableHostName(text))
		{
			text = "";
		}
		string text2 = (_manualHost ?? "").Trim();
		if (!S3Discovery.IsUsableIp(text2) && !S3Discovery.IsUsableHostName(text2))
		{
			text2 = "";
		}
		S3ResolveInputs s3ResolveInputs = new S3ResolveInputs();
		s3ResolveInputs.Memory = text;
		s3ResolveInputs.Manual = text2;
		if (forceSearch || verbose || _s3FailStreak != 0 || _lastStatus == null)
		{
			s3ResolveInputs.Probe = delegate(string ip)
			{
				string detail3;
				bool flag = S3Discovery.HttpProbe(ip, 80, 500, out detail3);
				_discoverDiag = "记忆 " + ip + ":" + (flag ? "在线" : ("不可达(" + detail3 + ")"));
				return flag;
			};
		}
		s3ResolveInputs.Discover = delegate
		{
			S3DiscoverOptions s3DiscoverOptions = new S3DiscoverOptions
			{
				TimeoutMs = 800
			};
			S3DiscoverOutcome s3DiscoverOutcome = S3Discovery.UdpDiscover(s3DiscoverOptions);
			_discoverDiag = "UDP→[" + s3DiscoverOptions.SentTo + "] " + s3DiscoverOutcome.Detail;
			return s3DiscoverOutcome;
		};
		S3ResolveResult s3ResolveResult = S3Discovery.Resolve(s3ResolveInputs);
		if (s3ResolveResult.Source == S3AddressSource.Hotspot)
		{
			List<string> list = S3Discovery.CandidateIps(text, text2, _s3.Host);
			string detail;
			string text3 = S3Discovery.ProbeFirst(list, 80, 500, out detail);
			_discoverDiag = _discoverDiag + "；TCP 探测[" + string.Join(",", list.ToArray()) + "] " + detail;
			if (S3Discovery.IsUsableIp(text3))
			{
				s3ResolveResult.Ip = text3;
				s3ResolveResult.Source = S3AddressSource.Probe;
				s3ResolveResult.Detail = "同网段 TCP 探测命中 /sys";
			}
		}
		ApplyBoardAddress(s3ResolveResult, verbose);
		try
		{
			if (!DirectReady && string.Equals(_rigChannel, "direct", StringComparison.OrdinalIgnoreCase))
			{
				int port;
				string text4 = S3Discovery.SplitHost(_cfg.DirectIp, out port);
				if (port <= 0)
				{
					port = ((_cfg.DirectPort > 0) ? _cfg.DirectPort : 4001);
				}
				if (port <= 0)
				{
					port = 4001;
				}
				string detail2;
				string text5 = S3Discovery.DiscoverRig(text4, s3ResolveResult.Ip, port, 300, out detail2);
				if (S3Discovery.IsUsableIp(text5) && !string.Equals(text5, text4, StringComparison.OrdinalIgnoreCase))
				{
					ApplyDirectEndpoint(text5, port, detail2);
				}
			}
		}
		catch
		{
		}
	}

	private void ApplyBoardAddress(S3ResolveResult r, bool verbose)
	{
		if (r == null || (!S3Discovery.IsUsableIp(r.Ip) && !S3Discovery.IsUsableHostName(r.Ip)))
		{
			SetMsgSafe(S3Discovery.FailureHint());
			return;
		}
		string ip = r.Ip;
		bool changed = !string.Equals((_s3.Host ?? "").Trim(), ip, StringComparison.OrdinalIgnoreCase);
		UiInvoke(delegate
		{
			_hostBoxSync = true;
			try
			{
				if (_txtHost != null && !string.Equals(_txtHost.Text.Trim(), ip, StringComparison.OrdinalIgnoreCase))
				{
					_txtHost.Text = ip;
				}
			}
			finally
			{
				_hostBoxSync = false;
			}
			_s3.Host = ip;
			_cfg.S3Host = ip;
			_cfg.S3HostSource = S3AddressSourceText.Short(r.Source);
			SaveConfig();
			RefreshBoardLabel();
			string text = "当前板子：" + ip + "（来源：" + S3AddressSourceText.Short(r.Source) + "）";
			if (r.ChangedFromMemory && !_ipChangeNotified)
			{
				_ipChangeNotified = true;
				SetMsg("⚠ 板子 IP 变了：已从记忆地址自动切换到 " + ip + "（" + S3AddressSourceText.Short(r.Source) + "）");
				try
				{
					if (_tray != null)
					{
						_tray.ShowBalloonTip(4000, "板子 IP 变了", text, ToolTipIcon.Warning);
					}
				}
				catch
				{
				}
			}
			else if (verbose)
			{
				SetMsg(text + ((r.Detail.Length > 0) ? (" · " + r.Detail) : ""));
			}
			else if (changed)
			{
				SetMsg(text);
			}
			if (r.Source == S3AddressSource.Hotspot && r.Hint.Length > 0)
			{
				SetMsg(r.Hint);
			}
			else if (r.Source == S3AddressSource.Hotspot)
			{
				SetMsg(S3Discovery.FailureHint());
			}
		});
	}

	private void ApplyDirectEndpoint(string ip, int port, string detail)
	{
		UiInvoke(delegate
		{
			if (_txtDirectIp != null && !string.Equals(_txtDirectIp.Text.Trim(), ip, StringComparison.OrdinalIgnoreCase))
			{
				_txtDirectIp.Text = ip;
			}
			_cfg.DirectIp = ip;
			_cfg.DirectIpSource = "discover";
			if (port > 0)
			{
				_cfg.DirectPort = port;
			}
			SaveConfig();
			SetMsg("电台直连端点已更新：" + ip + ":" + port + ((detail.Length > 0) ? ("（" + detail + "）") : ""));
		});
	}

	private void TestConnection()
	{
		string host = _txtHost.Text.Trim();
		_manualHost = host;
		_cfg.S3Host = host;
		_cfg.S3HostSource = S3AddressSourceText.Short(S3AddressSource.Manual);
		RunBg(delegate
		{
			_s3.Host = host;
			YzStatus s = _s3.GetStatus();
			if (s == null)
			{
				string err = _s3.LastError;
				UiInvoke(delegate
				{
					SetS3Dot(DotState.Fail, "连接失败");
					SetMsg("连接失败：" + err);
				});
				RediscoverBoard(userInitiated: false, forceSearch: true);
			}
			else
			{
				UiInvoke(delegate
				{
					SetS3Dot(DotState.Ok, "已连接");
					SetMsg("连接成功，固件 " + s.Ver + "，方位 " + s.Az.ToString("0.#") + "°");
					ApplyStatus(s);
				});
			}
		});
	}

	private void DisconnectS3()
	{
		try
		{
			_running = false;
			try
			{
				if (_poll != null && _poll.IsAlive)
				{
					_poll.Join(400);
				}
			}
			catch
			{
			}
			_poll = null;
			try
			{
				if (_rigTimer != null)
				{
					_rigTimer.Stop();
				}
			}
			catch
			{
			}
			try
			{
				if (_tRigLvl != null)
				{
					_tRigLvl.Stop();
				}
			}
			catch
			{
			}
			try
			{
				if (_pttKeep != null)
				{
					_pttKeep.Stop();
				}
			}
			catch
			{
			}
			try
			{
				if (_trackTimer != null)
				{
					_trackTimer.Stop();
				}
			}
			catch
			{
			}
			try
			{
				if (_diagTimer != null)
				{
					_diagTimer.Stop();
				}
			}
			catch
			{
			}
			try
			{
				_s3.RotatorClose();
			}
			catch
			{
			}
			RunBg(delegate
			{
				RigDirectDisconnect();
			});
			try
			{
				if (_mcast != null)
				{
					_mcast.Stop();
					_mcast.Dispose();
					_mcast = null;
				}
			}
			catch
			{
			}
		}
		catch
		{
		}
		SetS3Dot(DotState.Fail, "已断开");
		SetDot(_dotNet, DotState.Fail, "已停用");
		SetDot(_dotDirect, DotState.Fail, "未连接");
		SetDot(_dotMcast, DotState.Fail, "已停止");
		if (_lblNetState != null)
		{
			_lblNetState.Text = "已断开";
			_lblNetState.ForeColor = Theme.Danger;
		}
		if (_lblDirect != null)
		{
			_lblDirect.Text = "直连：未连接";
			_lblDirect.ForeColor = Theme.TextDim;
		}
		if (_lblMcState != null)
		{
			_lblMcState.Text = "组播音频：已断开";
		}
		if (_lblS3 != null)
		{
			_lblS3.Text = "S3：已断开（点“测试连接”恢复）";
			_lblS3.ForeColor = Theme.Danger;
		}
		SetMsg("已断开与 S3 的连接（轮询/直连/组播均已停止）；点击「测试连接」可重新连接");
	}

	private void SetS3Dot(DotState st, string caption)
	{
		// v1.5.1 修正：顶栏状态位宽固定 108px（TopStrip 里 FixWidth），
		// 原来把整条「未能解析此远程名称:'172.16.1.66'」塞进 caption，
		// StatusDot 会自适应变宽并**盖住右边「读取设备信息」按钮** ← 用户报的"顶栏按钮被掩藏"之一。
		// 现在：状态位只显示短标签，完整原因放 tooltip。
		if (_dotS3 != null)
		{
			string full = caption ?? "";
			string shortCap = full;
			if (shortCap.Length > 10)
			{
				shortCap = shortCap.Substring(0, 9) + "…";
			}
			SetDotTip(_dotS3, st, shortCap, (full.Length > 0) ? full : shortCap);
		}
		if (_dotS3Bar != null)
		{
			_dotS3Bar.Set(st, "");
		}
		if (_btnDisconn != null)
		{
			_btnDisconn.Enabled = st == DotState.Ok;
		}
	}

	private static void SetDot(StatusDot d, DotState st, string caption)
	{
		d?.Set(st, caption);
	}

	private static void SetDotTip(StatusDot d, DotState st, string shortCaption, string tip)
	{
		d?.Set(st, shortCaption, tip);
	}

	private void ToggleNetPassthrough()
	{
		if (_rigTunnelOn)
		{
			RigDropTunnel();
		}
		else
		{
			RigApplyNet(on: true);
		}
	}

	private void RefreshNetButton(bool on)
	{
		if (_btnNetToggle != null)
		{
			string text = (on ? "■ 停用透传" : "▶ 启用透传");
			if (_btnNetToggle.Text != text)
			{
				_btnNetToggle.Text = text;
			}
		}
	}

	private void RefreshDirectButton()
	{
		if (_btnDirect != null)
		{
			string text = (DirectReady ? "■ 断开直连" : "▶ 连接直连");
			if (_btnDirect.Text != text)
			{
				_btnDirect.Text = text;
			}
		}
	}

	private void ToggleMcast()
	{
		if (_mcastRunning)
		{
			McastStop();
		}
		else
		{
			McastStart();
		}
	}

	private void RefreshMcastButton(bool running)
	{
		_mcastRunning = running;
		if (_btnMcastToggle != null)
		{
			string text = (running ? "■ 停止接收" : "▶ 开始接收");
			if (_btnMcastToggle.Text != text)
			{
				_btnMcastToggle.Text = text;
			}
		}
	}

	private void RefreshPaneDot(int pane)
	{
		StatusDot statusDot = ((pane == 0) ? _dotWin1 : _dotWin2);
		if (statusDot != null)
		{
			IVideoEngine videoEngine = ((pane == 0) ? _s1 : _s2);
			bool flag = false;
			try
			{
				flag = videoEngine?.IsPlaying ?? false;
			}
			catch
			{
			}
			if (flag)
			{
				_paneDot[pane] = DotState.Ok;
				statusDot.Set(DotState.Ok, "出画面", "窗口" + (pane + 1) + "：播放器正在出画面（IsPlaying=true）");
			}
			else if (_paneDot[pane] == DotState.Busy)
			{
				statusDot.Set(DotState.Busy, "连接中", "窗口" + (pane + 1) + "：正在后台连接/解析地址");
			}
			else
			{
				_paneDot[pane] = DotState.Fail;
				statusDot.Set(DotState.Fail, "未播放", "窗口" + (pane + 1) + "：未在播放（已停止或连接失败）");
			}
		}
	}

	private void RefreshSys()
	{
		RunBg(delegate
		{
			string s = _s3.GetSys();
			string err = _s3.LastError;
			UiInvoke(delegate
			{
				_lastSysText = s;
				if (_txtSys != null)
				{
					_txtSys.Text = ((s == null) ? ("读取失败：" + err) : s.Replace(";", ";\r\n"));
				}
				if (s != null)
				{
					SplitSys(s, out var ver, out var bld);
					if (!string.IsNullOrEmpty(ver))
					{
						_fwVerCache = ver;
					}
					if (!string.IsNullOrEmpty(bld))
					{
						_fwBldCache = bld;
					}
					UpdateFirmwareLabels();
				}
				UpdateSysBrief();
			});
		});
	}

	private void SetMsg(string m)
	{
		if (!base.IsHandleCreated || base.IsDisposed)
		{
			return;
		}
		if (base.InvokeRequired)
		{
			BeginInvoke((MethodInvoker)delegate
			{
				_lblMsg.Text = m;
			});
		}
		else
		{
			_lblMsg.Text = m;
		}
	}

	private void Shutdown()
	{
		_running = false;
		try
		{
			if (_rigTimer != null)
			{
				_rigTimer.Stop();
				_rigTimer.Dispose();
			}
		}
		catch
		{
		}
		try
		{
			if (_tRigLvl != null)
			{
				_tRigLvl.Stop();
				_tRigLvl.Dispose();
			}
		}
		catch
		{
		}
		try
		{
			if (_pttKeep != null)
			{
				_pttKeep.Stop();
				_pttKeep.Dispose();
			}
		}
		catch
		{
		}
		try
		{
			if (_trackTimer != null)
			{
				_trackTimer.Stop();
				_trackTimer.Dispose();
				_trackTimer = null;
			}
		}
		catch
		{
		}
		try
		{
			if (_diagTimer != null)
			{
				_diagTimer.Stop();
				_diagTimer.Dispose();
				_diagTimer = null;
			}
		}
		catch
		{
		}
		try
		{
			_ptzHeld = false;
			if (_ptzHold != null)
			{
				_ptzHold.Stop();
				_ptzHold.Dispose();
				_ptzHold = null;
			}
		}
		catch
		{
		}
		try
		{
			if (_rigPttOn)
			{
				int httpTimeoutMs = _s3.HttpTimeoutMs;
				try
				{
					_s3.HttpTimeoutMs = 700;
					_s3.GetRig("/rptt?x=0");
				}
				finally
				{
					_s3.HttpTimeoutMs = httpTimeoutMs;
				}
			}
		}
		catch
		{
		}
		try
		{
			if (_mcast != null)
			{
				_mcast.Stop();
				_mcast.Dispose();
				_mcast = null;
			}
		}
		catch
		{
		}
		try
		{
			if (_hdr != null)
			{
				_hdr.Dispose();
				_hdr = null;
			}
		}
		catch
		{
		}
		try
		{
			RigDirectDisconnect();
		}
		catch
		{
		}
		try
		{
			StopMini();
		}
		catch
		{
		}
		try
		{
			if (_s1 != null)
			{
				_s1.Dispose();
			}
		}
		catch
		{
		}
		try
		{
			if (_s2 != null)
			{
				_s2.Dispose();
			}
		}
		catch
		{
		}
		try
		{
			_s3.RotatorClose();
		}
		catch
		{
		}
		try
		{
			if (_bgTimer != null)
			{
				_bgTimer.Stop();
				_bgTimer.Dispose();
				_bgTimer = null;
			}
		}
		catch
		{
		}
		try
		{
			BackgroundImage = null;
			if (_bgBmp != null)
			{
				_bgBmp.Dispose();
				_bgBmp = null;
			}
		}
		catch
		{
		}
		try
		{
			Icon3D.ClearCache();
		}
		catch
		{
		}
		try
		{
			// v1.5.1：退出时释放 libvlc 实例（没进过监控页时 Release 自身是空操作）
			VlcEngine.Release();
		}
		catch
		{
		}
		try
		{
			HudDraw.ClearCache();
		}
		catch
		{
		}
		SaveConfig();
	}
}
