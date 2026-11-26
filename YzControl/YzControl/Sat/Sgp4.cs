// ============================================================================
//  YzControl · Sat/Sgp4.cs —— SGP4 轨道递推（近地路径，wgs72）
//  底本：Vallado AIAA 2006-6753 官方 C++（经 python-sgp4 2.27 propagation.py
//        逐行核对移植），系数与方法保持一致。
//  范围：仅近地（周期 < 225 分钟）。深空目标（SDP4 路径）返回明确错误。
//  输出：TEME 坐标（km, km/s）——与官方实现一致。
// ============================================================================
using System;

namespace YzControl.Sat
{
    /// <summary>SGP4 卫星递推器（一颗卫星一个实例）。</summary>
    public sealed class Sgp4Model
    {
        // ---- wgs72 常量（getgravconst('wgs72')）----
        private const double Mu = 398600.8;                 // km3/s2
        private const double RadiusEarthKm = 6378.135;
        private static readonly double Xke = 60.0 / Math.Sqrt(RadiusEarthKm * RadiusEarthKm * RadiusEarthKm / Mu);
        private static readonly double Tumin = 1.0 / Xke;
        private const double J2 = 0.001082616;
        private const double J3 = -0.00000253881;
        private const double J4 = -0.00000165597;
        private static readonly double J3oj2 = J3 / J2;

        private const double Twopi = 2.0 * Math.PI;
        private const double X2o3 = 2.0 / 3.0;

        // ---- 根数（sgp4init 输入）----
        private readonly double _bstar, _ndot, _nddot, _ecco, _argpo, _inclo, _mo, _noKozai, _nodeo, _epochDays;

        // ---- sgp4init 保存的模型参数 ----
        private int _isimp;
        private string _method = "n";
        private double _aycof, _con41, _cc1, _cc4, _cc5, _d2, _d3, _d4;
        private double _delmo, _eta, _argpdot, _omgcof, _sinmao, _t2cof;
        private double _t3cof, _t4cof, _t5cof, _x1mth2, _x7thm1;
        private double _mdot, _nodedot, _xlcof, _xmcof, _nodecf;
        private double _noUnkozai, _gsto;

        /// <summary>上次 sgp4 的错误码（0=正常，6=已再入）。</summary>
        public int Error;

        /// <summary>根数历元（UTC）。</summary>
        public readonly DateTime EpochUtc;

        public Sgp4Model(SatEl el)
        {
            const double Deg2Rad = Math.PI / 180.0;
            const double Xpdotp = 1440.0 / Twopi;
            _ecco = el.Ecc;
            _inclo = el.IncloDeg * Deg2Rad;
            _nodeo = el.NodeoDeg * Deg2Rad;
            _argpo = el.ArgpoDeg * Deg2Rad;
            _mo = el.MoDeg * Deg2Rad;
            _noKozai = el.NoKozai / Xpdotp;                       // 圈/天 → rad/min
            _ndot = el.NdDot / (Xpdotp * 1440.0);
            _nddot = el.NdDotO2 / (Xpdotp * 1440.0 * 1440.0);
            _bstar = el.Bstar;
            EpochUtc = el.EpochUtc;
            _epochDays = Coord.ToJd(el.EpochUtc) - 2433281.5;     // 1950 Jan 0 起
            Init();
        }

