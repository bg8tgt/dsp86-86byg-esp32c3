namespace YzControl;

public sealed class S3Pong
{
	public string SourceIp = "";

	public string ReportedIp = "";

	public string Id = "";

	public string Port = "";

	public bool IdMatched;

	public string Raw = "";

	public string BestIp
	{
		get
		{
			if (!S3Discovery.IsUsableIp(SourceIp))
			{
				if (!S3Discovery.IsUsableIp(ReportedIp))
				{
					return "";
				}
				return ReportedIp;
			}
			return SourceIp;
		}
	}

	public override string ToString()
	{
		return SourceIp + " id=" + Id + (IdMatched ? "(本机板子)" : "(未匹配)") + ((ReportedIp.Length > 0 && ReportedIp != SourceIp) ? (" ip=" + ReportedIp) : "");
	}
}
