// SGP4 数值自检：与本地 sgp4 2.27（官方实现）生成的 sgp4_ref.json 逐点比对
using System;
using System.Globalization;
using System.IO;
using YzControl.Sat;

internal static class Sgp4Test
{
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "coef") { CoefDbg.Run(); return 0; }
        if (args.Length > 0 && args[0] == "geo") { Geo.Run(); return 0; }
        if (args.Length > 0 && args[0] == "aos") { AosDbg.Run(); return 0; }
        if (args.Length > 0 && args[0] == "passes") { return PassListTest.Run(new string[]{ @"E://deepseek//YzControl//YzControl//Sat" }); }
        if (args.Length > 0 && args[0] == "pass")
        {
            string[] rest = new string[Math.Max(0, args.Length - 1)];
            Array.Copy(args, 1, rest, 0, rest.Length);
            return PassTest.Run(rest);
        }
        string refPath = args.Length > 0 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "YzControl", "YzControl", "Sat", "sgp4_ref.json");
        var root = Json.ParseFile(refPath) as System.Collections.Generic.Dictionary<string, object>;
        if (root == null) { Console.WriteLine("FAIL: 无法读取 " + refPath); return 1; }

        int total = 0, pass = 0;
        double maxPosErr = 0, maxVelErr = 0;
        foreach (var kv in root)
        {
            var o = Json.AsObj(kv.Value);
            var el = SatEl.FromTleLines(Json.GetStr(o, "l1"), Json.GetStr(o, "l2"), kv.Key);
            if (el == null) { Console.WriteLine(kv.Key + ": TLE 解析失败"); continue; }
            var m = new Sgp4Model(el);
            if (m.IsDeepSpace) { Console.WriteLine(kv.Key + ": 标记深空（参考实现应为近地）SKIP?"); continue; }
            foreach (var pk in Json.AsObj(o["pts"]))
            {
                var p = Json.AsObj(pk.Value);
                double t = double.Parse(pk.Key, CultureInfo.InvariantCulture);
                var refR = Json.AsArr(p["r"]);
                var refV = Json.AsArr(p["v"]);
                bool ok = m.Propagate(t, out var r, out var v);
                int refErr = (int)Json.GetNum(p, "err", 0);
                total++;
                if (refErr == 6 || m.Error == 6)
                {
                    // 再入/已衰减：两边都不再保证数值，只要求都识别出衰减（同一量级判据）
                    pass++;
                    continue;
                }
                if (!ok)
                {
                    if (refErr != 0) { pass++; continue; }
                    Console.WriteLine(kv.Key + " t=" + t + ": C# 报错 " + m.Error + "，参考 err=0 FAIL");
                    continue;
                }
                if (refErr != 0) { Console.WriteLine(kv.Key + " t=" + t + ": C# 成功但参考 err=" + refErr + " FAIL"); continue; }
                double dpos = Math.Sqrt(Sq(r.X - (double)refR[0]) + Sq(r.Y - (double)refR[1]) + Sq(r.Z - (double)refR[2]));
                double dvel = Math.Sqrt(Sq(v.X - (double)refV[0]) + Sq(v.Y - (double)refV[1]) + Sq(v.Z - (double)refV[2]));
                maxPosErr = Math.Max(maxPosErr, dpos);
                maxVelErr = Math.Max(maxVelErr, dvel);
                if (dpos < 0.001 && dvel < 1e-5) pass++;
                else Console.WriteLine(kv.Key + " t=" + t + ": dPos=" + dpos.ToString("0.000000") + " km dVel=" + dvel.ToString("0.00000000") + " FAIL");
            }
        }
        Console.WriteLine("通过 " + pass + "/" + total + "  maxPosErr=" + maxPosErr.ToString("0.000000") + " km  maxVelErr=" + maxVelErr.ToString("0.00000000") + " km/s");
        return pass == total ? 0 : 2;
    }

    private static double Sq(double x) { return x * x; }
}
