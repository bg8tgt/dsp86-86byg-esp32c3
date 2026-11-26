using System;
using System.Threading;

namespace YzControl.Camera;

public sealed class VlcPreviewSession : IVideoEngine, IDisposable
{
	private IntPtr _player = IntPtr.Zero;

	private IntPtr _hwnd = IntPtr.Zero;

	private Thread _watch;

	private volatile bool _run;

	private volatile bool _playing;

	private bool _disposed;

	private string _recordFile;

	public string EngineName => "libvlc";

	public string Url { get; private set; }

	public string LastError { get; private set; }

	public bool IsPlaying => _playing;

	public bool IsRecording => !string.IsNullOrEmpty(_recordFile);

	public event EventHandler Started;

	public event EventHandler<string> Failed;

	public VlcPreviewSession()
	{
		Url = "";
		LastError = "";
	}

	public bool Start(string url, IntPtr hwnd)
	{
		Stop();
		LastError = "";
		Url = url;
		if (!VlcEngine.Available)
		{
			LastError = VlcEngine.Error;
			return false;
		}
		if (hwnd == IntPtr.Zero)
		{
			LastError = "渲染窗口句柄无效";
			return false;
		}
		try
		{
			_hwnd = hwnd;
			IntPtr intPtr = VlcEngine.libvlc_media_new_location(VlcEngine.Instance, url);
			if (intPtr == IntPtr.Zero)
			{
				LastError = "libvlc_media_new_location 失败：" + VlcEngine.ErrMsg();
				return false;
			}
			VlcEngine.libvlc_media_add_option(intPtr, ":rtsp-tcp");
			VlcEngine.libvlc_media_add_option(intPtr, ":network-caching=300");
			VlcEngine.libvlc_media_add_option(intPtr, ":no-video-title-show");
			VlcEngine.libvlc_media_add_option(intPtr, ":clock-jitter=0");
			VlcEngine.libvlc_media_add_option(intPtr, ":clock-synchro=0");
			VlcEngine.libvlc_media_add_option(intPtr, ":snapshot-format=png");
			if (!string.IsNullOrEmpty(_recordFile))
			{
				string text = _recordFile.Replace('\\', '/');
				VlcEngine.libvlc_media_add_option(intPtr, ":sout=#duplicate{dst=display,dst=std{access=file,mux=ts,dst=" + text + "}}");
			}
			_player = VlcEngine.libvlc_media_player_new_from_media(intPtr);
			VlcEngine.libvlc_media_release(intPtr);
			if (_player == IntPtr.Zero)
			{
				LastError = "libvlc_media_player_new_from_media 失败：" + VlcEngine.ErrMsg();
				return false;
			}
			VlcEngine.libvlc_media_player_set_hwnd(_player, hwnd);
			if (VlcEngine.libvlc_media_player_play(_player) != 0)
			{
				LastError = "libvlc_media_player_play 失败：" + VlcEngine.ErrMsg();
				Stop();
				return false;
			}
			_run = true;
			_watch = new Thread(Watch);
			_watch.IsBackground = true;
			_watch.Start();
			return true;
		}
		catch (Exception ex)
		{
			LastError = ex.GetType().Name + ": " + ex.Message;
			Stop();
			return false;
		}
	}

	private void Watch()
	{
		DateTime dateTime = DateTime.Now.AddSeconds(20.0);
		while (_run && _player != IntPtr.Zero)
		{
			int num;
			try
			{
				num = VlcEngine.libvlc_media_player_get_state(_player);
			}
			catch
			{
				break;
			}
			switch (num)
			{
			case 3:
				if (!_playing)
				{
					_playing = true;
					this.Started?.Invoke(this, EventArgs.Empty);
				}
				dateTime = DateTime.Now.AddSeconds(20.0);
				break;
			case 7:
				this.Failed?.Invoke(this, "libvlc 播放出错：" + VlcEngine.ErrMsg());
				return;
			default:
				if (!_playing && DateTime.Now > dateTime)
				{
					this.Failed?.Invoke(this, "libvlc 20 秒内未开始播放（地址/账号/网络？）");
					return;
				}
				break;
			}
			Thread.Sleep(150);
		}
	}

	public void Stop()
	{
		_run = false;
		_playing = false;
		if (_player != IntPtr.Zero)
		{
			try
			{
				VlcEngine.libvlc_media_player_stop(_player);
				VlcEngine.libvlc_media_player_release(_player);
			}
			catch
			{
			}
			_player = IntPtr.Zero;
		}
	}

	public bool Snap(string absoluteFileName)
	{
		if (_player == IntPtr.Zero)
		{
			LastError = "尚未开始预览";
			return false;
		}
		try
		{
			if (VlcEngine.libvlc_video_take_snapshot(_player, 0u, absoluteFileName, 0u, 0u) == 0)
			{
				return true;
			}
			LastError = "抓图失败：" + VlcEngine.ErrMsg();
			return false;
		}
		catch (Exception ex)
		{
			LastError = ex.GetType().Name + ": " + ex.Message;
			return false;
		}
	}

	public bool SetSound(bool on)
	{
		if (_player == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			return VlcEngine.libvlc_audio_set_volume(_player, on ? 100 : 0) == 0;
		}
		catch
		{
			return false;
		}
	}

	public bool StartRecord(string absoluteFileName)
	{
		if (string.IsNullOrEmpty(absoluteFileName))
		{
			LastError = "录像文件名无效";
			return false;
		}
		if (_player == IntPtr.Zero)
		{
			LastError = "尚未开始预览";
			return false;
		}
		_recordFile = absoluteFileName;
		return Restart();
	}

	public bool StopRecord()
	{
		if (!IsRecording)
		{
			return true;
		}
		_recordFile = null;
		return Restart();
	}

	private bool Restart()
	{
		string url = Url;
		IntPtr hwnd = _hwnd;
		bool playing = _playing;
		Stop();
		bool flag = Start(url, hwnd);
		if (!flag || !playing)
		{
			return flag;
		}
		return true;
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			Stop();
		}
	}
}
