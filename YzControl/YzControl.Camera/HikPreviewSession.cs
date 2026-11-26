using System;

namespace YzControl.Camera;

public sealed class HikPreviewSession : IVideoEngine, IDisposable
{
	private long _handle = -1L;

	private bool _started;

	private bool _disposed;

	private HikVideoSdk.MsgCallback _msgCb;

	private HikVideoSdk.StreamCallback _streamCb;

	private HikVideoSdk.DecodedDataCallback _yuvCb;

	private bool _recording;

	public string Url { get; private set; }

	public string LastError { get; private set; }

	public string EngineName => "海康SDK";

	public long Handle => _handle;

	public bool IsPlaying => _started;

	public bool IsRecording => _recording;

	public event EventHandler Started;

	public event EventHandler<string> Failed;

	public HikPreviewSession()
	{
		LastError = "";
	}

	public bool Start(string url, IntPtr hwnd)
	{
		Stop();
		LastError = "";
		Url = url;
		if (!HikVideoSdk.IsInited && !HikVideoSdk.EnsureInit(out var error))
		{
			LastError = (string.IsNullOrEmpty(error) ? "视频 SDK 未初始化" : ("视频 SDK 初始化失败：" + error));
			RaiseFailed(LastError);
			return false;
		}
		if (hwnd == IntPtr.Zero)
		{
			LastError = "渲染窗口句柄无效";
			RaiseFailed(LastError);
			return false;
		}
		_msgCb = OnMsg;
		_streamCb = null;
		_yuvCb = null;
		HikVideoSdk.PlayReq req = default(HikVideoSdk.PlayReq);
		req.iHardWareDecode = 0;
		req.strUsrmsg = IntPtr.Zero;
		req.appkey = IntPtr.Zero;
		req.secretkey = IntPtr.Zero;
		req.fnStream = _streamCb;
		req.fnMsg = _msgCb;
		req.fnDecodedStream = _yuvCb;
		req.pUserData = IntPtr.Zero;
		req.szReserve = "";
		try
		{
			_handle = HikVideoSdk.StartPreviewRaw(url, hwnd, ref req);
		}
		catch (Exception ex)
		{
			LastError = ex.GetType().Name + ": " + ex.Message;
			RaiseFailed(LastError);
			return false;
		}
		if (_handle < 0)
		{
			int code = HikVideoSdk.LastErrorCode();
			LastError = "启动预览失败（句柄 " + _handle + "，" + HikVideoSdk.ErrorText(code) + "）";
			RaiseFailed(LastError);
			return false;
		}
		return true;
	}

	public void Stop()
	{
		if (_handle >= 0)
		{
			try
			{
				HikVideoSdk.StopPreviewRaw(_handle);
			}
			catch
			{
			}
			_handle = -1L;
		}
		_started = false;
	}

	public bool Snap(string absoluteFileName)
	{
		if (_handle < 0)
		{
			LastError = "尚未开始预览";
			return false;
		}
		if (!_started)
		{
			LastError = "尚未收到播放开始消息，稍后再抓图";
			return false;
		}
		try
		{
			if (HikVideoSdk.SnapRaw(_handle, absoluteFileName) == 0)
			{
				return true;
			}
			LastError = "抓图失败：" + HikVideoSdk.ErrorText(HikVideoSdk.LastErrorCode());
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
		if (_handle < 0)
		{
			return false;
		}
		try
		{
			return HikVideoSdk.SoundCtrlRaw(_handle, (!on) ? 1 : 0) == 0;
		}
		catch
		{
			return false;
		}
	}

	public bool StartRecord(string absoluteFileName)
	{
		if (_handle < 0)
		{
			LastError = "尚未开始预览";
			return false;
		}
		try
		{
			if (HikVideoSdk.StartLocalRecordRaw(_handle, absoluteFileName, 0L, 0) >= 0)
			{
				_recording = true;
				return true;
			}
			LastError = "开始录像失败：" + HikVideoSdk.ErrorText(HikVideoSdk.LastErrorCode());
			return false;
		}
		catch (Exception ex)
		{
			LastError = ex.GetType().Name + ": " + ex.Message;
			return false;
		}
	}

	public bool StopRecord()
	{
		if (_handle < 0 || !_recording)
		{
			return true;
		}
		try
		{
			HikVideoSdk.StopLocalRecordRaw(_handle);
			_recording = false;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private void OnMsg(long playHandle, int msgType, IntPtr userData)
	{
		if (playHandle == _handle)
		{
			switch (msgType)
			{
			case 1:
				_started = true;
				this.Started?.Invoke(this, EventArgs.Empty);
				break;
			case 3:
			case 4:
				_started = false;
				RaiseFailed((msgType == 4) ? "网络异常" : "播放已停止");
				break;
			}
		}
	}

	private void RaiseFailed(string msg)
	{
		this.Failed?.Invoke(this, msg);
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
