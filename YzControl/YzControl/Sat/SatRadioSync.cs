// ============================================================================
//  YzControl · Sat/SatRadioSync.cs —— 多普勒 → 电台（CI-V）闭环同步
//
//  设计要点（针对「收发频率与多普勒不一致 / 不同步」的根因修复）：
//   1) 单一真值：每拍只由同一个距离率算一次 RX/TX，两个 VFO 同源下发，
//      不再出现「RX 更新了、TX 用旧缓存」的情况；
//   2) 写后不算完：缓存只在**确认写入成功**后才更新（旧实现先改缓存后写，
//      写失败就永久停更）；
//   3) 周期回读自愈：每 10 s 读回两个 VFO 实际频率，偏差 > 200 Hz 就整组重下，
//      界面同时显示「目标 / 实际」，不一致一眼可见；
//   4) 写 TX 不切显示：优先 25 01（写未选中 VFO）；该机不认时回退
//      「07 切 TX → 05 写 → 07 切回 RX」，且回退后立即复位显示 VFO。
//  机型：IC-7100（0x88，A=RX/B=TX，06 单字节模式，1B 03 亚音）
//        IC-705 （0xA4，A=TX/B=RX，06+01，1B 00/1B 01 + 16 42/16 43）
// ============================================================================
using System;
using System.Globalization;
using System.Text;
using System.Threading;

namespace YzControl.Sat
{
    public enum RigKind { Unknown = 0, Ic7100 = 1, Ic705 = 2 }

    /// <summary>一次下发的完整电台计划（频率均为多普勒修正后的绝对值）。</summary>
    public sealed class RigPlan
    {
        public long RxHz, TxHz;
        public string RxMode = "FM", TxMode = "FM";
        public double TxTone, RxTone;
        public bool Split;
        public string SatName = "";

        public string Key()
        {
            return string.Join("|", new[]
            {
                Split ? "1" : "0", RxMode ?? "", TxMode ?? "",
                TxTone.ToString("0.0", CultureInfo.InvariantCulture),
                RxTone.ToString("0.0", CultureInfo.InvariantCulture)
            });
        }

        public override string ToString()
        {
            return "RX " + MHz(RxHz) + " " + RxMode + " / TX " + (TxHz > 0 ? MHz(TxHz) + " " + TxMode : "单频");
        }

        public static string MHz(long hz)
        {
            return (hz / 1e6).ToString("0.000000", CultureInfo.InvariantCulture) + " MHz";
        }
    }

    public sealed class SatRadioSync
    {
        public bool AutoTune = true;            // 自动按多普勒设频
        public bool SyncTones = true;           // 同步 CTCSS/TSQL
        public int MinGapMs = 500;              // 两次下发最小间隔
        public long MinStepHz = 15;             // 小于该变化不下发
        public RigKind Kind = RigKind.Ic7100;

        public long AppliedRx, AppliedTx;       // 已确认下发的值
        public long ActualRx, ActualTx;         // 最近一次回读值
        public DateTime ActualAt = DateTime.MinValue;
        public string LastNote = "";
        public bool LastOk = true;

        private CivLink _link;
        private string _sig = "";
        private bool _splitOn;
        private bool _use25 = true;             // 25 01 是否可用
        private DateTime _lastWrite = DateTime.MinValue;
        private DateTime _lastVerify = DateTime.MinValue;
        private readonly object _gate = new object();

        private static readonly int[] ModeCodes = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 0x17 };
        private static readonly string[] ModeNames = { "LSB", "USB", "AM", "CW", "RTTY", "FM", "WFM", "CW-R", "RTTY-R", "DV" };

        public void Attach(CivLink link, RigKind kind)
        {
            lock (_gate) { _link = link; Kind = kind; _sig = ""; _splitOn = false; _use25 = true; }
        }

        public bool Ready { get { return _link != null && _link.IsOpen; } }

