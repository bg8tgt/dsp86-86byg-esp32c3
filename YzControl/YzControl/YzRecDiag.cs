namespace YzControl;

public static class YzRecDiag
{
	public static string Text(int rec, int recrst, long recn)
	{
		string text = ((recn < 0) ? "" : ("；累计进入待命 " + recn + " 次"));
		if (rec == 1)
		{
			return $"⚠ 云台处于「复位恢复待命」（rec=1）：复位原因 {YzFw.RecRstName(recrst)}{text}。待命期间双轴不动，发一条运动指令即解除（本程序请点「解除待命」）。";
		}
		return $"正常（rec=0）：无运行中意外复位残留{text}";
	}
}
