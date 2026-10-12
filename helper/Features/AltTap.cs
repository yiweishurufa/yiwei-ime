using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Yiwei
{
    /// <summary>
    /// 44 单独按 Alt 切中英（可选，默认关）：轻按一下 Alt（中间没按别的键、不到 400 毫秒）就切换中英。
    /// 键盘钩子在 Alt 松开时先补一个空虚拟键（不让程序弹出菜单栏），再轻按一下左 Shift 交给 RIME 切换。
    /// 可以只在指定程序里生效（白名单）；不填则所有程序，仍排除「不响应 Alt 手势的应用」和独占全屏。
    /// </summary>
    static class AltTap
    {
        public static bool Wants(Keys vk)
        {
            var s = Settings.Current;
            var mode = s.AltTapToggle ?? "";
            if (mode.Length == 0) return false;
            if (mode == "left" && vk != Keys.LMenu) return false;
            if (mode == "right" && vk != Keys.RMenu) return false;
            foreach (var m in new[] { Keys.LShiftKey, Keys.RShiftKey, Keys.LControlKey, Keys.RControlKey, Keys.LWin, Keys.RWin })
                if ((Native.GetAsyncKeyState((int)m) & 0x8000) != 0) return false; // Alt+Shift 换键盘布局等组合不算
            return AppAllowed(s, Native.ForegroundExe());
        }

        /// <summary>白名单非空：只在名单里的程序；否则除了 Alt 手势停用名单之外都行。</summary>
        public static bool AppAllowed(Settings s, string exe)
        {
            exe = (exe ?? "").Trim();
            var white = (s.AltTapApps ?? new System.Collections.Generic.List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            if (white.Count > 0) return white.Any(x => string.Equals(x.Trim(), exe, StringComparison.OrdinalIgnoreCase));
            if (Native.ExclusiveFullscreen()) return false;
            return s.AltBlocklist == null || !s.AltBlocklist.Any(b => string.Equals(b?.Trim(), exe, StringComparison.OrdinalIgnoreCase));
        }

        public static void ToggleSoon()
        {
            Task.Run(async () =>
            {
                await Task.Delay(40).ConfigureAwait(false); // 让 Alt 松开先送到程序
                try { TextOut.TapShift(); } catch (Exception e) { Log.Write("alt tap: " + e.Message); }
            });
        }

        /// <summary>设置页的白名单文本（逗号或空格分隔）→ 规范化的 exe 名单。</summary>
        public static System.Collections.Generic.List<string> ParseApps(string text) =>
            (text ?? "").Split(new[] { ',', '，', ' ', ';', '；', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().ToLowerInvariant()).Select(x => x.EndsWith(".exe") ? x : x + ".exe").Distinct().ToList();
    }
}