        private void Init()
        {
            double temp4 = 1.5e-12;
            _isimp = 0; _method = "n";
            _aycof = 0; _con41 = 0; _cc1 = 0; _cc4 = 0; _cc5 = 0; _d2 = 0; _d3 = 0; _d4 = 0;
            _delmo = 0; _eta = 0; _argpdot = 0; _omgcof = 0; _sinmao = 0; _t2cof = 0;
            _t3cof = 0; _t4cof = 0; _t5cof = 0; _x1mth2 = 0; _x7thm1 = 0;
            _mdot = 0; _nodedot = 0; _xlcof = 0; _xmcof = 0; _nodecf = 0;

            double ss = 78.0 / RadiusEarthKm + 1.0;
            double qzms2ttemp = (120.0 - 78.0) / RadiusEarthKm;
            double qzms2t = qzms2ttemp * qzms2ttemp * qzms2ttemp * qzms2ttemp;

            // ---------------- _initl ----------------
            double ecco = _ecco, inclo = _inclo, no = _noKozai;
            double eccsq = ecco * ecco;
            double omeosq = 1.0 - eccsq;
            double rteosq = Math.Sqrt(omeosq);
            double cosio = Math.Cos(inclo);
            double cosio2 = cosio * cosio;
            double ak = Math.Pow(Xke / no, X2o3);
            double d1 = 0.75 * J2 * (3.0 * cosio2 - 1.0) / (rteosq * omeosq);
            double del = d1 / (ak * ak);
            double adel = ak * (1.0 - del * del - del * (1.0 / 3.0 + 134.0 * del * del / 81.0));
            del = d1 / (adel * adel);
            no = no / (1.0 + del);
            double ao = Math.Pow(Xke / no, X2o3);
            double sinio = Math.Sin(inclo);
            double po = ao * omeosq;
            double con42 = 1.0 - 5.0 * cosio2;
            double con41 = -con42 - cosio2 - cosio2;
            double ainv = 1.0 / ao;
            double posq = po * po;
            double rp = ao * (1.0 - ecco);
            _noUnkozai = no;
            _con41 = con41;
            _gsto = Gstime(_epochDays + 2433281.5);
            double a = Math.Pow(_noUnkozai * Tumin, -2.0 / 3.0);

            if (omeosq >= 0.0 || _noUnkozai >= 0.0)
            {
                _isimp = 0;
                if (rp < 220.0 / RadiusEarthKm + 1.0) _isimp = 1;
                double sfour = ss;
                double qzms24 = qzms2t;
                double perige = (rp - 1.0) * RadiusEarthKm;
                if (perige < 156.0)
                {
                    sfour = perige - 78.0;
                    if (perige < 98.0) sfour = 20.0;
                    double qzms24temp = (120.0 - sfour) / RadiusEarthKm;
                    qzms24 = qzms24temp * qzms24temp * qzms24temp * qzms24temp;
                    sfour = sfour / RadiusEarthKm + 1.0;
                }
                double pinvsq = 1.0 / posq;
                double tsi = 1.0 / (ao - sfour);
                _eta = ao * ecco * tsi;
                double etasq = _eta * _eta;
                double eeta = ecco * _eta;
                double psisq = Math.Abs(1.0 - etasq);
                double coef = qzms24 * Math.Pow(tsi, 4.0);
                double coef1 = coef / Math.Pow(psisq, 3.5);
                double cc2 = coef1 * _noUnkozai * (ao * (1.0 + 1.5 * etasq + eeta * (4.0 + etasq)) +
                    0.375 * J2 * tsi / psisq * con41 * (8.0 + 3.0 * etasq * (8.0 + etasq)));
                _cc1 = _bstar * cc2;
                double cc3 = 0.0;
                if (ecco > 1.0e-4)
                    cc3 = -2.0 * coef * tsi * J3oj2 * _noUnkozai * sinio / ecco;
                _x1mth2 = 1.0 - cosio2;
                _cc4 = 2.0 * _noUnkozai * coef1 * ao * omeosq *
                    (_eta * (2.0 + 0.5 * etasq) + ecco * (0.5 + 2.0 * etasq) -
                     J2 * tsi / (ao * psisq) *
                     (-3.0 * con41 * (1.0 - 2.0 * eeta + etasq * (1.5 - 0.5 * eeta)) +
                      0.75 * _x1mth2 * (2.0 * etasq - eeta * (1.0 + etasq)) * Math.Cos(2.0 * _argpo)));
                _cc5 = 2.0 * coef1 * ao * omeosq * (1.0 + 2.75 * (etasq + eeta) + eeta * etasq);
                double cosio4 = cosio2 * cosio2;
                double temp1 = 1.5 * J2 * pinvsq * _noUnkozai;
                double temp2 = 0.5 * temp1 * J2 * pinvsq;
                double temp3 = -0.46875 * J4 * pinvsq * pinvsq * _noUnkozai;
                _mdot = _noUnkozai + 0.5 * temp1 * rteosq * con41 +
                    0.0625 * temp2 * rteosq * (13.0 - 78.0 * cosio2 + 137.0 * cosio4);
                _argpdot = -0.5 * temp1 * con42 +
                    0.0625 * temp2 * (7.0 - 114.0 * cosio2 + 395.0 * cosio4) +
                    temp3 * (3.0 - 36.0 * cosio2 + 49.0 * cosio4);
                double xhdot1 = -temp1 * cosio;
                _nodedot = xhdot1 + (0.5 * temp2 * (4.0 - 19.0 * cosio2) +
                    2.0 * temp3 * (3.0 - 7.0 * cosio2)) * cosio;
                double xpidot = _argpdot + _nodedot;
                _omgcof = _bstar * cc3 * Math.Cos(_argpo);
                _xmcof = 0.0;
                if (ecco > 1.0e-4) _xmcof = -X2o3 * coef * _bstar / eeta;
                _nodecf = 3.5 * omeosq * xhdot1 * _cc1;
                _t2cof = 1.5 * _cc1;
                if (Math.Abs(cosio + 1.0) > 1.5e-12)
                    _xlcof = -0.25 * J3oj2 * sinio * (3.0 + 5.0 * cosio) / (1.0 + cosio);
                else
                    _xlcof = -0.25 * J3oj2 * sinio * (3.0 + 5.0 * cosio) / temp4;
                _aycof = -0.5 * J3oj2 * sinio;
                double delmotemp = 1.0 + _eta * Math.Cos(_mo);
                _delmo = delmotemp * delmotemp * delmotemp;
                _sinmao = Math.Sin(_mo);
                _x7thm1 = 7.0 * cosio2 - 1.0;

                // 深空判定（本版只支持近地；与官方 2π/no >= 225 判据一致）
                if (Twopi / _noUnkozai >= 225.0)
                {
                    Error = -100;    // 深空目标：需要 SDP4，本版不支持
                    return;
                }

                if (_isimp != 1)
                {
                    double cc1sq = _cc1 * _cc1;
                    _d2 = 4.0 * ao * tsi * cc1sq;
                    double temp = _d2 * tsi * _cc1 / 3.0;
                    _d3 = (17.0 * ao + sfour) * temp;
                    _d4 = 0.5 * temp * ao * tsi * (221.0 * ao + 31.0 * sfour) * _cc1;
                    _t3cof = _d2 + 2.0 * cc1sq;
                    _t4cof = 0.25 * (3.0 * _d3 + _cc1 * (12.0 * _d2 + 10.0 * cc1sq));
                    _t5cof = 0.2 * (3.0 * _d4 + 12.0 * _cc1 * _d3 + 6.0 * _d2 * _d2 +
                        15.0 * cc1sq * (2.0 * _d2 + cc1sq));
                }
            }
        }

