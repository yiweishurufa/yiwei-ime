using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace Yiwei
{
    /// <summary>雾凇拼音功能开关、模糊音、生僻字（大字表 + 字体回退）。保存在 settings.json 的 Features 里。</summary>
    public class RimeFeatureSettings
    {
        // 快捷输入（雾凇 Lua 组件）
        public bool DateTime { get; set; } = true;      // date_translator：rq sj xq dt ts rqzh rqen（双拼：date time …）
        public bool Lunar { get; set; } = true;         // lunar：nl；N+公历日期
        public bool Calculator { get; set; } = true;    // calc_translator：cC
        public bool NumberUpper { get; set; } = true;   // number_translator：R+数字
        public bool Unicode { get; set; } = true;       // unicode：U+码位
        public bool Uuid { get; set; } = true;          // uuid
        public bool SelectChar { get; set; } = true;    // select_character：[ ]
        public bool RadicalLookup { get; set; } = true; // radical_lookup：uU 拆字反查
        public bool Corrector { get; set; } = true;     // corrector：错音错字提示

        // 模糊音（只对全拼 rime_ice 生效）
        public List<string> Fuzzy { get; set; } = new List<string>();

        // 生僻字
        public bool BigCharset { get; set; } = false;   // 挂载 cn_dicts/41448 大字表
        public bool FontFallback { get; set; } = true;  // 候选字体回退链
    }

    public static class RimeFeatures
    {
        public const string BigDict = "yiwei_ice_big";

        /// <summary>Schemas whose *.custom.yaml the helper writes (all rime-ice based).</summary>
        public static IEnumerable<string> PatchedSchemas => Rime.Schemas.Select(s => s.Id);

        public static RimeFeatureSettings Of(Settings s)
        {
            if (s.Features == null) s.Features = new RimeFeatureSettings();
            if (s.Features.Fuzzy == null) s.Features.Fuzzy = new List<string>();
            return s.Features;
        }

        // ---------------- 模糊音 ----------------

        public sealed class FuzzyPair
        {
            public string Id, Label, Example; public string[] Rules;
            public FuzzyPair(string id, string label, string example, params string[] rules) { Id = id; Label = label; Example = example; Rules = rules; }
        }

        /// <summary>Each pair works both ways. Rules are appended after rime-ice's own algebra.</summary>
        public static readonly FuzzyPair[] FuzzyPairs =
        {
            new FuzzyPair("zh_z", "zh = z", "zi ↔ zhi", "derive/^zh/z/", "derive/^z([^h])/zh$1/"),
            new FuzzyPair("ch_c", "ch = c", "ci ↔ chi", "derive/^ch/c/", "derive/^c([^h])/ch$1/"),
            new FuzzyPair("sh_s", "sh = s", "si ↔ shi", "derive/^sh/s/", "derive/^s([^h])/sh$1/"),
            new FuzzyPair("n_l", "n = l", "nai ↔ lai", "derive/^n/l/", "derive/^l/n/"),
            new FuzzyPair("f_h", "f = h", "fu ↔ hu", "derive/^f/h/", "derive/^h/f/"),
            new FuzzyPair("r_l", "r = l", "ri ↔ li", "derive/^r/l/", "derive/^l/r/"),
            // an/ang 不碰 ian/uan（各有单独选项）
            new FuzzyPair("an_ang", "an = ang", "fan ↔ fang", "derive/(^|[^iu])an$/$1ang/", "derive/(^|[^iu])ang$/$1an/"),
            new FuzzyPair("en_eng", "en = eng", "fen ↔ feng", "derive/en$/eng/", "derive/eng$/en/"),
            new FuzzyPair("in_ing", "in = ing", "jin ↔ jing", "derive/in$/ing/", "derive/ing$/in/"),
            new FuzzyPair("ian_iang", "ian = iang", "xian ↔ xiang", "derive/ian$/iang/", "derive/iang$/ian/"),
            new FuzzyPair("uan_uang", "uan = uang", "guan ↔ guang", "derive/uan$/uang/", "derive/uang$/uan/"),
        };

        // ---------------- 速查卡 ----------------

        public sealed class Feature
        {
            public string Key, Title, Desc;
            public Func<RimeFeatureSettings, bool> Get; public Action<RimeFeatureSettings, bool> Set;
            /// <summary>(触发码, 示例) for full pinyin and for double pinyin.</summary>
            public string[][] Full, Double;
        }

        static string[] R(string code, string example) => new[] { code, example };

        public static readonly Feature[] Features =
        {
            new Feature { Key = "date", Title = "日期与时间", Desc = "输入触发码，候选里出现当前日期、时间、星期",
                Get = f => f.DateTime, Set = (f, v) => f.DateTime = v,
                Full = new[] { R("rq", "今天的日期，如 2026-10-08"), R("sj", "现在时间，如 21:52"), R("xq", "星期几"), R("dt", "ISO 8601 日期时间"), R("ts", "Unix 时间戳"), R("rqzh", "中文日期，如 二〇二六年十月八日"), R("rqen", "英文日期，如 8 October 2026") },
                Double = new[] { R("date", "今天的日期，如 2026-10-08"), R("time", "现在时间"), R("week", "星期几"), R("datetime", "ISO 8601 日期时间"), R("timestamp", "Unix 时间戳"), R("datezh", "中文日期"), R("dateen", "英文日期") } },
            new Feature { Key = "lunar", Title = "农历", Desc = "今天的农历，或把公历日期换成农历",
                Get = f => f.Lunar, Set = (f, v) => f.Lunar = v,
                Full = new[] { R("nl", "今天的农历（干支年 + 生肖 + 月日）"), R("N20261001", "公历 2026-10-01 转农历") },
                Double = new[] { R("lunar", "今天的农历（干支年 + 生肖 + 月日）"), R("N20261001", "公历 2026-10-01 转农历") } },
            new Feature { Key = "calc", Title = "计算器", Desc = "cC 后面写算式，支持 + - * / ^ ( ) 和常用函数",
                Get = f => f.Calculator, Set = (f, v) => f.Calculator = v,
                Full = new[] { R("cC1+2*3", "7，或 1+2*3=7"), R("cC2^10", "1024"), R("cCsqrt(2)", "根号 2") } },
            new Feature { Key = "number", Title = "大写数字与金额", Desc = "R 后面写数字，转成中文大写或金额大写",
                Get = f => f.NumberUpper, Set = (f, v) => f.NumberUpper = v,
                Full = new[] { R("R1234.5", "中文数字和金额大写，如 壹仟贰佰叁拾肆元伍角") } },
            new Feature { Key = "unicode", Title = "Unicode 码位", Desc = "U 后面写十六进制码位，直接打出任意字符（包括生僻字）",
                Get = f => f.Unicode, Set = (f, v) => f.Unicode = v,
                Full = new[] { R("U62fc", "拼"), R("U20000", "𠀀（扩展 B）") } },
            new Feature { Key = "uuid", Title = "UUID", Desc = "生成一个随机 UUID",
                Get = f => f.Uuid, Set = (f, v) => f.Uuid = v,
                Full = new[] { R("uuid", "3f2b…-…（每次不同）") } },
            new Feature { Key = "select", Title = "以词定字", Desc = "打出词后按 [ 取第一个字、按 ] 取最后一个字",
                Get = f => f.SelectChar, Set = (f, v) => f.SelectChar = v,
                Full = new[] { R("jiandan [", "简单 → 简"), R("jiandan ]", "简单 → 单") } },
            new Feature { Key = "radical", Title = "拆字反查", Desc = "不会读的字：uU 后面依次写各部件的拼音",
                Get = f => f.RadicalLookup, Set = (f, v) => f.RadicalLookup = v,
                Full = new[] { R("uUmumumu", "森（木木木）"), R("uUshuishui", "沝") } },
            new Feature { Key = "corrector", Title = "错音错字提示", Desc = "打了常见的错读或错字时，在候选旁提示正确写法",
                Get = f => f.Corrector, Set = (f, v) => f.Corrector = v,
                Full = new[] { R("geiyu", "给予 · 提示 jǐ yǔ"), R("annai", "按耐 · 提示 按捺(nà)") } },
        };

        /// <summary>Always on (the component cannot be switched off safely), shown in the 速查卡 only.</summary>
        public static readonly string[][] AlwaysOn =
        {
            R("拼音 + ` + 部件拼音", "辅码筛字：ni`r 只留带「亻」的字，如 你"),
            R("v + 缩写", "符号：vfh 符号、vjt 箭头、vsx 数学、vhelp 全部列表（双拼用大写 V）"),
            R("Ctrl+Delete", "删除或降权选中的候选词"),
        };

        // ---------------- 写入 *.custom.yaml ----------------

        static string Q(string v) => "\"" + (v ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        /// <summary>Patch lines (indented under patch:) for one rime-ice schema.</summary>
        public static string PatchLines(string schema, Settings s)
        {
            var f = Of(s);
            bool full = schema == "rime_ice";
            var y = new StringBuilder();
            void P(string key, string value) => y.Append("  \"").Append(key).Append("\": ").Append(value).Append('\n');
            const string Never = "\"^$\""; // a recognizer pattern that never matches typed input

            if (!f.DateTime)
                foreach (var k in new[] { "date", "time", "week", "datetime", "timestamp", "datezh", "dateen" })
                    P("date_translator/" + k, "\"\"");
            if (!f.Lunar) { P("lunar", "\"\""); P("recognizer/patterns/gregorian_to_lunar", Never); }
            if (!f.Calculator) P("recognizer/patterns/calculator", Never);
            if (!f.NumberUpper) P("recognizer/patterns/number", Never);
            if (!f.Unicode) P("recognizer/patterns/unicode", Never);
            if (!f.Uuid) P("uuid", "\"\"");
            if (!f.SelectChar) { P("key_binder/select_first_character", "\"\""); P("key_binder/select_last_character", "\"\""); }
            if (!f.RadicalLookup) P("recognizer/patterns/radical_lookup", Never);
            // corrector.lua 依赖 spelling_hints 生成的注释；关掉提示 = 关掉错音错字提示（雾凇 corrector.lua 头部注释的做法）
            if (!f.Corrector) P("translator/spelling_hints", "0");

            if (f.BigCharset)
            {
                P("translator/dictionary", BigDict);
                P("translator/user_dict", "rime_ice"); // 继续用原来的用户词库，不丢已学的词
                P("radical_reverse_lookup/dictionary", BigDict);
            }

            // 模糊音：追加到雾凇原有 algebra 之后（librime 的 /+ 追加语法），不覆盖原规则。
            // 双拼方案的 algebra 先把全拼转成双拼键位，追加的全拼规则无效，所以只给全拼。
            if (full)
            {
                var rules = FuzzyPairs.Where(p => f.Fuzzy.Contains(p.Id)).SelectMany(p => p.Rules).ToList();
                if (rules.Count > 0)
                {
                    y.Append("  \"speller/algebra/+\":\n");
                    foreach (var r in rules) y.Append("    - ").Append(r).Append('\n');
                }
            }
            return y.ToString();
        }

        /// <summary>Makes sure the big-charset dictionary file exists in the user folder when it is switched on.</summary>
        public static void EnsureBigDict(Settings s)
        {
            if (!Of(s).BigCharset) return;
            try
            {
                var path = Path.Combine(Paths.UserDir, BigDict + ".dict.yaml");
                var y = new StringBuilder();
                y.Append("# 由「一维输入法设置」生成：雾凇拼音 + 41448 字大字表（生僻字）\n");
                y.Append("# encoding: utf-8\n---\nname: ").Append(BigDict).Append("\nversion: \"1\"\nsort: by_weight\nimport_tables:\n");
                // import_tables 不递归，所以把 rime_ice.dict.yaml 的表逐个列出；41448 紧跟 8105（雾凇的要求）
                foreach (var t in new[] { "rime_ice", "cn_dicts/8105", "cn_dicts/41448", "cn_dicts/base", "cn_dicts/ext", "cn_dicts/tencent", "cn_dicts/others" })
                    y.Append("  - ").Append(t).Append('\n');
                y.Append("...\n");
                var text = y.ToString();
                if (!File.Exists(path) || File.ReadAllText(path, Encoding.UTF8) != text)
                    File.WriteAllText(path, text, new UTF8Encoding(false));
            }
            catch (Exception e) { Log.Write("big dict: " + e.Message); }
        }

        /// <summary>Whether the 41448 table is installed (shared data or user folder).</summary>
        public static bool BigTableAvailable =>
            File.Exists(Path.Combine(Paths.SharedDataDir, "cn_dicts", "41448.dict.yaml")) ||
            File.Exists(Path.Combine(Paths.UserDir, "cn_dicts", "41448.dict.yaml"));

        // ---------------- 字体回退 ----------------

        /// <summary>Fallback fonts, in order: wide-coverage CJK fonts (if installed), Windows' own Ext-B/G fonts, emoji.</summary>
        public static readonly string[] FallbackFonts =
        {
            "Plangothic P1", "Plangothic P2",           // 遍黑体
            "MiSans L3",
            "HanaMinA", "HanaMinB",                     // 花园明朝
            "TH-Tshyn-P0", "TH-Tshyn-P1", "TH-Tshyn-P2", // 天珩全字库
            "SimSun-ExtB", "SimSun-ExtG",               // Windows 自带（扩展 B / 扩展 G）
            "Segoe UI Emoji", "Segoe UI Symbol",
        };

        static HashSet<string> _installed;

        /// <summary>Installed font family names (from the Fonts registry keys, machine and per-user).</summary>
        public static HashSet<string> InstalledFonts
        {
            get
            {
                if (_installed != null) return _installed;
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
                {
                    try
                    {
                        using (var k = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts"))
                            if (k != null)
                                foreach (var n in k.GetValueNames())
                                {
                                    var name = n;
                                    var paren = name.IndexOf(" (", StringComparison.Ordinal);
                                    if (paren > 0) name = name.Substring(0, paren);
                                    foreach (var part in name.Split('&')) set.Add(part.Trim());
                                }
                    }
                    catch { }
                }
                return _installed = set;
            }
        }

        public static bool IsInstalled(string family)
        {
            var set = InstalledFonts;
            if (set.Contains(family)) return true;
            // registry names can carry a style ("Plangothic P1 Regular")
            return set.Any(n => n.StartsWith(family + " ", StringComparison.OrdinalIgnoreCase));
        }

        public static List<string> InstalledFallbacks() => FallbackFonts.Where(IsInstalled).ToList();

        /// <summary>style/font_face value: the chosen font first, then the installed fallbacks.</summary>
        public static string FontFaceChain(Settings s)
        {
            var main = string.IsNullOrWhiteSpace(s.FontFace) ? "Microsoft YaHei UI" : s.FontFace.Trim();
            if (!Of(s).FontFallback || main.Contains(",")) return main; // a hand-written chain is kept as is
            var chain = new List<string> { main };
            foreach (var f in InstalledFallbacks())
                if (!chain.Contains(f, StringComparer.OrdinalIgnoreCase)) chain.Add(f);
            return string.Join(", ", chain);
        }
    }
}
