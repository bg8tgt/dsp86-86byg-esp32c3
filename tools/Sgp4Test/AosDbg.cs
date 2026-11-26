using System;
using System.Globalization;
using YzControl.Sat;
internal static class AosDbg {
  public static void Run() {
    var omm = Json.AsArr(Json.ParseFile(@"E:/deepseek/YzControl/YzControl/SatData/celestrak_omm_cache.json"));
    SatEl el = null;
    foreach (var it in omm) { var o = Json.AsObj(it); if ((int)Json.GetNum(o,"NORAD_CAT_ID")==27607) { el = SatEl.FromOmm(o); break; } }
    var m = new Sgp4Model(el);
    var site = new Site(23+1/60.0+15/3600.0, 104+22/60.0+30/3600.0, 1234.0);
    DateTime anchor = DateTime.Parse("2026-11-16T01:10:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal);
    var ps = PassPredictor.Predict(m, "SO-50", site, anchor, 12.0, 2.0, 20);
    foreach (var p in ps) {
      Console.WriteLine("my AOS " + p.AosUtc.ToString("HH:mm:ss") + "  LOS " + p.LosUtc.ToString("HH:mm:ss") + " maxEl " + p.MaxEl.ToString("0.00"));
      foreach (int d in new[]{-60,0,60}) Console.WriteLine("  el(" + d + "s) = " + PassPredictor.Elevation(m, site, p.AosUtc.AddSeconds(d)).ToString("0.000"));
    }
  }
}
