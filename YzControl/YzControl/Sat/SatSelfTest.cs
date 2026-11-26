// ============================================================================
//  YzControl · Sat/SatSelfTest.cs —— 无界面自检（YzControl.exe /selftest）
//  检查项：星历缓存 → SGP4 数值向量 → 过境预测 → 当前多普勒
//  输出：控制台（若已附加）+ <exe>\selftest.txt；退出码 0=通过
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace YzControl.Sat
{
    public static class SatSelfTest
    {
        public static int Run()
        {
            var sb = new StringBuilder();
            int fail = 0;
            Action<string> w = s => { sb.AppendLine(s); try { Console.WriteLine(s); } catch { } };
            w("YzControl 卫星追踪自检  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            try
            {
                var store = SatStore.I;
                store.Init();
                int near = 0, deep = 0;
                foreach (var s in store.Sats) { if (s.Model == null) deep++; else near++; }
                w("星历：共 " + store.Sats.Count + " 颗（可递推 " + near + "，深空跳过 " + deep + "）；数据年龄 " + store.AgeText());
                w("数据目录：" + store.DataDir + "；站点 " + store.Station.LatDeg.ToString("0.0000") + "°N / "
                  + store.Station.LonDeg.ToString("0.0000") + "°E / " + store.Station.AltM.ToString("0") + " m");
                if (near == 0) { w("★ 失败：没有可用星历（点「更新数据」联网拉取）"); fail++; }

                // ---- SGP4 数值向量（若随包带了 sgp4_ref.json）
                string exeDir = Path.GetDirectoryName(typeof(SatSelfTest).Assembly.Location) ?? ".";
                string refPath = Path.Combine(exeDir, "Sat", "sgp4_ref.json");
                if (!File.Exists(refPath)) refPath = Path.Combine(exeDir, "sgp4_ref.json");
                if (File.Exists(refPath))
                {
                    var root = Json.AsObj(Json.ParseFile(refPath));
                    int total = 0, ok = 0;
                    double maxPos = 0;
                    var fails = new List<string>();
                    if (root != null)
                    {
                        foreach (var kv in root)
                        {
                            var o = Json.AsObj(kv.Value);
                            var el = SatEl.FromTleLines(Json.GetStr(o, "l1"), Json.GetStr(o, "l2"), kv.Key);
                            if (el == null) continue;
                            var m = new Sgp4Model(el);
                            if (m.IsDeepSpace) continue;
                            foreach (var pk in Json.AsObj(o["pts"]))
                            {
                                var p = Json.AsObj(pk.Value);
                                int re = (int)Json.GetNum(p, "err", 0);
                                double t = double.Parse(pk.Key, CultureInfo.InvariantCulture);
                                bool good = m.Propagate(t, out var r, out var v);
                                if (re == 6 || m.Error == 6) { total++; ok++; continue; }
                                if (!good) continue;
                                var rr = Json.AsArr(p["r"]);
                                double d = Math.Sqrt(Sq(r.X - (double)rr[0]) + Sq(r.Y - (double)rr[1]) + Sq(r.Z - (double)rr[2]));
                                total++;
                                maxPos = Math.Max(maxPos, d);
                                if (d < 0.01) ok++;
                                else fails.Add(kv.Key + " t=" + t + " 偏差 " + d.ToString("0.000") + " km  r=("
                                    + r.X.ToString("0.0") + "," + r.Y.ToString("0.0") + "," + r.Z.ToString("0.0")
                                    + ") ref=(" + ((double)rr[0]).ToString("0.0") + "," + ((double)rr[1]).ToString("0.0")
                                    + "," + ((double)rr[2]).ToString("0.0") + ")");
                            }
                        }
                    }
                    w("SGP4 数值向量：" + ok + "/" + total + " 通过，最大位置偏差 " + maxPos.ToString("0.0000") + " km");
                    foreach (var f in fails) w("  ✗ " + f);
                    if (total > 0 && ok < total) { w("★ 失败：SGP4 与官方实现不一致（" + fails.Count + " 点，见上）"); fail++; }
                }
                else w("（未带 sgp4_ref.json，跳过数值向量）");

                // ---- 过境预测：取第一颗可递推卫星
                SatEntry pick = null;
                foreach (var s in store.Sats) if (s.Model != null) { pick = s; break; }
                if (pick != null)
                {
                    var ps = PassPredictor.Predict(pick.Model, pick.Name, store.Station, DateTime.UtcNow, 12, 2, 6);
                    w("过境预测（" + pick.Name + "，未来 12 h）：" + ps.Count + " 场");
                    for (int i = 0; i < Math.Min(3, ps.Count); i++)
                    {
                        var p = ps[i];
                        w("  " + p.AosUtc.ToLocalTime().ToString("MM-dd HH:mm:ss") + " → " + p.LosUtc.ToLocalTime().ToString("HH:mm:ss")
                          + "  最大仰角 " + p.MaxEl.ToString("0.0") + "°  时长 " + Math.Round(p.DurationSec / 60) + " 分");
                    }
                    if (ps.Count > 0)
                    {
                        var la = new LookAngle();
                        if (PassPredictor.Look(pick.Model, store.Station, ps[0].AosUtc, out la))
                        {
                            double dop = Coord.DopplerShiftHz(la.RangeRateKmS, 145.9e6);
                            w("  校验：AOS 时刻仰角 " + la.ElDeg.ToString("0.000") + "°（应≈0），145.9 MHz 多普勒 "
                              + dop.ToString("0") + " Hz");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                w("★ 异常：" + ex);
                fail++;
            }
            w(fail == 0 ? "== 自检通过 ==" : "== 自检失败（" + fail + " 项）==");
            try
            {
                string exeDir = Path.GetDirectoryName(typeof(SatSelfTest).Assembly.Location) ?? ".";
                string target = Path.Combine(exeDir, "selftest.txt");
                try { File.WriteAllText(target, sb.ToString(), Encoding.UTF8); }
                catch
                {
                    // 装在 Program Files 时普通用户不可写 → 落到用户数据目录
                    string alt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                              "YzControl", "selftest.txt");
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(alt));
                        File.WriteAllText(alt, sb.ToString(), Encoding.UTF8);
                    }
                    catch { }
                }
            }
            catch { }
            return fail == 0 ? 0 : 1;
        }

        private static double Sq(double x) { return x * x; }
    }
}
