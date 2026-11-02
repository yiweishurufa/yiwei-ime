using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Yiwei
{
    /// <summary>
    /// 设置 → 关于 →「生成维护报告」：用户手动点按钮，才在桌面生成 yiwei-report-日期.zip，由用户自己发给维护者。
    /// 内容：版本与环境、设置摘要（去掉密钥）、常用字/常用词 Top 200（只有字词和次数）、日志里的错误与警告、性能体检。
    /// 不含：原文句子、剪贴板、常用语、密码框输入、用户词库文件；不自动上传。
    /// </summary>
    public static class MaintReport
    {
        const int Top = 200;

        /// <summary>报告里的各个部分，生成前的弹窗里可以逐项取消勾选（阶段 2，用户 2026-10-19 要求）。</summary>
        [Flags]
        public enum Parts
        {
            None = 0,
            Environment = 1,   // 版本与环境
            Settings = 2,      // 设置摘要（去密钥）
            Frequent = 4,      // 常用字 / 常用词 Top 200
            Logs = 8,          // 日志里的错误与警告
            Perf = 16,         // 性能体检
            Typos = 32,        // 选词不顺的词（翻页才选到 / 退格重打）
            All = Environment | Settings | Frequent | Logs | Perf | Typos,
        }

        public static readonly (Parts part, string title, string desc)[] PartInfo =
        {
            (Parts.Environment, "版本与环境", "一维、小狼毫、Windows 版本"),
            (Parts.Settings, "设置摘要", "你改过哪些设置（已去除密钥、地址、账号）"),
            (Parts.Frequent, "常用字、常用词 Top " + Top, "只有字词和次数，没有整句；输入统计关闭时为空"),
            (Parts.Logs, "日志里的错误与警告", "最近 7 天的 RIME 日志与助手日志，只摘出错误行"),
            (Parts.Perf, "性能体检", "按键到出候选的耗时统计"),
            (Parts.Typos, "选词不顺的词 Top " + TypoTop, "翻页才选到的词、上屏后马上退格重打的词，只有词和次数；输入统计关闭时为空"),
        };

        const int TypoTop = 100;

        public static string Build() => Build(Parts.All);

        public static string Build(Parts parts)
        {
            if (parts == Parts.None) throw new InvalidOperationException("没有选择任何内容");
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop)) desktop = Path.GetTempPath();
            var zipPath = Path.Combine(desktop, "yiwei-report-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create, Encoding.UTF8))
            {
                if (parts.HasFlag(Parts.Environment)) Diagnostics.AddText(zip, "版本与环境.txt", Diagnostics.Environment_());
                if (parts.HasFlag(Parts.Settings)) try
                {
                    if (File.Exists(Paths.SettingsFile))
                    {
                        var json = File.ReadAllText(Paths.SettingsFile, Encoding.UTF8);
                        json = Regex.Replace(json, "\"[A-Za-z]*(Key|Token|Secret|Password|Pass|Url|Endpoint|User)[A-Za-z]*\"\\s*:\\s*\"[^\"]*\"", m =>
                            "\"" + m.Value.Split('"')[1] + "\":\"（已去除）\"", RegexOptions.IgnoreCase);
                        Diagnostics.AddText(zip, "设置摘要.json", json);
                    }
                }
                catch (Exception e) { Diagnostics.AddText(zip, "设置摘要.读取失败.txt", e.Message); }

                if (parts.HasFlag(Parts.Frequent)) try { var (chars, words) = Frequent(); Diagnostics.AddText(zip, "常用字-Top200.tsv", chars); Diagnostics.AddText(zip, "常用词-Top200.tsv", words); }
                catch (Exception e) { Diagnostics.AddText(zip, "常用字词.读取失败.txt", e.Message); }

                if (parts.HasFlag(Parts.Typos)) try { var (paged, retyped) = Typos(); Diagnostics.AddText(zip, "翻页才选到的词-Top" + TypoTop + ".tsv", paged); Diagnostics.AddText(zip, "退格重打的词-Top" + TypoTop + ".tsv", retyped); }
                catch (Exception e) { Diagnostics.AddText(zip, "选词不顺.读取失败.txt", e.Message); }

                if (parts.HasFlag(Parts.Logs)) Diagnostics.AddText(zip, "日志错误.txt", LogErrors());
                if (parts.HasFlag(Parts.Perf)) try { Diagnostics.AddText(zip, "性能体检.txt", PerfCheck.Run().ToString()); }
                catch (Exception e) { Diagnostics.AddText(zip, "性能体检.读取失败.txt", e.Message); }

                Diagnostics.AddText(zip, "说明.txt",
                    "一维输入法维护报告\r\n生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n\r\n" +
                    "包含：" + string.Join("、", PartInfo.Where(x => parts.HasFlag(x.part)).Select(x => x.title)) + "。\r\n" +
                    (parts == Parts.All ? "" : "生成时你取消了：" + string.Join("、", PartInfo.Where(x => !parts.HasFlag(x.part)).Select(x => x.title)) + "。\r\n") +
                    "不包含：原文句子、剪贴板、常用语、密码框输入、用户词库文件。\r\n" +
                    "输入统计关闭时没有常用字词数据（设置 → 统计）。\r\n" +
                    "这个文件不会自动上传，请你自己决定是否发给维护者。\r\n");
            }
            Log.Write("maint-report: " + zipPath + " parts=" + parts);
            return zipPath;
        }

        static (string chars, string words) Frequent()
        {
            var c = new Dictionary<string, long>(); var w = new Dictionary<string, long>();
            foreach (var f in Directory.GetFiles(Paths.YiweiDir, "stats-*.tsv"))
            {
                string[] lines; try { lines = File.ReadAllLines(f, Encoding.UTF8); } catch { continue; }
                foreach (var line in lines)
                {
                    int tab = line.IndexOf('\t'); if (tab <= 0) continue;
                    var text = line.Substring(tab + 1).Trim();
                    // 只统计纯汉字的一次上屏（含 2–8 字的词）；带标点/字母/数字的整句、密码类内容一律不统计
                    if (text.Length == 0 || text.Length > 8 || !text.All(ch => ch >= 0x3400 && ch <= 0x9FFF)) continue;
                    if (text.Length >= 2) { w.TryGetValue(text, out var n); w[text] = n + 1; }
                    foreach (var ch in text) { var k = ch.ToString(); c.TryGetValue(k, out var m); c[k] = m + 1; }
                }
            }
            string Fmt(Dictionary<string, long> d) => string.Join("\r\n", d.OrderByDescending(x => x.Value).Take(Top).Select(x => x.Key + "\t" + x.Value)) + "\r\n";
            return (Fmt(c), Fmt(w));
        }

        /// <summary>
        /// 汇总 yiwei/typo-*.tsv（yiwei_stats.lua 写的，只有词，没有时间和前后文）：
        /// 翻页才选到的词 →「词 次数」；退格重打 →「删掉的字 → 重打的字 次数」。
        /// </summary>
        internal static (string paged, string retyped) Typos() => Typos(Directory.GetFiles(Paths.YiweiDir, "typo-*.tsv"));

        internal static (string paged, string retyped) Typos(IEnumerable<string> files)
        {
            var paged = new Dictionary<string, long>(); var retyped = new Dictionary<string, long>();
            foreach (var f in files)
            {
                string[] lines; try { lines = File.ReadAllLines(f, Encoding.UTF8); } catch { continue; }
                foreach (var line in lines)
                {
                    var p = line.Split('\t');
                    if (p.Length == 2 && p[0] == "paged" && IsWord(p[1])) Bump(paged, p[1]);
                    else if (p.Length == 3 && p[0] == "retype" && IsWord(p[1]) && IsWord(p[2])) Bump(retyped, p[1] + "\t" + p[2]);
                }
            }
            string Fmt(Dictionary<string, long> d, string head) => head + "\r\n" + string.Join("\r\n", d.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Take(TypoTop).Select(x => x.Key + "\t" + x.Value)) + "\r\n";
            return (Fmt(paged, "词\t次数"), Fmt(retyped, "删掉的字\t重打成\t次数"));
        }

        static void Bump(Dictionary<string, long> d, string k) { d.TryGetValue(k, out var n); d[k] = n + 1; }

        // 与 Lua 端一致：1–8 个汉字；文件被手改过也不会把别的内容带进报告
        static bool IsWord(string s) => s.Length >= 1 && s.Length <= 8 && s.All(ch => ch >= 0x3400 && ch <= 0x9FFF);

        static string LogErrors()
        {
            var sb = new StringBuilder();
            var files = new List<string>();
            foreach (var d in new[] { "rime.yiwei", "rime.weasel" })
            {
                var dir = Path.Combine(Path.GetTempPath(), d);
                if (Directory.Exists(dir)) files.AddRange(Directory.GetFiles(dir).Where(f => DateTime.Now - File.GetLastWriteTime(f) < TimeSpan.FromDays(7)));
            }
            var hl = Path.Combine(Path.GetTempPath(), "yiwei-helper.log");
            if (File.Exists(hl)) files.Add(hl);
            foreach (var f in files)
            {
                var hits = new List<string>();
                try
                {
                    using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var r = new StreamReader(fs, Encoding.UTF8))
                    {
                        if (fs.Length > 2 << 20) fs.Seek(-(2 << 20), SeekOrigin.End);
                        string l;
                        while ((l = r.ReadLine()) != null)
                            if (Regex.IsMatch(l, @"\b(ERROR|WARNING|exception|failed)\b|^\S*[EW]\d{4}|start:|pipe:|send:", RegexOptions.IgnoreCase)) hits.Add(l.Length > 300 ? l.Substring(0, 300) : l);
                    }
                }
                catch (Exception e) { hits.Add("读取失败：" + e.Message); }
                sb.AppendLine("== " + Path.GetFileName(f) + "（共 " + hits.Count + " 行，保留最后 200 行）");
                foreach (var h in hits.Skip(Math.Max(0, hits.Count - 200))) sb.AppendLine(h);
            }
            return sb.Length == 0 ? "没有找到日志。\r\n" : sb.ToString();
        }

        static Parts _lastParts = Parts.All;

        public static void ConfirmAndBuild(Action<string> toast)
        {
            var parts = MaintReportDialog.Ask(_lastParts);
            if (parts == Parts.None) return;
            _lastParts = parts;
            Task.Run(() =>
            {
                try
                {
                    var p = Build(parts);
                    try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + p + "\""); } catch { }
                    try { Program.Ui.BeginInvoke(new Action(() => toast("已放到桌面：" + Path.GetFileName(p)))); } catch { }
                }
                catch (Exception e)
                {
                    Log.Write("maint-report: " + e);
                    try { Program.Ui.BeginInvoke(new Action(() => toast("生成失败：" + e.GetBaseException().Message))); } catch { }
                }
            });
        }
    }
}
