// ============================================================================
//  YzControl · Sat/Tle.cs —— 卫星根数（OMM JSON / TLE 两行根数）
//  字段语义与 Celestrak OMM（gp.php FORMAT=json）对齐。
// ============================================================================
using System;
using System.Globalization;

namespace YzControl.Sat
{
    /// <summary>一组轨道根数（近地/深空通用）。no 单位：圈/天。</summary>
    public sealed class SatEl
    {
        public string Name = "";
        public int NoradId;                 // NORAD 编号
        public DateTime EpochUtc;           // 历元（UTC）
        public double NoKozai;              // 平运动 n0（圈/天，Kozai）
        public double Ecc;                  // 偏心率
        public double IncloDeg;             // 轨道倾角（°）
        public double NodeoDeg;             // 升交点赤经（°）
        public double ArgpoDeg;             // 近地点幅角（°）
        public double MoDeg;                // 平近点角（°）
        public double Bstar;                // BSTAR（1/Re）
        public double NdDot;                // n0 二阶导（圈/天³）
        public double NdDotO2;              // n0 一阶导的一半（圈/天²）
        public string Source = "";          // 数据来源说明
        public DateTime FetchedUtc;         // 本地缓存写入时间

        public double PeriodMin             // 周期（分钟）
        {
            get { return NoKozai > 0 ? 1440.0 / NoKozai : 0; }
        }

        public bool IsDeepSpace
        {
            get
            {
                // per 224.975 min（地球同步 2π/86400 rad/min）判定深空（SDP4）
                if (NoKozai <= 0) return true;
                double a1 = Math.Pow(Xke / (NoKozai * Tpi / 1440.0), 2.0 / 3.0);
                double del1 = 1.5 * Xke * Ck2 / a1 * (3.0 * Sq(Math.Cos(IncloDeg * Rad)) - 1.0) / Math.Pow(1.0 - Ecc * Ecc, 1.5);
                double ao = a1 * (1.0 + del1 * (0.5 + del1 * (del1 * del1 / 24.0 + 0.01)));
                double delo = 1.5 * Xke * Ck2 * (3.0 * Sq(Math.Cos(IncloDeg * Rad)) - 1.0) / Math.Pow(1.0 - Ecc * Ecc, 1.5) / (ao * ao * ao * ao) * (ao * a1) * 1440.0 * 1440.0;
                return PeriodMin >= 225.0 || NoKozai * 1440.0 + delo >= 6.484328;   // ≈ 2π/86400
            }
        }

        // 常量（wgs72，与 SGP4 参考实现一致）
        internal const double Tpi = 2.0 * Math.PI;
        internal const double Xke = 0.07436691613317342;    // sqrt(mu) 缩放
        internal const double Ck2 = 5.413080e-4;
        internal const double Rad = Math.PI / 180.0;
        private static double Sq(double x) { return x * x; }

        public SatEl Copy()
        {
            return (SatEl)MemberwiseClone();
        }

        // ---------------- OMM JSON ----------------
        /// <summary>从 Celestrak OMM JSON 对象解析；失败返回 null。</summary>
        public static SatEl FromOmm(System.Collections.Generic.Dictionary<string, object> o)
        {
            if (o == null) return null;
            string name = Json.GetStr(o, "OBJECT_NAME", "");
            string epoch = Json.GetStr(o, "EPOCH", "");
            double no = Json.GetNum(o, "MEAN_MOTION", 0);
            double ecc = Json.GetNum(o, "ECCENTRICITY", -1);
            double inc = Json.GetNum(o, "INCLINATION", -1);
            if (no <= 0 || ecc < 0 || inc < 0 || string.IsNullOrEmpty(epoch)) return null;
            var e = new SatEl();
            e.Name = name;
            int nid;
            int.TryParse(Json.GetStr(o, "NORAD_CAT_ID", "0"), out nid);
            e.NoradId = nid;
            e.EpochUtc = ParseOmmEpoch(epoch);
            e.NoKozai = no;
            e.Ecc = ecc;
            e.IncloDeg = inc;
            e.NodeoDeg = Json.GetNum(o, "RA_OF_ASC_NODE", 0);
            e.ArgpoDeg = Json.GetNum(o, "ARG_OF_PERICENTER", 0);
            e.MoDeg = Json.GetNum(o, "MEAN_ANOMALY", 0);
            e.Bstar = SciNotation(Json.GetStr(o, "BSTAR", "0"));
            e.NdDot = SciNotation(Json.GetStr(o, "MEAN_MOTION_DOT", "0"));
            e.NdDotO2 = SciNotation(Json.GetStr(o, "MEAN_MOTION_DDOT", "0")) / 2.0;
            e.Source = "OMM";
            return e;
        }

