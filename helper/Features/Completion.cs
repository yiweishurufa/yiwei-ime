using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Windows.Forms;

namespace Yiwei
{
    /// <summary>
    /// W35 邮箱后缀、文件路径补全：在英文状态下打到「name@」或「C:\Us」时，长按 Alt + /（或自定义快捷键）
    /// 弹出补全菜单：邮箱后缀（qq.com、163.com…，常用的排前面）/ 该目录下的文件和文件夹。点一下只补上缺的部分。
    /// 光标前的文字用 UI 自动化（TextPattern）读取；程序不支持时退而用 Shift+Home 选中再复制读取，然后取消选中。
    /// </summary>
    public static class Completion
    {
        public static readonly string[] DefaultSuffixes =
        {
            "qq.com", "163.com", "126.com", "gmail.com", "outlook.com", "foxmail.com", "hotmail.com", "sina.com", "sohu.com", "yeah.net", "139.com", "icloud.com", "aliyun.com",
        };

        static readonly Regex EmailTail = new Regex(@"([A-Za-z0-9._%+\-]+)@([A-Za-z0-9.\-]*)$");
        static readonly Regex PathTail = new Regex(@"((?:[A-Za-z]:|\\\\[^\\/\s]+|%[A-Za-z]+%|~)[\\/][^""<>|*?\r\n]*)$");

        public static void Run()
        {
            var before = TextBeforeCaret();
            if (string.IsNullOrEmpty(before)) { Tray.Balloon("补全", "读不到光标前的文字。先打出「名字@」或「C:\\」再试"); return; }
            var items = Candidates(before, out var typed);
            if (items.Count == 0) { Tray.Balloon("补全", "光标前不是邮箱（xxx@）也不是路径（C:\\…）"); return; }
            var m = new ContextMenuStrip { ShowImageMargin = false, Font = new Font("Microsoft YaHei UI", 9.5f), MaximumSize = new Size(700, 600) };
            int n = 0;
            foreach (var full in items.Take(30))
            {
                var add = full.Substring(typed.Length);
                var label = (n < 9 ? "&" + (n + 1) + "  " : "    ") + full;
                m.Items.Add(new ToolStripMenuItem(label, null, (s, e) => Program.Ui.BeginInvoke(new Action(() => Accept(full, add)))));
                n++;
            }
            var p = Native.CaretPosition();
            m.Show(new Point(p.X, p.Y + 22));
        }

        static void Accept(string full, string add)
        {
            TextOut.Pump(60);
            TextOut.Type(add);
            var at = full.IndexOf('@');
            if (at >= 0)
            {
                var t = ToolSettings.Current;
                var suffix = full.Substring(at + 1);
                t.EmailUse.TryGetValue(suffix, out var c);
                t.EmailUse[suffix] = c + 1;
                t.Save();
            }
        }

        /// <summary>Completions for the text before the caret; <paramref name="typed"/> is the part they extend.</summary>
        public static List<string> Candidates(string before, out string typed)
        {
            typed = "";
            var res = new List<string>();
            var em = EmailTail.Match(before);
            if (em.Success)
            {
                typed = em.Value;
                var user = em.Groups[1].Value; var dom = em.Groups[2].Value;
                var t = ToolSettings.Current;
                var all = (t.EmailSuffixes != null && t.EmailSuffixes.Count > 0 ? t.EmailSuffixes : DefaultSuffixes.ToList())
                    .Concat(t.EmailUse.Keys).Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(s => s.StartsWith(dom, StringComparison.OrdinalIgnoreCase) && s.Length > dom.Length)
                    .OrderByDescending(s => t.EmailUse.TryGetValue(s, out var c) ? c : 0);
                foreach (var s in all) res.Add(user + "@" + dom + s.Substring(dom.Length));
                return res;
            }
            var pm = PathTail.Match(before);
            if (pm.Success)
            {
                typed = pm.Groups[1].Value;
                var raw = typed;
                var expanded = Environment.ExpandEnvironmentVariables(raw.StartsWith("~") ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + raw.Substring(1) : raw);
                int cut = Math.Max(expanded.LastIndexOf('\\'), expanded.LastIndexOf('/'));
                var dir = expanded.Substring(0, cut + 1); var prefix = expanded.Substring(cut + 1);
                int rawCut = Math.Max(raw.LastIndexOf('\\'), raw.LastIndexOf('/'));
                var rawDir = raw.Substring(0, rawCut + 1);
                char sep = raw[rawCut];
                try
                {
                    if (!Directory.Exists(dir)) return res;
                    var di = new DirectoryInfo(dir);
                    foreach (var d in di.EnumerateDirectories().Where(x => x.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && (x.Attributes & FileAttributes.Hidden) == 0).OrderBy(x => x.Name).Take(40))
                        res.Add(rawDir + prefix + d.Name.Substring(prefix.Length) + sep);
                    foreach (var f in di.EnumerateFiles().Where(x => x.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && (x.Attributes & FileAttributes.Hidden) == 0).OrderBy(x => x.Name).Take(40))
                        res.Add(rawDir + prefix + f.Name.Substring(prefix.Length));
                }
                catch (Exception e) { Log.Write("path completion: " + e.Message); }
            }
            return res;
        }

        /// <summary>Up to 200 characters before the caret on the current line.</summary>
        static string TextBeforeCaret()
        {
            try
            {
                var el = AutomationElement.FocusedElement;
                if (el != null && el.TryGetCurrentPattern(TextPattern.Pattern, out var pat))
                {
                    var tp = (TextPattern)pat;
                    var sel = tp.GetSelection();
                    if (sel.Length > 0)
                    {
                        var r = sel[0].Clone();
                        r.MoveEndpointByRange(TextPatternRangeEndpoint.Start, tp.DocumentRange, TextPatternRangeEndpoint.Start);
                        r.MoveEndpointByRange(TextPatternRangeEndpoint.End, sel[0], TextPatternRangeEndpoint.Start);
                        var text = r.GetText(-1) ?? "";
                        var line = text.Split('\n').Last().TrimEnd('\r');
                        return line.Length > 200 ? line.Substring(line.Length - 200) : line;
                    }
                }
            }
            catch (Exception e) { Log.Write("caret text uia: " + e.Message); }
            // fallback: Shift+Home, copy, then Right to drop the selection (caret ends where it was)
            try
            {
                ToolsHub.WaitKeysUp();
                SendKeys.SendWait("+{HOME}");
                var t = TextOut.CopySelection();
                if (!string.IsNullOrEmpty(t)) SendKeys.SendWait("{RIGHT}");
                return t ?? "";
            }
            catch { return ""; }
        }
    }
}
