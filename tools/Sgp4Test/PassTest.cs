// 坐标/过境/多普勒交叉验证：与 Skyfield（OrbitLink 同源）基准比对
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using YzControl.Sat;

internal static class PassTest
{
    public static int Run(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : @"E:\deepseek\YzControl\YzControl\Sat";
        var omm = Json.AsArr(Json.ParseFile(Path.Combine(dir, "..", "SatData", "celestrak_omm_cache.json")));
        var reference = Json.AsObj(Json.ParseFile(Path.Combine(dir, "pass_ref.json")));
        if (omm == null || reference == null) { Console.WriteLine("数据缺失"); return 1; }

        var site = new Site(23 + 1 / 60.0 + 15 / 3600.0, 104 + 22 / 60.0 + 30 / 3600.0, 1234.0);
        int total = 0, pass = 0;
        double maxAz = 0, maxEl = 0, maxRng = 0, maxRr = 0, maxDop = 0;

        foreach (var kv in reference)
        {
            var rec = Json.AsObj(kv.Value);
            int norad = (int)Json.GetNum(rec, "norad");
            SatEl el = null;
            foreach (var it in omm)
            {
                var o = Json.AsObj(it);
                if ((int)Json.GetNum(o, "NORAD_CAT_ID") == norad) { el = SatEl.FromOmm(o); break; }
            }
            if (el == null) { Console.WriteLine(kv.Key + " 缓存里找不到"); continue; }
            var model = new Sgp4Model(el);
            if (model.IsDeepSpace) { Console.WriteLine(kv.Key + " 深空不支持（基准不该选它）"); continue; }

            foreach (var s in Json.AsArr(rec["samples"]))
            {
                var o = Json.AsObj(s);
                DateTime utc = DateTime.Parse(Json.GetStr(o, "utc"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                double refAz = Json.GetNum(o, "az"), refEl = Json.GetNum(o, "el");
                double refRng = Json.GetNum(o, "range_km"), refRr = Json.GetNum(o, "range_rate");
                LookAngle la;
                total++;
                if (!PassPredictor.Look(model, site, utc, out la)) { Console.WriteLine(kv.Key + " 递推失败"); continue; }
                double dAz = Math.Abs(((la.AzDeg - refAz) % 360 + 540) % 360 - 180);
                double dEl = Math.Abs(la.ElDeg - refEl);
                double dRng = Math.Abs(la.RangeKm - refRng);
                double dRr = Math.Abs(la.RangeRateKmS - refRr);
                maxAz = Math.Max(maxAz, dAz); maxEl = Math.Max(maxEl, dEl);
                maxRng = Math.Max(maxRng, dRng); maxRr = Math.Max(maxRr, dRr);
                // 多普勒：145 MHz 参考下换算
                double dop = Math.Abs(Coord.DopplerShiftHz(la.RangeRateKmS, 145.9e6) - Coord.DopplerShiftHz(refRr, 145.9e6));
                maxDop = Math.Max(maxDop, dop);
                bool ok = dAz < 0.2 && dEl < 0.2 && dRng < 20 && dRr < 0.05;
                if (ok) pass++;
                else Console.WriteLine(kv.Key + " " + utc.ToString("HH:mm:ss") + " el=" + la.ElDeg.ToString("0.0") + "/" + refEl.ToString("0.0") + " dAz=" + dAz.ToString("0.000") +
                    "° dEl=" + dEl.ToString("0.000") + "° dRng=" + dRng.ToString("0.0") + "km dRr=" + dRr.ToString("0.0000"));
            }
        }
        Console.WriteLine("通过 " + pass + "/" + total + "  maxAz=" + maxAz.ToString("0.000") + "° maxEl=" + maxEl.ToString("0.000") +
            "° maxRange=" + maxRng.ToString("0.00") + "km maxRr=" + maxRr.ToString("0.0000") + "km/s maxDop@145.9=" + maxDop.ToString("0.0") + "Hz");
        return pass == total ? 0 : 2;
    }
}
