using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    sealed partial class SettingsWindow
    {
        static KeyValuePair<T, string> KV<T>(T k, string v) => new KeyValuePair<T, string>(k, v);

        static Border Danger(string title, string desc, string button, Action act)
        {
            var b = new Ui.Button { Content = button, Appearance = Ui.ControlAppearance.Danger, MinWidth = 96 };
            b.Click += (s, e) => act();
            return K.Card(title, desc, b, Ui.SymbolRegular.Delete24);
        }

        // ================= 常规 =================

        StackPanel PageGeneral()
        {
            var p = new StackPanel();
            p.Children.Add(K.PageTitle("常规"));
            p.Children.Add(K.Card("中/英切换提示", "切换中英文时，在光标旁短暂显示「中」或「A」",
                K.Toggle(S.ImeToast, v => { S.ImeToast = v; Save(); }), Ui.SymbolRegular.Chat24));
            p.Children.Add(K.Card("从输入法状态检测切换（实验）", "部分应用里输入法不会主动通知时，靠轮询检测中英状态；可能有误报",
                K.Toggle(S.ImeToastPolling, v => { S.ImeToastPolling = v; Save(); }), Ui.SymbolRegular.Lightbulb24));
            p.Children.Add(K.Card("默认输出繁体", "简繁转换由 OpenCC 在本机完成，不需要联网",
                K.Toggle(S.Traditional, v => { S.Traditional = v; Save(true); }), Ui.SymbolRegular.Translate24));
            p.Children.Add(K.Card("在输入框里显示拼音", "内嵌编码：拼音直接显示在光标处，而不是候选窗上方",
                K.Toggle(S.InlinePreedit, v => { S.InlinePreedit = v; Save(true); }), Ui.SymbolRegular.Document24));
            p.Children.Add(K.Card("首次引导", "重新走一遍方案、外观、导入旧习惯和 AI 的设置向导",
                K.Btn("打开引导", () => Program.ShowWizard()), Ui.SymbolRegular.Home24));
            p.Children.Add(K.Card("重新部署", "手动修改 YAML 后让配置生效；平时修改设置会自动部署",
                K.Btn("重新部署", () => Rime.ApplySoon(50)), Ui.SymbolRegular.ArrowSync24));

            AddSyncSection(p);

            p.Children.Add(K.Section("危险操作"));
            p.Children.Add(Danger("重置设置", "所有设置恢复默认（常用语、统计和词库不受影响）", "重置…", () =>
            {
                if (!Dialogs.Confirm("把一维输入法的所有设置恢复为默认值？\n\n常用语、输入统计和用户词库不会被删除。AI 密钥会被清除。")) return;
                Settings.Reset();
                Rime.ApplySoon(50);
                UiTheme.Apply();
                Close();
                Program.ShowSettings();
            }));
            return p;
        }

        // ================= 输入方案 =================

        StackPanel PageSchema()
        {
            var p = new StackPanel();
            p.Children.Add(K.PageTitle("输入方案"));
            p.Children.Add(K.Text("选一个默认方案。其他方案仍可在输入时按 Ctrl+` 切换。", 13, true));
            var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new Thickness(0, 12, 0, 0) };
            var cards = new List<Border>();
            foreach (var sc in Rime.Schemas)
            {
                var id = sc.Id;
                var card = SchemaCard(sc, S.Schema == id);
                card.MouseLeftButtonUp += (s, e) =>
                {
                    S.Schema = id; Save(true);
                    foreach (var c in cards) MarkSelected(c, (string)((FrameworkElement)c.Child).Tag == id);
                    Toast("已切换到「" + sc.Name + "」，正在部署…");
                };
                cards.Add(card); grid.Children.Add(card);
            }
            var wrap = K.Block("默认方案", "全拼或双拼；双拼卡片上是键位图", grid, Ui.SymbolRegular.Keyboard24);
            p.Children.Add(wrap);
            p.Children.Add(K.Card("方案选单", "打开小狼毫方案选单，启用更多方案（如五笔、仓颉，需要另行安装）",
                K.Btn("打开", () => Dialogs.Open(Paths.Deployer)), Ui.SymbolRegular.AppsList24));
            return p;
        }

        Border SchemaCard(SchemaInfo sc, bool selected)
        {
            var sp = new StackPanel { Tag = sc.Id };
            sp.Children.Add(K.Text(sc.Name, 15, false, FontWeights.SemiBold));
            sp.Children.Add(K.Text(sc.Desc, 12, true));
            if (sc.Id != "rime_ice")
            {
                var kb = new KeyboardDiagram(sc.Id) { Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
                _themed.Add(kb);
                sp.Children.Add(kb);
            }
            else
            {
                var demo = K.Text("yi wei → 一维　　zhong wen → 中文", 12, true);
                demo.Margin = new Thickness(0, 10, 0, 0);
                sp.Children.Add(demo);
            }
            var b = K.CardBorder(sp, new Thickness(16));
            b.Margin = new Thickness(0, 0, 8, 8);
            b.Cursor = System.Windows.Input.Cursors.Hand;
            MarkSelected(b, selected);
            return b;
        }

        static void MarkSelected(Border b, bool on)
        {
            if (on) { b.BorderBrush = new SolidColorBrush(UiTheme.Accent); b.BorderThickness = new Thickness(2); }
            else { b.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush"); b.BorderThickness = new Thickness(1); }
        }

        // ================= 外观 =================

        CandidatePreview _preview;

        StackPanel PageAppearance()
        {
            var p = new StackPanel();
            p.Children.Add(K.PageTitle("外观"));

            _preview = new CandidatePreview { Margin = new Thickness(0, 12, 0, 0) };
            _themed.Add(_preview);
            void Preview()
            {
                _preview.Scheme = WeaselConfig.ActiveScheme(S);
                _preview.Horizontal = S.Horizontal;
                _preview.FontName = S.FontFace; _preview.FontPoint = S.FontPoint;
                _preview.Radius = S.CornerRadius; _preview.HRadius = S.HilitedCornerRadius;
                _preview.Refresh();
            }
            p.Children.Add(K.Block("预览", "候选窗的样子会随下面的设置立即变化", _preview, Ui.SymbolRegular.Window24));

            // accent
            var swatches = new StackPanel { Orientation = Orientation.Horizontal };
            var swList = new List<Border>();
            foreach (var b in Brand.Palette)
            {
                var id = b.Id;
                var sw = new Border
                {
                    Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Margin = new Thickness(0, 0, 8, 0), Cursor = System.Windows.Input.Cursors.Hand,
                    Background = new SolidColorBrush(Color.FromRgb(b.R, b.G, b.B)), ToolTip = b.Name, Tag = id,
                    BorderThickness = new Thickness(S.Accent == id ? 3 : 0),
                };
                sw.SetResourceReference(Border.BorderBrushProperty, "TextFillColorPrimaryBrush");
                sw.MouseLeftButtonUp += (s, e) =>
                {
                    S.Accent = id; Save();
                    foreach (var x in swList) x.BorderThickness = new Thickness((string)x.Tag == id ? 3 : 0);
                    UiTheme.Apply();
                };
                swList.Add(sw); swatches.Children.Add(sw);
            }
            p.Children.Add(K.Card("强调色", "设置窗口、面板和提示气泡使用的颜色", swatches, Ui.SymbolRegular.Color24));

            // scheme cards
            var schemeGrid = new WrapPanel();
            var schemeCards = new List<Border>();
            ComboBox other = null;
            void SelectScheme(string id)
            {
                S.ColorScheme = id; Save(true); Preview();
                foreach (var c in schemeCards) MarkSelected(c, (string)c.Tag == id);
            }
            foreach (var b in Brand.Palette)
            {
                var id = Brand.SchemeId(b.Id, false);
                var colors = Brand.SchemeArgb(b, false);
                var card = MiniScheme("一维 · " + b.Name, colors);
                card.Tag = id;
                MarkSelected(card, S.ColorScheme == id);
                card.MouseLeftButtonUp += (s, e) => { SelectScheme(id); if (other != null) other.SelectedIndex = -1; };
                schemeCards.Add(card); schemeGrid.Children.Add(card);
            }
            var cardSchemes = new Border { Child = schemeGrid };
            p.Children.Add(K.Block("候选窗配色", "五种一维配色，深色模式下自动换成对应的深色版", cardSchemes, Ui.SymbolRegular.PaintBrush24));

            var others = WeaselConfig.BuiltinSchemes().Where(kv => Brand.FromScheme(kv.Key) == null).ToList();
            other = K.Combo(others, S.ColorScheme, id => { if (id != null) SelectScheme(id); });
            p.Children.Add(K.Card("更多配色", "经典主题、导入的主题和「我的配色」", other, Ui.SymbolRegular.Library24));

            var darkCombo = K.Combo(WeaselConfig.BuiltinSchemes(), S.ColorSchemeDark, id => { S.ColorSchemeDark = id; Save(true); Preview(); });
            darkCombo.IsEnabled = !S.FollowSystemDark || Brand.FromScheme(S.ColorScheme) == null;
            p.Children.Add(K.Card("跟随系统深浅色", "Windows 切换到深色模式时，自动使用所选配色的深色版",
                K.Toggle(S.FollowSystemDark, v => { S.FollowSystemDark = v; darkCombo.IsEnabled = !v || Brand.FromScheme(S.ColorScheme) == null; Save(true); Preview(); }), Ui.SymbolRegular.Lightbulb24));
            p.Children.Add(K.Card("深色模式配色", "不跟随一维配色时，深色模式下使用的配色", darkCombo));

            p.Children.Add(K.Section("排列与字体"));
            p.Children.Add(K.Card("候选排列", "横排一行，或竖排列表",
                K.Segmented(new[] { KV(true, "横排"), KV(false, "竖排") }, S.Horizontal, v => { S.Horizontal = v; S.VerticalText = false; Save(true); Preview(); }), Ui.SymbolRegular.Window24));
            p.Children.Add(K.Card("候选个数", "每页显示几个候选",
                K.Segmented(new[] { KV(5, "5"), KV(7, "7"), KV(9, "9") }, S.CandidateCount, v => { S.CandidateCount = v; Save(true); })));

            var fonts = new ComboBox { MinWidth = 240, IsEditable = true, Text = S.FontFace };
            try
            {
                foreach (var f in Fonts.SystemFontFamilies.Select(f => f.FamilyNames.TryGetValue(System.Windows.Markup.XmlLanguage.GetLanguage("zh-cn"), out var zh) ? zh : f.Source).OrderBy(x => x))
                    fonts.Items.Add(f);
            }
            catch { }
            fonts.Text = S.FontFace;
            fonts.SelectionChanged += (s, e) => { if (fonts.SelectedItem is string f) { S.FontFace = f; Save(true); Preview(); } };
            fonts.LostFocus += (s, e) => { if (!string.IsNullOrWhiteSpace(fonts.Text) && fonts.Text != S.FontFace) { S.FontFace = fonts.Text.Trim(); Save(true); Preview(); } };
            p.Children.Add(K.Card("字体", "候选窗使用的字体", fonts, Ui.SymbolRegular.TextGrammarWand24));
            p.Children.Add(K.Card("字号", "候选文字大小（磅）", Slider(10, 32, S.FontPoint, v => { S.FontPoint = v; Save(true); Preview(); })));
            p.Children.Add(K.Card("候选窗圆角", "窗口四角的圆角半径，默认 8", Slider(0, 20, S.CornerRadius, v => { S.CornerRadius = v; Save(true); Preview(); })));
            p.Children.Add(K.Card("高亮圆角", "选中候选背景的圆角半径", Slider(0, 16, S.HilitedCornerRadius, v => { S.HilitedCornerRadius = v; Save(true); Preview(); })));

            p.Children.Add(K.Section("主题"));
            p.Children.Add(K.Card("我的配色", "以当前配色为基础，逐项挑选背景、文字和高亮颜色",
                K.Btn("编辑…", () => { EditCustomTheme(); Preview(); }), Ui.SymbolRegular.Color24));
            var link = new Ui.TextBox { PlaceholderText = "粘贴 yiwei-ime://theme?… 链接", MinWidth = 320 };
            p.Children.Add(K.Card("导入主题链接", "与 AIME 主题包格式相同；aime-ime:// 链接也可以",
                K.Row(link, K.Btn("导入", () =>
                {
                    var t = link.Text.Trim(); if (t.StartsWith("aime-ime://")) t = "yiwei-ime://" + t.Substring(11);
                    if (t.Length > 0) { ThemeLinks.Handle(t); Toast("重新打开设置后，可在「更多配色」里找到导入的主题"); }
                })), Ui.SymbolRegular.ArrowDownload24));

            Loaded += (s, e) => Preview();
            UiTheme.Changed += Preview;
            Closed += (s, e) => UiTheme.Changed -= Preview;
            Preview();
            return p;
        }

        static Border MiniScheme(string name, Dictionary<string, uint> c)
        {
            Brush B(string k) => new SolidColorBrush(UiTheme.Rgb(c[k] | 0xFF000000));
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            row.Children.Add(new Border { Background = B("hilited_candidate_back_color"), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), Child = new TextBlock { Text = "1 一维", Foreground = B("hilited_candidate_text_color"), FontSize = 12 } });
            row.Children.Add(new TextBlock { Text = "  2 依偎", Foreground = B("candidate_text_color"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            var inner = new StackPanel();
            inner.Children.Add(new TextBlock { Text = "yi'wei", Foreground = B("text_color"), FontSize = 11 });
            inner.Children.Add(row);
            var sample = new Border { Background = B("back_color"), BorderBrush = B("border_color"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 6, 8, 6), Child = inner };
            var sp = new StackPanel();
            sp.Children.Add(sample);
            var label = K.Text(name, 12); label.Margin = new Thickness(2, 6, 0, 0);
            sp.Children.Add(label);
            var card = K.CardBorder(sp, new Thickness(10));
            card.Margin = new Thickness(0, 0, 8, 8);
            card.Width = 150;
            card.Cursor = System.Windows.Input.Cursors.Hand;
            return card;
        }

        static StackPanel Slider(int min, int max, int value, Action<int> changed)
        {
            var label = K.Text(value.ToString(), 13); label.Width = 28; label.TextAlignment = TextAlignment.Right; label.VerticalAlignment = VerticalAlignment.Center;
            var sl = new Slider { Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, value)), Width = 200, IsSnapToTickEnabled = true, TickFrequency = 1, VerticalAlignment = VerticalAlignment.Center };
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            timer.Tick += (s, e) => { timer.Stop(); changed((int)sl.Value); };
            sl.ValueChanged += (s, e) => { label.Text = ((int)sl.Value).ToString(); timer.Stop(); timer.Start(); };
            var row = K.Row(sl, label);
            return row;
        }

        void EditCustomTheme()
        {
            var baseColors = WeaselConfig.SchemeColors(WeaselConfig.ActiveScheme(S));
            uint Get(string k, uint d) => baseColors.TryGetValue(k, out var v) ? v : d;
            string Hex(uint v) => "0x" + v.ToString("X8");
            var pairs = new[] { ("back_color", "背景"), ("candidate_text_color", "候选文字"), ("hilited_candidate_back_color", "高亮背景"), ("hilited_candidate_text_color", "高亮文字"), ("comment_text_color", "注释") };
            var theme = S.CustomTheme != null ? new Dictionary<string, string>(S.CustomTheme) : new Dictionary<string, string>
            {
                ["name"] = "我的配色", ["author"] = Environment.UserName,
                ["back_color"] = Hex(Get("back_color", 0xFFFFFFFF)), ["border_color"] = Hex(Get("border_color", 0x14000000)),
                ["text_color"] = Hex(Get("text_color", 0xFF555555)), ["hilited_text_color"] = Hex(Get("hilited_text_color", 0xFF222222)),
                ["hilited_back_color"] = Hex(Get("hilited_back_color", 0x14000000)), ["candidate_text_color"] = Hex(Get("candidate_text_color", 0xFF222222)),
                ["comment_text_color"] = Hex(Get("comment_text_color", 0xFF777777)), ["label_color"] = Hex(Get("label_color", 0xFF888888)),
                ["hilited_candidate_back_color"] = Hex(Get("hilited_candidate_back_color", 0xFF0F9D8A)), ["hilited_candidate_text_color"] = Hex(Get("hilited_candidate_text_color", 0xFFFFFFFF)),
                ["hilited_comment_text_color"] = Hex(Get("hilited_comment_text_color", 0xE6FFFFFF)), ["hilited_label_color"] = Hex(Get("hilited_label_color", 0xE6FFFFFF)),
            };
            foreach (var (key, label) in pairs)
            {
                WeaselConfig.TryColor(theme.TryGetValue(key, out var cv) ? cv : "0xFF000000", out var cur);
                using (var dlg = new System.Windows.Forms.ColorDialog { Color = System.Drawing.Color.FromArgb(unchecked((int)(cur | 0xFF000000))), FullOpen = true })
                {
                    Toast("选择「" + label + "」的颜色");
                    if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    uint alpha = key == "back_color" ? 0xF5000000u : 0xFF000000u;
                    theme[key] = Hex(alpha | (uint)(dlg.Color.ToArgb() & 0xFFFFFF));
                }
            }
            S.CustomTheme = theme;
            S.ColorScheme = "yiwei_custom";
            Save(true);
            Toast("「我的配色」已启用，正在部署…");
        }

        // ================= 快捷键 =================

        StackPanel PageHotkeys()
        {
            var p = new StackPanel();
            p.Children.Add(K.PageTitle("快捷键"));
            p.Children.Add(K.Card("长按 Alt 手势", "总开关。关闭后长按 Alt 不会打开任何面板；Alt+Tab 等系统组合键始终不受影响",
                K.Toggle(S.AltHotkeys, v => { S.AltHotkeys = v; Save(); }), Ui.SymbolRegular.KeyboardShift24));
            p.Children.Add(K.Card("长按 Alt + 1–9 打开常用语", "数字对应分组；面板里按数字直接上屏",
                K.Toggle(S.SnippetsHotkey, v => { S.SnippetsHotkey = v; Save(); }), Ui.SymbolRegular.TextBulletListSquare24));
            p.Children.Add(K.Card("长按 Alt + 空格 打开 AI", "先选中文字；翻译、润色、粤语或自定义",
                K.Toggle(S.AiHotkey, v => { S.AiHotkey = v; Save(); }), Ui.SymbolRegular.Sparkle24));
            p.Children.Add(K.Card("长按判定时间", "按住 Alt 多久之后再按数字或空格才算手势（毫秒）",
                Slider(150, 1000, S.HoldMs, v => { S.HoldMs = (v / 50) * 50; Save(); })));
            p.Children.Add(K.Card("在全屏窗口里停用", "独占全屏的游戏和演示总是自动停用；开启后无边框全屏窗口也停用",
                K.Toggle(S.AltSkipBorderlessFullscreen, v => { S.AltSkipBorderlessFullscreen = v; Save(); }), Ui.SymbolRegular.Window24));

            var list = new ListBox { MinHeight = 120, MaxHeight = 240, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
            void Fill()
            {
                list.Items.Clear();
                foreach (var exe in S.AltBlocklist.OrderBy(x => x))
                {
                    var name = exe;
                    var row = new DockPanel { LastChildFill = true, Width = 520 };
                    var del = new Ui.Button { Content = "移除", Appearance = Ui.ControlAppearance.Transparent, Padding = new Thickness(8, 2, 8, 2) };
                    del.Click += (s, e) => { S.AltBlocklist.RemoveAll(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)); Save(); Fill(); };
                    DockPanel.SetDock(del, Dock.Right);
                    row.Children.Add(del);
                    row.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
                    list.Items.Add(row);
                }
            }
            Fill();
            var add = new Ui.TextBox { PlaceholderText = "例如 game.exe", MinWidth = 220 };
            void AddExe()
            {
                var n = add.Text.Trim().ToLowerInvariant(); if (n.Length == 0) return; if (!n.EndsWith(".exe")) n += ".exe";
                if (!S.AltBlocklist.Any(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase))) S.AltBlocklist.Add(n);
                add.Text = ""; Save(); Fill();
            }
            add.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) AddExe(); };
            var body = new StackPanel();
            body.Children.Add(list);
            var addRow = K.Row(add, K.Btn("添加", AddExe), K.Btn("恢复默认", () => { S.AltBlocklist = Settings.DefaultBlocklist(); Save(); Fill(); }));
            addRow.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(addRow);
            p.Children.Add(K.Block("不响应 Alt 手势的应用", "远程桌面、虚拟机和游戏平台默认在内，避免抢走它们的 Alt 组合键", body, Ui.SymbolRegular.Shield24));

            p.Children.Add(K.Section("速查"));
            p.Children.Add(K.CardBorder(CheatSheet()));
            return p;
        }

        public static Grid CheatSheet()
        {
            var rows = new[]
            {
                ("Shift", "切换中 / 英文"),
                ("长按 Alt + 1–9", "常用语面板：数字上屏，Tab 换分组"),
                ("选中文字，长按 Alt + 空格", "AI：翻译 / 润色 / 粤语 / 自定义"),
                ("Ctrl + `", "方案选单：简繁、全半角、双拼…"),
                ("- / =  或  , / .", "候选翻页"),
                ("rq · sj · xq", "日期 · 时间 · 星期"),
                ("V 开头", "计算器与符号"),
            };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < rows.Length; i++)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var key = K.Text(rows[i].Item1, 13, false, FontWeights.SemiBold); key.Margin = new Thickness(0, 4, 0, 4);
                var val = K.Text(rows[i].Item2, 13, true); val.Margin = new Thickness(0, 4, 0, 4);
                Grid.SetRow(key, i); Grid.SetRow(val, i); Grid.SetColumn(val, 1);
                g.Children.Add(key); g.Children.Add(val);
            }
            return g;
        }

        // ================= 应用规则 =================

        StackPanel PageApps()
        {
            var p = new StackPanel();
            p.Children.Add(K.PageTitle("应用规则"));
            p.Children.Add(K.Card("所有应用共用中英文状态", "关闭时，每个应用各自记住自己的中英文状态",
                K.Toggle(S.GlobalAscii, v => { S.GlobalAscii = v; Save(true); }), Ui.SymbolRegular.Apps24));

            p.Children.Add(AppModesBlock());
            return p;
        }

        // ================= 关于 =================

        StackPanel PageAbout()
        {
            var p = new StackPanel();
            p.Children.Add(K.PageTitle("关于"));
            var ver = typeof(SettingsWindow).Assembly.GetName().Version;
            p.Children.Add(K.Card("一维输入法 " + ver.ToString(3), "中文常新，自在表达。开源，注重隐私，基于 RIME。", K.Btn("检查更新", CheckAppUpdate, Ui.ControlAppearance.Primary), Ui.SymbolRegular.Info24));
            p.Children.Add(K.Card("打开用户文件夹", Paths.UserDir, K.Btn("打开", () => { Directory.CreateDirectory(Paths.UserDir); Dialogs.Open(Paths.UserDir); }), Ui.SymbolRegular.FolderOpen24));
            p.Children.Add(K.Card("编辑 YAML", "用记事本打开 default.custom.yaml（高级；一维生成的文件会被设置覆盖）", K.Btn("编辑", EditYaml), Ui.SymbolRegular.DocumentEdit24));
            p.Children.Add(K.Card("项目主页", "源代码、问题反馈与更新日志", K.Row(
                K.Btn("主页", () => Dialogs.Open("https://github.com/yiweishurufa/yiwei-ime")),
                K.Btn("反馈", () => Dialogs.Open("https://github.com/yiweishurufa/yiwei-ime/issues"))), Ui.SymbolRegular.Home24));

            p.Children.Add(K.Section("开源许可"));
            var lic = new StackPanel();
            foreach (var (name, license, note) in new[]
            {
                ("小狼毫 Weasel", "GPL-3.0", "Windows 输入法前端，一维输入法在其基础上修改"),
                ("librime", "BSD-3-Clause", "RIME 输入法引擎"),
                ("雾凇拼音 rime-ice", "GPL-3.0", "默认方案与词库"),
                ("OpenCC", "Apache-2.0", "简繁转换"),
                ("AIME 艾么输入法", "MIT · © 2026 ZOOL LLC", "产品设计、配色与科技词库的来源"),
                ("WPF-UI (lepoco/wpfui)", "MIT", "设置界面控件"),
                ("Costura.Fody", "MIT", "把依赖合并进单个程序文件"),
            })
            {
                var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
                var l = K.Text(license, 12, true); DockPanel.SetDock(l, Dock.Right);
                row.Children.Add(l);
                var t = new StackPanel(); t.Children.Add(K.Text(name, 13, false, FontWeights.SemiBold)); t.Children.Add(K.Text(note, 12, true));
                row.Children.Add(t);
                lic.Children.Add(row);
            }
            var footer = K.Text("一维输入法整体以 GPL-3.0 发布。", 12, true); footer.Margin = new Thickness(0, 8, 0, 0);
            lic.Children.Add(footer);
            var lb = K.CardBorder(lic);
            lb.Tag = "开源许可\nlicenses GPL BSD Apache MIT 许可证";
            p.Children.Add(lb);
            return p;
        }

        void EditYaml()
        {
            try
            {
                var path = Paths.DefaultCustom;
                if (!File.Exists(path)) Rime.WriteDefaultCustom(S);
                Process.Start(new ProcessStartInfo("notepad.exe", "\"" + path + "\"") { UseShellExecute = true });
                Toast("改完保存后，在托盘菜单里点「重新部署」");
            }
            catch (Exception e) { Dialogs.Error(e.Message); }
        }

        async void CheckAppUpdate()
        {
            Toast("正在检查更新…");
            try
            {
                var latest = await System.Threading.Tasks.Task.Run(() =>
                {
                    System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
                    using (var wc = new System.Net.WebClient { Encoding = System.Text.Encoding.UTF8 })
                    {
                        wc.Headers[System.Net.HttpRequestHeader.UserAgent] = "YiweiIME/0.2";
                        var rel = Json.Parse(wc.DownloadString("https://api.github.com/repos/yiweishurufa/yiwei-ime/releases/latest")) as Dictionary<string, object>;
                        return rel?["tag_name"] as string ?? "";
                    }
                });
                var cur = typeof(SettingsWindow).Assembly.GetName().Version;
                if (Version.TryParse(latest.TrimStart('v', 'V'), out var v) && v > cur)
                {
                    if (Dialogs.Confirm("有新版本 " + latest + "（当前 " + cur.ToString(3) + "）。打开下载页面？", "检查更新"))
                        Dialogs.Open("https://github.com/yiweishurufa/yiwei-ime/releases/latest");
                }
                else Toast("已是最新版本");
            }
            catch (Exception e) { Toast("检查失败：" + e.GetBaseException().Message); }
        }
    }
}
