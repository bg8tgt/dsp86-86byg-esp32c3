using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace YzControl;

internal sealed class WaveOutPlayer : IDisposable
{
	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	private struct WAVEFORMATEX
	{
		public ushort wFormatTag;

		public ushort nChannels;

		public uint nSamplesPerSec;

		public uint nAvgBytesPerSec;

		public ushort nBlockAlign;

		public ushort wBitsPerSample;

		public ushort cbSize;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	private struct WAVEHDR
	{
		public IntPtr lpData;

		public uint dwBufferLength;

		public uint dwBytesRecorded;

		public IntPtr dwUser;

		public uint dwFlags;

		public uint dwLoops;

		public IntPtr lpNext;

		public IntPtr reserved;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 8)]
	private struct MMTIME
	{
		public uint wType;

		public uint pad;

		public MMVAL u;
	}

	[StructLayout(LayoutKind.Explicit)]
	private struct MMVAL
	{
		[FieldOffset(0)]
		public uint ms;

		[FieldOffset(0)]
		public uint sample;

		[FieldOffset(0)]
		public uint cb;

		[FieldOffset(0)]
		public uint ticks;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	internal struct WAVEOUTCAPS
	{
		public ushort wMid;

		public ushort wPid;

		public uint vDriverVersion;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string szPname;

		public uint dwFormats;

		public ushort wChannels;

		public ushort wReserved1;

		public uint dwSupport;
	}

	private const int BufferCount = 4;

	private readonly int _bufBytes;

	private readonly byte[] _ring;

	private int _head;

	private int _tail;

	private int _count;

	private readonly object _ringLock = new object();

	private readonly int _prebuffer;

	private bool _primed;

	private IntPtr _hWave;

	private IntPtr[] _hdrPtr;

	private IntPtr[] _dataPtr;

	private bool[] _inUse;

	public int VolumePercent = 100;

	private const uint WHDR_DONE = 1u;

	private const uint CALLBACK_NULL = 0u;

	private readonly int _hdrSize = Marshal.SizeOf(typeof(WAVEHDR));

	public string LastError = "";

	public int FailCount;

	[DllImport("winmm.dll")]
	private static extern int waveOutOpen(out IntPtr h, int dev, ref WAVEFORMATEX fmt, IntPtr cb, IntPtr inst, uint flags);

	[DllImport("winmm.dll")]
	private static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, int size);

	[DllImport("winmm.dll")]
	private static extern int waveOutWrite(IntPtr h, IntPtr hdr, int size);

	[DllImport("winmm.dll")]
	private static extern int waveOutUnprepareHeader(IntPtr h, IntPtr hdr, int size);

	[DllImport("winmm.dll")]
	private static extern int waveOutReset(IntPtr h);

	[DllImport("winmm.dll")]
	private static extern int waveOutClose(IntPtr h);

	[DllImport("winmm.dll")]
	private static extern int waveOutGetPosition(IntPtr h, ref MMTIME t, int size);

	[DllImport("winmm.dll")]
	internal static extern int waveOutGetNumDevs();

	[DllImport("winmm.dll", CharSet = CharSet.Unicode)]
	internal static extern int waveOutGetDevCaps(IntPtr dev, ref WAVEOUTCAPS caps, int size);

	public WaveOutPlayer(int rate, int channels, int bits, int prebufferMs)
		: this(rate, channels, bits, prebufferMs, -1)
	{
	}

	public WaveOutPlayer(int rate, int channels, int bits, int prebufferMs, int deviceId)
	{
		WAVEFORMATEX fmt = default(WAVEFORMATEX);
		fmt.wFormatTag = 1;
		fmt.nChannels = (ushort)channels;
		fmt.nSamplesPerSec = (uint)rate;
		fmt.wBitsPerSample = (ushort)bits;
		fmt.nBlockAlign = (ushort)(channels * bits / 8);
		fmt.nAvgBytesPerSec = (uint)(rate * fmt.nBlockAlign);
		fmt.cbSize = 0;
		int num = waveOutOpen(out _hWave, (deviceId < 0) ? (-1) : deviceId, ref fmt, IntPtr.Zero, IntPtr.Zero, 0u);
		if (num != 0)
		{
			throw new InvalidOperationException("waveOutOpen 失败，代码 " + num + "（设备 " + deviceId + "）");
		}
		_bufBytes = rate * fmt.nBlockAlign / 1000 * 20;
		_ring = new byte[Math.Max(_bufBytes * 8, rate * fmt.nBlockAlign)];
		_prebuffer = Math.Max(_bufBytes, rate * fmt.nBlockAlign / 1000 * prebufferMs);
		_hdrPtr = new IntPtr[4];
		_dataPtr = new IntPtr[4];
		_inUse = new bool[4];
		for (int i = 0; i < 4; i++)
		{
			_dataPtr[i] = Marshal.AllocHGlobal(_bufBytes);
			_hdrPtr[i] = Marshal.AllocHGlobal(_hdrSize);
			Marshal.StructureToPtr(new WAVEHDR
			{
				lpData = _dataPtr[i],
				dwBufferLength = (uint)_bufBytes
			}, _hdrPtr[i], fDeleteOld: false);
			waveOutPrepareHeader(_hWave, _hdrPtr[i], _hdrSize);
		}
	}

