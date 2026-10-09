using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
            var app = Application.Current;
            if (app != null)
                foreach (Window w in app.Windows)
                    if (w is Ui.FluentWindow fw && fw.IsLoaded)
                        try { WindowBackgroundManager.UpdateBackground(fw, t, fw.WindowBackdropType); } catch { }
            Changed?.Invoke();
        }

        /// <summary>Backdrop to request: Mica on Windows 11, plain background elsewhere.</summary>
        public static Ui.WindowBackdropType Backdrop => SystemTheme.IsWindows11 ? Ui.WindowBackdropType.Mica : Ui.WindowBackdropType.None;
    }

    /// <summary>Small builders for the settings and wizard pages.</summary>
    static class K
    {
        public static readonly FontFamily Font = new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI, Segoe UI");

        public static T Res<T>(T e, DependencyProperty p, string key) where T : FrameworkElement { e.SetResourceReference(p, key); return e; }

        public static TextBlock Text(string t, double size = 14, bool secondary = false, FontWeight? weight = null)
        {
            var tb = new TextBlock { Text = t, FontSize = size, TextWrapping = TextWrapping.Wrap, FontWeight = weight ?? FontWeights.Normal };
            tb.SetResourceReference(TextBlock.ForegroundProperty, secondary ? "TextFillColorSecondaryBrush" : "TextFillColorPrimaryBrush");
            return tb;
        }

        /// <summary>Page title with the brand's one short stroke under it (the 「一」 of 一维).</summary>
        public static FrameworkElement PageTitle(string t)
        {
            var p = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            p.Children.Add(Text(t, 24, false, FontWeights.SemiBold));
            var bar = new Border { Width = 28, Height = 3, CornerRadius = new CornerRadius(1.5), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(1, 8, 0, 0) };
            bar.SetResourceReference(Border.BackgroundProperty, "AccentFillColorDefaultBrush");
            p.Children.Add(bar);
            return p;
        }
        public static TextBlock Section(string t) { var x = Text(t, 13, true, FontWeights.SemiBold); x.Margin = new Thickness(2, 22, 0, 8); return x; }

        public static Border CardBorder(UIElement child, Thickness? pad = null)
        {
            var b = new Border
            {
                CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = pad ?? new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 4), Child = child,
            };
            b.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
            b.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
            return b;
        }

        /// <summary>A setting row: icon, title, one-line grey description, control on the right.</summary>
        public static Border Card(string title, string desc, UIElement right, Ui.SymbolRegular icon = Ui.SymbolRegular.Empty)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (icon != Ui.SymbolRegular.Empty)
            {
                var ic = new Ui.SymbolIcon { Symbol = icon, FontSize = 20, Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center };
                g.Children.Add(ic);
            }
            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            texts.Children.Add(Text(title, 14));
            if (!string.IsNullOrEmpty(desc)) texts.Children.Add(Text(desc, 12, true));
            Grid.SetColumn(texts, 1); g.Children.Add(texts);
            if (right != null)
            {
                if (right is FrameworkElement fe) { fe.VerticalAlignment = VerticalAlignment.Center; fe.Margin = new Thickness(16, 0, 0, 0); }
                Grid.SetColumn(right, 2); g.Children.Add(right);
            }
            var card = CardBorder(g);
            card.MinHeight = 64;
            card.Tag = title + "\n" + desc;
            return card;
        }

        /// <summary>A card with content underneath the title (lists, previews).</summary>
        public static Border Block(string title, string desc, UIElement body, Ui.SymbolRegular icon = Ui.SymbolRegular.Empty)
        {
            var sp = new StackPanel();
            var head = Card(title, desc, null, icon).Child as Grid;
            ((Border)head.Parent).Child = null;
            sp.Children.Add(head);
            if (body is FrameworkElement fe && fe.Margin == default(Thickness)) fe.Margin = new Thickness(0, 12, 0, 0);
            sp.Children.Add(body);
            var card = CardBorder(sp);
            card.Tag = title + "\n" + desc;
            return card;
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
            content.Margin = new Thickness(28, 20, 28, 28);
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

    /// <summary>A small toast inside a window (bottom centre).</summary>
    sealed class ToastHost : Border
    {
        readonly TextBlock _text = new TextBlock { FontSize = 13, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
        readonly System.Windows.Threading.DispatcherTimer _timer = new System.Windows.Threading.DispatcherTimer();

        public ToastHost()
        {
            CornerRadius = new CornerRadius(8); Padding = new Thickness(16, 10, 16, 10);
            Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x2B, 0x2F, 0x36));
            HorizontalAlignment = HorizontalAlignment.Center; VerticalAlignment = VerticalAlignment.Bottom;
            Margin = new Thickness(0, 0, 0, 24); Child = _text; Visibility = Visibility.Collapsed; IsHitTestVisible = false;
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Opacity = 0.25 };
            _timer.Tick += (s, e) => { _timer.Stop(); var a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250)); a.Completed += (x, y) => { if (!_timer.IsEnabled) Visibility = Visibility.Collapsed; }; BeginAnimation(OpacityProperty, a); };
        }

        public void Show(string text, int ms = 2200)
        {
            _text.Text = text;
            Visibility = Visibility.Visible;
            BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(150)));
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

    static class Dialogs
    {
        public static bool Confirm(string text, string title = "一维输入法") =>
            MessageBox.Show(text, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
        public static void Info(string text) => MessageBox.Show(text, "一维输入法", MessageBoxButton.OK, MessageBoxImage.Information);
        public static void Error(string text) => MessageBox.Show(text, "一维输入法", MessageBoxButton.OK, MessageBoxImage.Warning);

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
            if (!IsVisible) { Show(); }
            UpdateLayout();
            Place();
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

    /// <summary>Colours for the floating panels: system light/dark + brand accent.</summary>
    sealed class PanelPalette
    {
        public Brush Back, Card, CardHover, Text, Dim, Accent, AccentText, Border, Del, DelText, Ins, InsText;
        public static PanelPalette Now()
        {
            bool dark = SystemTheme.IsDark;
            var acc = UiTheme.Accent;
            return new PanelPalette
            {
                Back = K.Brush(dark ? Color.FromArgb(0xF7, 0x2A, 0x2C, 0x31) : Color.FromArgb(0xF7, 0xFB, 0xFB, 0xFC)),
                Card = K.Brush(dark ? Color.FromRgb(0x35, 0x38, 0x3E) : Colors.White),
                CardHover = K.Brush(dark ? Color.FromRgb(0x40, 0x43, 0x4A) : Color.FromRgb(0xF0, 0xF2, 0xF5)),
                Text = K.Brush(dark ? Color.FromRgb(0xEC, 0xEE, 0xF1) : Color.FromRgb(0x1F, 0x23, 0x28)),
                Dim = K.Brush(dark ? Color.FromRgb(0x9A, 0xA0, 0xA8) : Color.FromRgb(0x6B, 0x72, 0x80)),
                Accent = K.Brush(acc),
                AccentText = Brushes.White,
                Border = K.Brush(dark ? Color.FromRgb(0x44, 0x47, 0x4E) : Color.FromRgb(0xE1, 0xE4, 0xE8)),
                Del = K.Brush(dark ? Color.FromArgb(0x55, 0xE0, 0x48, 0x3A) : Color.FromRgb(0xFD, 0xE2, 0xDF)),
                DelText = K.Brush(dark ? Color.FromRgb(0xFF, 0x9C, 0x92) : Color.FromRgb(0xB4, 0x23, 0x18)),
                Ins = K.Brush(dark ? Color.FromArgb(0x55, 0x0F, 0x9D, 0x8A) : Color.FromRgb(0xD8, 0xF3, 0xEC)),
                InsText = K.Brush(dark ? Color.FromRgb(0x7F, 0xE0, 0xC8) : Color.FromRgb(0x0B, 0x6E, 0x4F)),
            };
        }
    }
}
