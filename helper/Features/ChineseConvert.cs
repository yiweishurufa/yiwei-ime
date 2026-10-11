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

        public static string Upper(string t) => Map(t, LCMAP_UPPERCASE, "en-US");
        public static string Lower(string t) => Map(t, LCMAP_LOWERCASE, "en-US");
        /// <summary>全角：ＡＢＣ　１２３，中文标点不变。</summary>
        public static string FullWidth(string t) => Map(t, LCMAP_FULLWIDTH, "ja-JP");
        /// <summary>半角：全角字母数字、全角空格变半角（片假名也会变半角，所以只转 FF01–FF5E 和 3000）。</summary>
        public static string HalfWidth(string t)
        {
            var sb = new StringBuilder(t.Length);
            foreach (var c in t) sb.Append(c >= 0xFF01 && c <= 0xFF5E ? (char)(c - 0xFEE0) : c == 0x3000 ? ' ' : c);
            return sb.ToString();
        }
        /// <summary>每个英文单词首字母大写（Title Case）。</summary>
        public static string Title(string t)
        {
            var sb = new StringBuilder(t.Length); bool start = true;
            foreach (var c in t)
            {
                if (char.IsLetter(c)) { sb.Append(start ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c)); start = false; }
                else { sb.Append(c); start = !(c == '\'' || c == '’'); }
            }
            return sb.ToString();
        }
        /// <summary>大小写互换：全大写 → 全小写，否则 → 全大写。</summary>
        public static string ToggleCase(string t) => t == Upper(t) ? Lower(t) : Upper(t);
        /// <summary>中文标点 ↔ 英文标点。</summary>
        public static string PunctToAscii(string t) => Swap(t, "，。！？；：（）【】“”‘’、《》", ",.!?;:()[]\"\"'',<>");
        public static string PunctToChinese(string t)
        {
            var sb = new StringBuilder(t.Length); bool dq = false, sq = false;
            foreach (var c in t)
            {
                switch (c)
                {
                    case ',': sb.Append('，'); break; case '.': sb.Append('。'); break; case '!': sb.Append('！'); break;
                    case '?': sb.Append('？'); break; case ';': sb.Append('；'); break; case ':': sb.Append('：'); break;
                    case '(': sb.Append('（'); break; case ')': sb.Append('）'); break;
                    case '"': sb.Append(dq ? '”' : '“'); dq = !dq; break;
                    case '\'': sb.Append(sq ? '’' : '‘'); sq = !sq; break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
        static string Swap(string t, string from, string to)
        {
            var sb = new StringBuilder(t.Length);
            foreach (var c in t) { int i = from.IndexOf(c); sb.Append(i >= 0 ? to[i] : c); }
            return sb.ToString();
        }

        public static string ToSimplified(string t) => Map(t, LCMAP_SIMPLIFIED);
        public static string ToTraditional(string t) => Map(t, LCMAP_TRADITIONAL);
    }
}