        /// <summary>格林尼治恒星时（官方 gstime）。</summary>
        public static double Gstime(double jdut1)
        {
            const double Deg2Rad = Math.PI / 180.0;
            double tut1 = (jdut1 - 2451545.0) / 36525.0;
            double temp = -6.2e-6 * tut1 * tut1 * tut1 + 0.093104 * tut1 * tut1 +
                (876600.0 * 3600 + 8640184.812866) * tut1 + 67310.54841;
            temp = (temp * Deg2Rad / 240.0) % Twopi;
            if (temp < 0.0) temp += Twopi;
            return temp;
        }

        /// <summary>递推到 tsince 分钟（历元起）。成功返回 true，r/v 为 TEME（km, km/s）。</summary>
        public bool Propagate(double tsince, out Vec3 r, out Vec3 v)
        {
            r = new Vec3(); v = new Vec3();
            double mrt = 0.0;
            Error = 0;

            double xmdf = _mo + _mdot * tsince;
            double argpdf = _argpo + _argpdot * tsince;
            double nodedf = _nodeo + _nodedot * tsince;
            double argpm = argpdf;
            double mm = xmdf;
            double t2 = tsince * tsince;
            double nodem = nodedf + _nodecf * t2;
            double tempa = 1.0 - _cc1 * tsince;
            double tempe = _bstar * _cc4 * tsince;
            double templ = _t2cof * t2;

            if (_isimp != 1)
            {
                double delomg = _omgcof * tsince;
                double delmtemp = 1.0 + _eta * Math.Cos(xmdf);
                double delm = _xmcof * (delmtemp * delmtemp * delmtemp - _delmo);
                double temp = delomg + delm;
                mm = xmdf + temp;
                argpm = argpdf - temp;
                double t3 = t2 * tsince;
                double t4 = t3 * tsince;
                tempa = tempa - _d2 * t2 - _d3 * t3 - _d4 * t4;
                tempe = tempe + _bstar * _cc5 * (Math.Sin(mm) - _sinmao);
                templ = templ + _t3cof * t3 + t4 * (_t4cof + tsince * _t5cof);
            }

            double nm = _noUnkozai;
            double em = _ecco;
            double inclm = _inclo;

            if (nm <= 0.0) { Error = 2; return false; }
            double am = Math.Pow(Xke / nm, X2o3) * tempa * tempa;
            nm = Xke / Math.Pow(am, 1.5);
            em = em - tempe;
            if (em >= 1.0 || em < -0.001) { Error = 1; return false; }
            if (em < 1.0e-6) em = 1.0e-6;
            mm = mm + _noUnkozai * templ;
            double xlm = mm + argpm + nodem;
            double emsq = em * em;

            nodem = nodem >= 0.0 ? nodem % Twopi : -(-nodem % Twopi);
            argpm = FloorMod(argpm);
            xlm = FloorMod(xlm);
            mm = FloorMod(xlm - argpm - nodem);

            double sinim = Math.Sin(inclm);
            double cosim = Math.Cos(inclm);

            // 近地：无 lunar-solar periodics
            double ep = em;
            double xincp = inclm;
            double argpp = argpm;
            double nodep = nodem;
            double mp = mm;
            double sinip = sinim;
            double cosip = cosim;

            double axnl = ep * Math.Cos(argpp);
            double tempK = 1.0 / (am * (1.0 - ep * ep));
            double aynl = ep * Math.Sin(argpp) + tempK * _aycof;
            double xl = mp + argpp + nodep + tempK * _xlcof * axnl;

            // ---------------- solve kepler's equation ----------------
            double u = FloorMod(xl - nodep);
            double eo1 = u;
            double tem5 = 9999.9;
            int ktr = 1;
            double sineo1 = 0.0, coseo1 = 0.0;
            while (Math.Abs(tem5) >= 1.0e-12 && ktr <= 10)
            {
                sineo1 = Math.Sin(eo1);
                coseo1 = Math.Cos(eo1);
                tem5 = 1.0 - coseo1 * axnl - sineo1 * aynl;
                tem5 = (u - aynl * coseo1 + axnl * sineo1 - eo1) / tem5;
                if (Math.Abs(tem5) >= 0.95) tem5 = tem5 > 0.0 ? 0.95 : -0.95;
                eo1 += tem5;
                ktr++;
            }

            // ------------- short period preliminary quantities -------------
            double ecose = axnl * coseo1 + aynl * sineo1;
            double esine = axnl * sineo1 - aynl * coseo1;
            double el2 = axnl * axnl + aynl * aynl;
            double pl = am * (1.0 - el2);
            if (pl < 0.0) { Error = 4; return false; }

            double rl = am * (1.0 - ecose);
            double rdotl = Math.Sqrt(am) * esine / rl;
            double rvdotl = Math.Sqrt(pl) / rl;
            double betal = Math.Sqrt(1.0 - el2);
            double temp2s = esine / (1.0 + betal);
            double sinu = am / rl * (sineo1 - aynl - axnl * temp2s);
            double cosu = am / rl * (coseo1 - axnl + aynl * temp2s);
            double su = Math.Atan2(sinu, cosu);
            double sin2u = (cosu + cosu) * sinu;
            double cos2u = 1.0 - 2.0 * sinu * sinu;
            double tempd = 1.0 / pl;
            double temp1 = 0.5 * J2 * tempd;
            double temp2 = temp1 * tempd;

            mrt = rl * (1.0 - 1.5 * temp2 * betal * _con41) +
                0.5 * temp1 * _x1mth2 * cos2u;
            su = su - 0.25 * temp2 * _x7thm1 * sin2u;
            double xnode = nodep + 1.5 * temp2 * cosip * sin2u;
            double xinc = xincp + 1.5 * temp2 * cosip * sinip * cos2u;
            double mvt = rdotl - nm * temp1 * _x1mth2 * sin2u / Xke;
            double rvdot = rvdotl + nm * temp1 * (_x1mth2 * cos2u + 1.5 * _con41) / Xke;

            // --------------------- orientation vectors -------------------
            double sinsu = Math.Sin(su);
            double cossu = Math.Cos(su);
            double snod = Math.Sin(xnode);
            double cnod = Math.Cos(xnode);
            double sini = Math.Sin(xinc);
            double cosi = Math.Cos(xinc);
            double xmx = -snod * cosi;
            double xmy = cnod * cosi;
            double ux = xmx * sinsu + cnod * cossu;
            double uy = xmy * sinsu + snod * cossu;
            double uz = sini * sinsu;
            double vx = xmx * cossu - cnod * sinsu;
            double vy = xmy * cossu - snod * sinsu;
            double vz = sini * cossu;

            double mr = mrt * RadiusEarthKm;
            double vkmpersec = RadiusEarthKm * Xke / 60.0;
            r = new Vec3(mr * ux, mr * uy, mr * uz);
            v = new Vec3((mvt * ux + rvdot * vx) * vkmpersec,
                         (mvt * uy + rvdot * vy) * vkmpersec,
                         (mvt * uz + rvdot * vz) * vkmpersec);

            if (mrt < 1.0) Error = 6;
            return true;
        }

        /// <summary>是否深空目标（周期 ≥225 分钟，本版不支持递推）。</summary>
        public bool IsDeepSpace { get { return Error == -100; } }

        /// <summary>取模到 [0, 2π)（与官方 Python/C++ 的 % 语义一致）。</summary>
        private static double FloorMod(double x)
        {
            double r = x % Twopi;
            return r < 0.0 ? r + Twopi : r;
        }
    }
}
