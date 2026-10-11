using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace Yiwei
{
    /// <summary>
    /// W41 助手按需启动、加快开机：
    /// · 「延迟」（默认）：开机自启（/background 且系统刚启动 3 分钟内）时，助手先以低优先级只起托盘/手势，
    ///   工具组的后台任务（自动备份、WebDAV、壁纸取色、剪贴板合并…）推迟 90 秒再开始；
    /// · 「不自动启动」：在 Windows「启动应用」里关掉 YiweiHelper（HKCU StartupApproved，不需要管理员），
    ///   以后从语言栏菜单（设置、手写、符号…）或开始菜单打开时才启动，长按 Alt 手势在那之前不可用。
    /// </summary>
    public static class StartupMode
    {
        const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        const string Name = "YiweiHelper";

        public static bool AtBoot
        {
            get
            {
                if (!Environment.GetCommandLineArgs().Contains2("/background")) return false;
                return Environment.TickCount > 0 && Environment.TickCount < 3 * 60 * 1000;
            }
        }

        static bool Contains2(this string[] a, string v) { foreach (var x in a) if (string.Equals(x, v, StringComparison.OrdinalIgnoreCase)) return true; return false; }

        /// <summary>Windows 「启动应用」开关（StartupApproved：首字节 02 = 启用，03 = 停用）。</summary>
        public static bool AutoStart
        {
            get
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(Approved))
                        return !(k?.GetValue(Name) is byte[] b && b.Length > 0 && (b[0] & 1) == 1);
                }
                catch { return true; }
            }
            set
            {
                try
                {
                    using (var k = Registry.CurrentUser.CreateSubKey(Approved))
                    {
                        var b = new byte[12];
                        b[0] = (byte)(value ? 2 : 3);
                        if (!value) BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(b, 4);
                        k.SetValue(Name, b, RegistryValueKind.Binary);
                    }
                }
                catch (Exception e) { Log.Write("startup approved: " + e.Message); }
            }
        }

        /// <summary>Called first by ToolsHub.Start: at boot run at low priority for a minute.</summary>
        public static int DelayMs()
        {
            if (!ToolSettings.Current.DelayAtBoot || !AtBoot) return 0;
            try
            {
                using (var p = Process.GetCurrentProcess()) p.PriorityClass = ProcessPriorityClass.BelowNormal;
                var t = new System.Windows.Forms.Timer { Interval = 90 * 1000 };
                t.Tick += (s, e) => { t.Stop(); t.Dispose(); try { using (var p = Process.GetCurrentProcess()) p.PriorityClass = ProcessPriorityClass.Normal; } catch { } };
                t.Start();
            }
            catch { }
            return 90 * 1000;
        }
    }
}
