using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Yiwei
{
    /// <summary>
    /// The floating panel that never takes focus: snippets (hold Alt + 1..9) and AI results (hold Alt + Space).
    /// The keyboard hook routes keys here while it is visible.
    /// </summary>
    sealed class QuickPanel : Form
    {
        enum Mode { Snippets, Ai }
        Mode _mode;
        int _cat, _sel;
        string _aiSource = "";
        readonly List<string> _aiResults = new List<string>();
        readonly List<string> _aiNames = new List<string>();
        int _aiPending;
        int _aiGeneration;

        Color _back, _text, _dim, _hiBack, _hiText, _border, _accent;
        readonly Font _font = new Font("Microsoft YaHei UI", 10.5f);
        readonly Font _small = new Font("Microsoft YaHei UI", 9f);
        readonly Font _bold = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);

        public QuickPanel()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            DoubleBuffered = true;
            Width = 380; Height = 200;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TOPMOST | Native.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        public bool IsOpen => Visible;

        void LoadTheme()
        {
            bool dark = false;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    dark = k != null && (k.GetValue("AppsUseLightTheme") as int? ?? 1) == 0;
            }
            catch { }
            var s = Settings.Current;
            var c = WeaselConfig.SchemeColors(dark ? s.ColorSchemeDark : s.ColorScheme);
            Color C(string key, Color fallback) => c.TryGetValue(key, out var v) ? Color.FromArgb(unchecked((int)(v | 0xFF000000))) : fallback;
            _back = C("back_color", dark ? Color.FromArgb(30, 30, 32) : Color.FromArgb(252, 252, 251));
            _text = C("candidate_text_color", dark ? Color.FromArgb(242, 242, 240) : Color.FromArgb(36, 35, 33));
            _dim = C("comment_text_color", dark ? Color.FromArgb(163, 163, 160) : Color.FromArgb(107, 106, 103));
            _accent = C("label_color", _dim);
            _hiBack = C("hilited_candidate_back_color", Color.FromArgb(182, 64, 50));
            _hiText = C("hilited_candidate_text_color", Color.White);
            BackColor = _back;
            _border = dark ? Color.FromArgb(60, 60, 64) : Color.FromArgb(225, 224, 220);
        }

        // ---------- opening ----------

        public void OpenSnippets(int category)
        {
            var book = SnippetBook.Current;
            if (book.Categories.Count == 0) return;
            _mode = Mode.Snippets;
            _cat = Math.Min(category, book.Categories.Count - 1);
            _sel = 0;
            ShowAtCaret();
        }

        public void OpenAi()
        {
            var text = TextOut.CopySelection();
            _mode = Mode.Ai;
            _sel = 0;
            _aiSource = text;
            _aiResults.Clear(); _aiNames.Clear();
            var gen = ++_aiGeneration;
            var actions = Settings.Current.AiActions.Where(a => !string.IsNullOrWhiteSpace(a.Name)).Take(9).ToList();
            if (string.IsNullOrWhiteSpace(text))
            {
                _aiNames.Add("提示"); _aiResults.Add("先选中一段文字，再长按 Alt 并按空格。");
                _aiPending = 0;
            }
            else if (!Ai.Configured)
            {
                _aiNames.Add("提示"); _aiResults.Add("还没有配置 AI 接口。打开「一维输入法设置 → AI 助手」填写接口地址和密钥。");
                _aiPending = 0;
            }
            else
            {
                _aiPending = actions.Count;
                for (int i = 0; i < actions.Count; i++)
                {
                    int idx = i;
                    _aiNames.Add(actions[i].Name);
                    _aiResults.Add(null);
                    Ai.Run(actions[i].Prompt, text).ContinueWith(t =>
                    {
                        if (IsDisposed) return;
                        BeginInvoke(new Action(() =>
                        {
                            if (gen != _aiGeneration) return;
                            _aiResults[idx] = t.IsFaulted ? "⚠ " + (t.Exception?.GetBaseException().Message ?? "失败") : t.Result;
                            _aiPending--;
                            Relayout(); Invalidate();
                        }));
                    });
                }
            }
            ShowAtCaret();
        }

        void ShowAtCaret()
        {
            LoadTheme();
            Relayout();
            var p = Native.CaretPosition();
            var screen = Screen.FromPoint(p).WorkingArea;
            int x = Math.Max(screen.Left, Math.Min(p.X, screen.Right - Width));
            int y = p.Y + Height > screen.Bottom ? p.Y - Height - 30 : p.Y;
            Location = new Point(x, Math.Max(screen.Top, y));
            if (!Visible) Show();
            Invalidate();
        }

        public void Close2() { _aiGeneration++; Hide(); }

        // ---------- keys (from the hook) ----------

        public bool HandleKey(Keys k)
        {
            if (k == Keys.Escape) { Close2(); return true; }
            if (_mode == Mode.Snippets) return SnippetKey(k);
            return AiKey(k);
        }

        bool SnippetKey(Keys k)
        {
            var book = SnippetBook.Current;
            var items = book.Categories[_cat].Items;
            if (k >= Keys.D1 && k <= Keys.D9) { int c = k - Keys.D1; if (c < book.Categories.Count) { _cat = c; _sel = 0; Relayout(); Invalidate(); } return true; }
            if (k == Keys.Left || k == Keys.Right || k == Keys.Tab)
            {
                _cat = (_cat + (k == Keys.Left ? book.Categories.Count - 1 : 1)) % book.Categories.Count; _sel = 0; Relayout(); Invalidate(); return true;
            }
            if (k == Keys.Up) { if (items.Count > 0) _sel = (_sel + items.Count - 1) % items.Count; Invalidate(); return true; }
            if (k == Keys.Down) { if (items.Count > 0) _sel = (_sel + 1) % items.Count; Invalidate(); return true; }
            if (k == Keys.Enter || k == Keys.Space) { Commit(items, _sel); return true; }
            if (k >= Keys.A && k <= Keys.Z)
            {
                int idx = SnippetBook.ItemKeys.IndexOf((char)k);
                if (idx >= 0 && idx < items.Count) Commit(items, idx);
                return true;
            }
            return true; // swallow everything else while open
        }

        void Commit(List<Snippet> items, int idx)
        {
            if (idx < 0 || idx >= items.Count) return;
            var text = items[idx].Text;
            Close2();
            BeginInvoke(new Action(() => TextOut.Type(text)));
        }

        bool AiKey(Keys k)
        {
            if (k >= Keys.D1 && k <= Keys.D9) { ApplyAi(k - Keys.D1); return true; }
            if (k == Keys.Up) { if (_aiResults.Count > 0) _sel = (_sel + _aiResults.Count - 1) % _aiResults.Count; Invalidate(); return true; }
            if (k == Keys.Down) { if (_aiResults.Count > 0) _sel = (_sel + 1) % _aiResults.Count; Invalidate(); return true; }
            if (k == Keys.Enter) { ApplyAi(_sel); return true; }
            return true;
        }

        void ApplyAi(int idx)
        {
            if (string.IsNullOrWhiteSpace(_aiSource) || idx < 0 || idx >= _aiResults.Count) return;
            var r = _aiResults[idx];
            if (string.IsNullOrEmpty(r) || r.StartsWith("⚠")) return;
            Close2();
            BeginInvoke(new Action(() => TextOut.Replace(r)));
        }

        // ---------- drawing ----------

        const int Pad = 12, Row = 30, Header = 34, Footer = 24;

        void Relayout()
        {
            int rows;
            int width = 380;
            if (_mode == Mode.Snippets)
            {
                var items = SnippetBook.Current.Categories[_cat].Items;
                rows = Math.Max(1, Math.Min(items.Count, SnippetBook.ItemKeys.Length));
                using (var g = CreateGraphics())
                {
                    foreach (var it in items.Take(26)) width = Math.Max(width, (int)g.MeasureString(OneLine(it.Text), _font).Width + 80);
                    int tabs = SnippetBook.Current.Categories.Sum(c => (int)g.MeasureString(c.Name, _small).Width + 34);
                    width = Math.Max(width, tabs + 2 * Pad);
                }
                width = Math.Min(width, 640);
                Height = Header + rows * Row + Footer + Pad;
            }
            else
            {
                width = 460;
                int h = Header;
                using (var g = CreateGraphics())
                    foreach (var r in _aiResults)
                        h += Math.Max(Row, (int)g.MeasureString(r ?? "生成中…", _font, width - 70).Height + 12);
                Height = h + Footer + Pad;
            }
            Width = width;
            using (var p = Rounded(new Rectangle(0, 0, Width, Height), 12)) Region = new Region(p);
        }

        static string OneLine(string s) => (s ?? "").Replace("\r", "").Replace("\n", " ⏎ ");

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Rounded(rect, 12))
            using (var b = new SolidBrush(_back))
            using (var pen = new Pen(_border))
            { g.FillPath(b, path); g.DrawPath(pen, path); }

            if (_mode == Mode.Snippets) PaintSnippets(g); else PaintAi(g);
        }

        void PaintSnippets(Graphics g)
        {
            var book = SnippetBook.Current;
            int x = Pad;
            for (int i = 0; i < book.Categories.Count && i < 9; i++)
            {
                var label = (i + 1) + " " + book.Categories[i].Name;
                var sz = g.MeasureString(label, _small);
                var r = new Rectangle(x, 7, (int)sz.Width + 14, 22);
                if (i == _cat)
                {
                    using (var p = Rounded(r, 8)) using (var b = new SolidBrush(_hiBack)) g.FillPath(b, p);
                    TextRenderer.DrawText(g, label, _small, r, _hiText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else TextRenderer.DrawText(g, label, _small, r, _dim, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                x += r.Width + 6;
            }
            var items = book.Categories[_cat].Items;
            int y = Header;
            if (items.Count == 0)
                TextRenderer.DrawText(g, "这一类还是空的。在「一维输入法设置 → 常用语」里添加。", _font, new Rectangle(Pad, y, Width - 2 * Pad, Row), _dim, TextFormatFlags.VerticalCenter);
            for (int i = 0; i < items.Count && i < SnippetBook.ItemKeys.Length; i++)
            {
                var r = new Rectangle(Pad - 4, y, Width - 2 * Pad + 8, Row - 2);
                bool hi = i == _sel;
                if (hi) using (var p = Rounded(r, 7)) using (var b = new SolidBrush(_hiBack)) g.FillPath(b, p);
                TextRenderer.DrawText(g, SnippetBook.ItemKeys[i].ToString(), _bold, new Rectangle(r.X + 6, r.Y, 22, r.Height), hi ? _hiText : _accent, TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, OneLine(items[i].Text), _font, new Rectangle(r.X + 30, r.Y, r.Width - 36, r.Height), hi ? _hiText : _text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                y += Row;
            }
            TextRenderer.DrawText(g, "字母键上屏 · 数字切换分类 · ↑↓ 移动 · Esc 关闭", _small, new Rectangle(Pad, Height - Footer - 4, Width - 2 * Pad, Footer), _dim, TextFormatFlags.VerticalCenter);
        }

        void PaintAi(Graphics g)
        {
            var title = string.IsNullOrWhiteSpace(_aiSource) ? "AI 助手" : "AI · " + OneLine(_aiSource);
            TextRenderer.DrawText(g, title, _small, new Rectangle(Pad, 6, Width - 2 * Pad, 24), _dim, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            int y = Header;
            for (int i = 0; i < _aiResults.Count; i++)
            {
                var text = _aiResults[i] ?? "生成中…";
                var sz = g.MeasureString(text, _font, Width - 70);
                int h = Math.Max(Row, (int)sz.Height + 12);
                var r = new Rectangle(Pad - 4, y, Width - 2 * Pad + 8, h - 2);
                bool hi = i == _sel && _aiResults[i] != null;
                if (hi) using (var p = Rounded(r, 7)) using (var b = new SolidBrush(_hiBack)) g.FillPath(b, p);
                TextRenderer.DrawText(g, (i + 1).ToString(), _bold, new Rectangle(r.X + 6, r.Y + 5, 16, 22), hi ? _hiText : _accent, TextFormatFlags.Top);
                TextRenderer.DrawText(g, _aiNames[i], _small, new Rectangle(r.X + 22, r.Y + 6, 40, 22), hi ? _hiText : _dim, TextFormatFlags.Top);
                using (var b = new SolidBrush(hi ? _hiText : (_aiResults[i] == null ? _dim : _text)))
                    g.DrawString(text, _font, b, new RectangleF(r.X + 64, r.Y + 5, r.Width - 70, r.Height - 6));
                y += h;
            }
            var hint = _aiPending > 0 ? "正在生成… · Esc 关闭" : "数字键替换选中文字 · Esc 关闭";
            TextRenderer.DrawText(g, hint, _small, new Rectangle(Pad, Height - Footer - 4, Width - 2 * Pad, Footer), _dim, TextFormatFlags.VerticalCenter);
        }

        static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var p = new GraphicsPath(); int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }
    }
}
