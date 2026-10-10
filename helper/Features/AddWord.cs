using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Yiwei
{
    /// <summary>
    /// 选中文字一键加词：给词自动注音（先在雾凇词库里找整词的读音，找不到再逐字取最常用读音），
    /// 写进用户词库同步快照（与「导入搜狗/微软用户词」同一条路），由部署程序 /sync 合并。
    /// </summary>
    public static class AddWord
    {
        static Dictionary<string, string> _chars;              // 字 → 最常用读音
        static readonly object Gate = new object();
        public static readonly Regex Cjk = new Regex(@"^[\u3400-\u9FFF\uF900-\uFAFF\u3007]+$");
        static readonly Regex Syllables = new Regex(@"^[a-z]+( [a-z]+)*$");

        static IEnumerable<string> DictFiles(params string[] names)
        {
            foreach (var dir in new[] { Paths.UserDir, Paths.SharedDataDir })
                foreach (var n in names)
                {
                    var p = Path.Combine(dir, "cn_dicts", n + ".dict.yaml");
                    if (File.Exists(p)) { yield return p; break; }
                }
        }

        static IEnumerable<string[]> Rows(string file)
        {
            bool body = false;
            foreach (var line in File.ReadLines(file, Encoding.UTF8))
            {
                if (!body) { if (line.StartsWith("...")) body = true; continue; }
                if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split('\t');
                if (p.Length >= 2) yield return p;
            }
        }

        static void LoadChars()
        {
            lock (Gate)
            {
                if (_chars != null) return;
                var best = new Dictionary<string, Tuple<string, long>>();
                foreach (var f in DictFiles("8105").Concat(DictFiles("41448")))
                    foreach (var p in Rows(f))
                    {
                        if (p[0].Length == 0 || p[0].Length != (char.IsHighSurrogate(p[0][0]) ? 2 : 1)) continue;
                        long w = p.Length >= 3 && long.TryParse(p[2], out var x) ? x : 0;
                        if (!best.TryGetValue(p[0], out var cur) || w > cur.Item2) best[p[0]] = Tuple.Create(p[1].Trim(), w);
                    }
                _chars = best.ToDictionary(k => k.Key, v => v.Value.Item1);
            }
        }

        /// <summary>Best-guess pinyin ("ni hao"), or "" when some character has no known reading.</summary>
        public static string Guess(string word)
        {
            word = (word ?? "").Trim();
            if (word.Length == 0) return "";
            // whole word in the rime-ice dictionaries: its reading handles 多音字 correctly
            foreach (var f in DictFiles("base").Concat(DictFiles("ext")).Concat(DictFiles("tencent")))
            {
                try
                {
                    foreach (var p in Rows(f))
                        if (p[0] == word && Syllables.IsMatch(p[1].Trim())) return p[1].Trim();
                }
                catch (Exception e) { Log.Write("addword scan " + f + ": " + e.Message); }
            }
            LoadChars();
            var parts = new List<string>();
            for (int i = 0; i < word.Length; i++)
            {
                string ch = char.IsHighSurrogate(word[i]) && i + 1 < word.Length ? word.Substring(i++, 2) : word[i].ToString();
                if (!_chars.TryGetValue(ch, out var py)) return "";
                parts.Add(py);
            }
            return string.Join(" ", parts);
        }

        public static int CharCount(string s)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++) { if (char.IsHighSurrogate(s[i])) i++; n++; }
            return n;
        }

        /// <summary>Checks a word + pinyin pair; returns an error message or null.</summary>
        public static string Validate(string word, string pinyin)
        {
            word = (word ?? "").Trim(); pinyin = Normalize(pinyin);
            if (word.Length == 0) return "请输入要加的词";
            if (!Cjk.IsMatch(word)) return "只能加汉字词（不含空格、字母和标点）";
            if (CharCount(word) > 16) return "词太长了（最多 16 个字）";
            if (!Syllables.IsMatch(pinyin)) return "拼音只能是字母，音节之间用空格隔开";
            if (pinyin.Split(' ').Length != CharCount(word)) return "拼音音节数要和字数一样";
            return null;
        }

        public static string Normalize(string pinyin) =>
            Regex.Replace((pinyin ?? "").ToLowerInvariant().Replace("ü", "v").Replace("u:", "v").Replace("'", " "), @"\s+", " ").Trim();

        /// <summary>Adds the word to the user dictionary (sync snapshot + deployer /sync).</summary>
        public static void Add(string word, string pinyin)
        {
            var err = Validate(word, pinyin);
            if (err != null) throw new Exception(err);
            HabitImport.AddWords(new Dictionary<string, string> { { word.Trim(), Normalize(pinyin) + " " } });
            Log.Write("addword: " + CharCount(word) + " chars");
        }
    }
}