        public void Reset()
        {
            lock (_gate) { _sig = ""; AppliedRx = AppliedTx = 0; }
        }

        /// <summary>按计划同步（内部限速、增量、自愈）。返回是否真正下发了写操作。</summary>
        public bool Apply(RigPlan plan)
        {
            if (plan == null || !AutoTune) return false;
            lock (_gate)
            {
                if (_link == null || !_link.IsOpen) { LastOk = false; LastNote = "电台未连接"; return false; }
                DateTime now = DateTime.UtcNow;

                // 结构变化（SPLIT/模式/亚音）→ 整组重配一次
                string sig = plan.Key();
                bool structureChanged = sig != _sig;
                if (structureChanged)
                {
                    FullApply(plan);
                    _sig = sig;
                    _lastWrite = now;
                    return true;
                }

                long drx = Math.Abs(plan.RxHz - AppliedRx);
                long dtx = plan.TxHz > 0 ? Math.Abs(plan.TxHz - AppliedTx) : 0;
                bool needRx = drx >= MinStepHz || AppliedRx == 0;
                bool needTx = plan.Split && plan.TxHz > 0 && (dtx >= MinStepHz || AppliedTx == 0);
                if (!needRx && !needTx) { VerifyIfDue(plan, now, force: false); return false; }
                if ((now - _lastWrite).TotalMilliseconds < MinGapMs) return false;

                bool okRx = true, okTx = true;
                // ---- RX：写选中 VFO（IC-7100=A，IC-705=B）
                if (needRx && plan.RxHz > 0)
                {
                    SelectVfo(false);
                    okRx = WriteFreq(plan.RxHz);
                    if (okRx) AppliedRx = plan.RxHz;
                }
                // ---- TX：优先写未选中 VFO（不打断接收显示）
                if (needTx)
                {
                    okTx = false;
                    if (_use25)
                    {
                        okTx = WriteUnselected(plan.TxHz);
                        if (!okTx) { _use25 = false; LastNote = "该机不接受 25 01，改用切换 VFO 写 TX"; }
                    }
                    if (!okTx)
                    {
                        // 回退：切到 TX VFO 写 → 立刻切回 RX 并复位频率显示
                        SelectVfo(true);
                        okTx = WriteFreq(plan.TxHz);
                        WriteMode(plan.TxMode, true);
                        SelectVfo(false);
                        if (okTx) WriteFreq(plan.RxHz);
                    }
                    if (okTx) AppliedTx = plan.TxHz;
                }
                _lastWrite = now;
                LastOk = okRx && okTx;
                LastNote = "下发 " + (needRx ? "RX " + RigPlan.MHz(plan.RxHz) : "") +
                           (needRx && needTx ? " · " : "") +
                           (needTx ? "TX " + RigPlan.MHz(plan.TxHz) : "") + (LastOk ? " ✓" : " ✗");
                VerifyIfDue(plan, now, force: false);
                return okRx || okTx;
            }
        }

        /// <summary>整组配置：频率/模式/亚音/SPLIT，最后回到接收 VFO。</summary>
        public void FullApply(RigPlan plan)
        {
            if (plan == null || _link == null || !_link.IsOpen) return;
            bool wantSplit = plan.Split && plan.TxHz > 0 && Math.Abs(plan.TxHz - plan.RxHz) > 1;

            // 先写 RX VFO（7100=A / 705=B）
            SelectVfo(false);
            AppliedRx = WriteFreq(plan.RxHz) ? plan.RxHz : 0;
            WriteMode(plan.RxMode, false);

            if (wantSplit)
            {
                if (_use25)
                {
                    if (WriteUnselected(plan.TxHz)) { AppliedTx = plan.TxHz; }
                    else { _use25 = false; }
                }
                if (!_use25 || AppliedTx != plan.TxHz)
                {
                    SelectVfo(true);
                    AppliedTx = WriteFreq(plan.TxHz) ? plan.TxHz : 0;
                    WriteMode(plan.TxMode, true);
                    if (SyncTones) WriteTone(plan.TxTone, true);
                    SelectVfo(false);
                    WriteFreq(plan.RxHz);         // 复位显示 VFO 的频率
                    WriteMode(plan.RxMode, false);
                }
                SetSplit(true);
            }
            else
            {
                AppliedTx = 0;
                SetSplit(false);
            }
            if (SyncTones) WriteTone(plan.RxTone, false);
            _splitOn = wantSplit;
            LastNote = "整组配置 " + plan + (wantSplit ? " · SPLIT" : "");
        }

