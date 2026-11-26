using System;

namespace YzControl.Camera;

public interface IVideoEngine : IDisposable
{
	bool IsRecording { get; }

	string EngineName { get; }

	bool IsPlaying { get; }

	string LastError { get; }

	string Url { get; }

	event EventHandler Started;

	event EventHandler<string> Failed;

	bool Start(string url, IntPtr hwnd);

	void Stop();

	bool Snap(string absoluteFileName);

	bool SetSound(bool on);

	bool StartRecord(string absoluteFileName);

	bool StopRecord();
}
