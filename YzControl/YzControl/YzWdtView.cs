using System.Text;

namespace YzControl;

public sealed class YzWdtView
{
	public string L1 = "";

	public string L2 = "";

	public string Boot = "";

	public string Sil = "";

	public YzSev L1Sev;

	public YzSev L2Sev;

	public YzSev BootSev;

	public YzSev SilSev;

	public bool Layer2Unarmed;

	public string Advisory = "";

	public string Block()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("【看门狗 · 第 1 层（任务看门狗 TWDT，15 s）】").Append(L1);
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("【看门狗 · 第 2 层（硬件 RTC_WDT，30 s）】").Append(L2);
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("【启动阶段 bootSt】").Append(Boot);
		stringBuilder.AppendLine();
		stringBuilder.AppendLine("【限频闸 silN / TWDT 超时计数 wto】").Append(Sil);
		if (Advisory.Length > 0)
		{
			stringBuilder.AppendLine();
			stringBuilder.AppendLine("【看门狗自测的档位前提】");
			stringBuilder.Append(Advisory);
		}
		return stringBuilder.ToString();
	}
}
