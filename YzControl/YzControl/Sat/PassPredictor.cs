// ============================================================================
//  YzControl · Sat/PassPredictor.cs —— 过境（AOS/LOS/最大仰角）搜索
//  策略：粗采样（30~60 s）找仰角过零 → 二分精化到 1 s；再细采样求最大仰角。
// ============================================================================
using System;
using System.Collections.Generic;

namespace YzControl.Sat
{
    public sealed class PassInfo
    {
        public string SatName = "";
        public DateTime AosUtc;
        public DateTime LosUtc;
        public DateTime MaxUtc;
        public double AosAz, LosAz, MaxAz, MaxEl;
        public double AosRangeKm;
        public bool IsNow;              // 当前正在过境

        public double DurationSec { get { return (LosUtc - AosUtc).TotalSeconds; } }
    }

    public static class PassPredictor
    {
        /// <summary>预测 [from, from+hours] 内所有仰角 ≥ minEl 的过境。</summary>
        public static List<PassInfo> Predict(Sgp4Model model, string name, Site site, DateTime fromUtc,
            double hours = 12.0, double minEl = 3.0, int maxCount = 60)
        {
            var list = new List<PassInfo>();
            if (model == null) return list;
            DateTime t0 = fromUtc.AddMinutes(-1);
            double spanMin = hours * 60.0;
            double step = 30.0;                         // 粗采样 30 s
            double prevEl = double.NaN;
            DateTime prevT = t0;
            DateTime aosT = DateTime.MinValue;
            double aosAz = 0, aosRange = 0;

            for (double m = 0; m <= spanMin; m += step / 60.0)
            {
                DateTime t = t0.AddMinutes(m);
                double el = Elevation(model, site, t);
                if (double.IsNaN(el)) { prevEl = double.NaN; prevT = t; continue; }

                if (!double.IsNaN(prevEl))
                {
                    if (prevEl < 0 && el >= 0)
                    {
                        aosT = Refine(model, site, prevT, t, rising: true);
                        ElAz(model, site, aosT, out var a, out var e, out var rr);
                        aosAz = a; aosRange = rr;
                    }
                    else if (prevEl >= 0 && el < 0 && aosT != DateTime.MinValue)
                    {
                        DateTime losT = Refine(model, site, prevT, t, rising: false);
                        var p = BuildPass(model, name, site, aosT, losT, aosAz, aosRange);
                        if (p != null && p.MaxEl >= minEl) list.Add(p);
                        if (list.Count >= maxCount) return list;
                        aosT = DateTime.MinValue;
                    }
                }
                prevEl = el; prevT = t;
            }
            // 边界：预测窗口结束时仍在境（用窗口末尾收尾）
            if (aosT != DateTime.MinValue)
            {
                var p = BuildPass(model, name, site, aosT, t0.AddMinutes(spanMin), aosAz, aosRange);
                if (p != null && p.MaxEl >= minEl) list.Add(p);
            }
            // 标记当前正在过境
            DateTime now = DateTime.UtcNow;
            foreach (var p in list) p.IsNow = p.AosUtc <= now && now <= p.LosUtc;
            return list;
        }

        private static PassInfo BuildPass(Sgp4Model model, string name, Site site, DateTime aos, DateTime los, double aosAz, double aosRange)
        {
            var p = new PassInfo { SatName = name, AosUtc = aos, LosUtc = los, AosAz = aosAz, AosRangeKm = aosRange };
            double bestEl = -90, bestAz = 0;
            DateTime bestT = aos;
            int n = Math.Max(8, (int)((los - aos).TotalSeconds / 10.0));
            for (int i = 0; i <= n; i++)
            {
                DateTime t = aos.AddSeconds((los - aos).TotalSeconds * i / n);
                ElAz(model, site, t, out var az, out var el, out var _);
                if (el > bestEl) { bestEl = el; bestAz = az; bestT = t; }
            }
            ElAz(model, site, los, out var laz, out var lel, out var _);
            p.MaxEl = bestEl; p.MaxAz = bestAz; p.MaxUtc = bestT; p.LosAz = laz;
            return p;
        }

        /// <summary>二分求仰角过零点（rising=true 求上升穿过 0°，false 求下降穿过）。</summary>
        private static DateTime Refine(Sgp4Model model, Site site, DateTime tA, DateTime tB, bool rising)
        {
            for (int i = 0; i < 22 && (tB - tA).TotalSeconds > 1.0; i++)
            {
                DateTime tm = tA.AddSeconds((tB - tA).TotalSeconds / 2.0);
                double em = Elevation(model, site, tm);
                if (double.IsNaN(em)) break;
                if (rising) { if (em < 0) tA = tm; else tB = tm; }
                else { if (em >= 0) tA = tm; else tB = tm; }
            }
            return tA.AddSeconds((tB - tA).TotalSeconds / 2.0);
        }

        public static double Elevation(Sgp4Model model, Site site, DateTime utc)
        {
            ElAz(model, site, utc, out var _, out var el, out var _);
            return el;
        }

        public static void ElAz(Sgp4Model model, Site site, DateTime utc, out double az, out double el, out double rangeKm)
        {
            az = 0; el = double.NaN; rangeKm = 0;
            double tsince = (utc - model.EpochUtc).TotalMinutes;
            if (!model.Propagate(tsince, out var r, out var v)) return;
            var la = Coord.LookAngles(site, utc, r, v);
            az = la.AzDeg; el = la.ElDeg; rangeKm = la.RangeKm;
        }

        /// <summary>当前观测（方位/仰角/距离/距离率）。</summary>
        public static bool Look(Sgp4Model model, Site site, DateTime utc, out LookAngle la)
        {
            la = default(LookAngle);
            double tsince = (utc - model.EpochUtc).TotalMinutes;
            if (!model.Propagate(tsince, out var r, out var v)) return false;
            la = Coord.LookAngles(site, utc, r, v);
            return true;
        }
    }
}
