using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Yiwei
{
    /// <summary>
    /// 「我的词条」：用户自己写的词、拼音和权重，存成用户文件夹 yiwei_dicts\yiwei.mine.dict.yaml，
    /// 由 rime_ice.dict.yaml 的 import_tables 挂在最前面（DomainDicts.Mount / customize_data.py 同一规则），
    /// 雾凇每日更新不冲突。编辑框里一行一条「词 拼音 权重」，拼音和权重可省（拼音自动注音，权重默认 <see cref="DefaultWeight"/>）。
    /// 与「加词」(AddWord，写用户词库 userdb) 是两条路：这里是人手写的、可以随时看到和改的清单。
    /// </summary>
    static class CustomWords
    {
        public const string DictId = "mine";
        public const long DefaultWeight = 1000000;
        const string Header = "# 一维输入法 · 我的词条（设置 → 词库 →「我的词条」编辑；手改也行，格式：词<TAB>拼音<TAB>权重）\n---\nname: yiwei.mine\nversion: \"user\"\nsort: by_weight\n...\n";
        static readonly Regex Syllables = new Regex(@"^[a-z]+( [a-z]+)*$");

        public static string FilePath => Path.Combine(Paths.UserDir, "yiwei_dicts", "yiwei." + DictId + ".dict.yaml");

        public sealed class Entry { public string Word, Pinyin; public long Weight; }

        /// <summary>读用户文件夹里现有的词条（没有文件 = 空）。</summary>
        public static List<Entry> Load()
        {
            var list = new List<Entry>();
            if (!File.Exists(FilePath)) return list;
            bool body = false;
            foreach (var line in File.ReadLines(FilePath, Encoding.UTF8))
            {
                if (!body) { if (line.StartsWith("...")) body = true; continue; }
                if (line.Length == 0 || line[0] == '#') continue;
                var p = line.Split('\t');
                if (p.Length < 2) continue;
                long w = p.Length >= 3 && long.TryParse(p[2], out var x) ? x : DefaultWeight;
                list.Add(new Entry { Word = p[0], Pinyin = p[1], Weight = w });
            }
            return list;
        }

        /// <summary>编辑框里的文本（一行一条，权重和默认值相同时省略）。</summary>
        public static string ToText(IEnumerable<Entry> entries) =>
            string.Join("\r\n", entries.Select(e => e.Word + " " + e.Pinyin + (e.Weight == DefaultWeight ? "" : " " + e.Weight)));

        /// <summary>
        /// 把编辑框文本解析成词条；格式或注音有问题的行收集到 <paramref name="errors"/>（带行号），不会抛异常。
        /// 一行：词 [拼音 ...] [权重]。词不能含空白；拼音只许小写字母和空格；权重是正整数，最后一项是纯数字才算权重。
        /// </summary>
        /// <summary>去掉拼音声调符号（ǐ→i），ü/ǖ 系列统一成 v。</summary>
        static string StripTones(string py)
        {
            if (py.All(c => c < 0x80)) return py;
            var sb = new StringBuilder();
            foreach (var c in py.Replace('\u01d6', 'v').Replace('\u01d8', 'v').Replace('\u01da', 'v').Replace('\u01dc', 'v').Normalize(NormalizationForm.FormD))
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        public static List<Entry> Parse(string text, List<string> errors)
        {
            var result = new List<Entry>();
            var seen = new HashSet<string>();
            int no = 0;
            foreach (var raw in (text ?? "").Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                no++;
                var line = raw.Replace("\uFEFF", "").Replace("\u200B", "").Replace("\u200D", "").Normalize(NormalizationForm.FormKC).Trim(); // 去掉 BOM/零宽字符（txt 导入常带）；全角字母/数字/空格也认
                if (line.Length == 0 || line[0] == '#') continue;
                var tok = line.Split(new[] { ' ', '\t', '\u3000' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                var word = tok[0];
                tok.RemoveAt(0);
                long weight = DefaultWeight;
                if (tok.Count > 0 && tok[tok.Count - 1].All(char.IsDigit))
                {
                    if (!long.TryParse(tok[tok.Count - 1], out weight) || weight <= 0 || weight > 2000000000L) { errors.Add("第 " + no + " 行：权重要是 1 到 20 亿的整数"); continue; }
                    tok.RemoveAt(tok.Count - 1);
                }
                if (word.Length > 32) { errors.Add("第 " + no + " 行：词太长（最多 32 个字）"); continue; }
                string py = string.Join(" ", tok).ToLowerInvariant().Replace('\u00fc', 'v'); // nü 也认
                py = StripTones(py); // nǐ hǎo（带声调符号）也认
                py = Regex.Replace(py, "[^a-z ]+", " "); // 除字母外的任何符号（撇号、连字符、逗号、斜杠、句点等）都当音节分隔
                py = Regex.Replace(py, " {2,}", " ").Trim();
                if (py.Length == 0)
                {
                    if (!AddWord.Cjk.IsMatch(word)) { errors.Add("第 " + no + " 行「" + word + "」：不是纯汉字词，请写上拼音"); continue; }
                    py = AddWord.Guess(word);
                    if (py.Length == 0) { errors.Add("第 " + no + " 行「" + word + "」：有字找不到读音，请写上拼音"); continue; }
                }
                if (py.Length > 0 && py.IndexOf(' ') < 0 && word.Length > 1 && AddWord.Cjk.IsMatch(word))
                {   // 拼音连写（yiweishurufa）：和自动注音去掉空格后一致才拆开
                    var g = AddWord.Guess(word);
                    if (g.Length > 0 && g.Replace(" ", "") == py) py = g;
                }
                if (!Syllables.IsMatch(py)) { errors.Add("第 " + no + " 行「" + word + "」：拼音只能是小写字母，音节用空格隔开"); continue; }
                if (AddWord.Cjk.IsMatch(word) && py.Split(' ').Length != word.Length) { errors.Add("第 " + no + " 行「" + word + "」：拼音音节数和字数不一致"); continue; }
                if (!seen.Add(word + "\t" + py))
                {   // 同词同音只留一条，权重取较大的
                    var dup = result.First(x => x.Word == word && x.Pinyin == py);
                    if (weight > dup.Weight) dup.Weight = weight;
                    continue;
                }
                result.Add(new Entry { Word = word, Pinyin = py, Weight = weight });
            }
            return result;
        }

        /// <summary>写 yiwei.mine.dict.yaml；清空 = 删掉用户文件夹里的那份（回到安装包里的空词典）。</summary>
        public static void Save(List<Entry> entries)
        {
            if (entries.Count == 0)
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var sb = new StringBuilder(Header);
            foreach (var e in entries) sb.Append(e.Word).Append('\t').Append(e.Pinyin).Append('\t').Append(e.Weight).Append('\n');
            File.WriteAllText(FilePath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