        // ------------------------------------------------------------ 回读自愈
        public void VerifyIfDue(RigPlan plan, DateTime now, bool force)
        {
            if (plan == null) return;
            if (!force && (now - _lastVerify).TotalSeconds < 10) return;
            _lastVerify = now;
            long rx = ReadFreqSelected();
            long tx = ReadFreqUnselected();
            if (rx > 0) ActualRx = rx;
            if (tx > 0) ActualTx = tx;
            ActualAt = now;
            long erx = (rx > 0 && plan.RxHz > 0) ? Math.Abs(rx - plan.RxHz) : 0;
            long etx = (tx > 0 && plan.TxHz > 0) ? Math.Abs(tx - plan.TxHz) : 0;
            if (erx > 200 || etx > 200)
            {
                // 实际与目标脱节 → 整组重下（自愈），并记录偏差
                LastNote = "回读不一致（RX 偏 " + erx + " Hz / TX 偏 " + etx + " Hz）→ 重新下发";
                _sig = "";
                FullApply(plan);
                _sig = plan.Key();
                LastOk = true;
            }
        }

        // ------------------------------------------------------------ CI-V 原语
        private bool WriteFreq(long hz)
        {
            byte[] p = new byte[6];
            p[0] = 0x05;
            Bcd(hz, p, 1);
            return Send(p, ack: true);
        }

        private bool WriteUnselected(long hz)
        {
            byte[] p = new byte[7];
            p[0] = 0x25; p[1] = 0x01;
            Bcd(hz, p, 2);
            CivReply r = SendCmd(p, ack: true);
            // 有的机只回 FB；有的直接无应答但已执行 —— 用回读判定
            if (r != null) return true;
            long back = ReadFreqUnselected();
            return back > 0 && Math.Abs(back - hz) < 300;
        }

        private bool WriteMode(string mode, bool txVfo)
        {
            int code = CodeOf(mode);
            byte[] p = Kind == RigKind.Ic7100 ? new byte[] { 0x06, (byte)code } : new byte[] { 0x06, (byte)code, 0x01 };
            return Send(p, ack: true);
        }

        private bool WriteTone(double toneHz, bool txVfo)
        {
            int v = (int)Math.Round(toneHz * 10.0);
            if (v < 0) v = 0;
            if (v > 9999) v = 9999;
            if (Kind == RigKind.Ic7100)
            {
                if (v == 0) return true;
                // 1B 03 + 2 字节 BCD（小端）
                byte lo = (byte)((v % 10) | ((v / 10 % 10) << 4));
                byte hi = (byte)((v / 100 % 10) | ((v / 1000 % 10) << 4));
                return Send(new byte[] { 0x1B, 0x03, lo, hi }, ack: true);
            }
            // IC-705：1B 00=TX 亚音频率，1B 01=RX 亚音频率；16 42/16 43 开关
            string s = v.ToString("0000", CultureInfo.InvariantCulture);
            byte b1 = (byte)Convert.ToInt32(s.Substring(0, 2), 16);
            byte b2 = (byte)Convert.ToInt32(s.Substring(2, 2), 16);
            bool ok = Send(new byte[] { 0x1B, (byte)(txVfo ? 0x00 : 0x01), 0x00, b1, b2 }, ack: true);
            ok &= Send(new byte[] { 0x16, (byte)(txVfo ? 0x42 : 0x43), (byte)(v > 0 ? 0x01 : 0x00) }, ack: true);
            return ok;
        }

