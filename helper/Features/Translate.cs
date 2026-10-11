using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Yiwei
{
    /// <summary>
    /// 翻译：配置了 AI（设置 → AI）就用 AI，否则用免费的 MyMemory 接口（无需账号，每天约 5000 字，文字会发到该服务）。
    /// </summary>
    public static class Translator
    {
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        public static readonly KeyValuePair<string, string>[] Targets =
        {
            new KeyValuePair<string, string>("auto", "自动（中文→英文，其它→中文）"),
            new KeyValuePair<string, string>("zh", "中文"), new KeyValuePair<string, string>("en", "英文"),
            new KeyValuePair<string, string>("ja", "日文"), new KeyValuePair<string, string>("ko", "韩文"),
        };

        static string LangName(string code) => code == "zh" ? "简体中文" : code == "en" ? "英文" : code == "ja" ? "日文" : code == "ko" ? "韩文" : code;

        public static bool MostlyChinese(string text)
        {
            int cjk = 0, letters = 0;
            foreach (var c in text) { if (c >= 0x3400 && c <= 0x9FFF) cjk++; else if (char.IsLetter(c)) letters++; }
            return cjk > 0 && cjk * 2 >= letters;
        }

        public static string ResolveTarget(string text)
        {
            var t = ToolSettings.Current.TranslateTarget ?? "auto";
            if (t != "auto") return t;
            return MostlyChinese(text) ? "en" : "zh";
        }

        /// <summary>Translates <paramref name="text"/>; returns (translation, engine name).</summary>
        public static async Task<Tuple<string, string>> Translate(string text, string target = null)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return Tuple.Create("", "");
            target = target ?? ResolveTarget(text);
            var engine = ToolSettings.Current.TranslateEngine ?? "auto";
            bool useAi = engine == "ai" || (engine == "auto" && Ai.Configured);
            if (useAi)
            {
                if (!Ai.Configured) throw new Exception("还没有配置 AI：设置 → AI 里填好接口，或把翻译引擎改成「免费接口」");
                var r = await Ai.Run("把用户给的文字翻译成" + LangName(target) + "。只输出译文，保留换行，不要解释。", text);
                return Tuple.Create((r ?? "").Trim(), "AI");
            }
            return Tuple.Create(await MyMemory(text, target), "MyMemory");
        }

        static async Task<string> MyMemory(string text, string target)
        {
            string src = MostlyChinese(text) ? "zh-CN" : "en";
            string dst = target == "zh" ? "zh-CN" : target;
            if (src.StartsWith(dst.Substring(0, 2))) src = dst == "en" ? "zh-CN" : "en";
            var sb = new StringBuilder();
            foreach (var chunk in Chunks(text, 450))
            {
                if (chunk.Trim().Length == 0) { sb.Append(chunk); continue; }
                var url = "https://api.mymemory.translated.net/get?q=" + Uri.EscapeDataString(chunk) + "&langpair=" + src + "|" + dst;
                var json = await Http.GetStringAsync(url);
                var d = Json.Parse(json) as Dictionary<string, object>;
                var rd = d != null && d.TryGetValue("responseData", out var o) ? o as Dictionary<string, object> : null;
                var t = rd != null && rd.TryGetValue("translatedText", out var tt) ? tt as string : null;
                if (t == null) throw new Exception("翻译服务没有返回结果");
                if (t.StartsWith("MYMEMORY WARNING", StringComparison.OrdinalIgnoreCase)) throw new Exception("免费翻译接口今天的额度用完了；可以在设置 → AI 里配置自己的 AI 接口");
                sb.Append(System.Net.WebUtility.HtmlDecode(t));
            }
            return sb.ToString();
        }

        /// <summary>Splits on line ends (keeping them) into pieces of at most ~max characters.</summary>
        static IEnumerable<string> Chunks(string text, int max)
        {
            var cur = new StringBuilder();
            foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
            {
                var l = line;
                while (l.Length > max) { if (cur.Length > 0) { yield return cur.ToString(); cur.Clear(); } yield return l.Substring(0, max); l = l.Substring(max); }
                if (cur.Length + l.Length + 1 > max && cur.Length > 0) { yield return cur.ToString(); cur.Clear(); }
                if (cur.Length > 0) cur.Append('\n');
                cur.Append(l);
            }
            if (cur.Length > 0) yield return cur.ToString();
        }
    }

    /// <summary>W29 截图翻译：框选屏幕 → 离线 OCR → 翻译 → 原文/译文窗口，可上屏、复制。</summary>
    public static class ScreenTranslate
    {
        public static void Start()
        {
            OcrCapture.Start((text, error, target, where) => TranslateWindow.Show(text, error, target, where, "截图翻译"));
        }

        public static void Selection()
        {
            string sel = "";
            try { sel = TextOut.CopySelection(); } catch { }
            if (string.IsNullOrWhiteSpace(sel)) { Tray.Balloon("翻译", "先选中要翻译的文字"); return; }
            var target = Native.GetForegroundWindow();
            var p = Native.CaretPosition();
            TranslateWindow.Show(sel, null, target, new Point(p.X, p.Y + 24), "翻译选中文字", replace: true);
        }
    }

    sealed class TranslateWindow : Form
    {
        static readonly Color Blue = Color.FromArgb(0x2B, 0x5B, 0xD7);
        readonly TextBox _src, _dst;
        readonly Label _info;
        readonly IntPtr _target;
        readonly bool _replace;

        public static void Show(string text, string error, IntPtr target, Point where, string title, bool replace = false)
        {
            var f = new TranslateWindow(text ?? "", error, target, title, replace);
            var wa = Screen.FromPoint(where).WorkingArea;
            f.Location = new Point(Math.Max(wa.Left, Math.Min(where.X, wa.Right - f.Width)), Math.Max(wa.Top, Math.Min(where.Y, wa.Bottom - f.Height)));
            f.Show(); f.Activate();
            if (error == null && !string.IsNullOrWhiteSpace(text)) _ = f.Go();
        }

        TranslateWindow(string text, string error, IntPtr target, string title, bool replace)
        {
            _target = target; _replace = replace;
            Text = title + " · 一维输入法"; Icon = Program.AppIcon; StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.SizableToolWindow; TopMost = true; ShowInTaskbar = false; KeyPreview = true;
            AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Microsoft YaHei UI", 9.5f);
            ClientSize = new Size(480, 340); BackColor = Color.White;
            _src = Box(text); _dst = Box("");
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 120, Panel1MinSize = 40, Panel2MinSize = 40 };
            split.Panel1.Padding = split.Panel2.Padding = new Padding(10, 2, 10, 2);
            split.Panel1.Controls.Add(_src); split.Panel2.Controls.Add(_dst);
            _info = new Label { Dock = DockStyle.Top, Height = 28, Padding = new Padding(10, 6, 10, 0), ForeColor = Color.FromArgb(0x5B, 0x64, 0x78),
                Text = error != null ? "识别失败：" + error : string.IsNullOrWhiteSpace(text) ? "没有识别到文字" : "正在翻译…（上面是原文，可改后按「重新翻译」）" };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 6, 8, 6), BackColor = Color.FromArgb(0xF4, 0xF6, 0xFB) };
            var type = Btn(replace ? "替换原文" : "上屏译文", true); var copy = Btn("复制译文", false); var again = Btn("重新翻译", false);
            var lang = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Margin = new Padding(6, 4, 0, 0) };
            foreach (var t in Translator.Targets) lang.Items.Add(t.Value);
            lang.SelectedIndex = Math.Max(0, Array.FindIndex(Translator.Targets, t => t.Key == (ToolSettings.Current.TranslateTarget ?? "auto")));
            lang.SelectedIndexChanged += (s, e) => { ToolSettings.Current.TranslateTarget = Translator.Targets[lang.SelectedIndex].Key; ToolSettings.Current.Save(); _ = Go(); };
            bar.Controls.Add(type); bar.Controls.Add(copy); bar.Controls.Add(again); bar.Controls.Add(lang);
            Controls.Add(split); Controls.Add(_info); Controls.Add(bar);
            type.Click += (s, e) =>
            {
                var t = _dst.Text; if (t.Length == 0) return;
                Close();
                if (_target != IntPtr.Zero && Native.IsWindow(_target)) Native.SetForegroundWindow(_target);
                TextOut.Pump(150);
                if (_replace) TextOut.Replace(t); else TextOut.Type(t);
            };
            copy.Click += (s, e) => { try { if (_dst.Text.Length > 0) Clipboard.SetText(_dst.Text); } catch { } Close(); };
            again.Click += (s, e) => _ = Go();
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) Close();
                if (e.KeyCode == Keys.Enter && e.Control) { e.SuppressKeyPress = true; type.PerformClick(); }
            };
        }

        async Task Go()
        {
            var text = _src.Text;
            if (string.IsNullOrWhiteSpace(text)) return;
            _info.Text = "正在翻译…"; _dst.Text = "";
            try
            {
                var r = await Translator.Translate(text);
                if (IsDisposed) return;
                _dst.Text = r.Item1.Replace("\r\n", "\n").Replace("\n", "\r\n");
                _info.Text = "译文来自 " + r.Item2 + (r.Item2 == "AI" ? "" : "（文字会发送到该在线服务）") + " · Ctrl+Enter " + (_replace ? "替换" : "上屏");
            }
            catch (Exception e) { if (!IsDisposed) _info.Text = "翻译失败：" + e.GetBaseException().Message; }
        }

        static TextBox Box(string t) => new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("Microsoft YaHei UI", 11f), Text = t.Replace("\r\n", "\n").Replace("\n", "\r\n"), WordWrap = true };

        static Button Btn(string text, bool primary)
        {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(76, 32), FlatStyle = FlatStyle.Flat, Margin = new Padding(6, 0, 0, 0), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderColor = Blue;
            b.BackColor = primary ? Blue : Color.White;
            b.ForeColor = primary ? Color.White : Blue;
            return b;
        }
    }
}
