using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Yiwei
{
    public sealed class SchemaInfo
    {
        public string Id, Name, Desc;
        public SchemaInfo(string id, string name, string desc) { Id = id; Name = name; Desc = desc; }
    }

    /// <summary>Input schemas, default.custom.yaml, and "apply now, redeploy in the background".</summary>
    public static class Rime
    {
        const string Marker = "# 由「一维输入法设置」生成";

        public static readonly SchemaInfo[] Schemas =
        {
            new SchemaInfo("rime_ice", "全拼", "雾凇拼音全拼，词库最全，适合大多数人"),
            new SchemaInfo("double_pinyin_flypy", "小鹤双拼", "每个字两键，键位好记，双拼用户最多"),
            new SchemaInfo("double_pinyin", "自然码双拼", "历史悠久的双拼方案"),
            new SchemaInfo("double_pinyin_mspy", "微软双拼", "与 Windows 自带微软拼音的双拼一致"),
        };

        public static SchemaInfo Find(string id) => Schemas.FirstOrDefault(s => s.Id == id) ?? Schemas[0];

        /// <summary>Writes default.custom.yaml: the chosen schema first, the others after it; candidate count.</summary>
        public static void WriteDefaultCustom(Settings s)
        {
            var path = Paths.DefaultCustom;
            Directory.CreateDirectory(Paths.UserDir);
            if (File.Exists(path) && !File.ReadAllText(path, Encoding.UTF8).Contains(Marker))
                File.Copy(path, path + ".bak-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true);
            var first = Find(s.Schema).Id;
            var y = new StringBuilder();
            y.AppendLine(Marker + "，手动修改会被覆盖（原文件已备份为 .bak-*）。");
            y.AppendLine("patch:");
            y.AppendLine("  schema_list:");
            y.AppendLine("    - schema: " + first);
            foreach (var other in Schemas.Where(x => x.Id != first)) y.AppendLine("    - schema: " + other.Id);
            y.AppendLine("  \"menu/page_size\": " + s.CandidateCount);
            File.WriteAllText(path, y.ToString(), new UTF8Encoding(false));
            SetPreviouslySelected(first);
        }

        /// <summary>RIME remembers the last schema in user.yaml; point it at the chosen one so it takes effect.</summary>
        static void SetPreviouslySelected(string schema)
        {
            try
            {
                var path = Path.Combine(Paths.UserDir, "user.yaml");
                var line = "  previously_selected_schema: " + schema;
                if (!File.Exists(path)) { File.WriteAllText(path, "var:\n" + line + "\n", new UTF8Encoding(false)); return; }
                var lines = File.ReadAllLines(path, Encoding.UTF8).ToList();
                int i = lines.FindIndex(l => l.TrimStart().StartsWith("previously_selected_schema:"));
                if (i >= 0) lines[i] = line;
                else
                {
                    int v = lines.FindIndex(l => l.TrimEnd() == "var:");
                    if (v >= 0) lines.Insert(v + 1, line); else { lines.Add("var:"); lines.Add(line); }
                }
                File.WriteAllLines(path, lines, new UTF8Encoding(false));
            }
            catch (Exception e) { Log.Write("user.yaml: " + e.Message); }
        }

        // ---------- apply ----------

        static Timer _debounce;
        static readonly object Gate = new object();

        /// <summary>Saves settings now; writes the RIME files and redeploys shortly after (coalescing quick changes).</summary>
        public static void ApplySoon(int delayMs = 700)
        {
            try { Settings.Current.Save(); } catch (Exception e) { Log.Write("save: " + e.Message); }
            lock (Gate)
            {
                _debounce?.Dispose();
                _debounce = new Timer(_ => ApplyNow(), null, delayMs, Timeout.Infinite);
            }
        }

        public static Task ApplyNowAsync() => Task.Run(() => ApplyNow());

        static int _idleQueued;

        /// <summary>
        /// For redeploys nobody asked for right now (upgrading generated files after an app update, the daily dictionary):
        /// waits until the keyboard has been quiet for a minute (at most 30 minutes), then applies in the background.
        /// </summary>
        public static void ApplyWhenIdle()
        {
            try { Settings.Current.Save(); } catch (Exception e) { Log.Write("save: " + e.Message); }
            if (Interlocked.Exchange(ref _idleQueued, 1) == 1) return;
            Task.Run(async () =>
            {
                try { await Idle.WaitAsync(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(30)).ConfigureAwait(false); }
                finally { Interlocked.Exchange(ref _idleQueued, 0); }
                ApplySoon(100);
            });
        }

        static int _running;
        static bool _again;

        static void ApplyNow()
        {
            if (Interlocked.Exchange(ref _running, 1) == 1) { _again = true; return; }
            try
            {
                do
                {
                    _again = false;
                    var s = Settings.Current;
                    try
                    {
                        WeaselConfig.Write(s);
                        WriteDefaultCustom(s);
                    }
                    catch (Exception e) { Log.Write("write config: " + e.Message); }
                    Deploy.Run(true);
                } while (_again);
            }
            finally { Interlocked.Exchange(ref _running, 0); }
        }

        /// <summary>Switch schema immediately (tray menu, wizard).</summary>
        public static void UseSchema(string id)
        {
            Settings.Current.Schema = Find(id).Id;
            ApplySoon(100);
        }
    }

    /// <summary>
    /// Brings words from other input methods into the RIME user dictionary. Words are written as a
    /// userdb sync snapshot (sync/yiwei-import/rime_ice.userdb.txt) and merged by the deployer's /sync.
    /// </summary>
    public static class HabitImport
    {
        public sealed class Source
        {
            public string Name, Desc, Path;
            public bool Found, NeedsFile;
        }

        public static List<Source> Detect()
        {
            var list = new List<Source>();
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var weasel = Paths.LegacyWeaselUserDir;
            bool sameAsOurs = false;
            try { sameAsOurs = System.IO.Path.GetFullPath(weasel).TrimEnd('\\').Equals(System.IO.Path.GetFullPath(Paths.UserDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); } catch { }
            list.Add(new Source
            {
                Name = "小狼毫", Path = weasel, Found = Directory.Exists(weasel) && !sameAsOurs,
                Desc = "复制方案、补丁和用户词库（同步快照），合并进一维输入法",
            });
            var sogou = new[] { System.IO.Path.Combine(appData, "SogouPY.users"), System.IO.Path.Combine(local, "SogouPY.users"), System.IO.Path.Combine(appData, "SogouPY") }.FirstOrDefault(Directory.Exists);
            list.Add(new Source
            {
                Name = "搜狗拼音", Path = sogou, Found = sogou != null, NeedsFile = true,
                Desc = "搜狗的词库是加密格式。请先在搜狗「设置 → 词库 → 导出用户词库」，再选择导出的 .txt 文件",
            });
            var ms = System.IO.Path.Combine(appData, @"Microsoft\InputMethod\Chs");
            list.Add(new Source
            {
                Name = "微软拼音", Path = ms, Found = Directory.Exists(ms), NeedsFile = true,
                Desc = "在微软拼音「设置 → 词库和自学习 → 用户自定义短语 → 导出」，选择导出的文本文件（.txt）",
            });
            return list;
        }

        public static int ImportWeasel()
        {
            int n = WeaselImport.Run();
            Deploy.Run("/sync", false);
            return n;
        }

        static readonly Regex Cjk = new Regex(@"^[\u3400-\u9FFF\uF900-\uFAFF]+$");
        static readonly Regex Py = new Regex(@"^[a-zü:v']+$", RegexOptions.IgnoreCase);

        /// <summary>Reads a text export (搜狗 'ni'hao 你好 · 微软 nihao 1 你好 · 你好\tni hao …). Returns words imported.</summary>
        public static int ImportTextFile(string file)
        {
            string text;
            var bytes = File.ReadAllBytes(file);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) text = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            else
            {
                text = new UTF8Encoding(false, false).GetString(bytes);
                if (text.Contains('\uFFFD')) text = Encoding.GetEncoding(936).GetString(bytes);
            }
            var entries = new Dictionary<string, string>(); // phrase -> code
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim().TrimStart('\uFEFF');
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
                var tokens = line.Split(new[] { ' ', '\t', ',', '=' }, StringSplitOptions.RemoveEmptyEntries);
                var word = tokens.FirstOrDefault(t => Cjk.IsMatch(t));
                if (word == null || word.Length < 2 || word.Length > 12) continue;
                var py = tokens.Where(t => Py.IsMatch(t)).ToList();
                if (py.Count == 0) continue;
                List<string> syl;
                if (py.Count == 1) syl = py[0].Split(new[] { '\'' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                else syl = py;
                syl = syl.Select(x => x.ToLowerInvariant().Replace("ü", "v").Replace("u:", "v")).ToList();
                if (syl.Count != word.Length) continue; // without per-syllable pinyin we cannot place it
                if (!entries.ContainsKey(word)) entries[word] = string.Join(" ", syl) + " ";
            }
            if (entries.Count == 0) return 0;
            WriteSnapshot(entries);
            Deploy.Run("/sync", false);
            return entries.Count;
        }

        static void WriteSnapshot(Dictionary<string, string> entries)
        {
            var dir = System.IO.Path.Combine(Sync.EffectiveDir, "yiwei-import"); // 跟随 installation.yaml 的 sync_dir
            Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, "rime_ice.userdb.txt");
            var existing = new Dictionary<string, string>();
            if (File.Exists(path))
                foreach (var l in File.ReadAllLines(path, Encoding.UTF8))
                {
                    if (l.StartsWith("#")) continue;
                    var p = l.Split('\t'); if (p.Length >= 2) existing[p[1]] = p[0];
                }
            foreach (var kv in entries) existing[kv.Key] = kv.Value;
            var sb = new StringBuilder();
            sb.Append("# Rime user dictionary\n#@/db_name\trime_ice.userdb\n#@/db_type\tuserdb\n#@/rime_version\t1.11.2\n#@/tick\t1\n#@/user_id\tyiwei-import\n");
            foreach (var kv in existing.OrderBy(k => k.Value, StringComparer.Ordinal))
                sb.Append(kv.Value).Append('\t').Append(kv.Key).Append("\tc=1 d=1 t=1\n");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
