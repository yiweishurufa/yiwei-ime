using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace Yiwei
{
    /// <summary>One of the five 一维 brand colours. Rgb is 0xRRGGBB.</summary>
    public sealed class BrandColor
    {
        public string Id, Name;
        public uint Rgb;
        public BrandColor(string id, string name, uint rgb) { Id = id; Name = name; Rgb = rgb; }
        public byte R => (byte)(Rgb >> 16);
        public byte G => (byte)(Rgb >> 8);
        public byte B => (byte)Rgb;
    }

    /// <summary>
    /// Brand palette and the candidate-window colour schemes generated from it
    /// (yiwei_moblue, yiwei_qingbi, yiwei_zhusha, yiwei_dianzi, yiwei_shimo and their *_dark variants).
    /// </summary>
    public static class Brand
    {
        public static readonly BrandColor[] Palette =
        {
            // 默认：蓝色胶囊（白底深字，高亮候选是 #2B5BD7 胶囊白字；深色版底 #20232C）
            new BrandColor("pill", "蓝色胶囊", 0x2B5BD7),
            new BrandColor("moblue", "墨蓝", 0x2B5BD7),
            new BrandColor("qingbi", "青碧", 0x0E8C7A),
            new BrandColor("zhusha", "朱砂", 0xC8452F),
            new BrandColor("dianzi", "靛紫", 0x6450D6),
            new BrandColor("shimo", "石墨", 0x2E3431),
        };

        public static BrandColor Default => Palette[0];

        /// <summary>The 蓝色胶囊 scheme (light or dark): its highlight is drawn as a capsule.</summary>
        public static bool IsPill(string scheme) => FromScheme(scheme)?.Id == "pill";

        /// <summary>Brand colours offered as accent swatches (蓝色胶囊 shares 墨蓝's blue, so it is not listed twice).</summary>
        public static IEnumerable<BrandColor> Accents => Palette.Where(b => b.Id != "pill");

        public static BrandColor Find(string id) => Palette.FirstOrDefault(b => b.Id == id);

        public static BrandColor Current => Find(Settings.Current.Accent) ?? Default;

        public static string SchemeId(string brandId, bool dark) => "yiwei_" + brandId + (dark ? "_dark" : "");

        /// <summary>The brand a scheme id belongs to (yiwei_qingbi / yiwei_qingbi_dark), or null.</summary>
        public static BrandColor FromScheme(string scheme)
        {
            if (string.IsNullOrEmpty(scheme) || !scheme.StartsWith("yiwei_")) return null;
            var id = scheme.Substring(6);
            if (id.EndsWith("_dark")) id = id.Substring(0, id.Length - 5);
            return Find(id);
        }

        // ---------- colour maths (all values ARGB 0xAARRGGBB) ----------

        public static uint Argb(uint rgb, byte a = 0xFF) => ((uint)a << 24) | (rgb & 0xFFFFFF);

        /// <summary>Mixes a towards b; t = 0 keeps a, t = 1 gives b.</summary>
        public static uint Mix(uint a, uint b, double t)
        {
            byte C(int shift) => (byte)Math.Round(((a >> shift) & 0xFF) * (1 - t) + ((b >> shift) & 0xFF) * t);
            return 0xFF000000u | ((uint)C(16) << 16) | ((uint)C(8) << 8) | C(0);
        }

        static uint WithAlpha(uint argb, byte a) => ((uint)a << 24) | (argb & 0xFFFFFF);

        /// <summary>ARGB → Weasel's 0xAABBGGRR text.</summary>
        public static string ToWeasel(uint argb)
        {
            uint a = argb >> 24, r = (argb >> 16) & 0xFF, g = (argb >> 8) & 0xFF, b = argb & 0xFF;
            return "0x" + ((a << 24) | (b << 16) | (g << 8) | r).ToString("X8");
        }

        /// <summary>Colours of a generated scheme, as ARGB, keyed by Weasel colour names.</summary>
        public static Dictionary<string, uint> SchemeArgb(BrandColor c, bool dark)
        {
            // 「墨线」：纸色底、墨色字；品牌色只用在高亮候选上，其余退成灰（与 scripts/brand_schemes.py 一致）。
            if (c.Id == "pill") return PillArgb(dark);
            uint acc = Argb(c.Rgb);
            const uint White = 0xFFFFFFFF, Paper = 0xFFFBFCFA, Ink = 0xFF1F2421, Night = 0xFF1B1F1D, Moon = 0xFFE8ECE9;
            bool g = c.Id == "shimo";
            var d = new Dictionary<string, uint>();
            if (!dark)
            {
                d["back_color"] = Paper;
                d["border_color"] = Mix(0xFFDDE3DF, acc, 0.10);
                d["shadow_color"] = 0x1F1A2420;
                d["text_color"] = 0xFF6A736E;
                d["hilited_text_color"] = Ink;
                d["hilited_back_color"] = Paper;
                d["preedit_back_color"] = Paper;
                d["candidate_text_color"] = Ink;
                d["comment_text_color"] = 0xFF8E9792;
                d["label_color"] = 0xFF9AA39E;
                d["hilited_candidate_back_color"] = Mix(acc, Paper, g ? 0.90 : 0.86);
                d["hilited_candidate_text_color"] = g ? Ink : Mix(acc, Ink, 0.35);
                d["hilited_comment_text_color"] = g ? 0xFF6A736E : Mix(acc, Paper, 0.30);
                d["hilited_label_color"] = g ? Ink : acc;
            }
            else
            {
                uint lift = g ? 0xFF39403C : Mix(Night, acc, 0.30);
                uint glow = g ? Moon : Mix(acc, White, 0.55);
                d["back_color"] = Night;
                d["border_color"] = Mix(0xFF323936, acc, 0.12);
                d["shadow_color"] = 0x4D000000;
                d["text_color"] = 0xFF8F9893;
                d["hilited_text_color"] = Moon;
                d["hilited_back_color"] = Night;
                d["preedit_back_color"] = Night;
                d["candidate_text_color"] = Moon;
                d["comment_text_color"] = 0xFF7F8883;
                d["label_color"] = 0xFF6F7873;
                d["hilited_candidate_back_color"] = lift;
                d["hilited_candidate_text_color"] = glow;
                d["hilited_comment_text_color"] = Mix(glow, lift, 0.35);
                d["hilited_label_color"] = Mix(glow, lift, 0.2);
            }
            return d;
        }

        /// <summary>蓝色胶囊：白底、深色文字、#2B5BD7 胶囊高亮白字、小号灰色序号（与 scripts/brand_schemes.py 一致）。</summary>
        static Dictionary<string, uint> PillArgb(bool dark)
        {
            const uint Blue = 0xFF2B5BD7, White = 0xFFFFFFFF;
            var d = new Dictionary<string, uint>();
            if (!dark)
            {
                d["back_color"] = White;
                d["border_color"] = 0xFFE1E5EE;
                d["shadow_color"] = 0x1F1A2440;
                d["text_color"] = 0xFF787F8C;
                d["hilited_text_color"] = 0xFF1C2028;
                d["hilited_back_color"] = White;
                d["preedit_back_color"] = White;
                d["candidate_text_color"] = 0xFF1C2028;
                d["comment_text_color"] = 0xFF8C94A5;
                d["label_color"] = 0xFF8C94A5;
            }
            else
            {
                d["back_color"] = 0xFF20232C;
                d["border_color"] = 0xFF373C4B;
                d["shadow_color"] = 0x4D000000;
                d["text_color"] = 0xFF8C94A5;
                d["hilited_text_color"] = 0xFFEBEEF5;
                d["hilited_back_color"] = 0xFF20232C;
                d["preedit_back_color"] = 0xFF20232C;
                d["candidate_text_color"] = 0xFFEBEEF5;
                d["comment_text_color"] = 0xFF7F8796;
                d["label_color"] = 0xFF8C94A5;
            }
            d["hilited_candidate_back_color"] = Blue;
            d["hilited_candidate_text_color"] = White;
            d["hilited_comment_text_color"] = 0xFFDCE6FF;
            d["hilited_label_color"] = 0xFFDCE6FF;
            return d;
        }

        /// <summary>The YAML value of preset_color_schemes/yiwei_* (colours in Weasel's default abgr format).</summary>
        public static string WeaselScheme(BrandColor c, bool dark)
        {
            var parts = new List<string>
            {
                "name: \"一维 · " + c.Name + (dark ? "（深色）" : "") + "\"",
                "author: \"一维输入法\"",
                "color_format: abgr",
            };
            foreach (var kv in SchemeArgb(c, dark)) parts.Add(kv.Key + ": " + ToWeasel(kv.Value));
            return "{" + string.Join(", ", parts) + "}";
        }
    }

    /// <summary>Windows light/dark app mode, with a change notification.</summary>
    public static class SystemTheme
    {
        const string Key = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        static bool _watching, _last;

        /// <summary>Raised on the UI thread when Windows switches between light and dark apps.</summary>
        public static event Action<bool> Changed;

        public static bool IsDark
        {
            get
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(Key))
                        return k != null && (k.GetValue("AppsUseLightTheme") as int? ?? 1) == 0;
                }
                catch { return false; }
            }
        }

        public static void StartWatching()
        {
            if (_watching) return;
            _watching = true;
            _last = IsDark;
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category != UserPreferenceCategory.General && e.Category != UserPreferenceCategory.VisualStyle && e.Category != UserPreferenceCategory.Color) return;
                var now = IsDark;
                if (now == _last) return;
                _last = now;
                try { Program.Ui.BeginInvoke(new Action(() => { try { Changed?.Invoke(now); } catch (Exception ex) { Log.Write("theme changed: " + ex.Message); } })); }
                catch { }
            };
        }

        /// <summary>Windows 11 (build 22000+) supports Mica.</summary>
        public static bool IsWindows11
        {
            get
            {
                try
                {
                    using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                        return int.TryParse(k?.GetValue("CurrentBuildNumber") as string, out var b) && b >= 22000;
                }
                catch { return false; }
            }
        }
    }
}
