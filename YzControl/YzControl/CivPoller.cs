using System;
using System.Diagnostics;
using System.Text;

namespace YzControl;

public sealed class CivPoller
{
	public CivLink Link;

	public bool Legacy1C;

	public bool DeepVfoB;

	public bool ReadSmeter;

	public bool ReadPtt1C;

	public int TimeoutMs = 900;

	public int Retries = 1;

	public CivPoller(CivLink link)
	{
		Link = link;
	}

	/// <summary>
	/// v1.6.0：轮询即心跳（直连通道每 2s 一次）。真正的读帧逻辑在 PollCore 里没动，
	/// 这里只把结果交给链路做健康计数（连续 3 次失败 → 判死 → 退避）。
	/// </summary>
	public RigSnapshot Poll(RigSnapshot prev)
	{
		RigSnapshot s = PollCore(prev);
		if (Link != null)
		{
			Link.NoteHeartbeat(s != null && s.Ok);
		}
		return s;
	}

	private RigSnapshot PollCore(RigSnapshot prev)
	{
		RigSnapshot rigSnapshot = new RigSnapshot();
		rigSnapshot.At = DateTime.Now;
		rigSnapshot.Channel = ((Link == null) ? "未连接" : Link.Describe());
		StringBuilder stringBuilder = new StringBuilder();
		Stopwatch stopwatch = Stopwatch.StartNew();
		if (Link == null || !Link.IsOpen)
		{
			rigSnapshot.Ok = false;
			rigSnapshot.Error = "直连链路未建立";
			rigSnapshot.Log = stringBuilder.ToString();
			return rigSnapshot;
		}
		CivReply civReply = Link.Request(new byte[1] { 3 }, TimeoutMs, Retries);
		stringBuilder.Append("03→").Append((civReply == null) ? "(无应答)" : civReply.Hex).Append("  ");
		if (civReply == null)
		{
			rigSnapshot.Ok = false;
			rigSnapshot.Error = "03 读频率失败：" + Link.LastError;
			rigSnapshot.Log = stringBuilder.ToString();
			return rigSnapshot;
		}
		if (civReply.IsNg)
		{
			rigSnapshot.Ok = false;
			rigSnapshot.Error = "03 被电台拒绝（NG）";
			rigSnapshot.Log = stringBuilder.ToString();
			return rigSnapshot;
		}
		if (!civReply.TryFreq(out var hz) || hz < 1000)
		{
			rigSnapshot.Ok = false;
			rigSnapshot.Error = "03 回帧无法解析为频率（数据段 " + Civ.Hex(civReply.Data) + "）";
			rigSnapshot.Log = stringBuilder.ToString();
			return rigSnapshot;
		}
		rigSnapshot.Hz = hz;
		CivReply civReply2 = Link.Request(new byte[1] { 4 }, TimeoutMs, Retries);
		stringBuilder.Append("04→").Append((civReply2 == null) ? "(无应答)" : civReply2.Hex).Append("  ");
		if (civReply2 != null && !civReply2.IsNg && civReply2.Data.Length >= 1)
		{
			rigSnapshot.Mode = Civ.ModeName(civReply2.Data[0]);
		}
		else
		{
			rigSnapshot.Mode = ((prev != null) ? prev.Mode : "--");
		}
		if (Abort(Link, rigSnapshot, stringBuilder))
		{
			return rigSnapshot;
		}
		CivReply civReply3 = Link.Request(new byte[1] { 15 }, TimeoutMs, Retries);
		stringBuilder.Append("0F→").Append((civReply3 == null) ? "(无应答)" : civReply3.Hex).Append("  ");
		if (civReply3 != null && !civReply3.IsNg && civReply3.Data.Length >= 1)
		{
			rigSnapshot.SplitKnown = true;
			rigSnapshot.SplitOn = (civReply3.Data[0] & 1) != 0;
		}
		else if (prev != null && prev.SplitKnown)
		{
			rigSnapshot.SplitKnown = true;
			rigSnapshot.SplitOn = prev.SplitOn;
		}
		if (Abort(Link, rigSnapshot, stringBuilder))
		{
			return rigSnapshot;
		}
		if (rigSnapshot.SplitOn || !rigSnapshot.SplitKnown || Legacy1C)
		{
			long hz2;
			string note;
			string source;
			if (Legacy1C)
			{
				if (ProbeLegacy1C(out hz2, out note))
				{
					rigSnapshot.TxHz = hz2;
					rigSnapshot.TxSource = "1C 兼容";
					stringBuilder.Append(note).Append("  ");
				}
				else
				{
					stringBuilder.Append("1C 兼容探测：").Append(note).Append(" → 自动回退 25 00；  ");
					if (TryTx2500(out hz2, out source, stringBuilder))
					{
						rigSnapshot.TxHz = hz2;
						rigSnapshot.TxSource = source;
					}
				}
			}
			else if (TryTx2500(out hz2, out source, stringBuilder))
			{
				rigSnapshot.TxHz = hz2;
				rigSnapshot.TxSource = source;
			}
			if (rigSnapshot.TxHz < 30000 && DeepVfoB && TryTxDeepVfoB(rigSnapshot.Hz, out hz2, out note, stringBuilder))
			{
				rigSnapshot.TxHz = hz2;
				rigSnapshot.TxSource = "07 01+03(已还原)";
			}
			if (rigSnapshot.TxHz < 30000 && prev != null && prev.HasTx && prev.SplitOn == rigSnapshot.SplitOn)
			{
				rigSnapshot.TxHz = prev.TxHz;
				rigSnapshot.TxSource = (string.IsNullOrEmpty(prev.TxSource) ? "上次" : prev.TxSource) + "(沿用)";
			}
		}
		if (ReadSmeter)
		{
			CivReply civReply4 = Link.Request(new byte[2] { 21, 2 }, TimeoutMs, Retries);
			stringBuilder.Append("15 02→").Append((civReply4 == null) ? "(无应答)" : civReply4.Hex).Append("  ");
			if (civReply4 != null && !civReply4.IsNg && civReply4.TryValue2(out var v))
			{
				rigSnapshot.Smeter = v;
			}
		}
		if (ReadPtt1C)
		{
			CivReply civReply5 = Link.Request(new byte[2] { 28, 0 }, TimeoutMs, Retries);
			stringBuilder.Append("1C 00→").Append((civReply5 == null) ? "(无应答)" : civReply5.Hex).Append("  ");
			if (civReply5 != null && !civReply5.IsNg && civReply5.Data.Length >= 2)
			{
				rigSnapshot.PttKnown = true;
				rigSnapshot.PttOn = civReply5.Data[1] != 0;
			}
		}
		rigSnapshot.RttMs = (int)stopwatch.ElapsedMilliseconds;
		rigSnapshot.Ok = true;
		rigSnapshot.Log = stringBuilder.ToString();
		return rigSnapshot;
	}

