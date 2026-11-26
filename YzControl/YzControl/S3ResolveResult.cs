namespace YzControl;

public sealed class S3ResolveResult
{
	public string Ip = "";

	public S3AddressSource Source;

	public string Detail = "";

	public bool ChangedFromMemory;

	public bool MemoryProbeFailed;

	public bool UdpAttempted;

	public int Responders;

	public string Hint = "";

	public override string ToString()
	{
		return Ip + "（" + S3AddressSourceText.Short(Source) + "）" + ((Detail.Length > 0) ? (" · " + Detail) : "");
	}
}
