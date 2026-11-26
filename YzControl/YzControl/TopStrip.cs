using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace YzControl;

internal sealed class TopStrip : Control
{
	private TextBox _host;

	private Button _test;

	private Button _drop;

	private Button _sys;

	private Button _full;

	private Button _quit;

	private Button _redisc;

	private StatusDot _dotS3;

	private Label _lblHost;

	public TextBox HostBox => _host;

	public StatusDot S3Dot => _dotS3;

	public Button TestButton => _test;

	public Button DisconnectButton => _drop;

	public Button RediscoverButton => _redisc;

	public TopStrip()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value: true);
		base.Height = 44;
		Dock = DockStyle.Top;
		BackColor = Theme.P.ShellTop;
	}

	public void SetConnected(bool connected)
	{
		if (_drop != null)
		{
			_drop.Enabled = connected;
		}
		if (_test != null)
		{
			_test.Enabled = true;
		}
	}

	public void Build(EventHandler onTest, EventHandler onDisconnect, EventHandler onSys, EventHandler onFull, EventHandler onQuit)
	{
		Build(onTest, null, onDisconnect, onSys, onFull, onQuit);
	}

	public void Build(EventHandler onTest, EventHandler onRediscover, EventHandler onDisconnect, EventHandler onSys, EventHandler onFull, EventHandler onQuit)
	{
		_lblHost = new Label();
		_lblHost.AutoSize = false;
		_lblHost.Text = "S3 地址：";
		_lblHost.TextAlign = ContentAlignment.MiddleLeft;
		_lblHost.ForeColor = Theme.TextDim;
		_lblHost.BackColor = Color.Transparent;
		base.Controls.Add(_lblHost);
		_host = new TextBox();
		Theme.StyleTextBox(_host);
		base.Controls.Add(_host);
		_test = Neo("测试连接", onTest);
		_redisc = Neo("重新发现板子", onRediscover);
		_drop = Neo("断开连接", onDisconnect);
		_sys = Neo("读取设备信息", onSys);
		_full = Neo("全屏 (F11)", onFull);
		_quit = Neo("退出", onQuit);
		_dotS3 = new StatusDot();
		// v1.5.1：顶栏里状态位**宽度锁死 108px**（AutoFitWidth 会让它按错误信息变长，
		// 结果压住右边「读取设备信息」按钮）；完整原因走 tooltip。
		_dotS3.FixWidth(108);
		_dotS3.Set(DotState.Unknown, "未连接");
		base.Controls.Add(_test);
		if (_redisc != null)
		{
			base.Controls.Add(_redisc);
		}
		base.Controls.Add(_drop);
		base.Controls.Add(_dotS3);
		base.Controls.Add(_sys);
		base.Controls.Add(_full);
		base.Controls.Add(_quit);
		Relayout();
	}

	private Button Neo(string text, EventHandler h)
	{
		Button button = Theme.MakeButton(text, h);
		button.Height = 34;
		button.Margin = new Padding(0);
		if (button is NeoButton)
		{
			button.ForeColor = Theme.P.Text;
			button.BackColor = Color.Transparent;
		}
		return button;
	}

	public void RefreshTheme()
	{
		BackColor = Theme.P.ShellTop;
		if (_lblHost != null)
		{
			_lblHost.ForeColor = Theme.P.TextDim;
		}
		if (_host != null)
		{
			Theme.StyleTextBox(_host);
		}
		if (Theme.IsLight)
		{
			if (_test != null)
			{
				_test.ForeColor = Theme.P.Text;
			}
			if (_redisc != null)
			{
				_redisc.ForeColor = Theme.P.Text;
			}
			if (_drop != null)
			{
				_drop.ForeColor = Theme.P.Text;
			}
			if (_sys != null)
			{
				_sys.ForeColor = Theme.P.Text;
			}
			if (_full != null)
			{
				_full.ForeColor = Theme.P.Text;
			}
			if (_quit != null)
			{
				_quit.ForeColor = Theme.P.Text;
			}
		}
		else
		{
			if (_test != null)
			{
				_test.ForeColor = Theme.P.Text;
			}
			if (_redisc != null)
			{
				_redisc.ForeColor = Theme.P.Text;
			}
			if (_drop != null)
			{
				_drop.ForeColor = Theme.P.Text;
			}
			if (_sys != null)
			{
				_sys.ForeColor = Theme.P.Text;
			}
			if (_full != null)
			{
				_full.ForeColor = Theme.P.Text;
			}
			if (_quit != null)
			{
				_quit.ForeColor = Theme.P.Text;
			}
		}
		Invalidate(invalidateChildren: true);
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		Relayout();
	}

	private void Relayout()
	{
		if (_host != null)
		{
			int num = 34;
			int num2 = (base.ClientSize.Height - num) / 2;
			int num3 = 8;
			int num4 = TextRenderer.MeasureText("S3 地址：", Theme.FBody).Width + 6;
			_lblHost.SetBounds(num3, num2, num4, num);
			num3 += num4;
			_host.SetBounds(num3 + 1, num2 + (num - 23) / 2, 150, 23);
			num3 += 152;
			num3 += 6;
			_test.SetBounds(num3, num2, _test.Width, num);
			num3 += _test.Width + 12;
			if (_redisc != null)
			{
				_redisc.SetBounds(num3, num2, _redisc.Width, num);
				num3 += _redisc.Width + 12;
			}
			_drop.SetBounds(num3, num2, _drop.Width, num);
			num3 += _drop.Width + 14;
			_dotS3.SetBounds(num3, num2, 108, num);
			num3 += 122;
			_sys.SetBounds(num3, num2, _sys.Width, num);
			num3 += _sys.Width + 26;
			_full.SetBounds(num3, num2, _full.Width, num);
			num3 += _full.Width + 6;
			_quit.SetBounds(num3, num2, _quit.Width, num);
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		NeoPalette p = Theme.P;
		Rectangle clientRectangle = base.ClientRectangle;
		using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, clientRectangle.Width), Math.Max(1, clientRectangle.Height)), p.ShellTop, p.ShellBottom, 90f))
		{
			graphics.FillRectangle(brush, clientRectangle);
		}
		using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(150, p.Accent)))
		{
			graphics.FillRectangle(brush2, 0, clientRectangle.Height - 1, clientRectangle.Width, 1);
		}
		using SolidBrush brush3 = new SolidBrush(p.Accent);
		graphics.FillRectangle(brush3, 0, 0, 26, 2);
		graphics.FillRectangle(brush3, 0, 0, 2, clientRectangle.Height);
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
	}
}
