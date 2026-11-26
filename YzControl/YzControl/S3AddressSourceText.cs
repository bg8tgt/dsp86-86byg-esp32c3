namespace YzControl;

public static class S3AddressSourceText
{
	public static string Short(S3AddressSource s)
	{
		return s switch
		{
			S3AddressSource.Memory => "记忆", 
			S3AddressSource.Udp => "发现", 
			S3AddressSource.Manual => "手填", 
			S3AddressSource.Hotspot => "兜底", 
			S3AddressSource.Probe => "探测", 
			_ => "未知", 
		};
	}
}
