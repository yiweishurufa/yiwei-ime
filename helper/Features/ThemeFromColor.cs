using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace Yiwei
{
    /// <summary>
    /// W22 皮肤随壁纸取色 / W23 屏幕取色生成皮肤：从一个颜色生成「我的配色」（和品牌配色同一套规则：
    /// 纸色底、墨色字，取到的颜色用在高亮候选上；深色模式自动生成对应深色版）。
    /// </summary>
    public static class ThemeFromColor
    {
        /// <summary>Applies <paramref name="rgb"/> (0xRRGGBB) as 我的配色 and redeploys.</summary>
        public static void Apply(uint rgb, string name)
        {
            rgb = Readable(rgb);
            var colors = Brand.SchemeArgb(new BrandColor("custom", name, rgb), false);
            var theme = new Dictionary<string, string> { ["name"] = name, ["author"] = Environment.UserName };
            foreach (var kv in colors) theme[kv.Key] = "0x" + kv.Value.ToString("X8");
            var s = Settings.Current;
            s.CustomTheme = theme;
            s.ColorScheme = "yiwei_custom";
            ToolSettings.Current.ThemeRgb = rgb.ToString("X6"); ToolSettings.Current.Save();
            Rime.ApplySoon(200);
        }

        /// <summary>Keeps the highlight dark enough for white text (contrast ≥ ~4.5) and not grey.</summary>
        public static uint Readable(uint rgb)
        {
            double r = (rgb >> 16 & 0xFF) / 255.0, g = (rgb >> 8 & 0xFF) / 255.0, b = (rgb & 0xFF) / 255.0;
            for (int i = 0; i < 20 && Lum(r, g, b) > 0.18; i++) { r *= 0.88; g *= 0.88; b *= 0.88; }
            return (uint)(Math.Round(r * 255)) << 16 | (uint)(Math.Round(g * 255)) << 8 | (uint)Math.Round(b * 255);
        }

        static double Lin(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        static double Lum(double r, double g, double b) => 0.2126 * Lin(r) + 0.7152 * Lin(g) + 0.0722 * Lin(b);

        // ---------- wallpaper ----------

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SystemParametersInfo(uint action, uint param, StringBuilder buf, uint winIni);

        public static string WallpaperPath()
        {
            var sb = new StringBuilder(520);
            if (SystemParametersInfo(0x0073 /* SPI_GETDESKWALLPAPER */, (uint)sb.Capacity, sb, 0) && File.Exists(sb.ToString())) return sb.ToString();
            var cached = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
            return File.Exists(cached) ? cached : null;
        }

        /// <summary>Windows accent colour (used when the wallpaper is a solid colour / slideshow / Spotlight we cannot read).</summary>
        public static uint? SystemAccent()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    var v = k?.GetValue("AccentColor") ?? k?.GetValue("ColorizationColor");
                    if (v is int i)
                    {
                        uint u = unchecked((uint)i);
                        // AccentColor is ABGR
                        return k.GetValue("AccentColor") != null ? (u & 0xFF) << 16 | (u & 0xFF00) | (u >> 16 & 0xFF) : u & 0xFFFFFF;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>The most vivid frequent colour of an image (hue buckets weighted by saturation).</summary>
        public static uint? Dominant(Bitmap src)
        {
            using (var small = new Bitmap(src, new Size(64, 64 * Math.Max(1, src.Height) / Math.Max(1, src.Width) + 1)))
            {
                var buckets = new double[36]; var sums = new double[36, 3];
                for (int y = 0; y < small.Height; y++)
                    for (int x = 0; x < small.Width; x++)
                    {
                        var c = small.GetPixel(x, y);
                        float sat = c.GetSaturation(), bri = c.GetBrightness();
                        if (sat < 0.18 || bri < 0.12 || bri > 0.92) continue;
                        int h = (int)(c.GetHue() / 10) % 36;
                        double w = sat * (1 - Math.Abs(bri - 0.5));
                        buckets[h] += w; sums[h, 0] += c.R * w; sums[h, 1] += c.G * w; sums[h, 2] += c.B * w;
                    }
                int best = Array.IndexOf(buckets, buckets.Max());
                if (buckets[best] < 1) return null;
                double tot = buckets[best];
                return (uint)(sums[best, 0] / tot) << 16 | (uint)(sums[best, 1] / tot) << 8 | (uint)(sums[best, 2] / tot);
            }
        }

        public static uint? WallpaperColor(out string from)
        {
            from = "壁纸";
            var p = WallpaperPath();
            if (p != null)
                try { using (var b = new Bitmap(p)) { var c = Dominant(b); if (c != null) return c; } }
                catch (Exception e) { Log.Write("wallpaper: " + e.Message); }
            from = "系统主题色";
            return SystemAccent();
        }

        public static string FromWallpaper()
        {
            var c = WallpaperColor(out var from);
            if (c == null) throw new Exception("读不到壁纸颜色");
            Apply(c.Value, "壁纸取色");
            var p = WallpaperPath();
            ToolSettings.Current.WallpaperStamp = p == null ? "" : p + "|" + File.GetLastWriteTimeUtc(p).Ticks; ToolSettings.Current.Save();
            return "已按" + from + "生成「我的配色」#" + Readable(c.Value).ToString("X6");
        }

        static System.Windows.Forms.Timer _timer;

        /// <summary>跟随壁纸：每 30 分钟看一眼壁纸有没有换，换了就重新取色。</summary>
        public static void Start()
        {
            SystemEvents.UserPreferenceChanged += (s, e) => { if (e.Category == UserPreferenceCategory.Desktop) Check(); };
            _timer = new System.Windows.Forms.Timer { Interval = 30 * 60 * 1000 };
            _timer.Tick += (s, e) => Check();
            _timer.Start();
        }

        static void Check()
        {
            try
            {
                var t = ToolSettings.Current;
                if (!t.FollowWallpaper) return;
                var p = WallpaperPath();
                var stamp = p == null ? "" : p + "|" + File.GetLastWriteTimeUtc(p).Ticks;
                if (stamp == t.WallpaperStamp) return;
                FromWallpaper();
            }
            catch (Exception e) { Log.Write("follow wallpaper: " + e.Message); }
        }
    }
}
