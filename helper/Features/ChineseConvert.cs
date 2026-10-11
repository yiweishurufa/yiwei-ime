using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Yiwei
{
    /// <summary>
    /// W31 选中文字繁简互转 / W32 大小写、全半角：用 Windows 自带的 LCMapStringEx（离线、不需要词表）。
    /// 繁简是逐字转换（系统表），「乾隆/干燥」这类一对多的词按系统默认处理。
    /// </summary>
    public static class TextConvert
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern int LCMapStringEx(string locale, uint flags, string src, int srcLen, StringBuilder dest, int destLen, IntPtr ver, IntPtr reserved, IntPtr param);

        const uint LCMAP_LOWERCASE = 0x100, LCMAP_UPPERCASE = 0x200, LCMAP_HALFWIDTH = 0x400000, LCMAP_FULLWIDTH = 0x800000,
                   LCMAP_SIMPLIFIED = 0x2000000, LCMAP_TRADITIONAL = 0x4000000;

        static string Map(string text, uint flags, string locale = "zh-CN")
        {
            if (string.IsNullOrEmpty(text)) return text;
            int n = LCMapStringEx(locale, flags, text, text.Length, null, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (n <= 0) throw new Exception("系统转换失败（错误 " + Marshal.GetLastWin32Error() + "）");
            var sb = new StringBuilder(n);
            n = LCMapStringEx(locale, flags, text, text.Length, sb, n, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (n <= 0) throw new Exception("系统转换失败（错误 " + Marshal.GetLastWin32Error() + "）");
            return sb.ToString(0, n);
        }

        public static string ToSimplified(string t) => Map(t, LCMAP_SIMPLIFIED);
        public static string ToTraditional(string t) => Map(t, LCMAP_TRADITIONAL);
    }
}
