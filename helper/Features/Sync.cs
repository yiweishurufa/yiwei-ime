using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Yiwei
{
    /// <summary>
    /// 用户词库同步到用户自己的网盘目录（librime 内置机制）：
    /// 用户目录 installation.yaml 里的 sync_dir + installation_id，由 Deployer /sync 执行。
    /// librime 会在 sync_dir/&lt;installation_id&gt;/ 下写本机快照，并合并其它设备的快照。
    /// Helper 只负责选目录、写 installation.yaml、触发同步、记录时间和可选的每日自动同步。
    /// 自己的设置存在 yiwei/sync.json，不进 Settings。
    /// </summary>
    public static class Sync
    {
        public sealed class State
        {
            public bool AutoDaily { get; set; } = false;
            public string LastSync { get; set; } = "";   // 本地时间 yyyy-MM-dd HH:mm:ss
            public bool LastOk { get; set; } = true;
            public string LastMessage { get; set; } = "";
        }

        public sealed class Candidate { public string Name, Root; public string Target => Path.Combine(Root, FolderName); }

        /// <summary>网盘里存放一维同步数据的子文件夹名（ASCII，避免老版本 librime 处理非 ASCII 路径的问题）。</summary>
        public const string FolderName = "YiweiIME";

        static string StateFile => Path.Combine(Paths.YiweiDir, "sync.json");
        static string InstallationFile => Path.Combine(Paths.UserDir, "installation.yaml");
        static State _state;
        static readonly object Gate = new object();

        public static State Current
        {
            get { lock (Gate) return _state ?? (_state = Json.Load<State>(StateFile)); }
        }

        public static void Save() { lock (Gate) Json.Save(StateFile, Current); }

        public static event Action Changed;

        // ---------- installation.yaml ----------

        static string ReadInstallation() { try { return File.Exists(InstallationFile) ? File.ReadAllText(InstallationFile, Encoding.UTF8) : ""; } catch { return ""; } }

        static string Field(string yaml, string key)
        {
            var m = Regex.Match(yaml, @"^" + Regex.Escape(key) + @":[ \t]*(.*?)[ \t]*\r?$", RegexOptions.Multiline);
            if (!m.Success) return null;
            var v = m.Groups[1].Value;
            if (v.Length >= 2 && v[0] == '\'' && v[v.Length - 1] == '\'') return v.Substring(1, v.Length - 2).Replace("''", "'");
            if (v.Length >= 2 && v[0] == '"' && v[v.Length - 1] == '"')
                return Regex.Replace(v.Substring(1, v.Length - 2), @"\\(.)", mm => mm.Groups[1].Value == "n" ? "\n" : mm.Groups[1].Value);
            return v;
        }

        /// <summary>用户选定的同步目录；没有设置时为 null（librime 默认用 用户目录\sync）。</summary>
        public static string SyncDir
        {
            get { var v = Field(ReadInstallation(), "sync_dir"); return string.IsNullOrWhiteSpace(v) ? null : v; }
        }

        /// <summary>librime 实际使用的同步目录。</summary>
        public static string EffectiveDir => SyncDir ?? Path.Combine(Paths.UserDir, "sync");

        public static string InstallationId
        {
            get { var v = Field(ReadInstallation(), "installation_id"); return string.IsNullOrWhiteSpace(v) ? null : v; }
        }

        static string NewInstallationId()
        {
            var m = Regex.Replace(Environment.MachineName ?? "pc", @"[^A-Za-z0-9_\-]", "").ToLowerInvariant();
            if (m.Length == 0) m = "pc";
            return "yiwei-" + m;
        }

        static string YamlQuote(string s) => "'" + (s ?? "").Replace("'", "''") + "'";

        static string SetField(string yaml, string key, string value)
        {
            var re = new Regex(@"^" + Regex.Escape(key) + @":.*?\r?$", RegexOptions.Multiline);
            if (value == null) return re.Replace(yaml, "").Replace("\r\n\r\n", "\r\n").Replace("\n\n", "\n");
            var line = key + ": " + value;
            if (re.IsMatch(yaml)) return re.Replace(yaml, line.Replace("$", "$$"), 1);
            if (yaml.Length > 0 && !yaml.EndsWith("\n")) yaml += "\n";
            return yaml + line + "\n";
        }

        /// <summary>写入（或清除，dir 为 null）sync_dir，保留其它字段；没有 installation_id 时生成 yiwei-&lt;机器名&gt;。</summary>
        public static void SetSyncDir(string dir)
        {
            var y = ReadInstallation();
            if (string.IsNullOrWhiteSpace(Field(y, "installation_id"))) y = SetField(y, "installation_id", YamlQuote(NewInstallationId()));
            if (dir != null)
            {
                dir = Path.GetFullPath(dir).TrimEnd('\\');
                Directory.CreateDirectory(dir);
            }
            y = SetField(y, "sync_dir", dir == null ? null : YamlQuote(dir));
            Directory.CreateDirectory(Paths.UserDir);
            File.WriteAllText(InstallationFile, y, new UTF8Encoding(false));
            try { Changed?.Invoke(); } catch { }
        }

        // ---------- 探测网盘目录 ----------

        public static List<Candidate> Detect()
        {
            var list = new List<Candidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Add(string name, string root)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(root)) return;
                    root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(root)).TrimEnd('\\');
                    if (!Directory.Exists(root) || !seen.Add(root)) return;
                    list.Add(new Candidate { Name = name, Root = root });
                }
                catch { }
            }
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // OneDrive（个人 / 工作或学校）
            Add("OneDrive", Environment.GetEnvironmentVariable("OneDriveConsumer"));
            Add("OneDrive（工作或学校）", Environment.GetEnvironmentVariable("OneDriveCommercial"));
            Add("OneDrive", Environment.GetEnvironmentVariable("OneDrive"));
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts"))
                    if (k != null)
                        foreach (var n in k.GetSubKeyNames())
                            using (var a = k.OpenSubKey(n))
                                Add(n.StartsWith("Business", StringComparison.OrdinalIgnoreCase) ? "OneDrive（工作或学校）" : "OneDrive", a?.GetValue("UserFolder") as string);
            }
            catch { }

            // 坚果云：默认同步文件夹「我的坚果云」，常见于 %USERPROFILE%\Nutstore\<n>\我的坚果云
            var roots = new List<string> { home };
            try { roots.AddRange(DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady).Select(d => d.RootDirectory.FullName)); } catch { }
            foreach (var r in roots)
            {
                Add("坚果云", Path.Combine(r, "我的坚果云"));
                var ns = Path.Combine(r, "Nutstore");
                if (Directory.Exists(ns))
                    try
                    {
                        foreach (var sub in Directory.GetDirectories(ns))
                        {
                            Add("坚果云", Path.Combine(sub, "我的坚果云"));
                            if (Path.GetFileName(sub) == "我的坚果云") Add("坚果云", sub);
                        }
                    }
                    catch { }
            }
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Nutstore"))
                    foreach (var name in new[] { "DefaultSyncFolder", "SyncFolder", "DataDir" })
                        Add("坚果云", k?.GetValue(name) as string);
            }
            catch { }

            // 百度网盘同步盘：默认文件夹名 BaiduSyncdisk
            foreach (var r in roots) Add("百度网盘同步盘", Path.Combine(r, "BaiduSyncdisk"));

            // 其它常见同步目录
            Add("Dropbox", Path.Combine(home, "Dropbox"));
            Add("Syncthing", Path.Combine(home, "Sync"));
            Add("iCloud Drive", Path.Combine(home, "iCloudDrive"));
            return list;
        }

        // ---------- 执行同步 ----------

        static int _running;
        public static bool Running => _running != 0;

        /// <summary>后台运行 Deployer /sync，完成后更新「上次同步」。</summary>
        public static Task<bool> RunAsync() => Task.Run(() =>
        {
            if (System.Threading.Interlocked.Exchange(ref _running, 1) == 1) return false;
            try
            {
                try { Changed?.Invoke(); } catch { }
                // 保证 installation_id 存在（librime 也会自动生成，但用可读的名字更容易在网盘里认出本机）
                if (InstallationId == null) SetSyncDir(SyncDir);
                var id = InstallationId;
                var dir = EffectiveDir;
                Directory.CreateDirectory(dir);
                var before = DateTime.Now.AddSeconds(-2);
                Deploy.Run("/sync", true);
                bool ok = false;
                try
                {
                    var mine = id != null ? Path.Combine(dir, id) : null;
                    ok = mine != null && Directory.Exists(mine) &&
                         Directory.GetFiles(mine, "*", SearchOption.AllDirectories).Any(f => File.GetLastWriteTime(f) >= before);
                }
                catch { }
                var st = Current;
                st.LastSync = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                st.LastOk = ok;
                st.LastMessage = ok ? "" : "没有看到本机的同步快照更新，请确认同步文件夹可以写入";
                Save();
                Log.Write("sync: " + (ok ? "ok" : "no snapshot") + " → " + dir);
                return ok;
            }
            catch (Exception e)
            {
                Log.Write("sync: " + e.Message);
                var st = Current; st.LastSync = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); st.LastOk = false; st.LastMessage = e.Message; Save();
                return false;
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _running, 0);
                try { Changed?.Invoke(); } catch { }
            }
        });

        public static string Describe()
        {
            var st = Current;
            if (Running) return "正在同步…";
            if (string.IsNullOrEmpty(st.LastSync)) return "还没有同步过";
            return "上次同步：" + st.LastSync + (st.LastOk ? "" : "（可能未成功：" + st.LastMessage + "）");
        }

        // ---------- 每日自动同步 ----------

        static System.Windows.Forms.Timer _timer;

        /// <summary>在 UI 线程上调用一次（Program.Main）。每小时检查：开启了每日同步且距上次超过 23 小时、并且选了同步文件夹时运行。</summary>
        public static void StartScheduler()
        {
            _timer = new System.Windows.Forms.Timer { Interval = 60 * 60 * 1000 };
            _timer.Tick += (s, e) => MaybeAuto();
            _timer.Start();
            Task.Delay(3 * 60 * 1000).ContinueWith(_ => { try { Program.Ui.BeginInvoke(new Action(MaybeAuto)); } catch { } });
        }

        static void MaybeAuto()
        {
            try
            {
                var st = Current;
                if (!st.AutoDaily || SyncDir == null || Running) return;
                if (DateTime.TryParse(st.LastSync, out var last) && (DateTime.Now - last).TotalHours < 23) return;
                RunAsync();
            }
            catch (Exception e) { Log.Write("auto sync: " + e.Message); }
        }
    }
}