        /// <summary>OMM EPOCH 形如 2026-10-23T06:53:13.224768（视为 UTC）。</summary>
        public static DateTime ParseOmmEpoch(string s)
        {
            DateTime d;
            if (!DateTime.TryParseExact(s, "yyyy-MM-ddTHH:mm:ss.ffffff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out d))
                if (!DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out d))
                    return DateTime.UtcNow;
            return d;
        }

        /// <summary>科学计数法字符串（"28098-4" / "1.2e-5" / "0"）→ double。</summary>
        public static double SciNotation(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            s = s.Trim();
            double d;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            // TLE 风格：±nnnnn±e → ±0.nnnnnE±e
            int i = 0;
            bool neg = s[0] == '-';
            if (s[0] == '+' || s[0] == '-') i = 1;
            int sep = s.IndexOfAny(new[] { '+', '-' }, i + 1);
            if (sep <= i) return 0;
            string mant = s.Substring(i, sep - i);
            string exp = s.Substring(sep);
            int e2;
            if (!int.TryParse(exp, out e2)) return 0;
            double m;
            string ms = mant.Trim();
            if (!double.TryParse(ms, NumberStyles.Float, CultureInfo.InvariantCulture, out m)) return 0;
            // "28098" → 0.28098
            m = m / Math.Pow(10, ms.Length);
            if (neg) m = -m;
            return m * Math.Pow(10, e2);
        }

        // ---------------- 两行根数 ----------------
        /// <summary>TLE 两行解析（第 1/2 行各 69 列）；失败返回 null。</summary>
        public static SatEl FromTleLines(string l1, string l2, string name)
        {
            if (l1 == null || l2 == null) return null;
            l1 = l1.TrimEnd(); l2 = l2.TrimEnd();
            if (l1.Length < 68 || l2.Length < 68 || !l1.StartsWith("1 ") || !l2.StartsWith("2 ")) return null;
            var e = new SatEl();
            e.Name = name ?? "";
            int nid;
            if (!int.TryParse(l1.Substring(2, 5).Trim(), out nid)) return null;
            e.NoradId = nid;
            int yy = int.Parse(l1.Substring(18, 2), CultureInfo.InvariantCulture);
            double doy = double.Parse(l1.Substring(20, 12).Trim().Replace(' ', '0'), CultureInfo.InvariantCulture);
            int yr = yy < 57 ? 2000 + yy : 1900 + yy;
            e.EpochUtc = new DateTime(yr, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(doy - 1.0);
            e.NdDotO2 = SciNotation(l1.Substring(33, 10).Trim()) / 2.0;
            e.Bstar = SciNotation(l1.Substring(53, 8).Trim());
            e.IncloDeg = double.Parse(l2.Substring(8, 8).Trim(), CultureInfo.InvariantCulture);
            e.NodeoDeg = double.Parse(l2.Substring(17, 8).Trim(), CultureInfo.InvariantCulture);
            e.Ecc = double.Parse("0." + l2.Substring(26, 7).Trim(), CultureInfo.InvariantCulture);
            e.ArgpoDeg = double.Parse(l2.Substring(34, 8).Trim(), CultureInfo.InvariantCulture);
            e.MoDeg = double.Parse(l2.Substring(43, 8).Trim(), CultureInfo.InvariantCulture);
            e.NoKozai = double.Parse(l2.Substring(52, 11).Trim().Replace(' ', '0'), CultureInfo.InvariantCulture);
            if (e.NoKozai <= 0) return null;
            e.Source = "TLE";
            return e;
        }

        /// <summary>三行 TLE 文本（名称+两行）解析。</summary>
        public static SatEl FromTleBlock(string block)
        {
            if (string.IsNullOrWhiteSpace(block)) return null;
            var lines = block.Replace("\r\n", "\n").Split('\n');
            if (lines.Length >= 3) return FromTleLines(lines[1], lines[2], lines[0].Trim());
            if (lines.Length == 2) return FromTleLines(lines[0], lines[1], "SAT " + NoradOf(lines[0]));
            return null;
        }

        private static string NoradOf(string l1)
        {
            try { return l1.Substring(2, 5).Trim(); } catch { return ""; }
        }
    }
}
