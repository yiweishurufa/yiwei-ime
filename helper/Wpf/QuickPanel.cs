using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WinKeys = System.Windows.Forms.Keys;

namespace Yiwei
{
    /// <summary>
    /// 功能面板（参考微信输入法的候选条工具栏，但只放本机功能）：两列格子，每格一个数字，按数字直接打开；
    /// ↑↓←→ + 回车、鼠标点击也行，Esc 关闭。长按 Alt + Q、托盘「功能面板」或 /quick 打开。
    /// 不占用 V 键（雾凇里 v 是 ü 和符号模式，双拼里也是键位）。每页最多 9 格，Tab / PgDn 换页。
    /// </summary>
    sealed class QuickPanel : FloatingWindow
    {
        sealed class Cell
        {
            public string Icon, Name, Hint;
            public Action Run;
            public Func<string> Unavailable; // 返回非空 = 当前不可用（原因）
        }

        PanelPalette _p;
        int _page, _sel;
        static int _lastPage;
        List<Cell>[] _pages;

        public bool IsOpen => IsVisible;

        public void Open()
        {
            _p = PanelPalette.Now();
            _pages = BuildPages();
            _page = Math.Min(_lastPage, _pages.Length - 1);
            _sel = 0;
            Build();
            ShowAtCaret();
        }

        public void Close2() { if (IsVisible) Hide(); }

        static List<Cell>[] BuildPages()
        {
            var s = Settings.Current;
            string Off(bool on, string why) => on ? null : why;
            var page1 = new List<Cell>
            {
                new Cell { Icon = "🧮", Name = "计算", Hint = "打 cC 再写算式",
                    Unavailable = () => RimeFeatures.Of(s).Calculator ? null : "计算器已关闭（设置 → 输入方案 → 快捷输入）",
                    Run = () => TextOut.TapLetters("cC") },
                new Cell { Icon = "📋", Name = "剪贴板", Hint = "Alt+V",
                    Unavailable = () => Off(s.ClipHistory, "剪贴板历史已关闭（设置 → 快捷输入）"),
                    Run = () => Program.ShowClipboard() },
                new Cell { Icon = "💬", Name = "常用语", Hint = "Alt+1–9", Run = () => Program.ShowSnippets(0) },
                new Cell { Icon = "😀", Name = "符号表情", Hint = "Alt+E", Run = () => Program.ShowSymbols() },
                new Cell { Icon = "🌐", Name = "翻译选中", Hint = "先选中文字", Run = () => ToolsHub.Run("translate-sel") },
                new Cell { Icon = "🔍", Name = "截图识字", Hint = "Alt+O", Run = () => OcrCapture.Start() },
                new Cell { Icon = "📡", Name = "局域网互传", Hint = "同一网络的电脑", Run = () => Program.ShowLanShare() },
                new Cell { Icon = "✍", Name = "手写输入", Hint = "Alt+H", Run = () => Program.ShowHandwriting() },
                new Cell { Icon = "⚙", Name = "设置", Hint = "", Run = () => Program.ShowSettings() },
            };
            var page2 = new List<Cell>
            {
                new Cell { Icon = "➕", Name = "加词", Hint = "Alt+A，先选中词", Run = () => Program.AddSelectedWord() },
                new Cell { Icon = "🈺", Name = "截图翻译", Hint = "Alt+S", Run = () => ToolsHub.Run("translate-shot") },
                new Cell { Icon = "🔤", Name = "文字工具", Hint = "Alt+T，繁简/大小写…", Run = () => ToolsHub.Run("text-menu") },
                new Cell { Icon = "✨", Name = "AI 帮写", Hint = "Alt+空格，需先配接口", Run = () => Program.ShowAi() },
                new Cell { Icon = "⌨", Name = "快捷键总览", Hint = "Alt+K", Run = () => Program.ShowSettings("工具") },
                new Cell { Icon = "🎹", Name = "双拼键位图", Hint = "Alt+P", Run = () => ToolsHub.Run("shuangpin-map") },
            };
            return new[] { page1, page2 };
        }

