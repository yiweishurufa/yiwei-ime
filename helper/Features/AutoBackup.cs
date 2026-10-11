using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Yiwei
{
    /// <summary>
    /// W37 用户词库自动备份：每天（空闲时）导出一份 .yiwei-backup（词库快照 + 常用语 + 设置，和「备份与恢复」同一格式），
    /// 存到本地文件夹或 OneDrive（检测到 OneDrive 时可选，文件随 OneDrive 上云），保留最近 N 份。
    /// </summary>
    public static class AutoBackup
    {
        public static string OneDriveDir
        {
            get
            {
                foreach (var v in new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" })
                {
                    var d = Environment.GetEnvironmentVariable(v);
                    if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) return d;
                }
                return null;
            }
        }

        public static string DefaultDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "一维输入法备份");

        public static string TargetDir
        {
            get
            {
                var t = ToolSettings.Current;
                if (t.BackupWhere == "onedrive" && OneDriveDir != null) return Path.Combine(OneDriveDir, "一维输入法备份");
                if (t.BackupWhere == "custom" && !string.IsNullOrWhiteSpace(t.BackupDir)) return t.BackupDir;
                return DefaultDir;
            }
        }

        static int _running;

        /// <summary>Writes one backup now and prunes old ones. Returns the file path.</summary>
        public static string RunNow()
        {
            if (System.Threading.Interlocked.Exchange(ref _running, 1) == 1) throw new Exception("正在备份中");
            try
            {
                var dir = TargetDir; Directory.CreateDirectory(dir);
                var file = Path.Combine(dir, "一维输入法-" + Environment.MachineName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".yiwei-backup");
                var tmp = file + ".part";
                Backup.Export(tmp);
                if (File.Exists(file)) File.Delete(file);
                File.Move(tmp, file);
                var keep = Math.Max(1, ToolSettings.Current.BackupKeep);
                foreach (var old in Directory.GetFiles(dir, "一维输入法-" + Environment.MachineName + "-*.yiwei-backup").OrderByDescending(f => f).Skip(keep))
                    try { File.Delete(old); } catch { }
                var t = ToolSettings.Current; t.BackupLast = DateTime.Now.ToString("yyyy-MM-dd HH:mm"); t.Save();
                Log.Write("auto backup → " + file);
                return file;
            }
            finally { System.Threading.Interlocked.Exchange(ref _running, 0); }
        }

        static System.Windows.Forms.Timer _timer;

        public static void Start()
        {
            _timer = new System.Windows.Forms.Timer { Interval = 5 * 60 * 1000 };
            _timer.Tick += (s, e) =>
            {
                _timer.Interval = 60 * 60 * 1000;
                var t = ToolSettings.Current;
                if (!t.AutoBackup) return;
                if (DateTime.TryParse(t.BackupLast, out var last) && (DateTime.Now - last).TotalHours < 24) return;
                if (Idle.For < TimeSpan.FromMinutes(2)) return; // 等用户不打字时再做（导出前要整理词库）
                Task.Run(() => { try { RunNow(); } catch (Exception ex) { Log.Write("auto backup: " + ex.Message); } });
            };
            _timer.Start();
        }
    }
}
