using System.Collections.Generic;

namespace YzControl;

public sealed class CivFramer
{
	private readonly List<byte> _buf = new List<byte>(512);

	public int MaxBuffer = 4096;

	public long NoiseBytes;

	public int Pending
	{
		get
		{
			lock (_buf)
			{
				return _buf.Count;
			}
		}
	}

	public void Clear()
	{
		lock (_buf)
		{
			_buf.Clear();
		}
	}

	public void Append(byte[] data, int offset, int count)
	{
		if (data == null || count <= 0)
		{
			return;
		}
		lock (_buf)
		{
			for (int i = 0; i < count; i++)
			{
				_buf.Add(data[offset + i]);
			}
			if (_buf.Count > MaxBuffer)
			{
				int num = _buf.Count - 1;
				_buf.RemoveRange(0, num);
				NoiseBytes += num;
			}
		}
	}

	public bool TryTake(out byte[] frame)
	{
		frame = null;
		lock (_buf)
		{
			int num = -1;
			for (int i = 0; i + 1 < _buf.Count; i++)
			{
				if (_buf[i] == 254 && _buf[i + 1] == 254)
				{
					num = i;
					break;
				}
			}
			if (num < 0)
			{
				if (_buf.Count > 1)
				{
					int num2 = _buf.Count - 1;
					_buf.RemoveRange(0, num2);
					NoiseBytes += num2;
				}
				return false;
			}
			if (num > 0)
			{
				_buf.RemoveRange(0, num);
				NoiseBytes += num;
			}
			int num3 = -1;
			for (int j = 2; j < _buf.Count; j++)
			{
				if (_buf[j] == 253)
				{
					num3 = j;
					break;
				}
			}
			if (num3 < 0)
			{
				return false;
			}
			frame = _buf.GetRange(0, num3 + 1).ToArray();
			_buf.RemoveRange(0, num3 + 1);
			return true;
		}
	}
}
