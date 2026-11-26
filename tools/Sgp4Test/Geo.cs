using System;
using System.Globalization;
using YzControl.Sat;
internal static class Geo {
  public static void Run() {
    var arr = Json.AsArr(Json.ParseFile(@"E:/deepseek/YzControl/YzControl/SatData/celestrak_omm_cache.json"));
    SatEl el = null;
    foreach (var it in arr) { var o = Json.AsObj(it); if ((int)Json.GetNum(o,"NORAD_CAT_ID")==43017) { el = SatEl.FromOmm(o); break; } }
    Console.WriteLine("epoch " + el.EpochUtc.ToString("O") + " mm=" + el.NoKozai.ToString("R") + " bstar=" + el.Bstar.ToString("R"));
    var m = new Sgp4Model(el);
    foreach (var s in new[]{"2026-11-16T01:03:13Z","2026-11-16T01:05:13Z"}) {
      var t = DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal);
      m.Propagate((t - el.EpochUtc).TotalMinutes, out var r, out var v);
      Console.WriteLine(s + " |r|=" + r.Len().ToString("0.000") + " km");
    }
  }
}
