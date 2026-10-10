using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Yiwei
{
    /// <summary>
    /// 托盘「导出诊断信息」：把 RIME 日志、一维助手日志、用户文件夹里的配置 yaml 和版本信息打包成 zip 放到桌面，
    /// 并在资源管理器里选中它，方便用户提 Issue。
    /// 不打包：用户词库（*.userdb、*.userdb.txt、sync/ 快照）、输入统计（stats、apps-*.tsv）、常用语、剪贴板、
    /// 词典正文（*.dict.yaml）、编译产物（build/）和 AI 密钥。
    /// </summary>
    public static class Diagnostics
    {
        const long MaxLogBytes = 2L << 20;   // keep the tail of a long log
        static readonly TimeSpan LogAge = TimeSpan.FromDays(7);

        /// <summary>Builds the package; returns its path.</summary>
        public static string Export()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(desktop) || !Directory.Exists(desktop)) desktop = Path.GetTempPath();
            var zipPath = Path.Combine(desktop, "一维输入法诊断信息-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip");
            var skipped = new List<string>();
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create, Encoding.UTF8))
            {
                AddText(zip, "版本与环境.txt", Environment_());

                // RIME / 小狼毫日志（重命名后的 rime.yiwei 与原名 rime.weasel 都找）
                foreach (var dirName in new[] { "rime.yiwei", "rime.weasel" })
                {
                    var dir = Path.Combine(Path.GetTempPath(), dirName);
                    if (!Directory.Exists(dir)) continue;
                    foreach (var f in SafeFiles(dir, "*", SearchOption.TopDirectoryOnly))
                    {
                        var fi = new FileInfo(f);
                        if (DateTime.Now - fi.LastWriteTime > LogAge) continue;
                        AddLog(zip, "日志/" + dirName + "/" + fi.Name, f);
                    }
                }
                var helperLog = Path.Combine(Path.GetTempPath(), "yiwei-helper.log");
                if (File.Exists(helperLog)) AddLog(zip, "日志/yiwei-helper.log", helperLog);

                // 用户文件夹里的配置（只要 yaml 配置，不要词典正文和用户词库）
                var user = Paths.UserDir;
                if (Directory.Exists(user))
                {
                    foreach (var f in SafeFiles(user, "*.yaml", SearchOption.TopDirectoryOnly))
                    {
                        var name = Path.GetFileName(f);
                        if (IsPrivate(name)) { skipped.Add(name); continue; }
                        if (name.EndsWith(".dict.yaml", StringComparison.OrdinalIgnoreCase) && new FileInfo(f).Length > 64 * 1024) continue;
                        AddFile(zip, "配置/" + name, f);
                    }
                    foreach (var f in SafeFiles(user, "*.lua", SearchOption.TopDirectoryOnly)) AddFile(zip, "配置/" + Path.GetFileName(f), f);
                    // 一维助手自己的状态（设置去掉 AI 密钥）
                    var settings = Paths.SettingsFile;
                    if (File.Exists(settings))
                    {
                        var json = File.ReadAllText(settings, Encoding.UTF8);
                        json = Regex.Replace(json, "\"(AiKeyProtected|[A-Za-z]*(Key|Token|Secret|Password)[A-Za-z]*)\"\\s*:\\s*\"[^\"]*\"", "\"$1\":\"（已去除）\"");
                        AddText(zip, "一维/settings.json", json);
                    }
                    foreach (var n in new[] { "mirror.json", "deploy-times.json", "dict-update.json", "grammar.json", "sync.json" })
                    {
                        var f = Path.Combine(Paths.YiweiDir, n);
                        if (File.Exists(f)) AddFile(zip, "一维/" + n, f);
                    }
                    AddText(zip, "用户文件夹清单.txt", Listing(user));
                }
                AddText(zip, "说明.txt",
                    "一维输入法诊断信息\r\n生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n\r\n" +
                    "包含：版本与环境、RIME 日志（最近 7 天）、一维助手日志、用户文件夹里的配置文件（*.yaml、*.lua）、一维设置（已去除 AI 密钥）。\r\n" +
                    "不包含：用户词库（*.userdb）、输入统计、常用语、剪贴板、同步快照、词典正文。\r\n" +
                    (skipped.Count > 0 ? "已跳过：" + string.Join("、", skipped.Distinct()) + "\r\n" : "") +
                    "\r\n反馈问题：https://github.com/yiweishurufa/yiwei-ime/issues （把这个 zip 拖进去即可）\r\n");
            }
            Log.Write("diagnostics: " + zipPath);
            return zipPath;
        }

        /// <summary>Tray menu entry: build, then select the zip in Explorer.</summary>
        public static void ExportAndShow()
        {
            Task.Run(() =>
            {
                try
                {
                    var path = Export();
                    try { Process.Start("explorer.exe", "/select,\"" + path + "\""); } catch { }
                    Ui(() => Tray.Balloon("诊断信息已导出", "已放到桌面：" + Path.GetFileName(path) + "。不含用户词库和输入统计，可直接附在反馈里。"));
                }
                catch (Exception e)
                {
                    Log.Write("diagnostics: " + e);
                    Ui(() => Tray.Balloon("导出诊断信息失败", e.GetBaseException().Message));
                }
            });
        }

        static void Ui(Action a) { try { Program.Ui.BeginInvoke(a); } catch { } }

        /// <summary>User dictionaries, statistics and other personal data never leave the machine through this package.</summary>
        static bool IsPrivate(string name)
        {
            var n = name.ToLowerInvariant();
            return n.Contains(".userdb") || n.Contains("stats") || n.Contains("snippet") || n.Contains("clipboard") || n.StartsWith("custom_phrase");
        }

        static string Environment_()
        {
            var sb = new StringBuilder();
            void L(string k, string v) => sb.Append(k).Append("：").Append(v ?? "").Append("\r\n");
            L("一维助手", AppUpdate.Current.ToString());
            L("安装位置", Paths.InstallDir);
            foreach (var exe in new[] { "YiweiServer.exe", "WeaselServer.exe", "YiweiDeployer.exe", "WeaselDeployer.exe", "rime.dll" })
            {
                var p = Path.Combine(Paths.InstallDir ?? "", exe);
                if (!File.Exists(p)) continue;
                var vi = FileVersionInfo.GetVersionInfo(p);
                L(exe, (vi.ProductVersion ?? vi.FileVersion ?? "?") + "（" + File.GetLastWriteTime(p).ToString("yyyy-MM-dd") + "）");
            }
            L("用户文件夹", Paths.UserDir);
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    var build = k?.GetValue("CurrentBuildNumber") as string;
                    var ubr = k?.GetValue("UBR");
                    var product = k?.GetValue("ProductName") as string ?? "";
                    if (int.TryParse(build, out var b) && b >= 22000) product = product.Replace("Windows 10", "Windows 11");
                    L("Windows", product + " " + (k?.GetValue("DisplayVersion") as string ?? "") + "（" + build + "." + ubr + "）");
                }
            }
            catch { L("Windows", Environment.OSVersion.ToString()); }
            L("64 位系统", Environment.Is64BitOperatingSystem ? "是" : "否");
            L(".NET", Environment.Version.ToString());
            L("深色模式", SystemTheme.IsDark ? "是" : "否");
            var s = Settings.Current;
            L("当前方案", s.Schema);
            L("候选配色", s.ColorScheme + " / 深色 " + WeaselConfig.DarkSchemeFor(s));
            L("大字表", RimeFeatures.Of(s).BigCharset ? "开" : "关");
            L("语法模型", Grammar.Installed ? "已安装" : "未安装");
            L("上次下载源", Mirrors.NameOf(Mirrors.Last));
            L("部署程序在运行", Deploy.Running ? "是（" + Deploy.Kind + "）" : "否");
            return sb.ToString();
        }

        /// <summary>Names and sizes of what is in the user folder (top two levels), so we can see what is missing.</summary>
        static string Listing(string root)
        {
            var sb = new StringBuilder();
            void Dir(string d, int depth)
            {
                foreach (var f in SafeFiles(d, "*", SearchOption.TopDirectoryOnly).OrderBy(x => x))
                {
                    var fi = new FileInfo(f);
                    sb.Append(new string(' ', depth * 2)).Append(fi.Name).Append("  ").Append(fi.Length).Append(" 字节  ").Append(fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm")).Append("\r\n");
                }
                if (depth >= 1) return;
                string[] subs;
                try { subs = Directory.GetDirectories(d); } catch { return; }
                foreach (var sd in subs.OrderBy(x => x))
                {
                    sb.Append(new string(' ', depth * 2)).Append(Path.GetFileName(sd)).Append("\\\r\n");
                    Dir(sd, depth + 1);
                }
            }
            Dir(root, 0);
            return sb.ToString();
        }

        static IEnumerable<string> SafeFiles(string dir, string pattern, SearchOption opt)
        {
            try { return Directory.GetFiles(dir, pattern, opt); } catch { return new string[0]; }
        }

        static void AddText(ZipArchive zip, string name, string text)
        {
            var e = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var w = new StreamWriter(e.Open(), new UTF8Encoding(true))) w.Write(text);
        }

        static void AddFile(ZipArchive zip, string name, string path)
        {
            try
            {
                using (var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var dst = zip.CreateEntry(name, CompressionLevel.Optimal).Open())
                    src.CopyTo(dst);
            }
            catch (Exception ex) { AddText(zip, name + ".读取失败.txt", ex.Message); }
        }

        /// <summary>Logs are shared-open by the running server; long ones keep only their last 2 MB.</summary>
        static void AddLog(ZipArchive zip, string name, string path)
        {
            try
            {
                using (var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var dst = zip.CreateEntry(name, CompressionLevel.Optimal).Open())
                {
                    if (src.Length > MaxLogBytes) src.Seek(-MaxLogBytes, SeekOrigin.End);
                    src.CopyTo(dst);
                }
            }
            catch (Exception ex) { AddText(zip, name + ".读取失败.txt", ex.Message); }
        }
    }
}
