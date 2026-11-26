// ============================================================================
//  YzControl · Sat/Coord.cs —— 时间 / 恒星时 / ECI→ECEF→ENU / 站心观测角
//  精度策略（按移植计划 M1）：UTC→JD→GMST 一条链；TEME≈ECI、
//  极移/岁差/章动忽略（过境 AOS/LOS <10s、多普勒 <50Hz 量级足够）。
// ============================================================================
using System;

namespace YzControl.Sat
{
    public struct Vec3
    {
        public double X, Y, Z;
        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double Len() { return Math.Sqrt(X * X + Y * Y + Z * Z); }
        public static Vec3 operator -(Vec3 a, Vec3 b) { return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static Vec3 operator +(Vec3 a, Vec3 b) { return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static Vec3 operator *(Vec3 a, double k) { return new Vec3(a.X * k, a.Y * k, a.Z * k); }
    }

    /// <summary>站点参数（大地坐标）。</summary>
    public struct Site
    {
        public double LatDeg;   // 大地纬度
        public double LonDeg;   // 经度（东正）
        public double AltM;     // 海拔（米）

        public static readonly Site Default = new Site { LatDeg = 23.0208333, LonDeg = 104.375, AltM = 1234 };

        public Site(double latDeg, double lonDeg, double altM)
        {
            LatDeg = latDeg; LonDeg = lonDeg; AltM = altM;
        }
    }

    /// <summary>站心观测结果。</summary>
    public struct LookAngle
    {
        public double AzDeg;        // 方位 0~360（北=0，东=90）
        public double ElDeg;        // 仰角（可负）
        public double RangeKm;      // 距离
        public double RangeRateKmS; // 距离率（正=远离）
    }

    public static class Coord
    {
        private const double Rad = Math.PI / 180.0;
        // WGS84
        private const double ReKm = 6378.137;
        private const double Flat = 1.0 / 298.257223563;

        private static readonly DateTime J2000 = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>UTC → Julian Date（以 J2000 = 2451545.0 为基准，精度到 tick）。</summary>
        public static double ToJd(DateTime utc)
        {
            return 2451545.0 + (utc.ToUniversalTime() - J2000).TotalDays;
        }

        /// <summary>JD → UTC。</summary>
        public static DateTime FromJd(double jd)
        {
            return J2000.AddDays(jd - 2451545.0);
        }

        /// <summary>格林尼治平恒星时（弧度，IAU-82 简化，忽略 UT1-UTC）。</summary>
        public static double GmstRad(double jd)
        {
            double tu = (jd - 2451545.0) / 36525.0;
            double gmst = 280.46061837 + 360.98564736629 * (jd - 2451545.0) + tu * tu * (0.000387933 - tu / 38710000.0);
            gmst = gmst % 360.0;
            if (gmst < 0) gmst += 360.0;
            return gmst * Rad;
        }

        /// <summary>大地坐标 → ECEF（km）。</summary>
        public static Vec3 SiteEcef(Site s)
        {
            double lat = s.LatDeg * Rad;
            double lon = s.LonDeg * Rad;
            double n = ReKm / Math.Sqrt(1.0 - Flat * (2.0 - Flat) * Math.Sin(lat) * Math.Sin(lat));
            return new Vec3(
                (n + s.AltM / 1000.0) * Math.Cos(lat) * Math.Cos(lon),
                (n + s.AltM / 1000.0) * Math.Cos(lat) * Math.Sin(lon),
                (n * (1.0 - Flat * (2.0 - Flat)) + s.AltM / 1000.0) * Math.Sin(lat));
        }

        /// <summary>卫星 ECI(TEME) → 站心方位/俯仰/距离/距离率（含地球自转带来的测站速度）。</summary>
        public static LookAngle LookAngles(Site site, DateTime utc, Vec3 satEci, Vec3 satVel)
        {
            double gmst = GmstRad(ToJd(utc));
            double c = Math.Cos(gmst), sn = Math.Sin(gmst);
            Vec3 rSat = new Vec3(c * satEci.X + sn * satEci.Y, -sn * satEci.X + c * satEci.Y, satEci.Z);
            Vec3 vSatInertialEcef = new Vec3(c * satVel.X + sn * satVel.Y, -sn * satVel.X + c * satVel.Y, satVel.Z);

            Vec3 rSite = SiteEcef(site);
            Vec3 rho = rSat - rSite;
            double lat = site.LatDeg * Rad;
            double lon = site.LonDeg * Rad;
            double cl = Math.Cos(lat), sl = Math.Sin(lat), co = Math.Cos(lon), so = Math.Sin(lon);

            double e = -so * rho.X + co * rho.Y;
            double n2 = -sl * co * rho.X - sl * so * rho.Y + cl * rho.Z;
            double u = cl * co * rho.X + cl * so * rho.Y + sl * rho.Z;

            double rng = Math.Sqrt(e * e + n2 * n2 + u * u);
            double az = Math.Atan2(e, n2) / Rad;
            if (az < 0) az += 360.0;
            double el = Math.Asin(Math.Max(-1.0, Math.Min(1.0, u / Math.Max(rng, 1e-9)))) / Rad;

            // ---- 距离率：回到惯性系直接算 ρ̇ = û·(v_sat − ω×r_site)
            //      （ECEF 里做要扣帧旋转导数，容易重复扣地球自转，这里不做）
            const double omega = 7.2921150e-5;                       // rad/s
            // ECEF → ECI（Rz(-θ)）
            Vec3 rSiteEci = new Vec3(c * rSite.X - sn * rSite.Y, sn * rSite.X + c * rSite.Y, rSite.Z);
            Vec3 rhoEci = satEci - rSiteEci;
            double rhoLen = rhoEci.Len();
            Vec3 vSiteEci = new Vec3(-omega * rSiteEci.Y, omega * rSiteEci.X, 0.0);
            double rr = 0.0;
            if (rhoLen > 1e-9)
            {
                rr = (rhoEci.X * (satVel.X - vSiteEci.X)
                    + rhoEci.Y * (satVel.Y - vSiteEci.Y)
                    + rhoEci.Z * (satVel.Z - vSiteEci.Z)) / rhoLen;
            }
            return new LookAngle { AzDeg = az, ElDeg = el, RangeKm = rng, RangeRateKmS = rr };
        }

        /// <summary>多普勒频移（Hz）：f_obs − f = f·(−ρ̇/c)。ρ̇ 正=远离 → 收频率变低。</summary>
        public static double DopplerShiftHz(double rangeRateKmS, double freqHz)
        {
            // f_obs = f * (c - rr) / c，c = 299792.458 km/s
            const double c = 299792.458;
            return freqHz * (c - rangeRateKmS) / c - freqHz;
        }

        /// <summary>星下点（地心经纬度 + 高度）——世界地图用。</summary>
        public static void SubPoint(DateTime utc, Vec3 satEci, out double latDeg, out double lonDeg, out double altKm)
        {
            double gmst = GmstRad(ToJd(utc));
            double c = Math.Cos(gmst), sn = Math.Sin(gmst);
            double x = c * satEci.X + sn * satEci.Y;
            double y = -sn * satEci.X + c * satEci.Y;
            double z = satEci.Z;
            double r = Math.Sqrt(x * x + y * y);
            latDeg = Math.Atan2(z, r) / Rad;
            lonDeg = Math.Atan2(y, x) / Rad;
            altKm = Math.Sqrt(x * x + y * y + z * z) - ReKm;
        }

        /// <summary>地面覆盖圈半张角（度）——高度 h 处的可视地心角。</summary>
        public static double FootprintDeg(double altKm)
        {
            double h = Math.Max(altKm, 1.0);
            double c = ReKm / (ReKm + h);
            if (c > 1.0) c = 1.0;
            return Math.Acos(c) / Rad;
        }
    }
}
