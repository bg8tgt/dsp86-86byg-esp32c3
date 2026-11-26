// ============================================================================
//  YzControl · Sat/TrackerEngine.cs —— 追踪调度（云台 + 多普勒，同一拍同源）
//  · 2 Hz 计算 Az/El 与距离率；云台下发给 S3Client.Goto（0~355 线性，不 wrap）
//  · 死区 0.35°、命令间隔 ≥1.2 s、失败退避 —— 避免抖台与刷屏
//  · 多普勒：同一个距离率算 RX/TX，交给 SatRadioSync（闭环回读）
//  · 任何手动云台命令 → RequestManualStop() 立即停跟踪（防两边抢控制权）
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace YzControl.Sat
{
    public sealed class EngineSnapshot
    {
        public bool Running;
        public string SatName = "";
        public double TargetAz, TargetEl;
        public double ActualAz, ActualEl;
        public double RangeKm, RangeRateKmS;
        public long PlanRxHz, PlanTxHz;
        public double RxShiftHz, TxShiftHz;
        public string RxMode = "FM", TxMode = "FM";
        public double TxTone, RxTone;
        public string Note = "";
        public DateTime LosUtc = DateTime.MinValue;
        public DateTime AosUtc = DateTime.MinValue;
        public bool RadioOk;
        public string RadioText = "";
        public int PassCount;

        // ---- 指向误差（球面角距，单位度）----
        public double ErrDeg = double.NaN;       // 当前指向误差
        public double MaxErrDeg;                 // 本轮跟踪最大误差（峰值）
        public double AzErrDeg, ElErrDeg;        // 分轴误差（显示用）
        public bool BoardFresh;                  // 实际指向是否来自云台实时回执
        public int ErrOkCount, ErrBadCount;      // 误差达标/超差拍数（验收统计）
    }

    public sealed class TrackerEngine : IDisposable
    {
        private static TrackerEngine _inst;
        private static readonly object _instGate = new object();

        /// <summary>全局唯一引擎（UI 与 S3Client 的「手动即停」都走它）。</summary>
        public static TrackerEngine I
        {
            get
            {
                lock (_instGate)
                {
                    if (_inst == null) _inst = new TrackerEngine();
                    return _inst;
                }
            }
        }

        public readonly SatRadioSync Radio = new SatRadioSync();

        public event Action<EngineSnapshot> Updated;      // 每拍（已切到 UI 线程由页面负责）
        public event Action<string> Log;

        public S3Client S3;
        public bool DopplerOn = true;
        public double DeadbandDeg = 0.30;        // 目标增量死区（无回执时的判据）
        public double AimToleranceDeg = 0.45;    // 有云台回执时：误差超过它就重新瞄准
        public double MaxErrDeg = 1.0;           // 验收线：指向误差必须 < 1°
        public int MinGotoGapMs = 800;
        public int LeadMs = 400;                 // 前视补偿：瞄「现在+400ms」的位置，抵消 HTTP 与转动延迟
        public int ForceGapMs = 600;             // 超差强制纠偏的最小间隔

        private readonly object _gate = new object();
        private Thread _th;
        private volatile bool _run;
        private SatEntry _sat;
        private PassInfo _pass;
        private RigPlan _plan;
        private DateTime _lastGoto = DateTime.MinValue;
        private DateTime _lastLog = DateTime.MinValue;
        private int _failStreak;
        private double _lastAz = double.NaN, _lastEl = double.NaN;
        private double _maxErr;
        private int _okCount, _badCount;
        private bool _errBad;

        public EngineSnapshot Last = new EngineSnapshot();

        // 主板 /st 推送的真实指向（MainForm 主轮询复用，不额外打 HTTP）
        public double BoardAz = double.NaN, BoardEl = double.NaN;
        public DateTime BoardAt = DateTime.MinValue;
        public int BoardTrackOk;

        public bool Running { get { return _run; } }
        public string SatName { get { var s = _sat; return s == null ? "" : s.Name; } }

        private TrackerEngine() { }

        public void Start(SatEntry sat, PassInfo pass, S3Client s3)
        {
            if (sat == null || sat.Model == null) { Note("该卫星没有可用星历（深空目标暂不支持）"); return; }
            lock (_gate)
            {
                _sat = sat;
                _pass = pass;
                S3 = s3 ?? S3;
                _plan = null;
                _failStreak = 0;
                _lastGoto = DateTime.MinValue;
                _maxErr = 0; _okCount = 0; _badCount = 0; _errBad = false;
                _lastAz = double.NaN; _lastEl = double.NaN;
                Radio.Reset();
                if (_run) return;
                _run = true;
                _th = new Thread(Loop);
                _th.IsBackground = true;
                _th.Name = "SatTracker";
                _th.Start();
            }
            Note("开始跟踪 " + sat.Name + (pass != null ? "（LOS " + pass.LosUtc.ToLocalTime().ToString("HH:mm:ss") + "）" : ""));
        }

        public void Stop(string reason)
        {
            bool was;
            lock (_gate) { was = _run; _run = false; }
            if (was) Note("停止跟踪：" + (reason ?? ""));
            Last.Running = false;
            Raised();
        }

        /// <summary>手动即停（云台手动/定向命令发出前调用）。</summary>
        public bool RequestManualStop(string reason)
        {
            if (!Running) return false;
            Stop("手动操作（" + reason + "）");
            return true;
        }

        private void Loop()
        {
            while (_run)
            {
                try
                {
                    Tick();
                }
                catch (Exception ex)
                {
                    Note("跟踪异常：" + ex.Message);
                }
                for (int i = 0; i < 5 && _run; i++) Thread.Sleep(100);   // 2 Hz
            }
        }

        private void Tick()
        {
            SatEntry sat;
            lock (_gate) sat = _sat;
            if (sat == null || sat.Model == null) { Stop("无卫星"); return; }

            var store = SatStore.I;
            DateTime now = DateTime.UtcNow;
            LookAngle la;
            if (!PassPredictor.Look(sat.Model, store.Station, now, out la))
            {
                Note("递推失败（err " + sat.Model.Error + "）");
                Stop("星历递推失败");
                return;
            }

            // 过境结束自动停
            if (la.ElDeg < -1.0 && _pass != null && now > _pass.LosUtc)
            {
                Stop("过境结束（LOS）");
                return;
            }

            // ---- 多普勒：同一个距离率算 RX / TX（单一真值）
            RigPlan plan = BuildPlan(sat, la, now);
            _plan = plan;

            // ---- 云台：按「实际指向」闭环纠偏（有回执时），无回执时退化为目标增量
            double az = ((la.AzDeg % 360) + 360) % 360;
            bool reachable = az <= 355.0;
            DateTime t = DateTime.UtcNow;
            bool fresh = (t - BoardAt).TotalSeconds < 3.0 && !double.IsNaN(BoardAz);
            double actualAz = fresh ? BoardAz : _lastAz;
            double actualEl = fresh ? BoardEl : _lastEl;

            double sep = SepDeg(az, la.ElDeg, actualAz, actualEl);
            double eAz = AngDiff(az, actualAz);
            double eEl = double.IsNaN(actualEl) ? double.NaN : Math.Abs(la.ElDeg - actualEl);
            if (!double.IsNaN(sep))
            {
                if (sep > _maxErr) _maxErr = sep;
                if (sep <= MaxErrDeg) _okCount++; else _badCount++;
            }

            bool aim;
            if (sep > MaxErrDeg) aim = true;                     // 超验收线：强制纠偏
            else if (fresh) aim = sep > AimToleranceDeg;         // 正常闭环：偏了才动
            else
            {
                double dAz = AngDiff(az, _lastAz);
                double dEl = Math.Abs(la.ElDeg - _lastEl);
                aim = dAz > DeadbandDeg || dEl > DeadbandDeg;
            }
            int gap = sep > MaxErrDeg ? ForceGapMs : MinGotoGapMs;
            bool due = _lastGoto == DateTime.MinValue || (t - _lastGoto).TotalMilliseconds >= gap;
            if (S3 != null && reachable && la.ElDeg >= 0 && aim && due)
            {
                // 前视补偿：瞄 400 ms 之后的位置，抵消「命令延迟 + 转动滞后」造成的恒定落后
                double aimAz = az, aimEl = Math.Min(la.ElDeg, 89.0);
                if (LeadMs > 0)
                {
                    LookAngle lead;
                    if (PassPredictor.Look(sat.Model, store.Station, t.AddMilliseconds(LeadMs), out lead))
                    {
                        aimAz = ((lead.AzDeg % 360) + 360) % 360;
                        aimEl = Math.Min(Math.Max(lead.ElDeg, 0.0), 89.0);
                        if (aimAz > 355.0) aimAz = 355.0;
                    }
                }
                bool ok = S3.Goto(aimAz, aimEl);
                _lastGoto = DateTime.UtcNow;
                if (ok) { _lastAz = aimAz; _lastEl = aimEl; _failStreak = 0; }
                else
                {
                    _failStreak++;
                    if (_failStreak == 3) Note("云台定位连续失败：" + (S3.LastError ?? ""));
                }
            }
            // 误差越线 / 恢复：只在状态翻转时记一条，不刷屏
            if (!double.IsNaN(sep))
            {
                if (sep > MaxErrDeg && !_errBad) { _errBad = true; Note("指向误差 " + sep.ToString("0.00") + "° 超过 1°，正在纠偏"); }
                else if (sep <= MaxErrDeg && _errBad) { _errBad = false; Note("指向误差已回到 1° 内（" + sep.ToString("0.00") + "°）"); }
            }

            // ---- 电台
            bool radioDid = false;
            if (DopplerOn && plan != null && Radio.AutoTune && Radio.Ready)
            {
                try { radioDid = Radio.Apply(plan); }
                catch (Exception ex) { Note("设频异常：" + ex.Message); }
                // 多普勒闭环：即使本拍没有写操作，也按 10 s 周期回读一次，偏差 >200 Hz 自动整组重下
                try { Radio.VerifyIfDue(plan, DateTime.UtcNow, false); } catch { }
            }

            var snap = new EngineSnapshot
            {
                Running = _run,
                SatName = sat.Name,
                TargetAz = az,
                TargetEl = la.ElDeg,
                ActualAz = actualAz,
                ActualEl = actualEl,
                RangeKm = la.RangeKm,
                RangeRateKmS = la.RangeRateKmS,
                PlanRxHz = plan != null ? plan.RxHz : 0,
                PlanTxHz = plan != null ? plan.TxHz : 0,
                RxShiftHz = plan != null ? plan.RxHz - BaseRx(sat) : 0,
                TxShiftHz = plan != null ? plan.TxHz - BaseTx(sat) : 0,
                RxMode = plan != null ? plan.RxMode : "FM",
                TxMode = plan != null ? plan.TxMode : "FM",
                TxTone = plan != null ? plan.TxTone : 0,
                RxTone = plan != null ? plan.RxTone : 0,
                AosUtc = _pass != null ? _pass.AosUtc : DateTime.MinValue,
                LosUtc = _pass != null ? _pass.LosUtc : DateTime.MinValue,
                RadioOk = Radio.LastOk,
                RadioText = Radio.StatusText(),
                ErrDeg = sep,
                MaxErrDeg = _maxErr,
                AzErrDeg = eAz,
                ElErrDeg = eEl,
                BoardFresh = fresh,
                ErrOkCount = _okCount,
                ErrBadCount = _badCount,
                Note = reachable ? Radio.LastNote : ("目标方位 " + az.ToString("0.0") + "° 超出 0~355° 限位")
            };
            Last = snap;
            Raised();
        }

        private void Raised()
        {
            var h = Updated;
            if (h != null) { try { h(Last); } catch { } }
        }

        private void Note(string s)
        {
            var h = Log;
            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + s;
            if (h != null) { try { h(line); } catch { } }
        }

        private long BaseRx(SatEntry sat)
        {
            var tp = CurrentTp(sat);
            return tp != null ? tp.DownlinkHz : 0;
        }

        private long BaseTx(SatEntry sat)
        {
            var tp = CurrentTp(sat);
            return tp != null ? tp.UplinkHz : 0;
        }

        private Transponder _tp;

        public Transponder CurrentTp(SatEntry sat)
        {
            return _tp;
        }

        public void SetTransponder(Transponder tp) { _tp = tp; }

        /// <summary>由转发器基准频率 + 距离率生成计划（RX/TX 同一时刻、同一距离率）。</summary>
        public RigPlan BuildPlan(SatEntry sat, LookAngle la, DateTime utc)
        {
            var tp = _tp;
            if (tp == null) return null;
            const double c = 299792.458;                     // km/s
            double rr = la.RangeRateKmS;
            long rx = tp.DownlinkHz > 0 ? (long)Math.Round(tp.DownlinkHz * (1.0 - rr / c)) : 0;
            long tx = tp.UplinkHz > 0 ? (long)Math.Round(tp.UplinkHz * (1.0 + rr / c)) : 0;
            if (rx <= 0) return null;
            string rxMode = NormMode(tp.Mode);
            string txMode = !string.IsNullOrEmpty(tp.UpMode) ? NormMode(tp.UpMode) : rxMode;
            double txTone = tp.TxTone, rxTone = tp.RxTone;
            if (!rxMode.StartsWith("FM")) { rxTone = 0; if (!txMode.StartsWith("FM")) txTone = 0; }
            return new RigPlan
            {
                RxHz = rx,
                TxHz = tx,
                RxMode = rxMode,
                TxMode = txMode,
                TxTone = Radio.SyncTones ? txTone : 0,
                RxTone = Radio.SyncTones ? rxTone : 0,
                Split = tx > 0 && Math.Abs(tx - rx) > 1,
                SatName = sat.Name
            };
        }

        private static string NormMode(string m)
        {
            m = (m ?? "FM").Trim().ToUpperInvariant();
            if (m == "FMN" || m == "NFM" || m == "WFM" || m == "PKT" || m == "PKTFM") return "FM";
            if (m == "PKTUSB" || m == "DATA" || m == "DIG" || m == "SSB") return "USB";
            if (m == "PKTLSB") return "LSB";
            if (m == "CWR") return "CW-R";
            if (m == "RTTYR") return "RTTY-R";
            if (m == "D-STAR" || m == "DSTAR") return "DV";
            return m;
        }

        public static double AngDiff(double a, double b)
        {
            if (double.IsNaN(b)) return 999;
            double d = Math.Abs(((a - b) % 360 + 540) % 360 - 180);
            return d;
        }

        /// <summary>
        /// 两天球坐标的角距（度）。指向误差必须用它，而不是「方位差/俯仰差」——
        /// 高仰角时同样的方位差对应更小的真实偏差，用分轴差会虚报误差。
        /// </summary>
        public static double SepDeg(double az1, double el1, double az2, double el2)
        {
            if (double.IsNaN(az2) || double.IsNaN(el2)) return double.NaN;
            const double D2R = Math.PI / 180.0, R2D = 180.0 / Math.PI;
            double e1 = el1 * D2R, e2 = el2 * D2R, da = (az1 - az2) * D2R;
            double c = Math.Sin(e1) * Math.Sin(e2) + Math.Cos(e1) * Math.Cos(e2) * Math.Cos(da);
            if (c > 1.0) c = 1.0; else if (c < -1.0) c = -1.0;
            return Math.Acos(c) * R2D;
        }

        public void Dispose()
        {
            Stop("程序退出");
        }
    }
}
