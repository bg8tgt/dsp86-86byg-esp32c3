using System;

namespace YzControl.Camera;

[Serializable]
public class CameraEntry
{
	public string Name = "";

	public string Vendor = "hik";

	public string Ip = "";

	public int Port = 554;

	public string User = "admin";

	public string Password = "";

	public int Channel = 1;

	public bool MainStream = true;

	public string XAddr = "";

	public string Url = "";

	public CameraEntry Clone()
	{
		return (CameraEntry)MemberwiseClone();
	}

	public override string ToString()
	{
		if (!string.IsNullOrEmpty(Name))
		{
			return Name;
		}
		return Ip + " (" + Vendor + ")";
	}
}
