using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace YzControl.Camera;

public static class HikVideoSdk
{
	public delegate void StreamCallback(long playHandle, int streamDataType, IntPtr data, int dataLen, IntPtr userData);

	public delegate void MsgCallback(long playHandle, int msgType, IntPtr userData);

	public delegate void DecodedDataCallback(long playHandle, IntPtr data, int dataLen, int width, int height, int frameType, int timeStamp, IntPtr userData);

	public delegate void DownloadCallback(long playHandle, float percent, int msg, IntPtr userData);

	public struct PlayReq
	{
		public int iHardWareDecode;

		public IntPtr strUsrmsg;

		public IntPtr appkey;

		public IntPtr secretkey;

		public StreamCallback fnStream;

		public MsgCallback fnMsg;

		public DecodedDataCallback fnDecodedStream;

		public IntPtr pUserData;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
		public string szReserve;
	}

	public struct DownloadReq
	{
		public DownloadCallback DownloadCallBack;

		public IntPtr pUserData;

		public long lFileMaxSize;

		public long lRecordSize;

		public long lStartTimeStamp;

		public long lEndTimeStamp;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
		public string szReserve;
	}

	private const string Dll = "VideoSDK.dll";

	private static readonly object Sync = new object();

	private static bool _inited;

	private static string _initError = "";

	public const int MsgPlayStart = 1;

	public const int MsgPlayEnd = 2;

	public const int MsgPlayStop = 3;

	public const int MsgNetworkError = 4;

	public static bool IsInited => _inited;

	public static string InitError => _initError;

	[DllImport("VideoSDK.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int Video_Init(string szEnv);

	[DllImport("VideoSDK.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int Video_Fini();

	[DllImport("VideoSDK.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int Video_GetLastError();

	[DllImport("VideoSDK.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern long Video_StartPreview(string szUrl, IntPtr hWnd, ref PlayReq req);

	[DllImport("VideoSDK.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int Video_StopPreview(long playHandle);

	[DllImport("VideoSDK.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern long Video_StartPlayback(string szUrl, IntPtr hWnd, long startTs, long endTs, ref PlayReq req);

	[DllImport("VideoSDK.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int Video_StopPlayback(long playHandle);

	[DllImport("VideoSDK.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int Video_PlayCtrl(long playHandle, int ctrlType, long param);

	[DllImport("VideoSDK.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int Video_SoundCtrl(long playHandle, int ctrlType);

	[DllImport("VideoSDK.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int Video_GetVolume(long playHandle);

	[DllImport("VideoSDK.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int Video_SetVolume(long playHandle, int volume);

	[DllImport("VideoSDK.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int Video_PlaySnap(long playHandle, string szFileName);

	[DllImport("VideoSDK.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern long Video_StartDownload(string szUrl, string szFileName, ref DownloadReq req);

	[DllImport("VideoSDK.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int Video_StopDownload(long downloadHandle);

	[DllImport("VideoSDK.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int Video_StartLocalRecord(long playHandle, string szFileName, long fileMaxSize, int recordType);

	[DllImport("VideoSDK.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int Video_StopLocalRecord(long playHandle);

	public static bool EnsureInit(out string error)
	{
		lock (Sync)
		{
			if (_inited)
			{
				error = "";
				return true;
			}
			try
			{
				int num = Video_Init("");
				if (num == 0)
				{
					_inited = true;
					_initError = "";
					error = "";
					return true;
				}
				_initError = "Video_Init 返回 " + num + "，错误码 " + SafeLastError();
				error = _initError;
				return false;
			}
			catch (Exception ex)
			{
				_initError = ex.GetType().Name + ": " + ex.Message + "（通常是 VideoSDK.dll 或其依赖库不在程序目录）";
				error = _initError;
				return false;
			}
		}
	}

	public static void Shutdown()
	{
		lock (Sync)
		{
			if (_inited)
			{
				try
				{
					Video_Fini();
				}
				catch
				{
				}
				_inited = false;
			}
		}
	}

	public static void Prewarm()
	{
		ThreadPool.QueueUserWorkItem(delegate
		{
			try
			{
				EnsureInit(out var _);
			}
			catch
			{
			}
		});
	}

	private static int SafeLastError()
	{
		try
		{
			return Video_GetLastError();
		}
		catch
		{
			return -1;
		}
	}

	internal static int LastErrorCode()
	{
		return SafeLastError();
	}

	internal static long StartPreviewRaw(string url, IntPtr hwnd, ref PlayReq req)
	{
		return Video_StartPreview(url, hwnd, ref req);
	}

	internal static int StopPreviewRaw(long h)
	{
		return Video_StopPreview(h);
	}

	internal static long StartPlaybackRaw(string url, IntPtr hwnd, long startTs, long endTs, ref PlayReq req)
	{
		return Video_StartPlayback(url, hwnd, startTs, endTs, ref req);
	}

	internal static int StopPlaybackRaw(long h)
	{
		return Video_StopPlayback(h);
	}

	internal static int PlayCtrlRaw(long h, int ctrlType, long param)
	{
		return Video_PlayCtrl(h, ctrlType, param);
	}

	internal static int SoundCtrlRaw(long h, int ctrlType)
	{
		return Video_SoundCtrl(h, ctrlType);
	}

	internal static int GetVolumeRaw(long h)
	{
		return Video_GetVolume(h);
	}

	internal static int SetVolumeRaw(long h, int vol)
	{
		return Video_SetVolume(h, vol);
	}

	internal static int SnapRaw(long h, string file)
	{
		return Video_PlaySnap(h, file);
	}

	internal static long StartDownloadRaw(string url, string file, ref DownloadReq req)
	{
		return Video_StartDownload(url, file, ref req);
	}

	internal static int StopDownloadRaw(long h)
	{
		return Video_StopDownload(h);
	}

	internal static int StartLocalRecordRaw(long h, string file, long maxSize, int recordType)
	{
		return Video_StartLocalRecord(h, file, maxSize, recordType);
	}

	internal static int StopLocalRecordRaw(long h)
	{
		return Video_StopLocalRecord(h);
	}

	public static string ErrorText(int code)
	{
		return code switch
		{
			0 => "成功", 
			-1 => "失败", 
			_ => "错误码 " + code, 
		};
	}
}
