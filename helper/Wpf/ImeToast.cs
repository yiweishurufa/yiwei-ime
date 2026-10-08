using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Yiwei
{
    /// <summary>
    /// The small 中 / A bubble shown near the caret for ~600 ms when the input mode changes.
    /// Triggered by the pipe command "toast:中" / "toast:A" (sent by the IME), or by the optional IMM poller.
    /// </summary>
    sealed class ImeToast : FloatingWindow
    {
        readonly TextBlock _text = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        readonly Border _bubble;
        readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };

        public ImeToast() : base(clickThrough: true)
        {
            _bubble = new Border
            {
                Width = 30, Height = 30, CornerRadius = new CornerRadius(8), Child = _text, Margin = new Thickness(6),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, ShadowDepth = 1, Opacity = 0.25 },
            };
            Content = _bubble;
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                var a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
                a.Completed += (x, y) => { if (!_timer.IsEnabled) Hide(); };
                _bubble.BeginAnimation(OpacityProperty, a);
            };
        }

        public void Flash(bool chinese)
        {
            if (!Settings.Current.ImeToast) return;
            bool dark = SystemTheme.IsDark;
            _text.Text = chinese ? "中" : "A";
            _bubble.Background = new SolidColorBrush(chinese ? UiTheme.Accent : (dark ? Color.FromRgb(0x3A, 0x3D, 0x44) : Color.FromRgb(0x2B, 0x2F, 0x36)));
            _text.Foreground = Brushes.White;
            _bubble.BeginAnimation(OpacityProperty, null);
            _bubble.Opacity = 1;
            ShowAtCaret();
            _timer.Stop(); _timer.Start();
        }
    }

    /// <summary>
    /// Optional (experimental, off by default): polls the focused window's IMM open/conversion state.
    /// Weasel is a TSF text service and does not always mirror its ascii mode into IMM, so the reliable path
    /// is the IME sending "toast:中" / "toast:A" over the helper's pipe.
    /// </summary>
    static class ImeModeWatcher
    {
        const uint WM_IME_CONTROL = 0x283;
        const int IMC_GETCONVERSIONMODE = 1, IMC_GETOPENSTATUS = 5;
        static DispatcherTimer _timer;
        static IntPtr _lastWindow;
        static int _last = -1;

        public static void Start(Action<bool> onChange)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _timer.Tick += (s, e) =>
            {
                if (!Settings.Current.ImeToast || !Settings.Current.ImeToastPolling) { _last = -1; return; }
                var fg = Native.GetForegroundWindow();
                int state = Read(fg);
                if (fg != _lastWindow) { _lastWindow = fg; _last = state; return; } // a focus change is not a mode change
                if (state >= 0 && _last >= 0 && state != _last) onChange(state == 1);
                _last = state;
            };
            _timer.Start();
        }

        /// <summary>1 = Chinese, 0 = English, -1 = unknown.</summary>
        static int Read(IntPtr fg)
        {
            if (fg == IntPtr.Zero) return -1;
            try
            {
                var ime = Native.ImmGetDefaultIMEWnd(fg);
                if (ime == IntPtr.Zero) return -1;
                if (Native.SendMessageTimeout(ime, WM_IME_CONTROL, (IntPtr)IMC_GETOPENSTATUS, IntPtr.Zero, 2 /* ABORTIFHUNG */, 50, out var open) == IntPtr.Zero) return -1;
                if (open == IntPtr.Zero) return 0;
                if (Native.SendMessageTimeout(ime, WM_IME_CONTROL, (IntPtr)IMC_GETCONVERSIONMODE, IntPtr.Zero, 2, 50, out var mode) == IntPtr.Zero) return -1;
                return (mode.ToInt64() & 1) != 0 ? 1 : 0; // IME_CMODE_NATIVE
            }
            catch { return -1; }
        }
    }
}
