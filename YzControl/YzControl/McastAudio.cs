using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace YzControl;

internal sealed class McastAudio : IDisposable
{
	public string Group = "239.255.42.99";

	public int Port = 5004;

	public string Iface = "";

	public int VolumePercent = 100;

	public int WaveOutDeviceId = -1;

	public long Pkts;

	public long Lost;

	public long Ooo;

	public long Bytes;

	public double LossPct;

	public double JitterMs;

	public double Kbps;

	public double AudioSeconds;

	public double Rms;

	public int Peak;

	public double PeakDbfs = -99.0;

	public string Text = "未开始";

	public volatile bool Running;

	private Socket _sock;

	private Thread _rxThread;

	private Thread _playThread;

	private WaveOutPlayer _player;

	private readonly object _lock = new object();

	private long _lastSeq = -1L;

	private uint _ssrc;

	private double _ssrcAt;

	private double _t0;

	private double _ts0 = -1.0;

	private double _prevTransit;

	private bool _hasTs0;

	private bool _hasTransit;

	private double _jitSum;

	private long _jitN;

	private int _winPeak;

	private double _winSq;

	private long _winN;

	private double _lastRx;

	private volatile bool _stop;

	public bool IsAlive
	{
		get
		{
			if (_lastRx > 0.0)
			{
				return NowSec() - _lastRx < 3.0;
			}
			return false;
		}
	}

	public int PlayFailCount
	{
		get
		{
			try
			{
				return (_player != null) ? _player.FailCount : 0;
			}
			catch
			{
				return 0;
			}
		}
	}

	public string PlayLastError
	{
		get
		{
			try
			{
				return (_player != null) ? _player.LastError : "";
			}
			catch
			{
				return "";
			}
		}
	}

	public event Action<McastAudio> Stats;

	public event Action<string> Error;

	public event Action<byte[]> Pcm;

	public static string[] ListDevices()
	{
		int num = WaveOutPlayer.waveOutGetNumDevs();
		List<string> list = new List<string>();
		list.Add("系统默认");
		for (int i = 0; i < num; i++)
		{
			WaveOutPlayer.WAVEOUTCAPS caps = default(WaveOutPlayer.WAVEOUTCAPS);
			int num2 = WaveOutPlayer.waveOutGetDevCaps((IntPtr)i, ref caps, Marshal.SizeOf(typeof(WaveOutPlayer.WAVEOUTCAPS)));
			list.Add((num2 == 0) ? ("[" + i + "] " + caps.szPname) : ("[" + i + "] 设备 " + i));
		}
		return list.ToArray();
	}

	public long PlayedBytes()
	{
		try
		{
			return (_player != null) ? _player.PlayedBytes() : (-1);
		}
		catch
		{
			return -1L;
		}
	}

	private static double NowSec()
	{
		return (double)Environment.TickCount / 1000.0;
	}

