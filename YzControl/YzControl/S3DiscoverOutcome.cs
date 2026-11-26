using System.Collections.Generic;

namespace YzControl;

public sealed class S3DiscoverOutcome
{
	public string Ip = "";

	public string Id = "";

	public string Port = "";

	public bool IdMatched;

	public string Detail = "";

	public readonly List<S3Pong> All = new List<S3Pong>();

	public bool Found => S3Discovery.IsUsableIp(Ip);
}
