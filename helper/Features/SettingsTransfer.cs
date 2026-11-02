using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Yiwei
{
    /// <summary>
    /// 设置导入 / 导出（清单 #13，对标青简）：换电脑时只带走「怎么用」——各项设置、开关、快捷键、自定义标点、方案补丁，
    /// 不带词库、常用语、统计、剪贴板，也不带任何密钥和账号（AI 密钥、WebDAV 账号密码）。
    /// 文件是一个 zip（.yiwei-settings），可以直接打开看。导入前自动把本机当前设置存一份到 yiwei/backup，导错了能找回。
    /// 导入时本机的密钥、账号和只属于这台电脑的状态（词库版本、备份目录等）保持不变。
    /// </summary>
    public static class SettingsTransfer
    {
        public const string Extension = ".yiwei-settings";
        const string Manifest = "yiwei-settings.txt";
        const int FormatVersion = 1;

        /// <summary>settings.json 里不导出、导入时保留本机值的字段：密钥和只属于这台电脑的状态。</summary>
        internal static readonly HashSet<string> LocalSettingsKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "AiKeyProtected", "DictTag", "DictCheckedAt", "AppsScanned",
        };

        /// <summary>tools.json 里同样留在本机的字段：WebDAV 账号密码、备份目录、壁纸状态、邮箱后缀使用次数等。</summary>
        internal static readonly HashSet<string> LocalToolKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "DavUser", "DavPass", "DavLast", "BackupDir", "BackupLast", "WallpaperStamp", "ThemeRgb", "LowSpecSaved", "LowSpecHinted", "EmailUse",
        };

        /// <summary>yiwei/ 里的开关文件：存在即开/关的小文件和几个单值文件。运行时标记（*.now、*.reload、learn.clear）不算。</summary>
        internal static bool IsFlagFile(string name)
        {
            var n = name.ToLowerInvariant();
            if (n == "punct.txt") return true;
            if (n.IndexOfAny(new[] { '/', '\\' }) >= 0) return false;
            return n.EndsWith(".enabled") || n.EndsWith(".disabled") || n.EndsWith(".key") || n.EndsWith(".skip") || n.EndsWith(".level");
        }

        static bool IsCustomYaml(string name) =>
            name.EndsWith(".custom.yaml", StringComparison.OrdinalIgnoreCase) && name.IndexOfAny(new[] { '/', '\\' }) < 0;

        /// <summary>导出到 target，返回写入的文件数。</summary>
        public static int Export(string target)
        {
            int count = 0;
            var tmp = target + ".tmp";
            if (File.Exists(tmp)) File.Delete(tmp);
            using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create, Encoding.UTF8))
            {
                Settings.Current.Save(); // 先把内存里的改动落盘
                if (File.Exists(Paths.SettingsFile)) { Add(zip, "yiwei/settings.json", StripJson(File.ReadAllText(Paths.SettingsFile, Encoding.UTF8), LocalSettingsKeys)); count++; }
                var tools = Path.Combine(Paths.YiweiDir, "tools.json");
                if (File.Exists(tools)) { Add(zip, "yiwei/tools.json", StripJson(File.ReadAllText(tools, Encoding.UTF8), LocalToolKeys)); count++; }
                var modes = Path.Combine(Paths.YiweiDir, "appmodes.json");
                if (File.Exists(modes)) { zip.CreateEntryFromFile(modes, "yiwei/appmodes.json"); count++; }
                if (Directory.Exists(Paths.YiweiDir))
                    foreach (var f in Directory.GetFiles(Paths.YiweiDir).Where(f => IsFlagFile(Path.GetFileName(f))))
                    { zip.CreateEntryFromFile(f, "yiwei/" + Path.GetFileName(f)); count++; }
                if (Directory.Exists(Paths.UserDir))
                    foreach (var f in Directory.GetFiles(Paths.UserDir).Where(f => IsCustomYaml(Path.GetFileName(f))))
                    { zip.CreateEntryFromFile(f, "rime/" + Path.GetFileName(f)); count++; }
                Add(zip, Manifest,
                    "一维输入法设置\r\n" +
                    "format: " + FormatVersion + "\r\n" +
                    "created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" +
                    "app: " + AppUpdate.Current + "\r\n\r\n" +
                    "包含：设置、开关、快捷键、自定义标点、方案补丁（*.custom.yaml）。\r\n" +
                    "不包含：AI 密钥、WebDAV 账号密码、用户词库、常用语、输入统计、剪贴板。\r\n" +
                    "在另一台电脑「设置 → 常规 → 设置导入 / 导出」里选「导入」。\r\n");
            }
            if (File.Exists(target)) File.Delete(target);
            File.Move(tmp, target);
            Log.Write("settings-export: " + count + " files");
            return count;
        }

        /// <summary>导入 source：先自动备份本机设置，再覆盖。返回导入的文件数和备份文件路径。</summary>
        public static (int count, string backup) Import(string source)
        {
            // 先校验，再动本机文件
            using (var zip = ZipFile.OpenRead(source))
            {
                if (zip.GetEntry(Manifest) == null)
                    throw new InvalidDataException("这不是一维输入法的设置文件（" + Extension + "）。完整备份请用上面的「从备份恢复」");
                var json = zip.GetEntry("yiwei/settings.json");
                if (json != null && !(Json.Parse(ReadEntry(json)) is Dictionary<string, object>))
                    throw new InvalidDataException("设置文件已损坏（settings.json 读不出来）");
            }

            var backupDir = Path.Combine(Paths.YiweiDir, "backup");
            Directory.CreateDirectory(backupDir);
            var backup = Path.Combine(backupDir, "导入前的设置-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + Extension);
            Export(backup);

            int count = 0;
            using (var zip = ZipFile.OpenRead(source))
            {
                var incomingFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in zip.Entries)
                {
                    if (e.FullName.EndsWith("/")) continue;
                    if (e.FullName == "yiwei/settings.json")
                    {
                        var merged = MergeJson(ReadEntry(e), File.Exists(Paths.SettingsFile) ? File.ReadAllText(Paths.SettingsFile, Encoding.UTF8) : "{}", LocalSettingsKeys);
                        var s = Json.Read<Settings>(merged); // 字段类型不对时这里抛异常，本机文件不动
                        s.Save();
                        count++;
                    }
                    else if (e.FullName == "yiwei/tools.json")
                    {
                        var path = Path.Combine(Paths.YiweiDir, "tools.json");
                        var merged = MergeJson(ReadEntry(e), File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "{}", LocalToolKeys);
                        Json.Read<ToolSettings>(merged).Save();
                        count++;
                    }
                    else if (e.FullName == "yiwei/appmodes.json")
                    {
                        Json.Save(Path.Combine(Paths.YiweiDir, "appmodes.json"), Json.Read<AppModes.Store>(ReadEntry(e)) ?? new AppModes.Store());
                        count++;
                    }
                    else if (e.FullName.StartsWith("yiwei/") && IsFlagFile(e.FullName.Substring(6)))
                    {
                        var name = e.FullName.Substring(6);
                        incomingFlags.Add(name);
                        e.ExtractToFile(Path.Combine(Paths.YiweiDir, name), true);
                        count++;
                    }
                    else if (e.FullName.StartsWith("rime/") && IsCustomYaml(e.FullName.Substring(5)))
                    {
                        e.ExtractToFile(Path.Combine(Paths.UserDir, e.FullName.Substring(5)), true);
                        count++;
                    }
                }
                // 开关文件「存在即开」：导出方没有的，本机也去掉，两台电脑才一致
                foreach (var f in Directory.GetFiles(Paths.YiweiDir).Where(f => IsFlagFile(Path.GetFileName(f)) && !incomingFlags.Contains(Path.GetFileName(f))))
                    try { File.Delete(f); } catch (Exception ex) { Log.Write("settings-import: delete " + f + ": " + ex.Message); }
            }

            Settings.Reload();
            ToolSettings.Reload();
            AppModes.Reload();
            Rime.ApplySoon(500);
            Log.Write("settings-import: " + count + " files, backup " + backup);
            return (count, backup);
        }

        static void Add(ZipArchive zip, string name, string text)
        {
            using (var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false))) w.Write(text);
        }

        static string ReadEntry(ZipArchiveEntry e)
        {
            if (e.Length > 4 << 20) throw new InvalidDataException(e.FullName + " 太大，不像设置文件");
            using (var r = new StreamReader(e.Open(), Encoding.UTF8)) return r.ReadToEnd();
        }

        /// <summary>去掉 drop 里的字段（只看最外层）。</summary>
        internal static string StripJson(string json, ICollection<string> drop)
        {
            if (!(Json.Parse(json) is Dictionary<string, object> d)) return "{}";
            foreach (var k in drop) d.Remove(k);
            return Json.Write(d);
        }

        /// <summary>导入的字段覆盖本机，keepLocal 里的字段一律用本机的值（导入文件里即使有也不用）。</summary>
        internal static string MergeJson(string incoming, string local, ICollection<string> keepLocal)
        {
            var inc = Json.Parse(incoming) as Dictionary<string, object> ?? new Dictionary<string, object>();
            var loc = Json.Parse(local) as Dictionary<string, object> ?? new Dictionary<string, object>();
            foreach (var k in keepLocal) inc.Remove(k);
            foreach (var kv in inc) loc[kv.Key] = kv.Value;
            return Json.Write(loc);
        }
    }
}