	public bool Start(string group, int port, string iface)
	{
		Stop();
		Group = (string.IsNullOrEmpty(group) ? "239.255.42.99" : group.Trim());
		Port = ((port > 0 && port < 65536) ? port : 5004);
		Iface = (iface ?? "").Trim();
		if (!IPAddress.TryParse(Group, out var address))
		{
			Raise("组播地址不合法：" + Group);
			return false;
		}
		if (string.IsNullOrEmpty(Iface))
		{
			Iface = GuessLocalIp();
		}
		if (!IPAddress.TryParse(Iface, out var address2))
		{
			Raise("本机网卡地址不合法：" + Iface);
			return false;
		}
		try
		{
			_sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			_sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);
			try
			{
				_sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, 1048576);
			}
			catch
			{
			}
			_sock.Bind(new IPEndPoint(IPAddress.Any, Port));
			_sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(address, address2));
			_sock.ReceiveTimeout = 300;
		}
		catch (Exception ex)
		{
			try
			{
				if (_sock != null)
				{
					_sock.Close();
				}
			}
			catch
			{
			}
			_sock = null;
			Raise("加入组播失败：" + ex.Message + "（多网卡请把「本机网卡」填成与音频源同网段的地址，如 172.16.1.x）");
			return false;
		}
		bool flag = true;
		try
		{
			_player = new WaveOutPlayer(48000, 1, 16, 60, WaveOutDeviceId);
			_player.VolumePercent = VolumePercent;
			_player.Start();
		}
		catch (Exception ex2)
		{
			flag = false;
			_player = null;
			Raise("声卡打不开（只统计不出声）：" + ex2.Message);
		}
		lock (_lock)
		{
			Pkts = (Lost = (Ooo = (Bytes = 0L)));
			Peak = 0;
			Rms = 0.0;
			LossPct = 0.0;
			JitterMs = 0.0;
			Kbps = 0.0;
			AudioSeconds = 0.0;
			_lastSeq = -1L;
			_ssrc = 0u;
			_hasTs0 = false;
			_hasTransit = false;
			_jitSum = 0.0;
			_jitN = 0L;
			_winPeak = 0;
			_winSq = 0.0;
			_winN = 0L;
			_t0 = NowSec();
			_lastRx = 0.0;
			_stop = false;
			Running = true;
			Text = "接收中 " + Group + ":" + Port + " @" + Iface + (flag ? "" : "（无声卡）");
		}
		_rxThread = new Thread(RxLoop)
		{
			IsBackground = true,
			Name = "mcast_rx"
		};
		_rxThread.Start();
		if (_player != null)
		{
			_playThread = new Thread(PlayLoop)
			{
				IsBackground = true,
				Name = "mcast_out"
			};
			_playThread.Start();
		}
		return true;
	}

	public void Stop()
	{
		_stop = true;
		Running = false;
		try
		{
			if (_rxThread != null)
			{
				_rxThread.Join(1200);
			}
		}
		catch
		{
		}
		try
		{
			if (_playThread != null)
			{
				_playThread.Join(1200);
			}
		}
		catch
		{
		}
		_rxThread = null;
		_playThread = null;
		try
		{
			if (_player != null)
			{
				_player.Dispose();
			}
		}
		catch
		{
		}
		_player = null;
		try
		{
			if (_sock != null)
			{
				_sock.Close();
			}
		}
		catch
		{
		}
		_sock = null;
		Text = "已停止";
	}

	public void Dispose()
	{
		Stop();
	}

	private void Raise(string m)
	{
		try
		{
			if (this.Error != null)
			{
				this.Error(m);
			}
		}
		catch
		{
		}
	}

	private void RxLoop()
	{
		byte[] array = new byte[2048];
		double num = NowSec();
		while (!_stop)
		{
			int num2 = 0;
			try
			{
				num2 = _sock.Receive(array, 0, array.Length, SocketFlags.None);
			}
			catch (SocketException)
			{
				num2 = 0;
			}
			catch (Exception)
			{
				break;
			}
			if (num2 >= 12 && array[0] >> 6 == 2 && (array[1] & 0x7F) == 96)
			{
				Feed(array, num2);
			}
			double num3 = NowSec();
			if (num3 - num >= 1.0)
			{
				Publish(num3);
				num = num3;
			}
		}
	}

	private void Feed(byte[] buf, int n)
	{
		int num = (buf[2] << 8) | buf[3];
		uint num2 = (uint)((buf[4] << 24) | (buf[5] << 16) | (buf[6] << 8) | buf[7]);
		uint num3 = (uint)((buf[8] << 24) | (buf[9] << 16) | (buf[10] << 8) | buf[11]);
		int num4 = n - 12;
		if (num4 <= 0)
		{
			return;
		}
		double num5 = NowSec();
		if (_ssrc == 0)
		{
			_ssrc = num3;
			_ssrcAt = num5;
		}
		else if (num3 != _ssrc)
		{
			if (!(num5 - _lastRx > 2.0))
			{
				return;
			}
			_ssrc = num3;
			_ssrcAt = num5;
			_lastSeq = -1L;
			_hasTs0 = false;
			_hasTransit = false;
		}
		_ssrcAt = num5;
		if (!_hasTs0)
		{
			_ts0 = num2;
			_hasTs0 = true;
		}
		if (_lastSeq >= 0)
		{
			int num6 = (int)((num - _lastSeq) & 0xFFFF);
			if (num6 > 1 && num6 < 32768)
			{
				if (num6 > 500)
				{
					Ooo++;
				}
				else
				{
					Lost += num6 - 1;
				}
			}
			else if (num6 == 0 || num6 > 32768)
			{
				Ooo++;
			}
		}
		_lastSeq = num;
		Pkts++;
		Bytes += num4;
		_lastRx = num5;
		double num7 = num5 - _t0 - ((double)num2 - _ts0) / 48000.0;
		if (_hasTransit)
		{
			_jitSum += Math.Abs(num7 - _prevTransit);
			_jitN++;
		}
		_prevTransit = num7;
		_hasTransit = true;
		byte[] array = new byte[num4];
		Buffer.BlockCopy(buf, 12, array, 0, num4);
		for (int i = 0; i + 1 < num4; i += 2)
		{
			byte b = array[i];
			array[i] = array[i + 1];
			array[i + 1] = b;
		}
		int num8 = 0;
		double num9 = 0.0;
		long num10 = 0L;
		for (int j = 0; j + 1 < num4; j += 2)
		{
			short num11 = (short)(array[j] | (array[j + 1] << 8));
			int num12 = ((num11 < 0) ? (-num11) : num11);
			if (num12 > num8)
			{
				num8 = num12;
			}
			num9 += (double)num11 * (double)num11;
			num10++;
		}
		if (num8 > _winPeak)
		{
			_winPeak = num8;
		}
		_winSq += num9;
		_winN += num10;
		if (_player != null)
		{
			_player.Write(array, array.Length);
		}
		try
		{
			if (this.Pcm != null)
			{
				this.Pcm(array);
			}
		}
		catch
		{
		}
	}

	private void Publish(double now)
	{
		double num = Math.Max(1E-06, now - _t0);
		long num2 = Pkts + Lost;
		LossPct = ((num2 > 0) ? (100.0 * (double)Lost / (double)num2) : 0.0);
		JitterMs = ((_jitN > 0) ? (1000.0 * _jitSum / (double)_jitN) : 0.0);
		AudioSeconds = (double)Bytes / 96000.0;
		Kbps = (double)(Bytes * 8) / num / 1000.0;
		Peak = _winPeak;
		Rms = ((_winN > 0) ? Math.Sqrt(_winSq / (double)_winN) : 0.0);
		PeakDbfs = ((Peak > 0) ? (20.0 * Math.Log10((double)Peak / 32768.0)) : (-99.0));
		_winPeak = 0;
		_winSq = 0.0;
		_winN = 0L;
		string text = (IsAlive ? "" : "  ⚠无信号");
		Text = $"包 {Pkts}  丢 {Lost}({LossPct:F2}%)  乱序 {Ooo}  抖动 {JitterMs:F1}ms  音频 {AudioSeconds:F1}s  {Kbps:F0}kbps  峰值 {PeakDbfs:F1}dBFS{text}";
		try
		{
			if (this.Stats != null)
			{
				this.Stats(this);
			}
		}
		catch
		{
		}
	}

	private void PlayLoop()
	{
		byte[] array = new byte[1920];
		while (!_stop)
		{
			_player.Read(array, array.Length);
			_player.Submit(array);
		}
	}

	public static string GuessLocalIp()
	{
		try
		{
			using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			socket.Connect("8.8.8.8", 80);
			return ((IPEndPoint)socket.LocalEndPoint).Address.ToString();
		}
		catch
		{
			return "0.0.0.0";
		}
	}
}