	private static bool Abort(CivLink link, RigSnapshot s, StringBuilder log)
	{
		if (link != null && link.IsOpen)
		{
			return false;
		}
		s.Ok = false;
		s.Error = "直连链路已断开：" + ((link == null) ? "无链路" : link.LastError);
		s.Log = ((log == null) ? "" : log.ToString());
		return true;
	}

	public bool TryTx2500(out long hz, out string source, StringBuilder log)
	{
		hz = 0L;
		source = "";
		CivReply civReply = Link.Request(new byte[2] { 37, 0 }, TimeoutMs, Retries);
		log?.Append("25 00→").Append((civReply == null) ? "(无应答)" : civReply.Hex).Append("  ");
		if (civReply == null)
		{
			return false;
		}
		if (civReply.IsNg)
		{
			log?.Append("25 00 被拒(NG)；  ");
			return false;
		}
		if (civReply.Data.Length < 6)
		{
			log?.Append("25 00 回帧残缺(").Append(civReply.Data.Length).Append(" 字节)；  ");
			return false;
		}
		if (!Civ.TryBcdToHz(civReply.Data, 1, out var hz2) || hz2 < 30000)
		{
			return false;
		}
		hz = hz2;
		source = "25 00";
		return true;
	}

	public bool ProbeLegacy1C(out long hz, out string note)
	{
		hz = 0L;
		note = "";
		CivReply civReply = Link.Request(new byte[2] { 28, 1 }, TimeoutMs, Retries);
		if (civReply == null)
		{
			note = "无应答（" + Link.LastError + "）";
			return false;
		}
		if (civReply.IsNg)
		{
			note = "回 NG(FA) —— 该机不接受 1C 01";
			return false;
		}
		if (civReply.IsAck)
		{
			note = "只回 OK(FB)，无频率数据";
			return false;
		}
		if (civReply.Raw.Length < 12 || civReply.Data.Length < 6)
		{
			note = "★残缺帧（" + civReply.Raw.Length + " 字节：" + civReply.Hex + "）";
			return false;
		}
		if (!Civ.TryBcdToHz(civReply.Data, 1, out var hz2) || hz2 < 30000)
		{
			note = "★数据段非频率（" + Civ.Hex(civReply.Data) + "）";
			return false;
		}
		hz = hz2;
		note = "命中 12 字节完整帧 " + civReply.Hex;
		return true;
	}

