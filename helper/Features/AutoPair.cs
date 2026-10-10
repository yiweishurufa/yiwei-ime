using System;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace Yiwei
{
    /// <summary>
    /// 括号配对 / 包裹选中文字（yiwei_autopair.lua 发来 "pair:（）"）：
    /// 通过 UI 自动化（TextPattern）读当前选区，有选中的文字就输出「左 + 选区 + 右」替换它，否则输出一对括号并把光标移到中间。
    /// 读不到选区（程序不支持 TextPattern）时照常配对。
    /// </summary>
    public static class AutoPair
    {
        public static void Type(string open, string close)
        {
            string sel = Selection();
            if (!string.IsNullOrEmpty(sel) && sel.Length <= 2000 && !sel.Contains("\n"))
            {
                TextOut.Type(open + sel + close);
                return;
            }
            TextOut.Type(open + close);
            TextOut.Pump(20);
            TextOut.Left();
        }

        /// <summary>Selected text of the focused control via UIA, or "" (never throws; skips our own windows).</summary>
        static string Selection()
        {
            try
            {
                var fg = Native.GetForegroundWindow();
                Native.GetWindowThreadProcessId(fg, out var pid);
                if (pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id) return "";
                var el = AutomationElement.FocusedElement;
                if (el == null) return "";
                if (!el.TryGetCurrentPattern(TextPattern.Pattern, out var pat)) return "";
                var ranges = ((TextPattern)pat).GetSelection();
                if (ranges == null || ranges.Length != 1) return "";
                return ranges[0].GetText(2001) ?? "";
            }
            catch { return ""; }
        }
    }
}
