namespace YzControl;

public sealed class YzHangStage
{
	public string Key = "";

	public string Title = "";

	public int Seconds;

	public bool NoInt;

	public int Repeat = 1;

	public int ExpectResetAfterSec;

	public string ExpectText = "";

	public string Query => "/set?hang=" + Seconds + "&noint=" + (NoInt ? 1 : 0);
}
