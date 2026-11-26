namespace YzControl;

public sealed class YzRstEntry
{
	public string Name = "";

	public string Raw = "";

	public int Crumb;

	public long UpSec;

	public long HeapMin;

	public long LoopMax;

	public long PeakCrumb;

	public string TypeText => YzFw.RstName(Name);

	public string CrumbText => YzFw.CrumbText(Crumb);
}