        void Build()
        {
            var root = new StackPanel { Width = 340 };
            var head = new Grid { Margin = new Thickness(2, 0, 2, 8) };
            head.Children.Add(new TextBlock { Text = "功能面板", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = _p.Text });
            head.Children.Add(new TextBlock { Text = (_page + 1) + " / " + _pages.Length, FontSize = 11, Foreground = _p.Dim, HorizontalAlignment = HorizontalAlignment.Right });
            root.Children.Add(head);

            var grid = new UniformGrid { Columns = 2 };
            var cells = _pages[_page];
            for (int i = 0; i < cells.Count; i++)
            {
                int idx = i;
                var c = cells[i];
                bool sel = i == _sel;
                bool off = c.Unavailable?.Invoke() != null;
                var fg = sel ? _p.AccentText : _p.Text;
                var dim = sel ? _p.AccentText : _p.Dim;
                var line = new StackPanel { Orientation = Orientation.Horizontal };
                line.Children.Add(new TextBlock { Text = (i + 1).ToString(), FontSize = 12, Foreground = dim, Width = 14, VerticalAlignment = VerticalAlignment.Center });
                line.Children.Add(new TextBlock { Text = c.Icon, FontSize = 17, Width = 28, Foreground = fg, VerticalAlignment = VerticalAlignment.Center });
                var label = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                label.Children.Add(new TextBlock { Text = c.Name, FontSize = 13, Foreground = fg });
                if (!string.IsNullOrEmpty(c.Hint)) label.Children.Add(new TextBlock { Text = c.Hint, FontSize = 10, Foreground = dim, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 105 });
                line.Children.Add(label);
                var b = new Border
                {
                    Background = sel ? _p.Accent : _p.Card, BorderBrush = _p.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(3), Padding = new Thickness(8, 6, 6, 6), Height = 48, Cursor = Cursors.Hand, Opacity = off ? 0.5 : 1, Child = line,
                };
                if (!sel) { b.MouseEnter += (s, e) => b.Background = _p.CardHover; b.MouseLeave += (s, e) => b.Background = _p.Card; }
                b.MouseLeftButtonUp += (s, e) => Activate(idx);
                grid.Children.Add(b);
            }
            root.Children.Add(grid);
            root.Children.Add(PanelParts.Hint(_p, "按数字直接打开 · ↑↓←→ 回车 · Tab 换页 · Esc 关闭"));
            Content = PanelParts.Shell(_p, root);
        }

        void Activate(int idx)
        {
            var cells = _pages[_page];
            if (idx < 0 || idx >= cells.Count) return;
            var c = cells[idx];
            var why = c.Unavailable?.Invoke();
            if (why != null) { Tray.Balloon(c.Name, why); return; }
            _lastPage = _page;
            Close2();
            // 等面板收起、按键松开后再执行，避免打开的窗口或敲出的按键被这次点击吃掉
            Program.Ui.BeginInvoke(new Action(() =>
            {
                try { c.Run(); } catch (Exception e) { Log.Write("quick " + c.Name + ": " + e); Tray.Balloon(c.Name, e.Message); }
            }));
        }

        void Move(int page) { _page = page; _sel = 0; Build(); }

        public bool HandleKey(WinKeys k)
        {
            var cells = _pages[_page];
            if (k == WinKeys.Escape) { Close2(); return true; }
            if (k >= WinKeys.D1 && k <= WinKeys.D9 || k >= WinKeys.NumPad1 && k <= WinKeys.NumPad9)
            {
                int d = k >= WinKeys.NumPad1 ? k - WinKeys.NumPad1 : k - WinKeys.D1;
                if (d < cells.Count) Activate(d);
                return true;
            }
            if (k == WinKeys.Tab || k == WinKeys.Next) { Move((_page + 1) % _pages.Length); return true; }
            if (k == WinKeys.Prior) { Move((_page + _pages.Length - 1) % _pages.Length); return true; }
            if (k == WinKeys.Right) { _sel = _sel + 1 < cells.Count ? _sel + 1 : 0; Build(); return true; }
            if (k == WinKeys.Left) { _sel = _sel > 0 ? _sel - 1 : cells.Count - 1; Build(); return true; }
            if (k == WinKeys.Down) { if (_sel + 2 < cells.Count) _sel += 2; Build(); return true; }
            if (k == WinKeys.Up) { if (_sel >= 2) _sel -= 2; Build(); return true; }
            if (k == WinKeys.Enter || k == WinKeys.Space) { Activate(_sel); return true; }
            return true; // 面板打开时吞掉其它键，和符号面板一致
        }
    }
}
