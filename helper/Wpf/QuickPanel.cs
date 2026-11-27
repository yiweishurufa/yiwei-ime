using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WinKeys = System.Windows.Forms.Keys;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    /// <summary>
    /// 功能面板（参考微信输入法的候选条工具栏，但只放本机功能）。v2（DESIGN-v2 §7.2）：
    /// 4 个分类页签（常用 / 词库 / 工具 / 文件），每页 ≤ 9 格、3 列，数字 1–9 直接打开；↑↓←→ + 回车、鼠标点击也行，
    /// Tab / PgDn 下一页、Shift+Tab / PgUp 上一页，Esc 关闭。长按 Alt + Q、托盘「功能面板」或 /quick 打开。
    /// 不占用 V 键（雾凇里 v 是 ü 和符号模式，双拼里也是键位）。图标统一用 Fluent System Icons（WPF-UI 自带，MIT）。
    /// </summary>
    sealed class QuickPanel : FloatingWindow
    {
        sealed class Cell
        {
            public Ui.SymbolRegular Icon;
            public string Name, Hint;
            public Action Run;
            public Func<string> Unavailable; // 返回非空 = 当前不可用（原因）
        }

        PanelPalette _p;
        int _page, _sel;
        static int _lastPage;
        List<Cell>[] _pages;
        static readonly string[] PageNames = { "常用", "词库", "工具", "文件" };

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

        static void OpenUserFile(string f)
        {
            try
            {
                if (!System.IO.File.Exists(f)) System.IO.File.WriteAllText(f, "", new System.Text.UTF8Encoding(false));
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", "\"" + f + "\"") { UseShellExecute = true });
            }
            catch { }
        }

        static List<Cell>[] BuildPages()
        {
            var s = Settings.Current;
            string Off(bool on, string why) => on ? null : why;
            void Folder(string d) { try { System.IO.Directory.CreateDirectory(d); System.Diagnostics.Process.Start("explorer.exe", "\"" + d + "\""); } catch { } }
            void OpenIfExists(string f) { try { if (System.IO.File.Exists(f)) OpenUserFile(f); else System.Diagnostics.Process.Start("explorer.exe", Paths.UserDir); } catch { } }
            // 1 常用：打字时最常顺手用的
            var common = new List<Cell>
            {
                new Cell { Icon = Ui.SymbolRegular.Calculator24, Name = "计算", Hint = "打 cC 再写算式",
                    Unavailable = () => RimeFeatures.Of(s).Calculator ? null : "计算器已关闭（设置 → 快捷输入）",
                    Run = () => TextOut.TapLetters("cC") },
                new Cell { Icon = Ui.SymbolRegular.Clipboard24, Name = "剪贴板", Hint = "Alt+V",
                    Unavailable = () => Off(s.ClipHistory, "剪贴板历史已关闭（设置 → 快捷输入）"),
                    Run = () => Program.ShowClipboard() },
                new Cell { Icon = Ui.SymbolRegular.TextBulletListSquare24, Name = "常用语", Hint = "Alt+1–9", Run = () => Program.ShowSnippets(0) },
                new Cell { Icon = Ui.SymbolRegular.Emoji24, Name = "符号表情", Hint = "Alt+E", Run = () => Program.ShowSymbols() },
                new Cell { Icon = Ui.SymbolRegular.Pen24, Name = "手写输入", Hint = "Alt+H", Run = () => Program.ShowHandwriting() },
                new Cell { Icon = Ui.SymbolRegular.ScanText24, Name = "截图识字", Hint = "Alt+O", Run = () => OcrCapture.Start() },
                new Cell { Icon = Ui.SymbolRegular.Translate24, Name = "翻译选中", Hint = "先选中文字", Run = () => ToolsHub.Run("translate-sel") },
                new Cell { Icon = Ui.SymbolRegular.Sparkle24, Name = "AI 帮写", Hint = "Alt+空格，需先配接口", Run = () => Program.ShowAi() },
                new Cell { Icon = Ui.SymbolRegular.Settings24, Name = "设置", Hint = "全部设置", Run = () => Program.ShowSettings() },
            };
            // 2 词库：加词、查错、规则、同步
            var dict = new List<Cell>
            {
                new Cell { Icon = Ui.SymbolRegular.AddCircle24, Name = "加词", Hint = "Alt+A，先选中词", Run = () => Program.AddSelectedWord() },
                new Cell { Icon = Ui.SymbolRegular.Stethoscope24, Name = "词库体检", Hint = "查我的词条有没有打错", Run = () => Program.ShowSettings("工具") },
                new Cell { Icon = Ui.SymbolRegular.TextEditStyle24, Name = "自定义短语", Hint = "custom_phrase.txt", Run = () => OpenUserFile(System.IO.Path.Combine(Paths.UserDir, "custom_phrase.txt")) },
                new Cell { Icon = Ui.SymbolRegular.Wrench24, Name = "纠错规则", Hint = "corrections.txt", Run = () => OpenUserFile(Paths.CorrectionsFile) },
                new Cell { Icon = Ui.SymbolRegular.Prohibited24, Name = "屏蔽词", Hint = "blocked.txt，一行一个", Run = () => OpenUserFile(Paths.BlockedFile) },
                new Cell { Icon = Ui.SymbolRegular.Link24, Name = "联想记录", Hint = "bigram.tsv，可删行", Run = () => OpenUserFile(System.IO.Path.Combine(Paths.UserDir, "bigram.tsv")) },
                new Cell { Icon = Ui.SymbolRegular.CloudSync24, Name = "同步词库", Hint = "立即同步到同步目录", Run = () => System.Threading.Tasks.Task.Run(() => Deploy.Run("/sync", false)) },
                new Cell { Icon = Ui.SymbolRegular.ArrowSync24, Name = "重新部署", Hint = "改完词库后点一下", Run = () => System.Threading.Tasks.Task.Run(() => Deploy.Run("/deploy", false)) },
            };
            // 3 工具：文字处理、传输、查看
            var tools = new List<Cell>
            {
                new Cell { Icon = Ui.SymbolRegular.Screenshot24, Name = "截图翻译", Hint = "Alt+S", Run = () => ToolsHub.Run("translate-shot") },
                new Cell { Icon = Ui.SymbolRegular.TextCaseTitle24, Name = "文字工具", Hint = "Alt+T，繁简/大小写", Run = () => ToolsHub.Run("text-menu") },
                new Cell { Icon = Ui.SymbolRegular.Send24, Name = "局域网互传", Hint = "同一网络的电脑", Run = () => Program.ShowLanShare() },
                new Cell { Icon = Ui.SymbolRegular.Keyboard24, Name = "快捷键总览", Hint = "Alt+K", Run = () => Program.ShowSettings("工具") },
                new Cell { Icon = Ui.SymbolRegular.KeyboardLayoutFloat24, Name = "双拼键位图", Hint = "Alt+P", Run = () => ToolsHub.Run("shuangpin-map") },
                new Cell { Icon = Ui.SymbolRegular.Gauge24, Name = "性能体检", Hint = "候选卡顿时看看", Run = () => Program.ShowSettings("工具") },
                new Cell { Icon = Ui.SymbolRegular.ArrowCircleUp24, Name = "检查更新", Hint = "看有没有新版本", Run = () => { _ = AppUpdate.CheckAsync(true); } },
            };
            // 4 文件：配置与数据所在处、排查问题
            var files = new List<Cell>
            {
                new Cell { Icon = Ui.SymbolRegular.FolderOpen24, Name = "用户文件夹", Hint = "词库、配置所在处", Run = () => Folder(Paths.UserDir) },
                new Cell { Icon = Ui.SymbolRegular.Archive24, Name = "词库同步目录", Hint = "备份 / 迁移词库", Run = () => Folder(System.IO.Path.Combine(Paths.UserDir, "sync")) },
                new Cell { Icon = Ui.SymbolRegular.AppFolder24, Name = "程序目录", Hint = "安装目录", Run = () => Folder(Paths.InstallDir) },
                new Cell { Icon = Ui.SymbolRegular.DocumentBulletList24, Name = "方案定制", Hint = "default.custom.yaml", Run = () => OpenIfExists(Paths.DefaultCustom) },
                new Cell { Icon = Ui.SymbolRegular.PaintBrush24, Name = "外观定制", Hint = "weasel.custom.yaml", Run = () => OpenIfExists(Paths.WeaselCustom) },
                new Cell { Icon = Ui.SymbolRegular.DocumentSearch24, Name = "助手日志", Hint = "yiwei-helper.log", Run = () => OpenUserFile(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yiwei-helper.log")) },
                new Cell { Icon = Ui.SymbolRegular.Bug24, Name = "导出诊断", Hint = "反馈问题时用", Run = () => Diagnostics.ExportAndShow() },
            };
            return new[] { common, dict, tools, files };
        }

        const int Cols = 3;

        void Build(bool animate = false)
        {
            var root = new StackPanel { Width = 384 };
            var head = new Grid { Margin = new Thickness(2, 0, 2, Tok.S12) };
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.Children.Add(new TextBlock { Text = "功能面板", FontSize = Tok.Body, FontWeight = FontWeights.SemiBold, Foreground = _p.Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, Tok.S16, 0) });
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            for (int i = 0; i < _pages.Length; i++)
            {
                int pi = i;
                var chip = PanelParts.Chip(_p, PageNames[i], i == _page);
                chip.MouseLeftButtonUp += (s, e) => Move(pi);
                System.Windows.Automation.AutomationProperties.SetHelpText(chip, "第 " + (i + 1) + " 页，共 " + _pages.Length + " 页");
                tabs.Children.Add(chip);
            }
            Grid.SetColumn(tabs, 1); head.Children.Add(tabs);
            root.Children.Add(head);

            var grid = new UniformGrid { Columns = Cols };
            var cells = _pages[_page];
            for (int i = 0; i < 9; i++)
            {
                if (i >= cells.Count) { grid.Children.Add(new Border { Height = 88, Margin = new Thickness(3) }); continue; } // 占位，保持每页高度一致
                int idx = i;
                var c = cells[i];
                bool sel = i == _sel;
                bool off = c.Unavailable?.Invoke() != null;
                var body = new Grid();
                body.Children.Add(new TextBlock { Text = (i + 1).ToString(), FontSize = 11, Foreground = sel ? _p.AccentInk : _p.Faint, FontWeight = sel ? FontWeights.SemiBold : FontWeights.Normal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });
                var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var tile = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(Tok.RCard), Background = sel ? _p.Accent : _p.AccentSoft, HorizontalAlignment = HorizontalAlignment.Center };
                tile.Child = new Ui.SymbolIcon { Symbol = c.Icon, FontSize = 20, Foreground = sel ? _p.AccentText : _p.AccentInk, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                stack.Children.Add(tile);
                stack.Children.Add(new TextBlock { Text = c.Name, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = _p.Text, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) });
                if (!string.IsNullOrEmpty(c.Hint)) stack.Children.Add(new TextBlock { Text = c.Hint, FontSize = 10.5, Foreground = _p.Dim, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 104 });
                body.Children.Add(stack);
                var b = new Border
                {
                    Background = _p.Card, BorderBrush = sel ? _p.Focus : _p.Border, BorderThickness = new Thickness(sel ? 2 : 1), CornerRadius = new CornerRadius(Tok.RCard),
                    Margin = new Thickness(3), Padding = new Thickness(sel ? 7 : 8, sel ? 5 : 6, sel ? 7 : 8, sel ? 7 : 8), Height = 88, Cursor = Cursors.Hand, Opacity = off ? 0.45 : 1, Child = body,
                    ToolTip = string.IsNullOrEmpty(c.Hint) ? c.Name : c.Name + " · " + c.Hint,
                };
                System.Windows.Automation.AutomationProperties.SetName(b, (i + 1) + " " + c.Name);
                System.Windows.Automation.AutomationProperties.SetHelpText(b, (off ? c.Unavailable() + "。" : "") + (c.Hint ?? ""));
                b.MouseEnter += (s, e) => Motion.To(b, _p.CardHoverColor);
                b.MouseLeave += (s, e) => Motion.To(b, _p.CardColor);
                Motion.Pressable(b);
                b.MouseLeftButtonUp += (s, e) => Activate(idx);
                grid.Children.Add(b);
            }
            root.Children.Add(grid);
            root.Children.Add(PanelParts.Hint(_p, "数字直接打开 · ↑↓←→ 回车 · Tab 换页 · Esc 关闭"));
            var shell = PanelParts.Shell(_p, root);
            System.Windows.Automation.AutomationProperties.SetName(shell, "功能面板 · " + PageNames[_page]);
            SwapContent(shell, animate);
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

        void Move(int page) { if (page == _page) return; _page = page; _sel = 0; Build(true); }

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
            bool shift = (System.Windows.Forms.Control.ModifierKeys & WinKeys.Shift) != 0;
            if (k == WinKeys.Tab && shift) { Move((_page + _pages.Length - 1) % _pages.Length); return true; }
            if (k == WinKeys.Tab || k == WinKeys.Next) { Move((_page + 1) % _pages.Length); return true; }
            if (k == WinKeys.Prior) { Move((_page + _pages.Length - 1) % _pages.Length); return true; }
            if (k == WinKeys.Right) { _sel = _sel + 1 < cells.Count ? _sel + 1 : 0; Build(); return true; }
            if (k == WinKeys.Left) { _sel = _sel > 0 ? _sel - 1 : cells.Count - 1; Build(); return true; }
            if (k == WinKeys.Down) { if (_sel + Cols < cells.Count) _sel += Cols; Build(); return true; }
            if (k == WinKeys.Up) { if (_sel >= Cols) _sel -= Cols; Build(); return true; }
            if (k == WinKeys.Enter || k == WinKeys.Space) { Activate(_sel); return true; }
            return true; // 面板打开时吞掉其它键，和符号面板一致
        }
    }
}
