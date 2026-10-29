using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RPGWorkshop
{
    // 小型 JSON 解析器：对象 → Dictionary<string,object>，数组 → List<object>，数字 → double。
    // JsonUtility 读不了顶层数组和任意嵌套，内容包的表都是这种结构，所以自己解析。
    public static class MiniJson
    {
        public static object Parse(string s)
        {
            int i = 0;
            var v = Value(s, ref i);
            return v;
        }

        static void Ws(string s, ref int i)
        {
            while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == '﻿')) i++;
        }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (s.Length - i >= 5 && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (s.Length - i >= 4 && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            return Num(s, ref i);
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            Ws(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (i < s.Length)
            {
                Ws(s, ref i);
                string k = Str(s, ref i);
                Ws(s, ref i);
                i++; // ':'
                d[k] = Value(s, ref i);
                Ws(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                i++; // '}'
                break;
            }
            return d;
        }

        static List<object> Arr(string s, ref int i)
        {
            var a = new List<object>();
            i++;
            Ws(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return a; }
            while (i < s.Length)
            {
                a.Add(Value(s, ref i));
                Ws(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                i++; // ']'
                break;
            }
            return a;
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber));
                        i += 4;
                        break;
                    default: sb.Append(e); break;
                }
            }
            return sb.ToString();
        }

        static object Num(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) { i++; return null; }
            double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double d);
            return d;
        }
    }

    // 读字典字段的小工具，字段缺了或类型不对都给默认值，不抛异常
    public static class J
    {
        public static string S(object o, string k, string def = "")
        {
            if (o is Dictionary<string, object> d && d.TryGetValue(k, out var v) && v != null)
                return v is double n ? n.ToString(CultureInfo.InvariantCulture) : v.ToString();
            return def;
        }

        public static int I(object o, string k, int def = 0)
        {
            if (o is Dictionary<string, object> d && d.TryGetValue(k, out var v))
            {
                if (v is double n) return (int)System.Math.Round(n);
                if (v is string str && int.TryParse(str, out int p)) return p;
            }
            return def;
        }

        public static List<object> L(object o, string k)
        {
            if (o is Dictionary<string, object> d && d.TryGetValue(k, out var v) && v is List<object> l) return l;
            return new List<object>();
        }

        public static Dictionary<string, object> D(object o, string k)
        {
            if (o is Dictionary<string, object> d && d.TryGetValue(k, out var v) && v is Dictionary<string, object> r) return r;
            return null;
        }
    }
}
