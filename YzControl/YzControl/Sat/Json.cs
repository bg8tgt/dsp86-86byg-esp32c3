// ============================================================================
//  YzControl · Sat/Json.cs —— 极简 JSON 解析器（net48，不引第三方包）
//  用途：解析 OMM 星历缓存 / satnogs 转发器缓存 / 配置文件。
//  只支持读取（object / array / string / number / bool / null），不负责写出。
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace YzControl.Sat
{
    /// <summary>JSON 值：null / bool / double / string / List&lt;object&gt; / Dictionary&lt;string,object&gt;</summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            using (var r = new StringReader(text))
            {
                var p = new Parser(r);
                object v = p.ParseValue();
                p.SkipWs();
                return v;
            }
        }

        public static object ParseFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                return Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch { return null; }
        }

        public static Dictionary<string, object> AsObj(object v)
        {
            return v as Dictionary<string, object>;
        }

        public static List<object> AsArr(object v)
        {
            return v as List<object>;
        }

        public static string GetStr(Dictionary<string, object> o, string key, string def = null)
        {
            object v;
            if (o == null || !o.TryGetValue(key, out v) || v == null) return def;
            return v as string ?? Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static double GetNum(Dictionary<string, object> o, string key, double def = 0.0)
        {
            object v;
            if (o == null || !o.TryGetValue(key, out v) || v == null) return def;
            if (v is double) return (double)v;
            if (v is long) return (long)v;
            if (v is int) return (int)v;
            double d;
            if (double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            return def;
        }

        public static bool GetBool(Dictionary<string, object> o, string key, bool def = false)
        {
            object v;
            if (o == null || !o.TryGetValue(key, out v) || v == null) return def;
            if (v is bool) return (bool)v;
            return def;
        }

        private sealed class Parser
        {
            private readonly TextReader _r;
            private int _peek = -2;

            public Parser(TextReader r) { _r = r; }

            private int Next()
            {
                if (_peek == -2) _peek = _r.Read();
                int c = _peek;
                _peek = -2;
                return c;
            }

            private int Peek()
            {
                if (_peek == -2) _peek = _r.Read();
                return _peek;
            }

            public void SkipWs()
            {
                while (Peek() == ' ' || Peek() == '\t' || Peek() == '\r' || Peek() == '\n') Next();
            }

            public object ParseValue()
            {
                SkipWs();
                int c = Peek();
                if (c < 0) return null;
                if (c == '{') return ParseObj();
                if (c == '[') return ParseArr();
                if (c == '"') return ParseStr();
                if (c == 't' || c == 'f') return ParseBool();
                if (c == 'n') { ParseLit("null"); return null; }
                if (c == 'N') { ParseLit("NaN"); return double.NaN; }
                if (c == 'I') { ParseLit("Infinity"); return double.PositiveInfinity; }
                return ParseNum();
            }

            private object ParseObj()
            {
                var d = new Dictionary<string, object>();
                Next(); // {
                SkipWs();
                if (Peek() == '}') { Next(); return d; }
                while (true)
                {
                    SkipWs();
                    string k = ParseStr();
                    SkipWs();
                    if (Next() != ':') throw new FormatException("JSON 期望 ':'");
                    d[k] = ParseValue();
                    SkipWs();
                    int c = Next();
                    if (c == '}') return d;
                    if (c != ',') throw new FormatException("JSON 期望 ',' 或 '}'");
                }
            }

            private object ParseArr()
            {
                var a = new List<object>();
                Next(); // [
                SkipWs();
                if (Peek() == ']') { Next(); return a; }
                while (true)
                {
                    a.Add(ParseValue());
                    SkipWs();
                    int c = Next();
                    if (c == ']') return a;
                    if (c != ',') throw new FormatException("JSON 期望 ',' 或 ']'");
                }
            }

            private string ParseStr()
            {
                var sb = new StringBuilder();
                if (Next() != '"') throw new FormatException("JSON 期望 '\"'");
                while (true)
                {
                    int c = Next();
                    if (c < 0) throw new FormatException("JSON 字符串未闭合");
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        int e = Next();
                        switch (e)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                sb.Append((char)Convert.ToInt32("" + (char)Next() + (char)Next() + (char)Next() + (char)Next(), 16));
                                break;
                            default: throw new FormatException("JSON 非法转义 \\" + (char)e);
                        }
                    }
                    else sb.Append((char)c);
                }
            }

            private object ParseBool()
            {
                int c = Peek();
                if (c == 't') { ParseLit("true"); return true; }
                ParseLit("false"); return false;
            }

            private void ParseLit(string lit)
            {
                foreach (char ch in lit)
                {
                    if (Next() != ch) throw new FormatException("JSON 期望 " + lit);
                }
            }

            private object ParseNum()
            {
                var sb = new StringBuilder();
                while (true)
                {
                    int c = Peek();
                    if (c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E' || (c >= '0' && c <= '9'))
                    {
                        sb.Append((char)Next());
                        // 负无穷：-Infinity
                        if (sb.Length == 1 && sb[0] == '-' && Peek() == 'I')
                        {
                            ParseLit("Infinity");
                            return double.NegativeInfinity;
                        }
                    }
                    else break;
                }
                string s = sb.ToString();
                if (s.Length == 0) throw new FormatException("JSON 数字缺失");
                double d;
                if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) throw new FormatException("JSON 数字非法：" + s);
                return d;
            }
        }
    }
}