        private void SetSplit(bool on)
        {
            Send(new byte[] { 0x0F, (byte)(on ? 0x01 : 0x00) }, ack: true);
        }

        /// <summary>选择 VFO：sub=true → TX VFO（7100=B / 705=A）。</summary>
        private void SelectVfo(bool sub)
        {
            // IC-7100：00=A(RX) 01=B(TX)；IC-705：00=A(TX) 01=B(RX)
            bool wantB = Kind == RigKind.Ic705 ? !sub : sub;
            Send(new byte[] { 0x07, (byte)(wantB ? 0x01 : 0x00) }, ack: true);
            Thread.Sleep(20);
        }

        private long ReadFreqSelected()
        {
            CivReply r = SendCmd(new byte[] { 0x03 }, ack: false);
            if (r == null || r.IsNg || r.Data == null || r.Data.Length < 5) return 0;
            return BcdToHz(r.Data, 0);
        }

        private long ReadFreqUnselected()
        {
            CivReply r = SendCmd(new byte[] { 0x25, 0x01 }, ack: false);
            if (r == null || r.IsNg || r.Data == null || r.Data.Length < 6) return 0;
            return BcdToHz(r.Data, 1);
        }

        private bool Send(byte[] payload, bool ack)
        {
            return SendCmd(payload, ack) != null;
        }

        private CivReply SendCmd(byte[] payload, bool ack)
        {
            try
            {
                CivLink l = _link;
                if (l == null || !l.IsOpen) return null;
                return ack ? l.RequestAck(payload, l.DefaultTimeoutMs, 0) : l.Request(payload, l.DefaultTimeoutMs, 0);
            }
            catch { return null; }
        }

        private static void Bcd(long hz, byte[] dst, int off)
        {
            string s = Math.Abs(hz).ToString("0000000000", CultureInfo.InvariantCulture);
            for (int i = 0; i < 5; i++)
            {
                int hi = s[8 - i * 2] - '0';
                int lo = s[9 - i * 2] - '0';
                dst[off + i] = (byte)((hi << 4) | lo);
            }
        }

        private static long BcdToHz(byte[] d, int off)
        {
            if (d == null || d.Length < off + 5) return 0;
            long v = 0;
            for (int i = 4; i >= 0; i--)
            {
                int b = d[off + i];
                int hi = (b >> 4) & 0x0F, lo = b & 0x0F;
                if (hi > 9 || lo > 9) return 0;
                v = v * 100 + hi * 10 + lo;
            }
            return v;
        }

        private static int CodeOf(string mode)
        {
            string m = (mode ?? "FM").ToUpperInvariant().Replace(" ", "");
            for (int i = 0; i < ModeNames.Length; i++) if (ModeNames[i] == m) return ModeCodes[i];
            if (m == "PKTUSB") return 0x01;
            if (m == "PKTLSB") return 0x00;
            if (m == "NFM" || m == "FMN") return 0x05;
            if (m == "SSB") return 0x01;
            if (m == "CWR") return 0x07;
            return 0x05;
        }

        public static string ModeNameOf(int code)
        {
            for (int i = 0; i < ModeCodes.Length; i++) if (ModeCodes[i] == code) return ModeNames[i];
            return "--";
        }

        public string StatusText()
        {
            var sb = new StringBuilder();
            sb.Append("目标 RX ").Append(RigPlan.MHz(AppliedRx));
            if (AppliedTx > 0) sb.Append(" / TX ").Append(RigPlan.MHz(AppliedTx));
            if (ActualAt != DateTime.MinValue)
            {
                sb.Append("   实际 RX ").Append(RigPlan.MHz(ActualRx));
                if (ActualTx > 0) sb.Append(" / TX ").Append(RigPlan.MHz(ActualTx));
            }
            return sb.ToString();
        }
    }
}
