using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Yiwei
{
    /// <summary>
    /// v2「澄澈」设计 token（docs/design/DESIGN-v2.md）：字阶、间距、圆角、动效时长。页面代码只引用这里，不写裸数值。
    /// </summary>
    static class Tok
    {
        // ---- 字体（只用系统字体，不打包） ----
        public static readonly FontFamily Text = new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI, Segoe UI");
        public static readonly FontFamily Display = new FontFamily("Segoe UI Variable Display, Microsoft YaHei UI, Segoe UI");

        // ---- 字阶（px） ----
        public const double Display28 = 28, Title = 20, Subtitle = 16, Body = 14, Caption = 12;

        // ---- 间距 ----
        public const double S4 = 4, S8 = 8, S12 = 12, S16 = 16, S24 = 24, S32 = 32;

        // ---- 圆角 ----
        public const double RControl = 4, RCard = 8, RFlyout = 12;

        // ---- 动效 ----
        public static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(120);
        public static readonly TimeSpan Normal = TimeSpan.FromMilliseconds(200);
        public static readonly TimeSpan Slow = TimeSpan.FromMilliseconds(300);
        /// <summary>Fluent 的减速曲线（快出慢停）。</summary>
        public static readonly IEasingFunction Decelerate = Frozen(new CubicEase { EasingMode = EasingMode.EaseOut });

        static IEasingFunction Frozen(CubicEase e) { e.Freeze(); return e; }

        // ---- 颜色计算 ----
        public static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

        public static Color Mix(Color a, Color b, double t) => Color.FromRgb(
            (byte)Math.Round(a.R * (1 - t) + b.R * t), (byte)Math.Round(a.G * (1 - t) + b.G * t), (byte)Math.Round(a.B * (1 - t) + b.B * t));

        static double Lum(Color c)
        {
            double Ch(byte v) { var x = v / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }
            return 0.2126 * Ch(c.R) + 0.7152 * Ch(c.G) + 0.0722 * Ch(c.B);
        }

        public static double Contrast(Color a, Color b)
        {
            double la = Lum(a), lb = Lum(b);
            if (la < lb) { var t = la; la = lb; lb = t; }
            return (la + 0.05) / (lb + 0.05);
        }

        static readonly Color Ink = Color.FromRgb(0x0B, 0x0F, 0x17);

        /// <summary>强调色底上的文字：白或近黑，取对比度高的（与 Brand.OnAccent、contrast_v2.py 一致）。</summary>
        public static Color OnAccent(Color accent) => Contrast(Colors.White, accent) >= Contrast(Ink, accent) ? Colors.White : Ink;

        /// <summary>强调色当文字用（页签、选中序号）：浅色档向墨色压、深色档向白提亮，直到对底色 ≥ 4.5:1。</summary>
        public static Color AccentText(Color accent, bool dark, Color back)
        {
            for (double t = dark ? 0.45 : 0.25; ; t += 0.05)
            {
                var c = dark ? Mix(accent, Colors.White, t) : Mix(accent, Ink, t);
                if (Contrast(c, back) >= 4.5 || t >= 0.9) return c;
            }
        }

        /// <summary>设置项图标瓦片的底（强调色 12% / 深色 22%）。</summary>
        public static Brush TileBrush => K.Brush(WithAlpha(UiTheme.Accent, (byte)(UiTheme.Dark ? 0x38 : 0x1F)));

        /// <summary>图标瓦片里的图标颜色。</summary>
        public static Brush TileGlyphBrush => K.Brush(AccentText(UiTheme.Accent, UiTheme.Dark, UiTheme.Dark ? Color.FromRgb(0x20, 0x20, 0x20) : Color.FromRgb(0xF3, 0xF3, 0xF3)));

        /// <summary>浮动面板、对话框的阴影（一层 DropShadow 近似 DESIGN-v2 §4 的两层阴影）。</summary>
        public static Effect FlyoutShadow(bool dark) =>
            new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Direction = 270, Opacity = dark ? 0.40 : 0.18, Color = Colors.Black };
    }

    /// <summary>
    /// 克制的动效：全部作用在 Opacity / RenderTransform 上；系统「动画效果」关掉时（或远程桌面里）直接跳到终态。
    /// </summary>
    static class Motion
    {
        /// <summary>设置 → 辅助功能 → 视觉效果 → 动画效果；远程桌面会话里也不做动画。</summary>
        public static bool Enabled
        {
            get
            {
                try { return SystemParameters.ClientAreaAnimation && !System.Windows.Forms.SystemInformation.TerminalServerSession; }
                catch { return true; }
            }
        }

        static TranslateTransform Translate(UIElement e)
        {
            if (e.RenderTransform is TranslateTransform t && !t.IsFrozen) return t;
            if (e.RenderTransform is TransformGroup g)
                foreach (var c in g.Children) if (c is TranslateTransform tt && !tt.IsFrozen) return tt;
            var n = new TranslateTransform();
            e.RenderTransform = n;
            return n;
        }

        /// <summary>淡入 + 从下方 <paramref name="dy"/> 像素上移到位（页面切换、面板打开、提示出现）。</summary>
        public static void FadeInUp(UIElement e, double dy = 12, TimeSpan? duration = null, TimeSpan? delay = null)
        {
            if (e == null) return;
            if (!Enabled) { e.BeginAnimation(UIElement.OpacityProperty, null); e.Opacity = 1; return; }
            var d = duration ?? Tok.Normal;
            var t = Translate(e);
            var fade = new DoubleAnimation(0, 1, d) { EasingFunction = Tok.Decelerate, BeginTime = delay };
            var move = new DoubleAnimation(dy, 0, d) { EasingFunction = Tok.Decelerate, BeginTime = delay };
            if (delay.HasValue) { e.Opacity = 0; t.Y = dy; }
            e.BeginAnimation(UIElement.OpacityProperty, fade);
            t.BeginAnimation(TranslateTransform.YProperty, move);
        }

        /// <summary>淡入 + 从 0.96 放大到 1（对话框）。</summary>
        public static void PopIn(FrameworkElement e)
        {
            if (e == null || !Enabled) return;
            var s = new ScaleTransform(0.96, 0.96);
            e.RenderTransformOrigin = new Point(0.5, 0.5);
            e.RenderTransform = s;
            var a = new DoubleAnimation(0.96, 1, Tok.Normal) { EasingFunction = Tok.Decelerate };
            s.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            s.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            e.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, Tok.Normal) { EasingFunction = Tok.Decelerate });
        }

        /// <summary>把 Border 的底色平滑过渡到 <paramref name="to"/>（悬停）；动画关闭时直接换。</summary>
        public static void To(Border b, Color to, TimeSpan? d = null)
        {
            if (b == null) return;
            if (!Enabled || !(b.Background is SolidColorBrush cur)) { b.Background = new SolidColorBrush(to); return; }
            var brush = cur.IsFrozen ? new SolidColorBrush(cur.Color) : cur;
            if (!ReferenceEquals(brush, b.Background)) b.Background = brush;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, d ?? Tok.Fast) { EasingFunction = Tok.Decelerate });
        }

        /// <summary>按下时缩到 0.97，松开回到 1（功能面板格子、配色卡片）。</summary>
        public static void Pressable(FrameworkElement e)
        {
            if (e == null) return;
            e.RenderTransformOrigin = new Point(0.5, 0.5);
            var s = new ScaleTransform(1, 1);
            e.RenderTransform = s;
            void Go(double v)
            {
                if (!Enabled) { s.ScaleX = s.ScaleY = 1; return; }
                var a = new DoubleAnimation(v, Tok.Fast) { EasingFunction = Tok.Decelerate };
                s.BeginAnimation(ScaleTransform.ScaleXProperty, a);
                s.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            }
            e.PreviewMouseLeftButtonDown += (x, y) => Go(0.97);
            e.PreviewMouseLeftButtonUp += (x, y) => Go(1);
            e.MouseLeave += (x, y) => Go(1);
        }

        /// <summary>把一个元素在竖直方向滑到 <paramref name="y"/>（导航选中条）。</summary>
        public static void SlideY(FrameworkElement e, double y)
        {
            if (e == null) return;
            var t = Translate(e);
            if (!Enabled) { t.BeginAnimation(TranslateTransform.YProperty, null); t.Y = y; return; }
            t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(y, Tok.Slow) { EasingFunction = Tok.Decelerate });
        }
    }
}
