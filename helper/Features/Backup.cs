using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Yiwei
{
    /// <summary>
    /// 一键备份 / 恢复：把用户词库快照、常用语、设置、自定义配置打成一个 .yiwei-backup（zip）文件。
    /// 词库先用 deployer /sync 导出成 *.userdb.txt 快照；恢复时把快照放回同步文件夹再 /sync 合并，不会覆盖本机新学的词。
    /// 文件格式与平台无关，以后安卓版也读同一个文件。
    /// </summary>
    public static class Backup
    {
        public const string Extension = ".yiwei-backup";
        const string Manifest = "yiwei-backup.txt";

        /// <summary>User-folder files worth keeping: our own settings and the user's own YAML / phrases.</summary>
        static IEnumerable<string> UserFiles()
        {
            var dir = Paths.UserDir;
            if (!Directory.Exists(dir)) yield break;
            foreach (var f in Directory.GetFiles(dir))
            {
                var n = Path.GetFileName(f).ToLowerInvariant();
                if (n.EndsWith(".custom.yaml") || n == "custom_phrase.txt" || n.EndsWith(".dict.yaml") || n == "installation.yaml" || n == "user.yaml")
                    yield return f;
            }
            if (Directory.Exists(Paths.YiweiDir))
                foreach (var f in Directory.GetFiles(Paths.YiweiDir))
                {
                    var n = Path.GetFileName(f).ToLowerInvariant();
                    if (n.EndsWith(".json") || n.EndsWith(".enabled") || n.EndsWith(".disabled") || n.EndsWith(".tsv")) yield return f;
                }
        }

        /// <summary>Writes the backup file. Runs the deployer's /sync first so the word snapshots are fresh.</summary>
        public static int Export(string target)
        {
            try { Deploy.Run("/sync", true); } catch (Exception e) { Log.Write("backup sync: " + e.Message); }
            var root = Paths.UserDir;
            var syncDir = Path.Combine(Sync.EffectiveDir, Sync.InstallationId ?? "");
            int count = 0;
            if (File.Exists(target)) File.Delete(target);
            using (var zip = ZipFile.Open(target, ZipArchiveMode.Create, Encoding.UTF8))
            {
                foreach (var f in UserFiles())
                {
                    var rel = f.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                    zip.CreateEntryFromFile(f, "user/" + rel); count++;
                }
                if (Directory.Exists(syncDir))
                    foreach (var f in Directory.GetFiles(syncDir, "*.userdb.txt"))
                    { zip.CreateEntryFromFile(f, "words/" + Path.GetFileName(f)); count++; }
                var m = zip.CreateEntry(Manifest);
                using (var w = new StreamWriter(m.Open(), new UTF8Encoding(false)))
                {
                    w.WriteLine("一维输入法备份");
                    w.WriteLine("version: 1");
                    w.WriteLine("created: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    w.WriteLine("machine: " + Environment.MachineName);
                    w.WriteLine("app: " + AppUpdate.Current);
                }
            }
            return count;
        }

        /// <summary>Restores a backup: files go back to the user folder (existing ones kept as .bak-*), words are merged.</summary>
        public static int Import(string source)
        {
            var root = Paths.UserDir;
            var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var words = Path.Combine(Sync.EffectiveDir, "yiwei-restore-" + stamp);
            int count = 0;
            using (var zip = ZipFile.OpenRead(source))
            {
                if (zip.GetEntry(Manifest) == null) throw new InvalidDataException("这不是一维输入法的备份文件");
                foreach (var e in zip.Entries)
                {
                    if (e.FullName.EndsWith("/")) continue;
                    string dest;
                    if (e.FullName.StartsWith("user/")) dest = Path.Combine(root, e.FullName.Substring(5).Replace('/', Path.DirectorySeparatorChar));
                    else if (e.FullName.StartsWith("words/")) dest = Path.Combine(words, Path.GetFileName(e.FullName));
                    else continue;
                    var full = Path.GetFullPath(dest);
                    if (!full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase) &&
                        !full.StartsWith(Path.GetFullPath(Sync.EffectiveDir), StringComparison.OrdinalIgnoreCase)) continue; // zip-slip guard
                    Directory.CreateDirectory(Path.GetDirectoryName(full));
                    if (File.Exists(full) && !e.FullName.StartsWith("words/")) File.Copy(full, full + ".bak-" + stamp, true);
                    e.ExtractToFile(full, true);
                    count++;
                }
            }
            Settings.Reload();
            try { Deploy.Run("/sync", true); } catch (Exception e) { Log.Write("restore sync: " + e.Message); }
            Rime.ApplySoon(500);
            return count;
        }
    }
}