	public void Start()
	{
	}

	public void Write(byte[] pcm, int len)
	{
		lock (_ringLock)
		{
			int num = _ring.Length;
			int num2 = 48000;
			if (_count + len > num2)
			{
				int num3 = Math.Min(_count, _count + len - num2);
				_tail = (_tail + num3) % num;
				_count -= num3;
			}
			for (int i = 0; i < len; i++)
			{
				_ring[(_tail + i) % num] = pcm[i];
			}
			_count = Math.Min(num, _count + len);
			_tail = (_tail + len) % num;
		}
	}

	public void Read(byte[] dst, int len)
	{
		lock (_ringLock)
		{
			if (!_primed)
			{
				if (_count < _prebuffer)
				{
					Array.Clear(dst, 0, len);
					return;
				}
				_primed = true;
			}
			int num = Math.Min(_count, len);
			if (num > 0)
			{
				_count -= num;
			}
			for (int i = 0; i < num; i++)
			{
				dst[i] = _ring[(_head + i) % _ring.Length];
			}
			for (int j = num; j < len; j++)
			{
				dst[j] = 0;
			}
			_head = (_head + num) % _ring.Length;
		}
	}

	public void Submit(byte[] pcm)
	{
		if (VolumePercent != 100)
		{
			int num = Math.Max(0, Math.Min(200, VolumePercent));
			for (int i = 0; i + 1 < pcm.Length; i += 2)
			{
				int num2 = (short)(pcm[i] | (pcm[i + 1] << 8)) * num / 100;
				if (num2 > 32767)
				{
					num2 = 32767;
				}
				else if (num2 < -32768)
				{
					num2 = -32768;
				}
				pcm[i] = (byte)((uint)num2 & 0xFFu);
				pcm[i + 1] = (byte)((uint)(num2 >> 8) & 0xFFu);
			}
		}
		for (int j = 0; j < 200; j++)
		{
			int num3 = -1;
			for (int k = 0; k < 4; k++)
			{
				if (_inUse[k] && (((WAVEHDR)Marshal.PtrToStructure(_hdrPtr[k], typeof(WAVEHDR))).dwFlags & (true ? 1u : 0u)) != 0)
				{
					_inUse[k] = false;
				}
				if (!_inUse[k])
				{
					num3 = k;
					break;
				}
			}
			if (num3 >= 0)
			{
				int num4 = Math.Min(pcm.Length, _bufBytes);
				Marshal.Copy(pcm, 0, _dataPtr[num3], num4);
				WAVEHDR structure = (WAVEHDR)Marshal.PtrToStructure(_hdrPtr[num3], typeof(WAVEHDR));
				structure.dwBufferLength = (uint)num4;
				structure.dwFlags &= 4294967294u;
				Marshal.StructureToPtr(structure, _hdrPtr[num3], fDeleteOld: false);
				int num5 = waveOutWrite(_hWave, _hdrPtr[num3], _hdrSize);
				if (num5 == 0)
				{
					_inUse[num3] = true;
					LastError = "";
				}
				else
				{
					LastError = "waveOutWrite 失败，代码 " + num5 + ((num5 == 33) ? "（缓冲未 prepare）" : "");
					FailCount++;
					_ = 5;
				}
				return;
			}
			Thread.Sleep(1);
		}
		LastError = "声卡缓冲一直不空闲（设备可能未真正运行）";
	}

	public long PlayedBytes()
	{
		try
		{
			MMTIME t = default(MMTIME);
			t.wType = 4u;
			if (waveOutGetPosition(_hWave, ref t, Marshal.SizeOf(typeof(MMTIME))) == 0)
			{
				return t.u.cb;
			}
		}
		catch
		{
		}
		return -1L;
	}

	public void Dispose()
	{
		try
		{
			if (_hWave != IntPtr.Zero)
			{
				waveOutReset(_hWave);
			}
		}
		catch
		{
		}
		try
		{
			if (_hdrPtr != null)
			{
				for (int i = 0; i < 4; i++)
				{
					try
					{
						waveOutUnprepareHeader(_hWave, _hdrPtr[i], _hdrSize);
					}
					catch
					{
					}
					if (_dataPtr != null && _dataPtr[i] != IntPtr.Zero)
					{
						Marshal.FreeHGlobal(_dataPtr[i]);
					}
					if (_hdrPtr[i] != IntPtr.Zero)
					{
						Marshal.FreeHGlobal(_hdrPtr[i]);
					}
				}
			}
		}
		catch
		{
		}
		try
		{
			if (_hWave != IntPtr.Zero)
			{
				waveOutClose(_hWave);
			}
		}
		catch
		{
		}
		_hWave = IntPtr.Zero;
	}
}
