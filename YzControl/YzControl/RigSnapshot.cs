using System;

namespace YzControl;

public sealed class RigSnapshot
{
	public bool Ok;

	public string Error = "";

	public long Hz;

	public string Mode = "--";

	public bool SplitKnown;

	public bool SplitOn;

	public long TxHz;

	public string TxSource = "";

	public int Smeter = -1;

	public bool PttKnown;

	public bool PttOn;

	public DateTime At = DateTime.MinValue;

	public string Channel = "";

	public string Log = "";

	public int RttMs;

	public bool HasTx => TxHz >= 30000;

	public RigSnapshot Copy()
	{
		return (RigSnapshot)MemberwiseClone();
	}
}
