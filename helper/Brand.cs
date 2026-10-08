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
            new BrandColor("moblue", "墨蓝", 0x2F5BEA),
            new BrandColor("qingbi", "青碧", 0x0F9D8A),
            new BrandColor("zhusha", "朱砂", 0xE0483A),
            new BrandColor("dianzi", "靛紫", 0x6B4EE6),
            new BrandColor("shimo", "石墨", 0x2B2F36),
        };

        public static BrandColor Default => Palette[1];

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
            uint accent = Argb(c.Rgb);
            const uint White = 0xFFFFFFFF;
            var d = new Dictionary<string, uint>();
            if (!dark)
            {
                d["back_color"] = 0xFFFFFFFF;
                d["border_color"] = Mix(0xFFE4E6EA, accent, 0.18);
                d["shadow_color"] = 0x26000000;
                d["text_color"] = 0xFF5B6270;
                d["hilited_text_color"] = c.Id == "shimo" ? 0xFF2B2F36 : accent;
                d["hilited_back_color"] = Mix(accent, White, 0.88);
                d["candidate_text_color"] = 0xFF1F2328;
                d["comment_text_color"] = 0xFF8A9099;
                d["label_color"] = c.Id == "shimo" ? 0xFF6B7280 : accent;
                d["hilited_candidate_back_color"] = accent;
                d["hilited_candidate_text_color"] = White;
                d["hilited_comment_text_color"] = Mix(White, accent, 0.22);
                d["hilited_label_color"] = Mix(White, accent, 0.15);
                d["preedit_back_color"] = Mix(accent, White, 0.92);
            }
            else
            {
                // 石墨 is almost the dark background itself, so its dark variant lifts the highlight.
                uint hi = c.Id == "shimo" ? 0xFF4A505A : Mix(accent, 0xFF000000, 0.08);
                uint soft = c.Id == "shimo" ? 0xFFB8BEC8 : Mix(accent, White, 0.38);
                d["back_color"] = 0xFF202226;
                d["border_color"] = Mix(0xFF3A3D44, accent, 0.15);
                d["shadow_color"] = 0x4D000000;
                d["text_color"] = 0xFFA9AFB8;
                d["hilited_text_color"] = soft;
                d["hilited_back_color"] = Mix(0xFF202226, accent, 0.22);
                d["candidate_text_color"] = 0xFFECEEF1;
                d["comment_text_color"] = 0xFF8B919A;
                d["label_color"] = soft;
                d["hilited_candidate_back_color"] = hi;
                d["hilited_candidate_text_color"] = White;
                d["hilited_comment_text_color"] = Mix(White, hi, 0.25);
                d["hilited_label_color"] = Mix(White, hi, 0.18);
                d["preedit_back_color"] = Mix(0xFF202226, accent, 0.15);
            }
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
