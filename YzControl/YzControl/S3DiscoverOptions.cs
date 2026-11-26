namespace YzControl;

public sealed class S3DiscoverOptions
{
	public int UdpPort = 60059;

	public int TimeoutMs = 800;

	public string[] Targets;

	public string KnownIdPrefix = "1051DB3D";

	public string KnownFullId = "1051DB3D2124";

	public string SentTo = "";
}
