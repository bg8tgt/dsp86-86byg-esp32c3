// 过境时刻交叉验证：AOS/LOS/最大仰角 vs Skyfield 基准（同一锚点、同一份 OMM）
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using YzControl.Sat;

internal static class PassListTest
{
    public static int Run(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : @"E:\deepseek\YzControl\YzControl\Sat";
        var omm = Json.AsArr(Json.ParseFile(Path.Combine(dir, "..", "SatData", "celestrak_omm_cache.json")));
        var refRoot = Json.AsObj(Json.ParseFile(Path.Combine(dir, "pass_list.json")));
        if (omm == null || refRoot == null) { Console.WriteLine("数据缺失"); return 1; }
        DateTime anchor = DateTime.Parse(Json.GetStr(refRoot, "anchor"), CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        var site = new Site(23 + 1 / 60.0 + 15 / 3600.0, 104 + 22 / 60.0 + 30 / 3600.0, 1234.0);

        int total = 0, pass = 0;
        double maxAos = 0, maxLos = 0, maxEl = 0, maxAz = 0;
        foreach (var kv in Json.AsObj(refRoot["sats"]))
        {
            var rec = Json.AsObj(kv.Value);
            int norad = (int)Json.GetNum(rec, "norad");
            SatEl el = null;
            foreach (var it in omm)
            {
                var o = Json.AsObj(it);
                if ((int)Json.GetNum(o, "NORAD_CAT_ID") == norad) { el = SatEl.FromOmm(o); break; }
            }
            if (el == null) continue;
            var model = new Sgp4Model(el);
            var mine = PassPredictor.Predict(model, kv.Key, site, anchor, 12.0, 2.0, 20);
            var theirs = Json.AsArr(rec["passes"]);
            int n = Math.Min(mine.Count, theirs == null ? 0 : theirs.Count);
            for (int i = 0; i < n; i++)
            {
                var r = Json.AsObj(theirs[i]);
                var m = mine[i];
                DateTime raos = DateTime.Parse(Json.GetStr(r, "aos"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                DateTime rlos = DateTime.Parse(Json.GetStr(r, "los"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                double rel = Json.GetNum(r, "max_el"), raz = Json.GetNum(r, "max_az");
                total++;
                double dAos = Math.Abs((m.AosUtc - raos).TotalSeconds);
                double dLos = Math.Abs((m.LosUtc - rlos).TotalSeconds);
                double dEl = Math.Abs(m.MaxEl - rel);
                double dAz = Math.Abs(((m.MaxAz - raz) % 360 + 540) % 360 - 180);
                maxAos = Math.Max(maxAos, dAos); maxLos = Math.Max(maxLos, dLos);
                maxEl = Math.Max(maxEl, dEl); maxAz = Math.Max(maxAz, dAz);
                bool ok = dAos <= 10 && dLos <= 10 && dEl <= 0.5;
                if (ok) pass++;
                Console.WriteLine((ok ? "OK  " : "FAIL") + " " + kv.Key + " #" + (i + 1) +
                    " AOS Δ" + dAos.ToString("0.0") + "s LOS Δ" + dLos.ToString("0.0") + "s" +
                    " maxEl " + m.MaxEl.ToString("0.00") + "/" + rel.ToString("0.00") +
                    " (Δ" + dEl.ToString("0.00") + "°) maxAz " + m.MaxAz.ToString("0.0") + "/" + raz.ToString("0.0"));
            }
        }
        Console.WriteLine("通过 " + pass + "/" + total + "  maxΔAOS=" + maxAos.ToString("0.0") + "s maxΔLOS=" + maxLos.ToString("0.0") + "s maxΔEl=" + maxEl.ToString("0.00") + "° maxΔAz=" + maxAz.ToString("0.00") + "°");
        return pass == total ? 0 : 2;
    }
}
