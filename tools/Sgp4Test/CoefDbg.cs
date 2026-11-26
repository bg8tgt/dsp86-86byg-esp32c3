using System;
using System.Reflection;
using YzControl.Sat;

internal static class CoefDbg
{
    public static void Run()
    {
        string l1 = "1 29141U 85108AA  06170.26783845  .99999999  00000-0  13519-0 0   718";
        string l2 = "2 29141  82.4288 273.4882 0015848 277.2124  83.9133 15.93343074  6828";
        var el = SatEl.FromTleLines(l1, l2, "29141");
        var m = new Sgp4Model(el);
        string[] names = { "_isimp","_aycof","_con41","_cc1","_cc4","_cc5","_d2","_d3","_d4","_delmo","_eta",
                           "_argpdot","_omgcof","_sinmao","_t2cof","_t3cof","_t4cof","_t5cof","_x1mth2","_x7thm1",
                           "_mdot","_nodedot","_xlcof","_xmcof","_nodecf","_noUnkozai","_gsto" };
        var f = typeof(Sgp4Model).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var n in names)
        {
            foreach (var fi in f)
            {
                if (fi.Name != n) continue;
                object v = fi.GetValue(m);
                Console.WriteLine("{0,-11} = {1}", n.TrimStart('_'), v is int ? ((int)v).ToString() : Convert.ToDouble(v).ToString("R"));
            }
        }
        m.Propagate(0, out var r0, out var v0);
        Console.WriteLine("t=0 r=(" + r0.X.ToString("0.0000") + "," + r0.Y.ToString("0.0000") + "," + r0.Z.ToString("0.0000") + ")");
        m.Propagate(60, out var r1, out var v1);
        Console.WriteLine("t=60 r=(" + r1.X.ToString("0.0000") + "," + r1.Y.ToString("0.0000") + "," + r1.Z.ToString("0.0000") + ")");
    }
}
