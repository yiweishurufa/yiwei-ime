using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yiwei
{
    /// <summary>
    /// W30 选中文字加注拼音：先在雾凇 base/ext 词库里找词（多音字按词读），找不到再逐字取最常用读音。
    /// 词库只有不带声调的拼音，所以结果不带声调。
    /// 样式：after「你(ni)好(hao)」/ line「拼音单独一行」/ only「只要拼音」/ ruby「HTML &lt;ruby&gt;」。
    /// </summary>
    public static class PinyinAnnotate
    {
        public static readonly KeyValuePair<string, string>[] Styles =
        {
            new KeyValuePair<string, string>("after", "字后括号：你(ni)好(hao)"),
            new KeyValuePair<string, string>("line", "拼音在上一行"),
            new KeyValuePair<string, string>("only", "只要拼音：ni hao"),
            new KeyValuePair<string, string>("ruby", "HTML 注音：<ruby>"),
        };

        static bool IsHan(char c) => c >= 0x3400 && c <= 0x9FFF || c >= 0xF900 && c <= 0xFAFF || c == 0x3007;

        /// <summary>Pinyin for every Han character of <paramref name="text"/> (null where unknown).</summary>
        public static string[] Readings(string text)
        {
            var res = new string[text.Length];
            // all 2–4 character substrings of Han runs, looked up in one pass over the dictionaries
            var want = new HashSet<string>();
            for (int i = 0; i < text.Length; i++)
                for (int n = 2; n <= 4 && i + n <= text.Length; n++)
                {
                    bool han = true; for (int j = i; j < i + n; j++) if (!IsHan(text[j])) { han = false; break; }
                    if (!han) break;
                    want.Add(text.Substring(i, n));
                }
            var words = new Dictionary<string, string>();
            if (want.Count > 0)
                try
                {
                    foreach (var r in AddWord.WordRows())
                        if (want.Contains(r[0]) && !words.ContainsKey(r[0])) words[r[0]] = r[1].Trim();
                }
                catch (Exception e) { Log.Write("pinyin words: " + e.Message); }
            for (int i = 0; i < text.Length;)
            {
                if (!IsHan(text[i])) { i++; continue; }
                int took = 0;
                for (int n = 4; n >= 2 && took == 0; n--)
                {
                    if (i + n > text.Length) continue;
                    if (words.TryGetValue(text.Substring(i, n), out var py))
                    {
                        var parts = py.Split(' ');
                        if (parts.Length != n) continue;
                        for (int k = 0; k < n; k++) res[i + k] = parts[k];
                        took = n;
                    }
                }
                if (took == 0) { res[i] = AddWord.CharPinyin(text[i].ToString()); took = 1; }
                i += took;
            }
            return res;
        }

        public static string Annotate(string text, string style)
        {
            if (text.Length > 2000) throw new Exception("选中的文字太长了（最多 2000 字）");
            var py = Readings(text);
            if (py.All(p => p == null)) throw new Exception("选中的文字里没有能注音的汉字");
            var sb = new StringBuilder();
            switch (style)
            {
                case "only":
                    for (int i = 0; i < text.Length; i++)
                    {
                        if (py[i] != null) { if (sb.Length > 0 && sb[sb.Length - 1] != ' ' && sb[sb.Length - 1] != '\n') sb.Append(' '); sb.Append(py[i]); }
                        else sb.Append(text[i]);
                    }
                    return sb.ToString();
                case "ruby":
                    for (int i = 0; i < text.Length; i++)
                        sb.Append(py[i] != null ? "<ruby>" + text[i] + "<rt>" + py[i] + "</rt></ruby>" : System.Net.WebUtility.HtmlEncode(text[i].ToString()));
                    return sb.ToString();
                case "line":
                    var lines = text.Replace("\r\n", "\n").Split('\n');
                    int at = 0;
                    var outp = new List<string>();
                    foreach (var line in lines)
                    {
                        var top = new List<string>();
                        for (int i = 0; i < line.Length; i++) if (py[at + i] != null) top.Add(py[at + i]);
                        if (top.Count > 0) outp.Add(string.Join(" ", top));
                        outp.Add(line);
                        at += line.Length + 1;
                    }
                    return string.Join(Environment.NewLine, outp);
                default:
                    for (int i = 0; i < text.Length; i++) { sb.Append(text[i]); if (py[i] != null) sb.Append('(').Append(py[i]).Append(')'); }
                    return sb.ToString();
            }
        }
    }
}
