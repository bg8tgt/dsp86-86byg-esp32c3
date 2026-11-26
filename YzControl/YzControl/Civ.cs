using System;
using System.Text;

namespace YzControl;

public static class Civ
{
	public const byte FE = 254;

	public const byte FD = 253;

	public const byte CtrlAddrDefault = 224;

	public const byte RadioAddrDefault = 136;

	public static byte[] Frame(byte to, byte from, byte[] payload)
	{
		int num = ((payload != null) ? payload.Length : 0);
		byte[] array = new byte[4 + num + 1];
		array[0] = 254;
		array[1] = 254;
		array[2] = to;
		array[3] = from;
		for (int i = 0; i < num; i++)
		{
			array[4 + i] = payload[i];
		}
		array[4 + num] = 253;
		return array;
	}

	public static string Hex(byte[] b)
	{
		return Hex(b, 0, (b != null) ? b.Length : 0);
	}

	public static string Hex(byte[] b, int off, int len)
	{
		if (b == null || len <= 0)
		{
			return "";
		}
		StringBuilder stringBuilder = new StringBuilder(len * 3);
		for (int i = 0; i < len; i++)
		{
			if (i > 0)
			{
				stringBuilder.Append(' ');
			}
			stringBuilder.Append(b[off + i].ToString("X2"));
		}
		return stringBuilder.ToString();
	}

	public static byte[] ParseHex(string s)
	{
		if (s == null)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder(s.Length);
		foreach (char c in s)
		{
			if (c != ' ' && c != '-' && c != '\t' && c != '\r' && c != '\n' && c != ':')
			{
				if ((c < '0' || c > '9') && (c < 'a' || c > 'f') && (c < 'A' || c > 'F'))
				{
					return null;
				}
				stringBuilder.Append(c);
			}
		}
		if (stringBuilder.Length == 0 || ((uint)stringBuilder.Length & (true ? 1u : 0u)) != 0)
		{
			return null;
		}
		byte[] array = new byte[stringBuilder.Length / 2];
		for (int j = 0; j < array.Length; j++)
		{
			array[j] = Convert.ToByte(stringBuilder.ToString(j * 2, 2), 16);
		}
		return array;
	}

	public static bool TryBcdToHz(byte[] b, int off, out long hz)
	{
		hz = 0L;
		if (b == null || off < 0 || off + 5 > b.Length)
		{
			return false;
		}
		long num = 1L;
		for (int i = 0; i < 5; i++)
		{
			int num2 = b[off + i] & 0xF;
			int num3 = (b[off + i] >> 4) & 0xF;
			if (num2 > 9 || num3 > 9)
			{
				hz = 0L;
				return false;
			}
			hz += num2 * num + num3 * num * 10;
			if (i < 4)
			{
				num *= 100;
			}
		}
		return true;
	}

	public static long BcdToHz(byte[] b, int off)
	{
		if (!TryBcdToHz(b, off, out var hz))
		{
			return 0L;
		}
		return hz;
	}

	public static void HzToBcd(long hz, byte[] dst, int off)
	{
		long num = hz;
		for (int i = 0; i < 5; i++)
		{
			int num2 = (int)(num % 10);
			num /= 10;
			int num3 = (int)(num % 10);
			num /= 10;
			dst[off + i] = (byte)(num2 | (num3 << 4));
		}
	}

	public static bool TryBcd2(byte[] b, int off, out int v)
	{
		v = 0;
		if (b == null || off + 2 > b.Length)
		{
			return false;
		}
		int num = (b[off] >> 4) & 0xF;
		int num2 = b[off] & 0xF;
		int num3 = (b[off + 1] >> 4) & 0xF;
		int num4 = b[off + 1] & 0xF;
		if (num > 9 || num2 > 9 || num3 > 9 || num4 > 9)
		{
			return false;
		}
		if (num > 2)
		{
			return false;
		}
		v = num2 * 100 + num3 * 10 + num4;
		return true;
	}

	public static bool IsHexString(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return false;
		}
		foreach (char c in s)
		{
			if ((c < '0' || c > '9') && (c < 'a' || c > 'f') && (c < 'A' || c > 'F'))
			{
				return false;
			}
		}
		return true;
	}

	public static string ModeName(byte b)
	{
		return b switch
		{
			0 => "LSB", 
			1 => "USB", 
			2 => "AM", 
			3 => "CW", 
			4 => "RTTY", 
			5 => "FM", 
			6 => "WFM", 
			7 => "CW-R", 
			8 => "RTTY-R", 
			23 => "DV", 
			_ => "0x" + b.ToString("X2"), 
		};
	}

	public static byte ModeByte(string name)
	{
		return (name ?? "").Trim().ToUpperInvariant() switch
		{
			"LSB" => 0, 
			"USB" => 1, 
			"AM" => 2, 
			"CW" => 3, 
			"RTTY" => 4, 
			"FM" => 5, 
			"WFM" => 6, 
			"CW-R" => 7, 
			"RTTY-R" => 8, 
			"DV" => 23, 
			_ => 1, 
		};
	}
}
