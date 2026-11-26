using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using YzControl.Camera;

namespace YzControl;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
		{
			try
			{
				File.AppendAllText(Path.Combine(Path.GetTempPath(), "yz-crash.txt"), DateTime.Now.ToString("s") + " [AppDomain]\r\n" + ((e.ExceptionObject == null) ? "(null)" : e.ExceptionObject.ToString()));
			}
			catch
			{
			}
		};
		Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
		{
			try
			{
				File.AppendAllText(Path.Combine(Path.GetTempPath(), "yz-crash.txt"), DateTime.Now.ToString("s") + " [Thread]\r\n" + e.Exception);
			}
			catch
			{
			}
		};
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(defaultValue: false);
		CameraStore.Prewarm();
		Theme.Load();
		int result = 0;
		bool selfTest = false;
		string pageDump = null;
		foreach (string text in args)
		{
			if (text.StartsWith("/page=", StringComparison.OrdinalIgnoreCase))
			{
				int.TryParse(text.Substring(6), out result);
			}
			else if (text.StartsWith("/dumpui=", StringComparison.OrdinalIgnoreCase))
			{
				MainForm.DumpUiPath = text.Substring(8).Trim('"');
			}
			else if (text.StartsWith("/pagedump=", StringComparison.OrdinalIgnoreCase))
			{
				pageDump = text.Substring(10).Trim('"');
			}
			else if (text.Equals("/selftest", StringComparison.OrdinalIgnoreCase))
			{
				selfTest = true;
			}
		}
		if (pageDump != null)
		{
			Environment.ExitCode = PageDump(pageDump);
			return;
		}
		if (selfTest)
		{
			// v1.7：无界面自检（星历 / SGP4 / 过境 / 多普勒），售后与版本验收用
			Environment.ExitCode = Sat.SatSelfTest.Run();
			return;
		}
		// v1.5.1 性能：**不再在启动时预热** 海康 VideoSDK / libvlc。
		// 这两个原生库加载后会连带 VideoSDK 的全部依赖（HPSClient/HikAes/hpr/libvlccore/plugins…），
		// 实测常驻内存 +40~60MB，而绝大多数时间用不到摄像头。
		// 现改为「第一次切到『监控（摄像机）』页时才初始化」——见 MainForm.OnMonitorPageEnter()。
		try
		{
			Application.Run(new MainForm(result));
		}
		catch (Exception ex)
		{
			try
			{
				File.AppendAllText(Path.Combine(Path.GetTempPath(), "yz-crash.txt"), DateTime.Now.ToString("s") + "\r\n" + ex.ToString());
			}
			catch
			{
			}
			throw;
		}
		finally
		{
			HikVideoSdk.Shutdown();
		}
	}

	/// <summary>
	/// 无界面页面体检：离屏构造「卫星追踪」页并导出控件树（位置/尺寸/文本/越界/重叠）。
	/// 用法：YzControl.exe /pagedump=D:\dump.txt —— 不需要桌面会话，售后与验收用。
	/// </summary>
	private static int PageDump(string path)
	{
		StringBuilder sb = new StringBuilder();
		try
		{
			using (Form f = new Form
			{
				Size = new Size(1400, 900),
				FormBorderStyle = FormBorderStyle.None,
				ShowInTaskbar = false,
				StartPosition = FormStartPosition.Manual,
				Location = new Point(-4000, -4000)
			})
			{
				Panel page = TrackerPage.Build(new S3Client(), () => (CivLink)null, () => Sat.RigKind.Ic7100);
				f.Controls.Add(page);
				f.Show();
				for (int i = 0; i < 15; i++)
				{
					Application.DoEvents();
					Thread.Sleep(80);
				}
				sb.AppendLine("ClientSize=" + f.ClientSize.Width + "x" + f.ClientSize.Height);
				DumpNode(sb, page, 1);
				// 同时落一张 PNG，方便肉眼验收（自绘控件也走 DrawToBitmap）
				try
				{
					using (Bitmap bmp = new Bitmap(page.Width, page.Height))
					{
						page.DrawToBitmap(bmp, new Rectangle(0, 0, page.Width, page.Height));
						bmp.Save(Path.ChangeExtension(path, ".png"), System.Drawing.Imaging.ImageFormat.Png);
					}
				}
				catch (Exception ex) { sb.AppendLine("PNG ERR " + ex.Message); }
				f.Close();
			}
		}
		catch (Exception ex)
		{
			sb.AppendLine("ERR " + ex);
		}
		try { File.WriteAllText(path, sb.ToString(), Encoding.UTF8); } catch { }
		return 0;
	}

	private static void DumpNode(StringBuilder sb, Control c, int depth)
	{
		string pad = new string(' ', depth * 2);
		string text = "";
		if (c is Label || c is Button || c is CheckBox)
		{
			text = " '" + (c.Text ?? "").Replace("\r", " ").Replace("\n", " ") + "'";
			if (text.Length > 70) text = text.Substring(0, 70) + "…'";
		}
		bool outOfParent = false;
		if (c.Parent != null)
		{
			var pc = c.Parent.ClientRectangle;
			outOfParent = c.Left < 0 || c.Top < 0 || c.Right > pc.Width || c.Bottom > pc.Height;
		}
		sb.AppendLine(pad + c.GetType().Name + " vis=" + (c.Visible ? 1 : 0)
			+ " @" + c.Left + "," + c.Top + " " + c.Width + "x" + c.Height + text
			+ (outOfParent ? "  <<CLIPPED>>" : ""));
		// 同级重叠体检（只比可见的、有意义的控件）
		if (c.Parent != null && c.Visible && c.Width > 4 && c.Height > 4)
		{
			foreach (Control s in c.Parent.Controls)
			{
				if (ReferenceEquals(s, c) || !s.Visible || s.Width <= 4 || s.Height <= 4) continue;
				if (string.CompareOrdinal(s.Name + s.GetHashCode().ToString(), c.Name + c.GetHashCode().ToString()) <= 0) continue;
				var a = c.Bounds; var b = s.Bounds;
				int ow = Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left);
				int oh = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top);
				if (ow > 6 && oh > 6) sb.AppendLine(pad + "  !! 与 " + s.GetType().Name + "(" + s.Left + "," + s.Top + " " + s.Width + "x" + s.Height + ") 重叠 " + ow + "x" + oh);
			}
		}
		foreach (Control ch in c.Controls) DumpNode(sb, ch, depth + 1);
	}
}
