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

        public static string Build()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop)) desktop = Path.GetTempPath();
            var zipPath = Path.Combine(desktop, "yiwei-report-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create, Encoding.UTF8))
            {
                Diagnostics.AddText(zip, "版本与环境.txt", Diagnostics.Environment_());
                try
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

                try { var (chars, words) = Frequent(); Diagnostics.AddText(zip, "常用字-Top200.tsv", chars); Diagnostics.AddText(zip, "常用词-Top200.tsv", words); }
                catch (Exception e) { Diagnostics.AddText(zip, "常用字词.读取失败.txt", e.Message); }

                Diagnostics.AddText(zip, "日志错误.txt", LogErrors());
                try { Diagnostics.AddText(zip, "性能体检.txt", PerfCheck.Run().ToString()); }
                catch (Exception e) { Diagnostics.AddText(zip, "性能体检.读取失败.txt", e.Message); }

                Diagnostics.AddText(zip, "说明.txt",
                    "一维输入法维护报告\r\n生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n\r\n" +
                    "包含：版本与环境、设置摘要（已去除密钥与地址）、常用字/常用词 Top " + Top + "（只有字词和次数，没有整句）、日志里的错误与警告、性能体检。\r\n" +
                    "不包含：原文句子、剪贴板、常用语、密码框输入、用户词库文件。\r\n" +
                    "输入统计关闭时没有常用字词数据（设置 → 统计）。\r\n" +
                    "这个文件不会自动上传，请你自己决定是否发给维护者。\r\n");
            }
            Log.Write("maint-report: " + zipPath);
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

        public static void ConfirmAndBuild(Action<string> toast)
        {
            if (!Dialogs.Confirm("将在桌面生成 yiwei-report-日期.zip，内容：\n\n· 版本与设置摘要（已去除密钥）\n· 常用字、常用词 Top 200（只有字词和次数）\n· 日志里的错误与警告\n· 性能体检数据\n\n不含原文句子、剪贴板、常用语和密码框输入；不会自动上传，你可以先打开看看再决定是否发给维护者。\n\n现在生成吗？")) return;
            Task.Run(() =>
            {
                try
                {
                    var p = Build();
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
