using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    /// <summary>Fluent theme for the helper's WPF windows: follows Windows light/dark, accent from the brand palette.</summary>
    static class UiTheme
    {
        public static event Action Changed;
        public static bool Dark { get; private set; }
        static ThemesDictionary _themes;

        public static void Init(Application app)
        {
            Dark = SystemTheme.IsDark;
            try
            {
                _themes = new ThemesDictionary { Theme = Dark ? ApplicationTheme.Dark : ApplicationTheme.Light };
                app.Resources.MergedDictionaries.Add(_themes);
                app.Resources.MergedDictionaries.Add(new ControlsDictionary());
            }
            catch (Exception e) { Log.Write("wpf-ui resources: " + e); }
            Apply();
            SystemTheme.Changed += _ => Apply();
        }

        public static Color Accent
        {
            get
            {
                var b = Brand.Current;
                var c = Color.FromRgb(b.R, b.G, b.B);
                if (Dark && b.Id == "shimo") c = Color.FromRgb(0x8A, 0x93, 0xA3); // graphite on a dark window
                return c;
            }
        }

        public static Color Rgb(uint argb) => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

        public static void Apply()
        {
            Dark = SystemTheme.IsDark;
            var t = Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
            try { ApplicationThemeManager.Apply(t, Ui.WindowBackdropType.None, false); } catch (Exception e) { Log.Write("theme: " + e.Message); }
            try { ApplicationAccentColorManager.Apply(Accent, t, false); } catch (Exception e) { Log.Write("accent: " + e.Message); }
            RefreshAccentBrushes();
            try { K.RepaintTiles(); } catch (Exception e) { Log.Write("tiles: " + e.Message); }
            var app = Application.Current;
            if (app != null)
                foreach (Window w in app.Windows)
                    if (w is Ui.FluentWindow fw && fw.IsLoaded)
                        try { WindowBackgroundManager.UpdateBackground(fw, t, fw.WindowBackdropType); } catch { }
            Changed?.Invoke();
        }

        // WPF-UI 3.0.5 的主题字典里，主按钮、开关、勾选框等画刷写成 <SolidColorBrush Color="{DynamicResource SystemAccentColorPrimary}"/>，
        // 字典里画刷的 DynamicResource 不会跟着强调色更新（会停在上一次的颜色）：引导页换了青碧，下一步/横排按钮还是朱砂红（用户 2026-10-19 反馈）。
        // 每次换强调色后，用新的颜色重建这些画刷，放在 Application.Resources 顶层覆盖主题字典里的旧画刷。
        static readonly (string Brush, string Color)[] AccentBrushKeys =
        {
            ("AccentButtonBackground", "SystemAccentColorPrimary"), ("AccentButtonBackgroundPointerOver", "SystemAccentColorSecondary"), ("AccentButtonBackgroundPressed", "SystemAccentColorTertiary"), ("CalendarViewSelectedBackground", "SystemAccentColorPrimary"), ("CalendarViewSelectedBorderBrush", "SystemAccentColorPrimary"), ("CalendarViewTodayBackground", "SystemAccentColorPrimary"), ("CheckBoxCheckBackgroundFillChecked", "SystemAccentColorPrimary"), ("CheckBoxCheckBackgroundFillCheckedPointerOver", "SystemAccentColorSecondary"), ("ComboBoxBorderBrushFocused", "SystemAccentColorSecondary"), ("ComboBoxItemPillFillBrush", "SystemAccentColorPrimary"), ("HyperlinkButtonForeground", "SystemAccentColorPrimary"), ("HyperlinkButtonForegroundPointerOver", "SystemAccentColorSecondary"), ("HyperlinkButtonForegroundPressed", "SystemAccentColorTertiary"), ("InfoBarInformationalSeverityIconBackground", "SystemAccentColorPrimary"), ("ListBoxItemSelectedBackgroundThemeBrush", "SystemAccentColorPrimary"), ("ListViewItemPillFillBrush", "SystemAccentColorPrimary"), ("NavigationViewSelectionIndicatorForeground", "SystemAccentColorPrimary"), ("ProgressBarForeground", "SystemAccentColorPrimary"), ("ProgressRingForegroundThemeBrush", "SystemAccentColorPrimary"), ("RadioButtonOuterEllipseCheckedStroke", "SystemAccentColorPrimary"), ("RadioButtonOuterEllipseCheckedStrokePointerOver", "SystemAccentColorSecondary"), ("RatingControlSelectedForeground", "SystemAccentColorPrimary"), ("SliderThumbBackground", "SystemAccentColorPrimary"), ("SliderThumbBackgroundPointerOver", "SystemAccentColorSecondary"), ("TextControlFocusedBorderBrush", "SystemAccentColorPrimary"), ("ThumbRateForeground", "SystemAccentColorPrimary"), ("ToggleButtonBackgroundChecked", "SystemAccentColorPrimary"), ("ToggleButtonBackgroundCheckedPressed", "SystemAccentColorTertiary"), ("ToggleButtonForegroundCheckedPointerOver", "SystemAccentColorSecondary"), ("ToggleSwitchFillOn", "SystemAccentColorPrimary"), ("ToggleSwitchFillOnPointerOver", "SystemAccentColorSecondary"), ("ToggleSwitchStrokeOn", "SystemAccentColorPrimary"), ("ToggleSwitchStrokeOnPointerOver", "SystemAccentColorSecondary"), ("TreeViewItemSelectionIndicatorForeground", "SystemAccentColorPrimary")
        };

        static void RefreshAccentBrushes()
        {
            var app = Application.Current;
            if (app == null) return;
            foreach (var (brush, color) in AccentBrushKeys)
            {
                try
                {
                    if (app.TryFindResource(color) is Color c) { var b = new SolidColorBrush(c); b.Freeze(); app.Resources[brush] = b; }
                }
                catch (Exception e) { Log.Write("accent brush " + brush + ": " + e.Message); }
            }
        }

        /// <summary>Backdrop to request: Mica on Windows 11, plain background elsewhere.</summary>
        public static Ui.WindowBackdropType Backdrop => SystemTheme.IsWindows11 ? Ui.WindowBackdropType.Mica : Ui.WindowBackdropType.None;
    }

    /// <summary>Small builders for the settings and wizard pages (v2「澄澈」, docs/design/DESIGN-v2.md §7).</summary>
    static class K
    {
        public static readonly FontFamily Font = Tok.Text;

        /// <summary>每个设置页的图标和一句「这一页管什么」（页面头用；名字与 SettingsWindow 的页名一致）。</summary>
        public static readonly Dictionary<string, (Ui.SymbolRegular Icon, string Desc)> PageInfo = new Dictionary<string, (Ui.SymbolRegular, string)>
        {
            ["常规"] = (Ui.SymbolRegular.Settings24, "输入习惯、隐私、提示和任务栏图标"),
            ["输入方案"] = (Ui.SymbolRegular.Keyboard24, "全拼、双拼、笔画，以及模糊音、纠错、屏蔽词"),
            ["快捷输入"] = (Ui.SymbolRegular.Flash24, "计算器、日期时间、Unicode、以词定字等 v 键和快捷编码"),
            ["外观"] = (Ui.SymbolRegular.PaintBrush24, "候选窗配色、强调色、排列和字体"),
            ["快捷键"] = (Ui.SymbolRegular.KeyboardShift24, "长按 Alt 的面板、反悔重选和各类功能热键"),
            ["语音"] = (Ui.SymbolRegular.Mic24, "按住说话转文字的识别方式和按键"),
            ["常用语"] = (Ui.SymbolRegular.TextBulletListSquare24, "Alt + 数字一键上屏的短语和分组"),
            ["AI"] = (Ui.SymbolRegular.Sparkle24, "只在按快捷键时调用的 AI 帮写，接口和密钥只存本机"),
            ["词库"] = (Ui.SymbolRegular.Library24, "雾凇词库更新、分类词库、语法模型和我的词条"),
            ["工具"] = (Ui.SymbolRegular.Toolbox24, "词库体检、性能体检、备份导入和诊断"),
            ["应用规则"] = (Ui.SymbolRegular.AppsList24, "在哪些程序里默认英文、关掉快捷键"),
            ["统计"] = (Ui.SymbolRegular.DataTrending24, "只存本机的输入统计：字数、速度、常用词"),
            ["关于"] = (Ui.SymbolRegular.Info24, "版本、更新内容、开源许可与隐私说明"),
        };

        public static T Res<T>(T e, DependencyProperty p, string key) where T : FrameworkElement { e.SetResourceReference(p, key); return e; }

        public static TextBlock Text(string t, double size = Tok.Body, bool secondary = false, FontWeight? weight = null)
        {
            var tb = new TextBlock { Text = t, FontSize = size, TextWrapping = TextWrapping.Wrap, FontWeight = weight ?? FontWeights.Normal };
            tb.SetResourceReference(TextBlock.ForegroundProperty, secondary ? "TextFillColorSecondaryBrush" : "TextFillColorPrimaryBrush");
            return tb;
        }

        /// <summary>36×36 的浅强调色圆角瓦片，里面一个线性图标（设置项、页面头、空状态）。</summary>
        public static Border IconTile(Ui.SymbolRegular icon, double size = 36)
        {
            var glyph = new Ui.SymbolIcon { Symbol = icon, FontSize = Math.Round(size * 0.53), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Tok.TileGlyphBrush };
            var tile = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(Tok.RCard), Background = Tok.TileBrush, Child = glyph, VerticalAlignment = VerticalAlignment.Center };
            Tiles.Add(new WeakReference<Border>(tile));
            return tile;
        }

        static readonly List<WeakReference<Border>> Tiles = new List<WeakReference<Border>>();

        /// <summary>强调色 / 深浅色变了：重刷图标瓦片的颜色（瓦片用的是计算出的颜色，不是主题资源）。</summary>
        public static void RepaintTiles()
        {
            Tiles.RemoveAll(w => !w.TryGetTarget(out _));
            foreach (var w in Tiles)
                if (w.TryGetTarget(out var t))
                {
                    t.Background = Tok.TileBrush;
                    if (t.Child is Ui.SymbolIcon g) g.Foreground = Tok.TileGlyphBrush;
                }
        }

        /// <summary>页面头：图标瓦片 + 28px 标题 + 一句这一页管什么。</summary>
        public static FrameworkElement PageTitle(string t)
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, Tok.S24) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            PageInfo.TryGetValue(t, out var info);
            if (info.Icon != Ui.SymbolRegular.Empty)
            {
                var tile = IconTile(info.Icon, 48);
                tile.Margin = new Thickness(0, 0, Tok.S16, 0);
                g.Children.Add(tile);
            }
            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var title = Text(t, Tok.Display28, false, FontWeights.SemiBold);
            title.FontFamily = Tok.Display;
            texts.Children.Add(title);
            if (!string.IsNullOrEmpty(info.Desc)) { var d = Text(info.Desc, Tok.Body, true); d.Margin = new Thickness(0, 2, 0, 0); texts.Children.Add(d); }
            Grid.SetColumn(texts, 1); g.Children.Add(texts);
            return g;
        }

        /// <summary>分组标题（14px SemiBold），可带一句说明。</summary>
        public static FrameworkElement Section(string t, string desc)
        {
            var sp = new StackPanel { Margin = new Thickness(2, Tok.S24, 0, Tok.S8) };
            var h = Text(t, Tok.Body, false, FontWeights.SemiBold);
            sp.Children.Add(h);
            if (!string.IsNullOrEmpty(desc)) { var d = Text(desc, Tok.Caption, true); d.Margin = new Thickness(0, 2, 0, 0); sp.Children.Add(d); }
            return sp;
        }

        public static TextBlock Section(string t)
        {
            var x = Text(t, Tok.Body, false, FontWeights.SemiBold);
            x.Margin = new Thickness(2, Tok.S24, 0, Tok.S8);
            return x;
        }

        public static Border CardBorder(UIElement child, Thickness? pad = null)
        {
            var b = new Border
            {
                CornerRadius = new CornerRadius(Tok.RCard), BorderThickness = new Thickness(1), Padding = pad ?? new Thickness(Tok.S16, Tok.S12, Tok.S16, Tok.S12),
                Margin = new Thickness(0, 0, 0, Tok.S4), Child = child,
            };
            b.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
            b.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
            return b;
        }

        /// <summary>卡片悬停时换成次级卡片底（和 Windows 11 设置一样，不做位移）。</summary>
        static void Hoverable(Border b)
        {
            b.MouseEnter += (s, e) => b.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorSecondaryBrush");
            b.MouseLeave += (s, e) => b.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        }

        /// <summary>A setting row: icon tile, title, one-line grey description, control on the right.</summary>
        public static Border Card(string title, string desc, UIElement right, Ui.SymbolRegular icon = Ui.SymbolRegular.Empty)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (icon != Ui.SymbolRegular.Empty)
            {
                var tile = IconTile(icon);
                tile.Margin = new Thickness(0, 0, Tok.S16, 0);
                g.Children.Add(tile);
            }
            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            texts.Children.Add(Text(title, Tok.Body));
            if (!string.IsNullOrEmpty(desc)) { var d = Text(desc, Tok.Caption, true); d.Margin = new Thickness(0, 2, 0, 0); texts.Children.Add(d); }
            Grid.SetColumn(texts, 1); g.Children.Add(texts);
            if (right != null)
            {
                if (right is FrameworkElement fe)
                {
                    fe.VerticalAlignment = VerticalAlignment.Center; fe.Margin = new Thickness(Tok.S16, 0, 0, 0);
                    if (string.IsNullOrEmpty(AutomationProperties.GetName(fe))) AutomationProperties.SetName(fe, title);
                    if (!string.IsNullOrEmpty(desc) && string.IsNullOrEmpty(AutomationProperties.GetHelpText(fe))) AutomationProperties.SetHelpText(fe, desc);
                }
                Grid.SetColumn(right, 2); g.Children.Add(right);
            }
            var card = CardBorder(g);
            card.MinHeight = 68;
            card.Tag = title + "\n" + desc;
            Hoverable(card);
            return card;
        }

        /// <summary>A card with content underneath the title (lists, previews).</summary>
        public static Border Block(string title, string desc, UIElement body, Ui.SymbolRegular icon = Ui.SymbolRegular.Empty)
        {
            var sp = new StackPanel();
            var head = Card(title, desc, null, icon).Child as Grid;
            ((Border)head.Parent).Child = null;
            sp.Children.Add(head);
            if (body is FrameworkElement fe && fe.Margin == default(Thickness)) fe.Margin = new Thickness(0, Tok.S12, 0, 0);
            sp.Children.Add(body);
            var card = CardBorder(sp);
            card.Tag = title + "\n" + desc;
            return card;
        }

        /// <summary>空状态：大号线性图标 + 一句话 + 一句引导 + 可选操作（不用彩色圆底）。</summary>
        public static FrameworkElement EmptyState(Ui.SymbolRegular icon, string title, string hint, UIElement action = null)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, Tok.S24, 0, Tok.S24), MaxWidth = 420 };
            var ic = new Ui.SymbolIcon { Symbol = icon, FontSize = 40, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, Tok.S12) };
            ic.SetResourceReference(Control.ForegroundProperty, "TextFillColorTertiaryBrush");
            sp.Children.Add(ic);
            var t = Text(title, Tok.Subtitle, false, FontWeights.SemiBold); t.TextAlignment = TextAlignment.Center; sp.Children.Add(t);
            if (!string.IsNullOrEmpty(hint)) { var h = Text(hint, Tok.Body, true); h.TextAlignment = TextAlignment.Center; h.Margin = new Thickness(0, Tok.S4, 0, 0); sp.Children.Add(h); }
            if (action is FrameworkElement fa) { fa.HorizontalAlignment = HorizontalAlignment.Center; fa.Margin = new Thickness(0, Tok.S16, 0, 0); sp.Children.Add(fa); }
            return sp;
        }

        /// <summary>加载中：小号环形进度 + 文字。</summary>
        public static FrameworkElement Loading(string text)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, Tok.S8, 0, Tok.S8) };
            sp.Children.Add(new Ui.ProgressRing { IsIndeterminate = true, Width = 18, Height = 18, Margin = new Thickness(0, 0, Tok.S12, 0), VerticalAlignment = VerticalAlignment.Center });
            var t = Text(text, Tok.Body, true); t.VerticalAlignment = VerticalAlignment.Center; sp.Children.Add(t);
            AutomationProperties.SetName(sp, text);
            AutomationProperties.SetLiveSetting(sp, AutomationLiveSetting.Polite);
            return sp;
        }

        /// <summary>出错：警示图标 + 原因 + 可选「重试」。</summary>
        public static FrameworkElement ErrorState(string reason, Action retry = null)
        {
            var g = new Grid { Margin = new Thickness(0, Tok.S8, 0, Tok.S8) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var ic = new Ui.SymbolIcon { Symbol = Ui.SymbolRegular.ErrorCircle24, FontSize = 20, Margin = new Thickness(0, 0, Tok.S12, 0), VerticalAlignment = VerticalAlignment.Center };
            ic.SetResourceReference(Control.ForegroundProperty, "SystemFillColorCriticalBrush");
            g.Children.Add(ic);
            var t = Text(reason, Tok.Body); t.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(t, 1); g.Children.Add(t);
            if (retry != null) { var b = Btn("重试", retry); b.Margin = new Thickness(Tok.S12, 0, 0, 0); Grid.SetColumn(b, 2); g.Children.Add(b); }
            AutomationProperties.SetLiveSetting(g, AutomationLiveSetting.Assertive);
            return g;
        }

        public static Ui.ToggleSwitch Toggle(bool value, Action<bool> changed)
        {
            var t = new Ui.ToggleSwitch { IsChecked = value, OnContent = "开", OffContent = "关" };
            t.Checked += (s, e) => changed(true);
            t.Unchecked += (s, e) => changed(false);
            return t;
        }

        public static Ui.Button Btn(string text, Action click, Ui.ControlAppearance look = Ui.ControlAppearance.Secondary, Ui.SymbolRegular icon = Ui.SymbolRegular.Empty)
        {
            var b = new Ui.Button { Content = text, Appearance = look, Margin = new Thickness(0, 0, 8, 0), MinWidth = 88 };
            if (icon != Ui.SymbolRegular.Empty) b.Icon = new Ui.SymbolIcon { Symbol = icon };
            b.Click += (s, e) => { try { click(); } catch (Exception ex) { Log.Write("button: " + ex); Dialogs.Error(ex.Message); } };
            return b;
        }

        public static ComboBox Combo<T>(IEnumerable<KeyValuePair<T, string>> items, T selected, Action<T> changed, double width = 220)
        {
            var c = new ComboBox { MinWidth = width };
            foreach (var kv in items) { var it = new ComboBoxItem { Content = kv.Value, Tag = kv.Key }; c.Items.Add(it); if (Equals(kv.Key, selected)) c.SelectedItem = it; }
            c.SelectionChanged += (s, e) => { if (c.SelectedItem is ComboBoxItem it) changed((T)it.Tag); };
            return c;
        }

        /// <summary>A row of exclusive choice buttons (segmented control).</summary>
        public static StackPanel Segmented<T>(IEnumerable<KeyValuePair<T, string>> items, T selected, Action<T> changed)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var buttons = new List<Ui.Button>();
            foreach (var kv in items)
            {
                var b = new Ui.Button { Content = kv.Value, Tag = kv.Key, Margin = new Thickness(0, 0, 4, 0), MinWidth = 64 };
                buttons.Add(b); sp.Children.Add(b);
            }
            void Mark(object key) { foreach (var b in buttons) b.Appearance = Equals(b.Tag, key) ? Ui.ControlAppearance.Primary : Ui.ControlAppearance.Secondary; }
            foreach (var b in buttons) b.Click += (s, e) => { Mark(b.Tag); changed((T)b.Tag); };
            Mark(selected);
            return sp;
        }

        public static StackPanel Row(params UIElement[] children)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var c in children) sp.Children.Add(c);
            return sp;
        }

        public static ScrollViewer Page(StackPanel content)
        {
            content.Margin = new Thickness(Tok.S32, Tok.S8, Tok.S32, Tok.S32);
            content.MaxWidth = 1000;
            return new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        }

        public static SolidColorBrush Brush(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

        /// <summary>Briefly outlines an element in the accent colour (search result).</summary>
        public static void Flash(Border b)
        {
            var old = b.BorderBrush;
            var oldT = b.BorderThickness;
            var brush = new SolidColorBrush(UiTheme.Accent);
            b.BorderBrush = brush; b.BorderThickness = new Thickness(2);
            var anim = new ColorAnimation(UiTheme.Accent, Color.FromArgb(0, UiTheme.Accent.R, UiTheme.Accent.G, UiTheme.Accent.B), TimeSpan.FromMilliseconds(1600)) { BeginTime = TimeSpan.FromMilliseconds(600) };
            anim.Completed += (s, e) => { b.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush"); b.BorderThickness = oldT; };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        }
    }

    /// <summary>窗口底部居中的小提示（v2：卡片样式、跟随深浅色、带状态图标、淡入上移）。</summary>
    sealed class ToastHost : Border
    {
        readonly TextBlock _text = new TextBlock { FontSize = Tok.Body, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 520 };
        readonly Ui.SymbolIcon _icon = new Ui.SymbolIcon { FontSize = 18, Margin = new Thickness(0, 0, Tok.S8, 0), VerticalAlignment = VerticalAlignment.Center };
        readonly System.Windows.Threading.DispatcherTimer _timer = new System.Windows.Threading.DispatcherTimer();

        public ToastHost()
        {
            CornerRadius = new CornerRadius(Tok.RCard); Padding = new Thickness(Tok.S16, 10, Tok.S16, 10); BorderThickness = new Thickness(1);
            HorizontalAlignment = HorizontalAlignment.Center; VerticalAlignment = VerticalAlignment.Bottom;
            Margin = new Thickness(0, 0, 0, Tok.S24); Visibility = Visibility.Collapsed; IsHitTestVisible = false;
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(_icon); row.Children.Add(_text);
            Child = row;
            SetResourceReference(BackgroundProperty, "SolidBackgroundFillColorTertiaryBrush");
            SetResourceReference(BorderBrushProperty, "SurfaceStrokeColorFlyoutBrush");
            _text.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
            AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                if (!Motion.Enabled) { Visibility = Visibility.Collapsed; return; }
                var a = new DoubleAnimation(0, Tok.Slow) { EasingFunction = Tok.Decelerate };
                a.Completed += (x, y) => { if (!_timer.IsEnabled) Visibility = Visibility.Collapsed; };
                BeginAnimation(OpacityProperty, a);
            };
        }

        public void Show(string text, int ms = 2200)
        {
            text = text ?? "";
            // 状态不只靠颜色：成功 ✓ / 失败 ! / 进行中 ⋯ 各有自己的图标
            bool ok = text.StartsWith("✓") || text.StartsWith("已");
            bool bad = text.Contains("失败") || text.Contains("未完成") || text.Contains("出错");
            bool busy = text.StartsWith("正在");
            if (text.StartsWith("✓")) text = text.Substring(1).TrimStart();
            _icon.Symbol = bad ? Ui.SymbolRegular.ErrorCircle24 : ok ? Ui.SymbolRegular.CheckmarkCircle24 : busy ? Ui.SymbolRegular.ArrowSync24 : Ui.SymbolRegular.Info24;
            _icon.SetResourceReference(Control.ForegroundProperty, bad ? "SystemFillColorCriticalBrush" : ok ? "SystemFillColorSuccessBrush" : "TextFillColorSecondaryBrush");
            _text.Text = text;
            AutomationProperties.SetName(this, text);
            Effect = Tok.FlyoutShadow(UiTheme.Dark);
            bool wasVisible = Visibility == Visibility.Visible && Opacity > 0.9;
            Visibility = Visibility.Visible;
            BeginAnimation(OpacityProperty, null); Opacity = 1;
            if (!wasVisible) Motion.FadeInUp(this, 8, Tok.Slow);
            _timer.Stop(); _timer.Interval = TimeSpan.FromMilliseconds(ms); _timer.Start();
        }

        /// <summary>Shows "正在重新部署…" / "部署完成" while this window is open.</summary>
        public void TrackDeploy(Window owner)
        {
            Action<bool, bool> h = (finished, ok) => owner.Dispatcher.BeginInvoke(new Action(() =>
                Show(finished ? (ok ? "✓ 已生效（部署完成）" : "部署未完成，可在托盘菜单里「重新部署」") : "正在后台重新部署…", finished ? 2200 : 60000)));
            Deploy.StateChanged += h;
            owner.Closed += (s, e) => Deploy.StateChanged -= h;
        }
    }

    /// <summary>
    /// v2 对话框：圆角卡片、图标 + 标题 + 正文、右下主次按钮；Esc = 取消、回车 = 确定。
    /// 只在 WPF 界面线程上用；其他线程回退系统 MessageBox（见 Dialogs.Show）。
    /// </summary>
    sealed class FluentDialog : Window
    {
        bool _ok;

        FluentDialog(string text, string title, bool cancel, bool warn)
        {
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight; WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true; ShowInTaskbar = true; Title = title; FontFamily = Tok.Text;
            AutomationProperties.SetName(this, title);

            var g = new Grid { MinWidth = 360, MaxWidth = 520 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var ic = new Ui.SymbolIcon { Symbol = warn ? Ui.SymbolRegular.Warning24 : Ui.SymbolRegular.Info24, FontSize = 24, Margin = new Thickness(0, 2, Tok.S12, 0), VerticalAlignment = VerticalAlignment.Top };
            if (warn) ic.SetResourceReference(Control.ForegroundProperty, "SystemFillColorCautionBrush"); else ic.Foreground = Tok.TileGlyphBrush;
            Grid.SetRowSpan(ic, 2); g.Children.Add(ic);
            var head = K.Text(title, Tok.Title, false, FontWeights.SemiBold); head.FontFamily = Tok.Display;
            Grid.SetColumn(head, 1); g.Children.Add(head);
            var body = K.Text(text, Tok.Body); body.Margin = new Thickness(0, Tok.S8, 0, 0); body.LineHeight = 21;
            var bodyScroll = new ScrollViewer { Content = body, MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetColumn(bodyScroll, 1); Grid.SetRow(bodyScroll, 1); g.Children.Add(bodyScroll);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, Tok.S24, 0, 0) };
            var okBtn = new Ui.Button { Content = "确定", Appearance = Ui.ControlAppearance.Primary, MinWidth = 96, IsDefault = true };
            okBtn.Click += (s, e) => { _ok = true; Close(); };
            if (cancel)
            {
                var c = new Ui.Button { Content = "取消", Appearance = Ui.ControlAppearance.Secondary, MinWidth = 96, IsCancel = true, Margin = new Thickness(0, 0, Tok.S8, 0) };
                c.Click += (s, e) => Close();
                buttons.Children.Add(c);
            }
            buttons.Children.Add(okBtn);
            Grid.SetColumnSpan(buttons, 2); Grid.SetRow(buttons, 2); g.Children.Add(buttons);

            var card = new Border { CornerRadius = new CornerRadius(Tok.RFlyout), BorderThickness = new Thickness(1), Padding = new Thickness(Tok.S24), Margin = new Thickness(Tok.S24), Child = g, Effect = Tok.FlyoutShadow(UiTheme.Dark) };
            card.SetResourceReference(Border.BackgroundProperty, "SolidBackgroundFillColorBaseBrush");
            card.SetResourceReference(Border.BorderBrushProperty, "SurfaceStrokeColorFlyoutBrush");
            card.MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
            Content = card;
            PreviewKeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) { _ok = false; Close(); e.Handled = true; } };
            Loaded += (s, e) => { Motion.PopIn(card); okBtn.Focus(); };
            ContentRendered += (s, e) => { try { Activate(); } catch { } };
        }

        public static bool Ask(string text, string title, bool cancel, bool warn)
        {
            var d = new FluentDialog(text, title, cancel, warn);
            d.ShowDialog();
            return d._ok;
        }
    }

    static class Dialogs
    {
        // 助手是后台进程，从语言栏菜单 / 管道命令弹出的对话框常被 Windows 压到其它窗口后面（只在任务栏闪），
        // 用户会以为「点了没反应」。v2 对话框自带置顶；回退的系统框用一个置顶的隐形窗口当 owner。
        static MessageBoxResult Show(string text, string title, MessageBoxButton buttons, MessageBoxImage image)
        {
            try
            {
                var app = Application.Current;
                if (app != null && app.Dispatcher.CheckAccess())
                {
                    bool ok = FluentDialog.Ask(text, title, buttons == MessageBoxButton.OKCancel, image == MessageBoxImage.Warning);
                    return ok ? MessageBoxResult.OK : MessageBoxResult.Cancel;
                }
            }
            catch (Exception e) { Log.Write("dialog: " + e.Message); }
            Window owner = null;
            try
            {
                owner = new Window
                {
                    Width = 0, Height = 0, WindowStyle = WindowStyle.None, ShowInTaskbar = false, Topmost = true,
                    AllowsTransparency = true, Background = System.Windows.Media.Brushes.Transparent,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen, ShowActivated = true,
                };
                owner.Show();
                owner.Activate();
                return MessageBox.Show(owner, text, title, buttons, image);
            }
            catch
            {
                return MessageBox.Show(text, title, buttons, image);
            }
            finally { try { owner?.Close(); } catch { } }
        }

        public static bool Confirm(string text, string title = "一维输入法") =>
            Show(text, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
        public static void Info(string text, string title = "一维输入法") => Show(text, title, MessageBoxButton.OK, MessageBoxImage.Information);
        public static void Error(string text, string title = "一维输入法") => Show(text, title, MessageBoxButton.OK, MessageBoxImage.Warning);

        public static void Open(string path, string args = null)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path, args ?? "") { UseShellExecute = true }); }
            catch (Exception e) { Error("无法打开：" + e.Message); }
        }
    }

    /// <summary>
    /// A borderless, transparent, always-on-top window that never takes focus (unless asked to),
    /// positioned in screen pixels next to the caret.
    /// </summary>
    class FloatingWindow : Window
    {
        protected IntPtr Hwnd;
        readonly bool _clickThrough;
        bool _noActivate = true;
        Native.RECT _anchor;

        public FloatingWindow(bool clickThrough = false)
        {
            _clickThrough = clickThrough;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = false; Topmost = true; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight; FontFamily = K.Font;
            Left = -32000; Top = -32000;
            SourceInitialized += (s, e) =>
            {
                Hwnd = new WindowInteropHelper(this).Handle;
                SetNoActivate(true);
            };
            SizeChanged += (s, e) => { if (IsVisible && Hwnd != IntPtr.Zero) Place(); };
        }

        protected void SetNoActivate(bool on)
        {
            _noActivate = on;
            if (Hwnd == IntPtr.Zero) return;
            int ex = Native.GetWindowLong(Hwnd, Native.GWL_EXSTYLE);
            ex |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST;
            if (on) ex |= Native.WS_EX_NOACTIVATE; else ex &= ~Native.WS_EX_NOACTIVATE;
            if (_clickThrough) ex |= Native.WS_EX_TRANSPARENT;
            Native.SetWindowLong(Hwnd, Native.GWL_EXSTYLE, ex);
        }

        protected bool NoActivate => _noActivate;

        /// <summary>Shows next to the caret of the foreground app (or under the mouse cursor).</summary>
        public void ShowAtCaret()
        {
            var r = Native.CaretRect();
            if (r == null)
            {
                Native.GetCursorPos(out var c);
                r = new Native.RECT { Left = c.X, Top = c.Y, Right = c.X + 1, Bottom = c.Y + 20 };
            }
            ShowAt(r.Value);
        }

        public void ShowAt(Native.RECT anchor)
        {
            _anchor = anchor;
            bool appearing = !IsVisible;
            if (appearing) { Show(); }
            UpdateLayout();
            Place();
            if (appearing && AnimateOpen && Content is UIElement c) Motion.FadeInUp(c, 8); // v2：面板打开时淡入上移
        }

        /// <summary>打开时是否淡入上移（中/英提示这类打字路径上的气泡关掉）。</summary>
        protected virtual bool AnimateOpen => !_clickThrough;

        /// <summary>重建内容后保持可见（换页时新内容也淡入，但幅度更小）。</summary>
        protected void SwapContent(UIElement c, bool animate)
        {
            Content = c;
            if (animate && IsVisible && AnimateOpen) Motion.FadeInUp(c, 4, Tok.Fast);
        }

        void Place()
        {
            if (Hwnd == IntPtr.Zero) return;
            Native.GetWindowRect(Hwnd, out var me);
            int w = me.Right - me.Left, h = me.Bottom - me.Top;
            var wa = Native.WorkArea(_anchor.Left, _anchor.Bottom);
            int x = Math.Max(wa.Left, Math.Min(_anchor.Left - 8, wa.Right - w));
            int y = _anchor.Bottom + 6;
            if (y + h > wa.Bottom) y = Math.Max(wa.Top, _anchor.Top - h - 6);
            Native.SetWindowPos(Hwnd, Native.HWND_TOPMOST, x, y, 0, 0, Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }
    }

    /// <summary>浮动面板的配色（v2「澄澈」，DESIGN-v2 §2.2）：系统深浅色 + 强调色。</summary>
    sealed class PanelPalette
    {
        public bool Dark;
        public Brush Back, Card, CardHover, CardPressed, Text, Dim, Faint, Accent, AccentText, AccentInk, AccentSoft, Border, Highlight, Focus, Del, DelText, Ins, InsText;
        public Color BackColor, CardColor, CardHoverColor;
        public static PanelPalette Now()
        {
            bool dark = SystemTheme.IsDark;
            var acc = UiTheme.Accent;
            if (dark) acc = Tok.Mix(acc, Colors.White, 0.30); // 深色档强调色提亮（与候选窗 v2 一致）
            var back = dark ? Color.FromRgb(0x1D, 0x21, 0x29) : Color.FromRgb(0xF6, 0xF8, 0xFB);
            var card = dark ? Color.FromRgb(0x27, 0x2C, 0x36) : Colors.White;
            var hover = dark ? Color.FromRgb(0x31, 0x37, 0x44) : Color.FromRgb(0xEE, 0xF2, 0xF8);
            return new PanelPalette
            {
                Dark = dark, BackColor = back, CardColor = card, CardHoverColor = hover,
                Back = K.Brush(Color.FromArgb((byte)(dark ? 0xF5 : 0xF7), back.R, back.G, back.B)),
                Card = K.Brush(card),
                CardHover = K.Brush(hover),
                CardPressed = K.Brush(dark ? Color.FromRgb(0x3A, 0x41, 0x50) : Color.FromRgb(0xE4, 0xE9, 0xF1)),
                Text = K.Brush(dark ? Color.FromRgb(0xEE, 0xF1, 0xF6) : Color.FromRgb(0x16, 0x1B, 0x26)),
                Dim = K.Brush(dark ? Color.FromRgb(0xA4, 0xAD, 0xBE) : Color.FromRgb(0x5A, 0x64, 0x78)),
                Faint = K.Brush(dark ? Color.FromRgb(0x7C, 0x85, 0x98) : Color.FromRgb(0x7A, 0x83, 0x98)),
                Accent = K.Brush(acc),
                AccentText = K.Brush(Tok.OnAccent(acc)),
                AccentInk = K.Brush(Tok.AccentText(UiTheme.Accent, dark, back)),
                AccentSoft = K.Brush(Tok.WithAlpha(acc, (byte)(dark ? 0x33 : 0x1F))),
                Border = K.Brush(dark ? Color.FromRgb(0x3A, 0x41, 0x50) : Color.FromRgb(0xE2, 0xE7, 0xEF)),
                Highlight = K.Brush(dark ? Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF)),
                Focus = K.Brush(acc),
                Del = K.Brush(dark ? Color.FromArgb(0x55, 0xE0, 0x48, 0x3A) : Color.FromRgb(0xFD, 0xE2, 0xDF)),
                DelText = K.Brush(dark ? Color.FromRgb(0xFF, 0x9C, 0x92) : Color.FromRgb(0xB4, 0x23, 0x18)),
                Ins = K.Brush(dark ? Color.FromArgb(0x55, 0x0F, 0x9D, 0x8A) : Color.FromRgb(0xD8, 0xF3, 0xEC)),
                InsText = K.Brush(dark ? Color.FromRgb(0x7F, 0xE0, 0xC8) : Color.FromRgb(0x0B, 0x6E, 0x4F)),
            };
        }
    }
}
