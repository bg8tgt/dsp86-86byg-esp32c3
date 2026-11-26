// ============================================================================
//  YzControl · Sat/SatStore.cs —— 星历/转发器/收藏/站点数据层
//  数据目录：<exe>\SatData（便携），不可写时回落 %LOCALAPPDATA%\YzControl\SatData
//  在线更新：OMM ← celestrak gp.php?GROUP=amateur；转发器 ← dtrac.cn → satnogs.org
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace YzControl.Sat
{
    public sealed class Transponder
    {
        public string Name = "";
        public string Mode = "FM";
        public string UpMode = "";
        public long DownlinkHz;
        public long UplinkHz;
        public double TxTone;
        public double RxTone;
        public bool Invert;

        public string Describe()
        {
            string dl = DownlinkHz > 0 ? (DownlinkHz / 1e6).ToString("0.000000", CultureInfo.InvariantCulture) + " MHz" : "--";
            string ul = UplinkHz > 0 ? (UplinkHz / 1e6).ToString("0.000000", CultureInfo.InvariantCulture) + " MHz" : "仅接收";
            string tone = TxTone > 0 || RxTone > 0
                ? "  CTCSS " + (TxTone > 0 ? "TX " + TxTone.ToString("0.0") : "") + (RxTone > 0 ? " RX " + RxTone.ToString("0.0") : "")
                : "";
            return Name + "  " + Mode + "  下行 " + dl + " / 上行 " + ul + tone + (Invert ? "  倒置" : "");
        }
    }

    public sealed class SatEntry
    {
        public SatEl El;
        public Sgp4Model Model;
        public List<Transponder> Transponders = new List<Transponder>();
        public bool Favorite;

        public string Name { get { return El != null ? El.Name : "?"; } }
        public int Norad { get { return El != null ? El.NoradId : 0; } }

        public bool Build()
        {
            if (El == null) return false;
            Model = new Sgp4Model(El);
            return !Model.IsDeepSpace;
        }
    }

    public sealed class SatStore
    {
        private static readonly object _gate = new object();
        private static SatStore _inst;

        public static SatStore I
        {
            get
            {
                lock (_gate)
                {
                    if (_inst == null) _inst = new SatStore();
                    return _inst;
                }
            }
        }

        public string DataDir { get; private set; }
        public List<SatEntry> Sats = new List<SatEntry>();
        public HashSet<string> Favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public Site Station = Site.Default;
        public DateTime TleAgeUtc = DateTime.MinValue;      // 缓存文件写入时间
        public string LastSource = "";
        public int DeepSpaceCount;

        public const string OmmUrl = "https://celestrak.org/NORAD/elements/gp.php?GROUP=amateur&FORMAT=json";
        public const string OmmUrlCn = "https://celestrak.org/NORAD/elements/gp.php?GROUP=amateur&FORMAT=tle";
        public const string TransUrl = "https://dtrac.cn/download/satnogs.json";
        public const string TransUrlFallback = "https://db.satnogs.org/api/transmitters/?format=json";
        public const string Ua = "YzControl-Sat/1.7 (+YzControl)";

        private SatStore() { }

        // ---------------------------------------------------------------- 路径
        public void Init()
        {
            string exeDir = Path.GetDirectoryName(typeof(SatStore).Assembly.Location);
            string a = Path.Combine(exeDir ?? ".", "SatData");
            string b = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YzControl", "SatData");
            DataDir = a;
            try
            {
                if (!Directory.Exists(a)) Directory.CreateDirectory(a);
                string probe = Path.Combine(a, ".wtest");
                File.WriteAllText(probe, "1");
                File.Delete(probe);
            }
            catch
            {
                DataDir = b;
                try { Directory.CreateDirectory(b); } catch { }
            }
            // 装在 Program Files 时普通用户对安装目录不可写 → 已回落到 LOCALAPPDATA，
            // 这里把随包预置的星历 / 转发器缓存补过去，避免首启出现空星表
            try
            {
                if (!string.Equals(DataDir, a, StringComparison.OrdinalIgnoreCase) && Directory.Exists(a))
                {
                    foreach (string f in Directory.GetFiles(a))
                    {
                        string dst = Path.Combine(DataDir, Path.GetFileName(f));
                        if (!File.Exists(dst)) { try { File.Copy(f, dst); } catch { } }
                    }
                }
            }
            catch { }
            LoadLocal();
        }

        public string PathOf(string name) { return Path.Combine(DataDir, name); }

        private void LoadLocal()
        {
            Sats.Clear();
            // 1) OMM 缓存
            string omm = PathOf("celestrak_omm_cache.json");
            if (!File.Exists(omm))
            {
                // 随包预置的同名文件（安装目录 SatData 已带；这里再兜底到 exe 目录）
                string alt = Path.Combine(Path.GetDirectoryName(typeof(SatStore).Assembly.Location) ?? ".", "celestrak_omm_cache.json");
                if (File.Exists(alt)) { try { File.Copy(alt, omm, true); } catch { } }
            }
            var arr = Json.AsArr(Json.ParseFile(omm));
            if (arr != null)
            {
                foreach (var it in arr)
                {
                    var el = SatEl.FromOmm(Json.AsObj(it));
                    if (el == null) continue;
                    var e = new SatEntry { El = el };
                    if (!e.Build()) DeepSpaceCount++;
                    Sats.Add(e);
                }
                try { TleAgeUtc = File.GetLastWriteTimeUtc(omm); } catch { }
            }
            MergeTransponders(PathOf("transponders_cache.json"), true);
            LoadUser();
            LastSource = "本地缓存 " + DataDir;
        }

        public void MergeTransponders(string path, bool replace)
        {
            object obj = Json.ParseFile(path);
            if (obj == null) return;
            var map = obj as Dictionary<string, object>;
            if (map == null) return;
            var byNorad = new Dictionary<int, List<Transponder>>();
            foreach (var kv in map)
            {
                int norad;
                if (!int.TryParse(kv.Key, out norad)) continue;
                var list = new List<Transponder>();
                var items = Json.AsArr(kv.Value);
                if (items != null)
                {
                    foreach (var it in items)
                    {
                        var o = Json.AsObj(it);
                        if (o == null) continue;
                        var tp = new Transponder
                        {
                            Name = Json.GetStr(o, "name", "Transmitter"),
                            Mode = Json.GetStr(o, "mode", "FM"),
                            UpMode = Json.GetStr(o, "uplink_mode", ""),
                            DownlinkHz = (long)Json.GetNum(o, "downlink", 0),
                            UplinkHz = (long)Json.GetNum(o, "uplink", 0),
                            TxTone = Json.GetNum(o, "tx_tone", 0),
                            RxTone = Json.GetNum(o, "rx_tone", 0),
                            Invert = Json.GetBool(o, "invert", false)
                        };
                        if (tp.DownlinkHz <= 0 && tp.UplinkHz <= 0) continue;
                        list.Add(tp);
                    }
                }
                byNorad[norad] = list;
            }
            foreach (var s in Sats)
            {
                List<Transponder> l;
                if (byNorad.TryGetValue(s.Norad, out l))
                {
                    if (replace) s.Transponders = l;
                    else foreach (var t in l) s.Transponders.Add(t);
                }
            }
        }

        // ---------------------------------------------------------------- 用户配置
        private sealed class UserCfg
        {
            public double Lat, Lon, Alt;
            public List<string> Fav = new List<string>();
            public List<string> Recent = new List<string>();
        }

        private void LoadUser()
        {
            var o = Json.AsObj(Json.ParseFile(PathOf("sat_user.json")));
            if (o != null)
            {
                Station = new Site(Json.GetNum(o, "lat", Site.Default.LatDeg), Json.GetNum(o, "lon", Site.Default.LonDeg), Json.GetNum(o, "alt", Site.Default.AltM));
                Favorites.Clear();
                var fav = Json.AsArr(o["fav"]);
                if (fav != null) foreach (var f in fav) if (f is string) Favorites.Add((string)f);
            }
            foreach (var s in Sats) s.Favorite = Favorites.Contains(s.Name);
        }

        public void SaveUser()
        {
            var sb = new StringBuilder();
            sb.Append("{\n \"lat\": ").Append(Station.LatDeg.ToString("0.0000000", CultureInfo.InvariantCulture));
            sb.Append(",\n \"lon\": ").Append(Station.LonDeg.ToString("0.0000000", CultureInfo.InvariantCulture));
            sb.Append(",\n \"alt\": ").Append(Station.AltM.ToString("0.0", CultureInfo.InvariantCulture));
            sb.Append(",\n \"fav\": [");
            bool first = true;
            foreach (var f in Favorites)
            {
                if (!first) sb.Append(", ");
                sb.Append("\"").Append(f.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\"");
                first = false;
            }
            sb.Append("]\n}\n");
            try { File.WriteAllText(PathOf("sat_user.json"), sb.ToString(), Encoding.UTF8); } catch { }
        }

        public void ToggleFavorite(SatEntry s)
        {
            if (s == null) return;
            s.Favorite = !s.Favorite;
            if (s.Favorite) Favorites.Add(s.Name); else Favorites.Remove(s.Name);
            SaveUser();
        }

        public SatEntry Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var s in Sats) if (string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        // ---------------------------------------------------------------- 在线更新
        /// <summary>在线更新星历（OMM）与转发器；log 回调给 UI。返回更新条数。</summary>
        public int UpdateOnline(Action<string> log, CancellationToken ct)
        {
            int n = 0;
            string body = HttpGet(OmmUrl, 20000);
            if (!string.IsNullOrEmpty(body) && body.TrimStart().StartsWith("["))
            {
                var arr = Json.AsArr(Json.Parse(body));
                if (arr != null && arr.Count > 0)
                {
                    var list = new List<SatEntry>();
                    int deep = 0;
                    foreach (var it in arr)
                    {
                        var el = SatEl.FromOmm(Json.AsObj(it));
                        if (el == null) continue;
                        el.Source = "celestrak";
                        el.FetchedUtc = DateTime.UtcNow;
                        var e = new SatEntry { El = el };
                        if (!e.Build()) deep++;
                        list.Add(e);
                    }
                    if (list.Count > 10)
                    {
                        // 保留原有转发器映射
                        var byNorad = new Dictionary<int, List<Transponder>>();
                        foreach (var s in Sats) if (s.Transponders.Count > 0) byNorad[s.Norad] = s.Transponders;
                        foreach (var s in list)
                        {
                            List<Transponder> t;
                            if (byNorad.TryGetValue(s.Norad, out t)) s.Transponders = t;
                            s.Favorite = Favorites.Contains(s.Name);
                        }
                        Sats = list;
                        DeepSpaceCount = deep;
                        n = list.Count;
                        try
                        {
                            File.WriteAllText(PathOf("celestrak_omm_cache.json"), body, Encoding.UTF8);
                            TleAgeUtc = File.GetLastWriteTimeUtc(PathOf("celestrak_omm_cache.json"));
                        }
                        catch { }
                        LastSource = "celestrak 在线（" + DateTime.Now.ToString("HH:mm:ss") + "）";
                        if (log != null) log("星历已更新：" + n + " 颗（深空不可递推 " + deep + " 颗）");
                    }
                }
            }
            else if (log != null) log("星历更新失败：celestrak 无有效返回");

            // 转发器
            foreach (string url in new[] { TransUrl, TransUrlFallback })
            {
                if (ct.IsCancellationRequested) break;
                string tb = HttpGet(url, 25000);
                if (string.IsNullOrEmpty(tb)) continue;
                object obj = null;
                try { obj = Json.Parse(tb); } catch { }
                var arr = Json.AsArr(obj);
                var wrap = Json.AsObj(obj);
                if (arr == null && wrap != null) arr = Json.AsArr(wrap["transmitters"]);
                if (arr == null || arr.Count == 0) continue;
                var db = new Dictionary<string, object>();
                foreach (var it in arr)
                {
                    var o = Json.AsObj(it);
                    if (o == null) continue;
                    string st = Json.GetStr(o, "status", "active");
                    if (!string.IsNullOrEmpty(st) && st != "active" && st != "alive") continue;
                    string norad = Json.GetStr(o, "norad_cat_id", "");
                    if (string.IsNullOrEmpty(norad)) continue;
                    double dl = Json.GetNum(o, "downlink_low", 0);
                    if (dl <= 0) dl = Json.GetNum(o, "downlink_high", 0);
                    double ul = Json.GetNum(o, "uplink_low", 0);
                    if (ul <= 0) ul = Json.GetNum(o, "uplink_high", 0);
                    if (dl <= 0 && ul <= 0) continue;
                    var list = Json.AsArr(db.ContainsKey(norad) ? db[norad] : null) ?? new List<object>();
                    var rec = new Dictionary<string, object>
                    {
                        {"name", (Json.GetStr(o,"description", Json.GetStr(o,"type","Transmitter")) ?? "Transmitter")},
                        {"mode", Json.GetStr(o,"mode", Json.GetStr(o,"uplink_mode","FM")) },
                        {"uplink", ul}, {"downlink", dl},
                        {"tx_tone", 0.0}, {"rx_tone", 0.0},
                        {"invert", Json.GetBool(o,"invert",false)}
                    };
                    list.Add(rec);
                    db[norad] = list;
                }
                if (db.Count > 0)
                {
                    try
                    {
                        File.WriteAllText(PathOf("transponders_cache.json"), ToJson(db), Encoding.UTF8);
                        MergeTransponders(PathOf("transponders_cache.json"), true);
                        if (log != null) log("转发器已更新：" + db.Count + " 颗（来源 " + url + "）");
                    }
                    catch { }
                    break;
                }
            }
            foreach (var s in Sats) s.Favorite = Favorites.Contains(s.Name);
            return n;
        }

        private static string ToJson(Dictionary<string, object> db)
        {
            var sb = new StringBuilder("{");
            bool f1 = true;
            foreach (var kv in db)
            {
                if (!f1) sb.Append(",");
                f1 = false;
                sb.Append("\n \"").Append(kv.Key).Append("\":[");
                var items = Json.AsArr(kv.Value);
                bool f2 = true;
                foreach (var it in items)
                {
                    var o = Json.AsObj(it);
                    if (!f2) sb.Append(",");
                    f2 = false;
                    sb.Append("\n  {\"name\":").Append(Quote(Json.GetStr(o, "name", "")))
                      .Append(",\"mode\":").Append(Quote(Json.GetStr(o, "mode", "FM")))
                      .Append(",\"uplink\":").Append(((long)Json.GetNum(o, "uplink", 0)).ToString(CultureInfo.InvariantCulture))
                      .Append(",\"downlink\":").Append(((long)Json.GetNum(o, "downlink", 0)).ToString(CultureInfo.InvariantCulture))
                      .Append(",\"tx_tone\":").Append(Json.GetNum(o, "tx_tone", 0).ToString("0.0", CultureInfo.InvariantCulture))
                      .Append(",\"rx_tone\":").Append(Json.GetNum(o, "rx_tone", 0).ToString("0.0", CultureInfo.InvariantCulture))
                      .Append(",\"invert\":").Append(Json.GetBool(o, "invert", false) ? "true" : "false")
                      .Append("}");
                }
                sb.Append("]");
            }
            sb.Append("\n}");
            return sb.ToString();
        }

        private static string Quote(string s)
        {
            return "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string HttpGet(string url, int timeoutMs)
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = Ua;
                req.Accept = "application/json,*/*";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return sr.ReadToEnd();
            }
            catch { return null; }
        }

        /// <summary>数据年龄文案。</summary>
        public string AgeText()
        {
            if (TleAgeUtc == DateTime.MinValue) return "未知";
            double h = (DateTime.UtcNow - TleAgeUtc).TotalHours;
            if (h < 1) return "刚刚（" + (int)(h * 60) + " 分钟前）";
            if (h < 48) return h.ToString("0.0") + " 小时前";
            return (h / 24).ToString("0.0") + " 天前";
        }
    }
}
