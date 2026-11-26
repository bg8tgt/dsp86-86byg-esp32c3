using System;
using System.Collections.Generic;

namespace YzControl.Camera;

[Serializable]
public class CameraConfig
{
	public List<CameraEntry> Cameras = new List<CameraEntry>();

	public int Pane1 = -1;

	public int Pane2 = -1;

	public string S3Host = "172.16.1.122";

	public string S3HostSource = "";

	public string S3HostSeen = "";

	public string DirectIpSource = "";

	public string DirectIpSeen = "";

	public bool AutoTakePriority;

	public string DirectLink = "tcp";

	public string DirectCom = "COM2";

	public int DirectBaud = 19200;

	public string DirectCivAddr = "88";

	public string RigChannel = "direct";

	public string DirectIp = "172.16.1.123";

	public int DirectPort = 4001;

	public int CivPollMs = 2000;

	public bool CivPaused;

	public bool CivLegacy1C;

	public bool CivDeepVfoB;

	public bool CivReadSmeter;
}
