using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Yiwei
{
    /// <summary>
    /// W40 低配机模式：关掉 AI 手势、语音输入、取色跟随壁纸、WebDAV 自动同步、Win+V 合并，
    /// 每 5 分钟回收一次内存（GC + 让系统收回工作集），目标常驻内存 &lt; 100 MB。关闭时恢复原来的设置。
    /// 内存 ≤ 4 GB 或 ≤ 2 核的电脑第一次启动时提示一次。
    /// </summary>
    public static class LowSpec
    {
        [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);
        [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
        [StructLayout(LayoutKind.Sequential)]
        struct MEMORYSTATUSEX { public uint dwLength, dwMemoryLoad; public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual; }

        public static double TotalRamGb
        {
            get { var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) }; return GlobalMemoryStatusEx(ref m) ? m.ullTotalPhys / 1073741824.0 : 0; }
        }

        public static bool LooksLowEnd => (TotalRamGb > 0 && TotalRamGb <= 4.2) || Environment.ProcessorCount <= 2;

        public static long WorkingSetMb { get { using (var p = Process.GetCurrentProcess()) { p.Refresh(); return p.WorkingSet64 / 1048576; } } }

        public static void Set(bool on)
        {
            var s = Settings.Current; var t = ToolSettings.Current;
            if (on == t.LowSpec) return;
            if (on)
            {
                t.LowSpecSaved = string.Join("|", s.AiHotkey, s.VoiceKey ?? "", t.FollowWallpaper, t.DavAuto, t.MergeWinClipboard);
                s.AiHotkey = false; s.VoiceKey = ""; t.FollowWallpaper = false; t.DavAuto = false; t.MergeWinClipboard = false;
            }
            else if (!string.IsNullOrEmpty(t.LowSpecSaved))
            {
                var p = t.LowSpecSaved.Split('|');
                if (p.Length >= 5)
                {
                    bool.TryParse(p[0], out var ai); s.AiHotkey = ai; s.VoiceKey = p[1];
                    bool.TryParse(p[2], out var fw); t.FollowWallpaper = fw; bool.TryParse(p[3], out var da); t.DavAuto = da; bool.TryParse(p[4], out var mw); t.MergeWinClipboard = mw;
                }
            }
            t.LowSpec = on; t.Save(); s.Save();
            if (on) Trim();
        }

        public static void Trim()
        {
            try
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                using (var p = Process.GetCurrentProcess()) SetProcessWorkingSetSize(p.Handle, new IntPtr(-1), new IntPtr(-1));
            }
            catch (Exception e) { Log.Write("trim: " + e.Message); }
        }

        static System.Windows.Forms.Timer _timer;

        public static void Start()
        {
            _timer = new System.Windows.Forms.Timer { Interval = 5 * 60 * 1000 };
            _timer.Tick += (s, e) => { if (ToolSettings.Current.LowSpec) Trim(); };
            _timer.Start();
            var t = ToolSettings.Current;
            if (!t.LowSpec && !t.LowSpecHinted && LooksLowEnd)
            {
                t.LowSpecHinted = true; t.Save();
                Tray.Balloon("这台电脑配置较低", "可以在 设置 → 工具 里打开「低配机模式」，关掉 AI 和语音、减少内存占用（点这里打开）", () => Program.ShowSettings("工具"));
            }
        }
    }
}
