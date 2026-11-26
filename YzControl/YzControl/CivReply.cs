namespace YzControl;

public sealed class CivReply
{
	public byte[] Raw;

	public byte[] Data;

	public byte Cmd;

	public int RttMs;

	public int Attempts = 1;

	public bool IsAck => Cmd == 251;

	public bool IsNg => Cmd == 250;

	public string Hex => Civ.Hex(Raw);

	public bool TryFreq(out long hz)
	{
		return Civ.TryBcdToHz(Data, 0, out hz);
	}

	public bool TryFreqAt(int off, out long hz)
	{
		return Civ.TryBcdToHz(Data, off, out hz);
	}

	public bool TryValue2(out int v)
	{
		return Civ.TryBcd2(Data, 1, out v);
	}

	public bool TryValue2At(int off, out int v)
	{
		return Civ.TryBcd2(Data, off, out v);
	}

	public override string ToString()
	{
		return Hex;
	}
}
