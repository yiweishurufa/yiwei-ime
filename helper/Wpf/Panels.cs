using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WinKeys = System.Windows.Forms.Keys;

namespace Yiwei
{
    static class PanelParts
    {
        public static Border Shell(PanelPalette p, UIElement child) => new Border
        {
            Background = p.Back, BorderBrush = p.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12), Margin = new Thickness(14), Child = child,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.22, Direction = 270 },
        };

        public static Border Chip(PanelPalette p, string text, bool on)
        {
            return new Border
            {
                CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 4, 0),
                Background = on ? p.Accent : Brushes.Transparent, Cursor = Cursors.Hand,
                Child = new TextBlock { Text = text, FontSize = 12, Foreground = on ? p.AccentText : p.Dim },
            };
        }

        public static TextBlock Hint(PanelPalette p, string text) =>
            new TextBlock { Text = text, FontSize = 11, Foreground = p.Dim, Margin = new Thickness(2, 8, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };

        public static string OneLine(string s) => (s ?? "").Replace("\r", "").Replace("\n", " ⏎ ");
    }

    /// <summary>Hold Alt + 1–9: snippet cards 1–9 of a group, group tabs on top.</summary>
    sealed class SnippetPanel : FloatingWindow
    {
        int _cat, _page, _sel;
        PanelPalette _p;

        public bool IsOpen => IsVisible;

        public void Open(int category)
        {
            var book = SnippetBook.Current;
            if (book.Categories.Count == 0) return;
            _cat = Math.Max(0, Math.Min(category, book.Categories.Count - 1));
            _page = 0; _sel = 0;
            _p = PanelPalette.Now();
            Build();
            ShowAtCaret();
        }

        public void Close2() { Hide(); }

        List<Snippet> Items => SnippetBook.Current.Categories[_cat].Items;
        int PageCount => Math.Max(1, (Items.Count + 8) / 9);

        void Build()
        {
            var book = SnippetBook.Current;
            var root = new StackPanel { MinWidth = 420, MaxWidth = 620 };
            var tabs = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            for (int i = 0; i < book.Categories.Count && i < 9; i++)
            {
                int idx = i;
                var chip = PanelParts.Chip(_p, (i + 1) + " " + book.Categories[i].Name, i == _cat);
                chip.MouseLeftButtonUp += (s, e) => { _cat = idx; _page = 0; _sel = 0; Build(); };
                tabs.Children.Add(chip);
            }
            root.Children.Add(tabs);

            var items = Items;
            if (items.Count == 0)
            {
                root.Children.Add(new TextBlock { Text = "这个分组还是空的。在「设置 → 常用语」里添加。", Foreground = _p.Dim, FontSize = 13, Margin = new Thickness(2, 6, 2, 6) });
            }
            else
            {
                var grid = new UniformGrid { Columns = 3 };
                for (int k = 0; k < 9; k++)
                {
                    int i = _page * 9 + k;
                    if (i >= items.Count) break;
                    bool hi = k == _sel;
                    var num = new TextBlock { Text = (k + 1).ToString(), FontWeight = FontWeights.SemiBold, FontSize = 12, Foreground = hi ? _p.AccentText : _p.Accent, Margin = new Thickness(0, 0, 0, 2) };
                    var text = new TextBlock
                    {
                        Text = items[i].Text.Replace("\r", ""), FontSize = 13, Foreground = hi ? _p.AccentText : _p.Text,
                        TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 38,
                    };
                    var card = new Border
                    {
                        Background = hi ? _p.Accent : _p.Card, BorderBrush = _p.Border, BorderThickness = new Thickness(hi ? 0 : 1),
                        CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6, 10, 8), Margin = new Thickness(3), MinHeight = 58, Cursor = Cursors.Hand,
                        Child = new StackPanel { Children = { num, text } },
                    };
                    int commit = i;
                    card.MouseLeftButtonUp += (s, e) => Commit(commit);
                    grid.Children.Add(card);
                }
                root.Children.Add(grid);
            }
            var hint = "数字键上屏 · Tab / ← → 换分组" + (PageCount > 1 ? $" · PgDn 下一页（{_page + 1}/{PageCount}）" : "") + " · Esc 关闭";
            root.Children.Add(PanelParts.Hint(_p, hint));
            Content = PanelParts.Shell(_p, root);
        }

        public bool HandleKey(WinKeys k, bool altHeld)
        {
            var book = SnippetBook.Current;
            int n = book.Categories.Count;
            if (k == WinKeys.Escape) { Close2(); return true; }
            if (k >= WinKeys.D1 && k <= WinKeys.D9 || k >= WinKeys.NumPad1 && k <= WinKeys.NumPad9)
            {
                int d = k >= WinKeys.NumPad1 ? k - WinKeys.NumPad1 : k - WinKeys.D1;
                if (altHeld) { if (d < n) { _cat = d; _page = 0; _sel = 0; Build(); } }   // Alt still held: switch group
                else Commit(_page * 9 + d);
                return true;
            }
            if (k == WinKeys.Tab || k == WinKeys.Right && Items.Count == 0) { _cat = (_cat + 1) % n; _page = 0; _sel = 0; Build(); return true; }
            if (k == WinKeys.Left && Items.Count == 0) { _cat = (_cat + n - 1) % n; _page = 0; _sel = 0; Build(); return true; }
            int onPage = Math.Min(9, Items.Count - _page * 9);
            if (k == WinKeys.Right) { if (_sel + 1 < onPage) _sel++; else { _cat = (_cat + 1) % n; _page = 0; _sel = 0; } Build(); return true; }
            if (k == WinKeys.Left) { if (_sel > 0) _sel--; else { _cat = (_cat + n - 1) % n; _page = 0; _sel = 0; } Build(); return true; }
            if (k == WinKeys.Down) { if (_sel + 3 < onPage) _sel += 3; Build(); return true; }
            if (k == WinKeys.Up) { if (_sel >= 3) _sel -= 3; Build(); return true; }
            if (k == WinKeys.Next || k == WinKeys.Oemplus) { if (_page + 1 < PageCount) { _page++; _sel = 0; Build(); } return true; }
            if (k == WinKeys.Prior || k == WinKeys.OemMinus) { if (_page > 0) { _page--; _sel = 0; Build(); } return true; }
            if (k == WinKeys.Enter || k == WinKeys.Space) { Commit(_page * 9 + _sel); return true; }
            return true; // swallow everything else while open
        }

        void Commit(int idx)
        {
            var items = Items;
            if (idx < 0 || idx >= items.Count) return;
            var text = items[idx].Text;
            Close2();
            Program.Ui.BeginInvoke(new Action(() => TextOut.Type(text)));
        }
    }

    /// <summary>
    /// Hold Alt + Space on a selection: streams the AI result next to it.
    /// Modes 翻译 / 润色 / 粤语 (from settings) + 自定义; 润色 shows a word-level diff.
    /// </summary>
    sealed class AiPanel : FloatingWindow
    {
        sealed class Mode { public string Name, Prompt; public bool Custom; }

        PanelPalette _p;
        readonly List<Mode> _modes = new List<Mode>();
        readonly Dictionary<int, string> _results = new Dictionary<int, string>();
        int _mode;
        string _source = "", _status = "", _live = "";
        bool _busy, _error;
        IntPtr _sourceWindow;
        CancellationTokenSource _cts;
        int _gen;
        TextBox _customBox;

        /// <summary>Keys are routed here by the hook unless the user is typing in the panel itself.</summary>
        public bool WantsKeys => IsVisible && !IsActive;

        public void Open()
        {
            _sourceWindow = Native.GetForegroundWindow();
            _source = TextOut.CopySelection();
            _p = PanelPalette.Now();
            _modes.Clear(); _results.Clear();
            foreach (var a in Settings.Current.AiActions.Where(a => !string.IsNullOrWhiteSpace(a.Name)).Take(8))
                _modes.Add(new Mode { Name = a.Name, Prompt = a.Prompt });
            _modes.Add(new Mode { Name = "自定义", Custom = true });
            _mode = 0;
            Build();
            ShowAtCaret();
            Run(0);
        }

        public void Close2()
        {
            _gen++;
            try { _cts?.Cancel(); } catch { }
            Hide();
            SetNoActivate(true);
        }

        bool IsPolish(int i) => i >= 0 && i < _modes.Count && _modes[i].Name.Contains("润色");

        void Run(int mode, bool retry = false)
        {
            _mode = mode;
            var m = _modes[mode];
            if (!retry && _results.ContainsKey(mode)) { _busy = false; _error = false; _status = ""; Build(); return; }
            _results.Remove(mode);
            _error = false; _live = "";
            if (string.IsNullOrWhiteSpace(_source)) { _status = "先选中一段文字，再长按 Alt 并按空格。"; _error = true; Build(); return; }
            if (!Settings.Current.AiEnabled) { _status = "AI 助手还没有开启。打开「设置 → AI」开启并填写接口。"; _error = true; Build(); return; }
            if (!Ai.Configured) { _status = "还没有配置 AI 接口。打开「设置 → AI」填写接口地址和密钥。"; _error = true; Build(); return; }
            string prompt = m.Prompt;
            if (m.Custom)
            {
                var custom = Settings.Current.AiCustomPrompt ?? "";
                if (string.IsNullOrWhiteSpace(custom)) { _status = "在上面的框里写下要求，按回车开始。"; _busy = false; Build(); FocusCustom(); return; }
                prompt = "按照用户的要求处理下面的文字，只输出结果，不要解释。要求：" + custom;
            }
            try { _cts?.Cancel(); } catch { }
            _cts = new CancellationTokenSource();
            var gen = ++_gen;
            _busy = true; _status = "生成中…";
            Build();
            var text = _source;
            Ai.Stream(prompt, text, partial => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (gen != _gen) return;
                _live = partial; UpdateResult();
            })), _cts.Token).ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (gen != _gen) return;
                _busy = false;
                if (t.IsCanceled) return;
                if (t.IsFaulted) { _error = true; _status = "⚠ " + (t.Exception?.GetBaseException().Message ?? "失败"); }
                else { _results[mode] = t.Result; _status = ""; }
                Build();
            })));
        }

        TextBlock _resultBlock;
        TextBlock _statusBlock;

        void Build()
        {
            var root = new StackPanel { Width = 480 };
            var tabs = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            var title = new TextBlock { Text = "AI", FontWeight = FontWeights.SemiBold, Foreground = _p.Accent, FontSize = 12, Margin = new Thickness(2, 3, 10, 0) };
            tabs.Children.Add(title);
            for (int i = 0; i < _modes.Count; i++)
            {
                int idx = i;
                var chip = PanelParts.Chip(_p, (i + 1) + " " + _modes[i].Name, i == _mode);
                chip.MouseLeftButtonUp += (s, e) => Run(idx);
                tabs.Children.Add(chip);
            }
            root.Children.Add(tabs);
            root.Children.Add(new TextBlock
            {
                Text = "原文：" + PanelParts.OneLine(_source), Foreground = _p.Dim, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(2, 0, 2, 8),
            });

            if (_modes.Count > 0 && _modes[_mode].Custom)
            {
                _customBox = new TextBox { Text = Settings.Current.AiCustomPrompt ?? "", FontSize = 13, Margin = new Thickness(0, 0, 0, 8), ToolTip = "例如：改成更正式的商务邮件语气" };
                _customBox.PreviewMouseLeftButtonDown += (s, e) => FocusCustom();
                _customBox.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        Settings.Current.AiCustomPrompt = _customBox.Text.Trim();
                        try { Settings.Current.Save(); } catch { }
                        e.Handled = true; Run(_mode, true);
                    }
                    else if (e.Key == Key.Escape) { e.Handled = true; CloseAndReturn(); }
                };
                root.Children.Add(_customBox);
            }
            else _customBox = null;

            _resultBlock = new TextBlock { FontSize = 14, Foreground = _p.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 2, 2, 2) };
            var resultCard = new Border
            {
                Background = _p.Card, BorderBrush = _p.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8, 10, 8),
                Child = new ScrollViewer { Content = _resultBlock, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            };
            root.Children.Add(resultCard);
            _statusBlock = new TextBlock { FontSize = 12, Margin = new Thickness(2, 6, 2, 0), TextWrapping = TextWrapping.Wrap };
            root.Children.Add(_statusBlock);
            UpdateResult();

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            bool ready = _results.ContainsKey(_mode);
            buttons.Children.Add(PanelButton("替换  ↵", true, ready, Replace));
            buttons.Children.Add(PanelButton("复制  C", false, ready, Copy));
            buttons.Children.Add(PanelButton("重试  R", false, !string.IsNullOrWhiteSpace(_source), () => Run(_mode, true)));
            buttons.Children.Add(PanelButton("关闭  Esc", false, true, CloseAndReturn));
            root.Children.Add(buttons);
            Content = PanelParts.Shell(_p, root);
        }

        Border PanelButton(string text, bool primary, bool enabled, Action click)
        {
            var b = new Border
            {
                CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 6, 0),
                Background = primary ? _p.Accent : _p.Card, BorderBrush = _p.Border, BorderThickness = new Thickness(primary ? 0 : 1),
                Opacity = enabled ? 1 : 0.45, Cursor = enabled ? Cursors.Hand : Cursors.Arrow,
                Child = new TextBlock { Text = text, FontSize = 12, Foreground = primary ? _p.AccentText : _p.Text },
            };
            if (enabled) b.MouseLeftButtonUp += (s, e) => click();
            return b;
        }

        void UpdateResult()
        {
            if (_resultBlock == null) return;
            _resultBlock.Inlines.Clear();
            if (_results.TryGetValue(_mode, out var done))
            {
                if (IsPolish(_mode))
                {
                    foreach (var piece in TextDiff.Compute(_source, done))
                    {
                        var run = new Run(piece.Text);
                        if (piece.Op == TextDiff.Op.Del) { run.Background = _p.Del; run.Foreground = _p.DelText; run.TextDecorations = TextDecorations.Strikethrough; }
                        else if (piece.Op == TextDiff.Op.Ins) { run.Background = _p.Ins; run.Foreground = _p.InsText; }
                        _resultBlock.Inlines.Add(run);
                    }
                }
                else _resultBlock.Inlines.Add(new Run(done));
            }
            else if (_busy) _resultBlock.Inlines.Add(new Run(_live.Length > 0 ? _live + " ▍" : "…") { Foreground = _live.Length > 0 ? _p.Text : _p.Dim });
            else _resultBlock.Inlines.Add(new Run(" ") { Foreground = _p.Dim });
            if (_statusBlock != null)
            {
                var hint = IsPolish(_mode) && _results.ContainsKey(_mode) ? "红色为删去，绿色为新增 · 数字键换模式" : "数字键换模式";
                _statusBlock.Text = _status.Length > 0 ? _status : hint;
                _statusBlock.Foreground = _error ? _p.DelText : _p.Dim;
            }
        }

        void FocusCustom()
        {
            if (_customBox == null) return;
            if (NoActivate) { SetNoActivate(false); }
            try { Activate(); } catch { }
            _customBox.Focus(); Keyboard.Focus(_customBox);
            _customBox.CaretIndex = _customBox.Text.Length;
        }

        void ReturnFocus()
        {
            if (_sourceWindow != IntPtr.Zero && Native.IsWindow(_sourceWindow) && Native.GetForegroundWindow() != _sourceWindow)
            {
                Native.SetForegroundWindow(_sourceWindow);
                TextOut.Pump(120);
            }
        }

        void CloseAndReturn() { bool wasActive = IsActive; Close2(); if (wasActive) ReturnFocus(); }

        void Replace()
        {
            if (!_results.TryGetValue(_mode, out var text) || string.IsNullOrEmpty(text)) return;
            Close2();
            ReturnFocus();
            Program.Ui.BeginInvoke(new Action(() => TextOut.Replace(text)));
        }

        void Copy()
        {
            if (!_results.TryGetValue(_mode, out var text) || string.IsNullOrEmpty(text)) return;
            try { Clipboard.SetText(text); _status = "已复制到剪贴板"; _error = false; UpdateResult(); }
            catch (Exception e) { _status = "复制失败：" + e.Message; _error = true; UpdateResult(); }
        }

        public bool HandleKey(WinKeys k, bool altHeld)
        {
            if (k == WinKeys.Escape) { CloseAndReturn(); return true; }
            if (k >= WinKeys.D1 && k <= WinKeys.D9) { int i = k - WinKeys.D1; if (i < _modes.Count) Run(i); return true; }
            if (k >= WinKeys.NumPad1 && k <= WinKeys.NumPad9) { int i = k - WinKeys.NumPad1; if (i < _modes.Count) Run(i); return true; }
            if (k == WinKeys.Enter) { Replace(); return true; }
            if (k == WinKeys.C) { Copy(); return true; }
            if (k == WinKeys.R) { Run(_mode, true); return true; }
            if (k == WinKeys.Tab || k == WinKeys.Right) { Run((_mode + 1) % _modes.Count); return true; }
            if (k == WinKeys.Left) { Run((_mode + _modes.Count - 1) % _modes.Count); return true; }
            return true;
        }
    }
}
