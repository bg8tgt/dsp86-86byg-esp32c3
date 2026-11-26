using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace YzControl.Camera;

public static class VlcEngine
{
	private static readonly object Sync = new object();

	private static bool _tried;

	private static bool _ok;

	private static string _error = "";

	private static IntPtr _instance = IntPtr.Zero;

	private const string Vlc = "libvlc.dll";

	internal const int StatePlaying = 3;

	internal const int StateError = 7;

	public static bool Available
	{
		get
		{
			Ensure();
			return _ok;
		}
	}

	public static string Error
	{
		get
		{
			Ensure();
			return _error;
		}
	}

	internal static IntPtr Instance => _instance;

	public static void Prewarm()
	{
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				Ensure();
			}
			catch
			{
			}
		});
	}

	/// <summary>
	/// v1.5.1：释放 libvlc 实例并把状态复位（下次用到会重新 libvlc_new）。
	/// 离开「监控（摄像机）」页时调用，让 VLC 的插件/线程/显存在不用时不占内存。
	/// 注意：调用方必须先 Dispose 所有 VlcPreviewSession（播放器/媒体），否则会留下悬挂引用。
	/// </summary>
	public static void Release()
	{
		lock (Sync)
		{
			try
			{
				if (_instance != IntPtr.Zero)
				{
					libvlc_release(_instance);
				}
			}
			catch
			{
			}
			finally
			{
				_instance = IntPtr.Zero;
				_ok = false;
				_tried = false;
				_error = "";
			}
		}
	}

	private static void Ensure()
	{
		lock (Sync)
		{
			if (_tried)
			{
				return;
			}
			_tried = true;
			try
			{
				string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
				if (File.Exists(Path.Combine(baseDirectory, "libvlc.dll")))
				{
					TrySetDllDirectory(baseDirectory);
				}
				else
				{
					string[] array = new string[2]
					{
						Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VideoLAN", "VLC"),
						Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "VideoLAN", "VLC")
					};
					bool flag = false;
					for (int i = 0; i < array.Length; i++)
					{
						if (File.Exists(Path.Combine(array[i], "libvlc.dll")))
						{
							TrySetDllDirectory(array[i]);
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						_error = "未找到 libvlc.dll（程序目录或已安装的 VLC 都没有）。通用 RTSP 预览不可用。";
						return;
					}
				}
				IntPtr zero = IntPtr.Zero;
				_instance = libvlc_new(0, zero);
				if (_instance == IntPtr.Zero)
				{
					_error = "libvlc_new 失败：" + ErrMsg();
					return;
				}
				_ok = true;
				_error = "";
			}
			catch (Exception ex)
			{
				_error = ex.GetType().Name + ": " + ex.Message;
			}
		}
	}

	private static void TrySetDllDirectory(string dir)
	{
		try
		{
			SetDllDirectory(dir);
			string text = Path.Combine(dir, "plugins");
			if (Directory.Exists(text) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("VLC_PLUGIN_PATH")))
			{
				Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", text);
			}
		}
		catch
		{
		}
	}

	internal static string ErrMsg()
	{
		try
		{
			IntPtr intPtr = libvlc_errmsg();
			return (intPtr == IntPtr.Zero) ? "" : Marshal.PtrToStringAnsi(intPtr);
		}
		catch
		{
			return "";
		}
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool SetDllDirectory(string lpPathName);

	[DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr libvlc_new(int argc, IntPtr argv);

	[DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void libvlc_release(IntPtr inst);

	[DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern IntPtr libvlc_media_new_location(IntPtr inst, string mrl);

	[DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern IntPtr libvlc_media_player_new_from_media(IntPtr media);

	[DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void libvlc_media_release(IntPtr media);

	[DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int libvlc_media_player_play(IntPtr mp);

	[DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void libvlc_media_player_stop(IntPtr mp);

	[DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void libvlc_media_player_release(IntPtr mp);

	[DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void libvlc_media_player_set_hwnd(IntPtr mp, IntPtr hwnd);

	[DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int libvlc_media_player_get_state(IntPtr mp);

	[DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int libvlc_media_player_is_playing(IntPtr mp);

	[DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int libvlc_video_take_snapshot(IntPtr mp, uint num, string file, uint w, uint h);

	[DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern int libvlc_audio_set_volume(IntPtr mp, int vol);

	[DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern void libvlc_media_add_option(IntPtr media, string option);

	[DllImport("libvlc.dll", CallingConvention = CallingConvention.Cdecl)]
	internal static extern IntPtr libvlc_errmsg();
}
