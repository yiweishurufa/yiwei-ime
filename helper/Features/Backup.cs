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
        /// <summary>Platform-neutral snippets (常用语) shared with the Android version: {"format":"yiwei-snippets","version":1,"groups":[…]}.</summary>
        const string SharedSnippets = "shared/snippets.json";

        static void WriteSharedSnippets(ZipArchive zip)
        {
            var book = SnippetBook.Current;
            var doc = new Dictionary<string, object>
            {
                { "format", "yiwei-snippets" }, { "version", 1 },
                { "groups", book.Categories.Select(c => new Dictionary<string, object>
                    {
                        { "name", c.Name },
                        { "items", c.Items.Where(i => !string.IsNullOrEmpty(i.Text)).Select(i => new Dictionary<string, object> { { "text", i.Text }, { "code", i.Code ?? "" } }).ToList() },
                    }).ToList() },
            };
            var e = zip.CreateEntry(SharedSnippets);
            using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(Json.Write(doc));
        }

        /// <summary>Adds snippets from a shared snippets.json that are not here yet (same group name + same text). Returns how many were added.</summary>
        public static int MergeSharedSnippets(string json)
        {
            var root = Json.Parse(json) as Dictionary<string, object>;
            if (root == null || !(root.TryGetValue("groups", out var g) && g is object[] groups)) return 0;
            var book = SnippetBook.Current;
            int added = 0;
            foreach (var go in groups.OfType<Dictionary<string, object>>())
            {
                var name = go.TryGetValue("name", out var n) ? n as string ?? "" : "";
                if (name.Length == 0) continue;
                var cat = book.Categories.FirstOrDefault(c => c.Name == name);
                if (cat == null) { cat = new SnippetCategory { Name = name }; book.Categories.Add(cat); }
                if (!(go.TryGetValue("items", out var it) && it is object[] items)) continue;
                foreach (var io in items.OfType<Dictionary<string, object>>())
                {
                    var text = io.TryGetValue("text", out var t) ? t as string ?? "" : "";
                    if (text.Length == 0 || cat.Items.Any(x => x.Text == text)) continue;
                    cat.Items.Add(new Snippet { Text = text, Code = io.TryGetValue("code", out var c) ? c as string ?? "" : "" });
                    added++;
                }
            }
            if (added > 0) book.Save();
            return added;
        }

        /// <summary>
        /// 导出给安卓：一个按 RIME 用户文件夹排好的 zip。解压到手机的 rime 文件夹（同文：/sdcard/rime）后点「同步用户数据」即可合并词库；
        /// custom_phrase.txt 带上有编码的常用语，yiwei/snippets.json 给一维安卓版导入全部常用语。
        /// </summary>
        public static int ExportForAndroid(string target)
        {
            try { Deploy.Run("/sync", true); } catch (Exception e) { Log.Write("android export sync: " + e.Message); }
            var syncDir = Path.Combine(Sync.EffectiveDir, Sync.InstallationId ?? "");
            int count = 0;
            if (File.Exists(target)) File.Delete(target);
            using (var zip = ZipFile.Open(target, ZipArchiveMode.Create, Encoding.UTF8))
            {
                if (Directory.Exists(syncDir))
                    foreach (var f in Directory.GetFiles(syncDir, "*.userdb.txt"))
                    { zip.CreateEntryFromFile(f, "rime/sync/yiwei-windows/" + Path.GetFileName(f)); count++; }
                var phrases = Path.Combine(Paths.UserDir, "custom_phrase.txt");
                if (File.Exists(phrases)) { zip.CreateEntryFromFile(phrases, "rime/custom_phrase.txt"); count++; }
                WriteSharedSnippets(zip); count++;
                var readme = zip.CreateEntry("rime/一维-导入说明.txt");
                using (var w = new StreamWriter(readme.Open(), new UTF8Encoding(false)))
                    w.Write("一维输入法 · 电脑 → 手机\n\n1. 把 rime 文件夹里的内容复制到手机输入法的用户文件夹（同文输入法默认是 内部存储/rime）。\n" +
                            "2. 在输入法设置里点「同步用户数据」，电脑上学到的词会合并进手机，不会覆盖手机自己学的词。\n" +
                            "3. shared/snippets.json 是全部常用语，一维安卓版可以直接导入。\n\n手机 → 电脑：把手机 rime/sync 文件夹打包成 zip，在电脑「设置 → 常规 → 备份与恢复」里选「恢复」即可合并。\n");
            }
            return count;
        }

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
                WriteSharedSnippets(zip); count++;
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
                bool ours = zip.GetEntry(Manifest) != null;
                if (!ours && !zip.Entries.Any(x => x.FullName.EndsWith(".userdb.txt", StringComparison.OrdinalIgnoreCase) || x.FullName == SharedSnippets))
                    throw new InvalidDataException("这不是一维输入法的备份文件，里面也没有 RIME 用户词库（*.userdb.txt）");
                foreach (var e in zip.Entries)
                {
                    if (e.FullName.EndsWith("/")) continue;
                    if (e.FullName == SharedSnippets && !ours)
                    {
                        using (var r = new StreamReader(e.Open(), Encoding.UTF8)) count += MergeSharedSnippets(r.ReadToEnd());
                        continue;
                    }
                    string dest;
                    if (!ours)
                    {
                        // 安卓（同文 / 一维安卓版）导出的 rime 文件夹或同步文件夹：只取词库快照，合并进本机
                        if (!e.FullName.EndsWith(".userdb.txt", StringComparison.OrdinalIgnoreCase)) continue;
                        dest = Path.Combine(words, Path.GetFileName(e.FullName));
                    }
                    else if (e.FullName.StartsWith("user/")) dest = Path.Combine(root, e.FullName.Substring(5).Replace('/', Path.DirectorySeparatorChar));
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
