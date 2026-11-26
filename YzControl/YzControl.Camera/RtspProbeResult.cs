using System.Collections.Generic;

namespace YzControl.Camera;

public sealed class RtspProbeResult
{
	public string Url = "";

	public string Path = "";

	public bool Reachable;

	public bool AuthRequired;

	public bool Authorized;

	public string Server = "";

	public string Sdp = "";

	public string Error = "";

	public List<string> Tracks = new List<string>();

	public bool Usable
	{
		get
		{
			if (Reachable)
			{
				return Authorized;
			}
			return false;
		}
	}

	public string Summary
	{
		get
		{
			if (!Reachable)
			{
				return "不可达（" + Error + "）";
			}
			if (!Authorized)
			{
				if (!AuthRequired)
				{
					return "不可用（" + Error + "）";
				}
				return "需要认证/认证失败（" + Error + "）";
			}
			return "可用 · " + string.Join(", ", Tracks.ToArray());
		}
	}
}