	public bool TryTxDeepVfoB(long rxHzBefore, out long hz, out string note, StringBuilder log)
	{
		hz = 0L;
		note = "";
		CivReply civReply = Link.RequestAck(new byte[2] { 7, 1 }, TimeoutMs, Retries);
		log?.Append("07 01→").Append((civReply == null) ? "(无应答)" : civReply.Hex).Append("  ");
		if (civReply == null || civReply.IsNg)
		{
			note = "切 VFO-B 失败（" + ((civReply == null) ? Link.LastError : "NG") + "）";
			RestoreVfoA(rxHzBefore, note, log);
			return false;
		}
		CivReply civReply2 = Link.Request(new byte[1] { 3 }, TimeoutMs, Retries);
		log?.Append("03(VFO-B)→").Append((civReply2 == null) ? "(无应答)" : civReply2.Hex).Append("  ");
		long hz2 = 0L;
		bool num = civReply2 != null && !civReply2.IsNg && civReply2.TryFreq(out hz2) && hz2 >= 30000;
		string text = RestoreVfoA(rxHzBefore, note, log);
		if (!num)
		{
			note = "VFO-B 频率读取失败；" + text;
			return false;
		}
		hz = hz2;
		note = "读到 VFO-B " + ((double)hz2 / 1000000.0).ToString("0.00000") + " MHz；" + text;
		return true;
	}

	private string RestoreVfoA(long rxHzBefore, string note, StringBuilder log)
	{
		CivReply civReply = Link.RequestAck(new byte[2] { 7, 0 }, TimeoutMs, Retries);
		log?.Append("07 00(还原)→").Append((civReply == null) ? "(无应答)" : civReply.Hex).Append("  ");
		CivReply civReply2 = Link.Request(new byte[1] { 3 }, TimeoutMs, Retries);
		log?.Append("03(校验)→").Append((civReply2 == null) ? "(无应答)" : civReply2.Hex).Append("  ");
		if (civReply2 != null && !civReply2.IsNg && civReply2.TryFreq(out var hz))
		{
			if (rxHzBefore <= 0 || Math.Abs(hz - rxHzBefore) <= 5000)
			{
				return "已还原 VFO-A（复读 " + ((double)hz / 1000000.0).ToString("0.00000") + " MHz ✓）";
			}
			return "⚠ 还原后复读 " + ((double)hz / 1000000.0).ToString("0.00000") + " MHz ≠ 原值 " + ((double)rxHzBefore / 1000000.0).ToString("0.00000") + " MHz";
		}
		return "⚠ 还原命令已发但复读失败（" + Link.LastError + "）";
	}
}
