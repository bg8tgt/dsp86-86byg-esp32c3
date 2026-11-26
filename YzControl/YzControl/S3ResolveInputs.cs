using System;

namespace YzControl;

public sealed class S3ResolveInputs
{
	public string Memory = "";

	public string Manual = "";

	public Func<string, bool> Probe;

	public Func<S3DiscoverOutcome> Discover;

	public string HotspotIp = "192.168.4.1";
}
