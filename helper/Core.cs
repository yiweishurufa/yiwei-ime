using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace Yiwei
{
    /// <summary>Where 一维输入法 keeps things on this PC.</summary>
    public static class Paths
    {
        public const string RegKey = @"Software\Yiwei\YiweiIME";

        public static string UserDir
        {
            get
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(RegKey))
                    {
                        var v = k?.GetValue("RimeUserDir") as string;
                        if (!string.IsNullOrWhiteSpace(v)) return Environment.ExpandEnvironmentVariables(v);
                    }
                }
                catch { }
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YiweiIME");
            }
        }

        public static string InstallDir
        {
            get
            {
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    try
                    {
                        using (var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                        using (var k = b.OpenSubKey(RegKey))
                        {
                            var v = k?.GetValue("WeaselRoot") as string;
                            if (!string.IsNullOrWhiteSpace(v) && Directory.Exists(v)) return v;
                        }
                    }
                    catch { }
                }
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        public static string SharedDataDir => Path.Combine(InstallDir, "data");
        public static string YiweiDir { get { var d = Path.Combine(UserDir, "yiwei"); Directory.CreateDirectory(d); return d; } }
        public static string SettingsFile => Path.Combine(YiweiDir, "settings.json");
        public static string SnippetsFile => Path.Combine(YiweiDir, "snippets.json");
        public static string StatsFlag => Path.Combine(YiweiDir, "stats.enabled");
        public static string WeaselCustom => Path.Combine(UserDir, "weasel.custom.yaml");
        public static string Deployer => Path.Combine(InstallDir, "WeaselDeployer.exe");
        public static string Server => Path.Combine(InstallDir, "WeaselServer.exe");
        public static string LegacyWeaselUserDir
        {
            get
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Rime\Weasel"))
                    {
                        var v = k?.GetValue("RimeUserDir") as string;
                        if (!string.IsNullOrWhiteSpace(v)) return Environment.ExpandEnvironmentVariables(v);
                    }
                }
                catch { }
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Rime");
            }
        }
    }

    public static class Json
    {
        static readonly JavaScriptSerializer S = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        public static string Write(object o) => S.Serialize(o);
        public static T Read<T>(string s) => S.Deserialize<T>(s);
        public static object Parse(string s) => S.DeserializeObject(s);

        public static T Load<T>(string path) where T : new()
        {
            try { if (File.Exists(path)) return Read<T>(File.ReadAllText(path, Encoding.UTF8)) ?? new T(); }
            catch (Exception e) { Log.Write("load " + path + ": " + e.Message); }
            return new T();
        }

        public static void Save(string path, object o)
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, Pretty(Write(o)), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
        }

        static string Pretty(string json)
        {
            var sb = new StringBuilder(); int indent = 0; bool str = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i - 1] != '\\')) str = !str;
                if (str) { sb.Append(c); continue; }
                switch (c)
                {
                    case '{': case '[': sb.Append(c).Append('\n').Append(' ', ++indent * 2); break;
                    case '}': case ']': sb.Append('\n').Append(' ', --indent * 2).Append(c); break;
                    case ',': sb.Append(c).Append('\n').Append(' ', indent * 2); break;
                    case ':': sb.Append(": "); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }

    public static class Log
    {
        public static void Write(string msg)
        {
            try
            {
                var p = Path.Combine(Path.GetTempPath(), "yiwei-helper.log");
                if (File.Exists(p) && new FileInfo(p).Length > 1 << 20) File.Delete(p);
                File.AppendAllText(p, DateTime.Now.ToString("s") + " " + msg + Environment.NewLine);
            }
            catch { }
        }
    }

    public class AiAction
    {
        public string Name { get; set; } = "";
        public string Prompt { get; set; } = "";
    }

    public class Settings
    {
        // AI（OpenAI 兼容接口）
        public string AiBaseUrl { get; set; } = "https://api.openai.com/v1";
        public string AiModel { get; set; } = "gpt-4o-mini";
        public string AiKeyProtected { get; set; } = "";
        public List<AiAction> AiActions { get; set; } = DefaultActions();

        // 快捷键
        public int HoldMs { get; set; } = 300;
        public bool SnippetsHotkey { get; set; } = true;
        public bool AiHotkey { get; set; } = true;

        // 外观
        public string ColorScheme { get; set; } = "yiwei_light";
        public string ColorSchemeDark { get; set; } = "yiwei_dark";
        public bool Horizontal { get; set; } = true;
        public bool VerticalText { get; set; } = false;
        public int FontPoint { get; set; } = 14;
        public string FontFace { get; set; } = "Microsoft YaHei UI";
        public int CornerRadius { get; set; } = 10;
        public int HilitedCornerRadius { get; set; } = 6;
        public bool InlinePreedit { get; set; } = true;

        // 应用
        public bool GlobalAscii { get; set; } = false;
        public Dictionary<string, bool> AppAscii { get; set; } = new Dictionary<string, bool>();
        public bool AppsScanned { get; set; } = false;

        // 词库
        public bool DictAutoUpdate { get; set; } = true;
        public string DictTag { get; set; } = "";
        public string DictCheckedAt { get; set; } = "";

        // 简繁
        public bool Traditional { get; set; } = false;

        // 主题导入
        public Dictionary<string, Dictionary<string, string>> ImportedThemes { get; set; } = new Dictionary<string, Dictionary<string, string>>();
        public Dictionary<string, string> CustomTheme { get; set; } = null;

        public static List<AiAction> DefaultActions() => new List<AiAction>
        {
            new AiAction { Name = "翻译", Prompt = "把下面的文字翻译成英文；如果原文是英文或其他外文，就翻译成简体中文。只输出译文，不要解释。" },
            new AiAction { Name = "润色", Prompt = "在不改变原意和语言的前提下润色下面的文字，让它更通顺、自然、得体。只输出润色后的文字。" },
            new AiAction { Name = "粤语", Prompt = "把下面的文字改写成地道的粤语口语（繁体字）。只输出结果。" },
        };

        [ScriptIgnore]
        public string AiKey
        {
            get
            {
                if (string.IsNullOrEmpty(AiKeyProtected)) return "";
                try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(AiKeyProtected), null, DataProtectionScope.CurrentUser)); }
                catch { return ""; }
            }
            set
            {
                AiKeyProtected = string.IsNullOrEmpty(value) ? "" :
                    Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
            }
        }

        static Settings _current;
        public static Settings Current => _current ?? (_current = Json.Load<Settings>(Paths.SettingsFile));
        public static void Reload() { _current = null; }
        public void Save() => Json.Save(Paths.SettingsFile, this);

        public bool StatsEnabled
        {
            get => File.Exists(Paths.StatsFlag);
            set { if (value) File.WriteAllText(Paths.StatsFlag, "1"); else if (File.Exists(Paths.StatsFlag)) File.Delete(Paths.StatsFlag); }
        }
    }

    /// <summary>Runs the RIME deployer so a changed configuration takes effect.</summary>
    public static class Deploy
    {
        public static void Run(bool wait = false)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(Paths.Deployer, "/deploy") { UseShellExecute = false, CreateNoWindow = true });
                if (wait) p?.WaitForExit(120000);
            }
            catch (Exception e) { Log.Write("deploy: " + e.Message); }
        }
    }

    /// <summary>
    /// Writes weasel.custom.yaml from the settings model. The file is owned by 一维输入法设置;
    /// anything the user wrote by hand is kept in a backup the first time.
    /// </summary>
    public static class WeaselConfig
    {
        const string Marker = "# 由「一维输入法设置」生成";

        public static void Write(Settings s)
        {
            var path = Paths.WeaselCustom;
            Directory.CreateDirectory(Paths.UserDir);
            if (File.Exists(path) && !File.ReadAllText(path).Contains(Marker))
                File.Copy(path, path + ".bak-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true);

            var y = new StringBuilder();
            y.AppendLine(Marker + "，手动修改会被覆盖。需要额外补丁请写在 yiwei.custom.yaml 之外的方案文件里。");
            y.AppendLine("patch:");
            void P(string key, string value) => y.Append("  \"").Append(key).Append("\": ").AppendLine(value);
            P("style/color_scheme", Q(s.ColorScheme));
            P("style/color_scheme_dark", Q(s.ColorSchemeDark));
            P("style/horizontal", B(s.Horizontal));
            P("style/vertical_text", B(s.VerticalText));
            P("style/inline_preedit", B(s.InlinePreedit));
            P("style/font_point", s.FontPoint.ToString());
            P("style/label_font_point", Math.Max(8, s.FontPoint - 3).ToString());
            P("style/comment_font_point", Math.Max(8, s.FontPoint - 3).ToString());
            P("style/font_face", Q(s.FontFace));
            P("style/comment_font_face", Q(s.FontFace));
            P("style/layout/corner_radius", s.CornerRadius.ToString());
            P("style/layout/round_corner", s.HilitedCornerRadius.ToString());
            P("global_ascii", B(s.GlobalAscii));
            foreach (var kv in s.AppAscii)
                P("app_options/" + kv.Key.ToLowerInvariant().Replace("/", "_"), "{ascii_mode: " + B(kv.Value) + "}");
            var themes = new Dictionary<string, Dictionary<string, string>>(s.ImportedThemes);
            if (s.CustomTheme != null) themes["yiwei_custom"] = s.CustomTheme;
            foreach (var t in themes)
            {
                var parts = new List<string> { "color_format: argb" };
                foreach (var c in t.Value)
                {
                    var key = c.Key == "hilited_candidate_label_color" ? "hilited_label_color" : c.Key;
                    parts.Add(key == "name" || key == "author" ? key + ": " + Q(c.Value) : key + ": " + c.Value);
                }
                P("preset_color_schemes/" + t.Key, "{" + string.Join(", ", parts) + "}");
            }
            File.WriteAllText(path, y.ToString(), new UTF8Encoding(false));

            // Simplified / Traditional default for rime-ice schemas
            WriteSchemaPatch("rime_ice", s.Traditional);
        }

        static void WriteSchemaPatch(string schema, bool traditional)
        {
            var path = Path.Combine(Paths.UserDir, schema + ".custom.yaml");
            if (File.Exists(path) && !File.ReadAllText(path).Contains(Marker)) return; // user owns it
            var y = Marker + "\npatch:\n  \"switches/@2/reset\": " + (traditional ? "1" : "0") + "\n";
            File.WriteAllText(path, y, new UTF8Encoding(false));
        }

        static string Q(string v) => "\"" + (v ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        static string B(bool v) => v ? "true" : "false";

        /// <summary>Reads preset_color_schemes ids and names from the shared weasel.yaml.</summary>
        public static List<KeyValuePair<string, string>> BuiltinSchemes()
        {
            var list = new List<KeyValuePair<string, string>>();
            try
            {
                var path = Path.Combine(Paths.SharedDataDir, "weasel.yaml");
                bool inPresets = false; string id = null;
                foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var line = raw.Replace("\t", "  ");
                    if (line.StartsWith("preset_color_schemes:")) { inPresets = true; continue; }
                    if (!inPresets) continue;
                    if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && !line.StartsWith("#")) break;
                    if (line.StartsWith("  ") && !line.StartsWith("   ") && line.TrimEnd().EndsWith(":")) { id = line.Trim().TrimEnd(':'); continue; }
                    var t = line.Trim();
                    if (id != null && t.StartsWith("name:"))
                    {
                        var name = t.Substring(5).Trim().Trim('"', '\'');
                        var hash = name.IndexOf(" #"); if (hash > 0) name = name.Substring(0, hash).Trim().Trim('"');
                        list.Add(new KeyValuePair<string, string>(id, name)); id = null;
                    }
                }
            }
            catch (Exception e) { Log.Write("schemes: " + e.Message); }
            if (list.Count == 0) list.Add(new KeyValuePair<string, string>("yiwei_light", "一维 · 浅色"));
            foreach (var t in Settings.Current.ImportedThemes)
                list.Add(new KeyValuePair<string, string>(t.Key, (t.Value.TryGetValue("name", out var n) ? n : t.Key) + "（导入）"));
            if (Settings.Current.CustomTheme != null) list.Add(new KeyValuePair<string, string>("yiwei_custom", "我的配色"));
            return list;
        }

        /// <summary>Reads colours of a builtin scheme for the preview.</summary>
        public static Dictionary<string, uint> SchemeColors(string id)
        {
            var d = new Dictionary<string, uint>();
            if (Settings.Current.ImportedThemes.TryGetValue(id, out var imp) || (id == "yiwei_custom" && (imp = Settings.Current.CustomTheme) != null))
            {
                foreach (var kv in imp) if (TryColor(kv.Value, out var c)) d[kv.Key] = c;
                return d;
            }
            try
            {
                var path = Path.Combine(Paths.SharedDataDir, "weasel.yaml");
                bool inside = false; string format = "abgr";
                foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var line = raw.Replace("\t", "  ");
                    if (line.StartsWith("  " + id + ":")) { inside = true; continue; }
                    if (!inside) continue;
                    if (!line.StartsWith("    ")) break;
                    var t = line.Trim(); var i = t.IndexOf(':'); if (i < 0) continue;
                    var k = t.Substring(0, i).Trim(); var v = t.Substring(i + 1).Trim();
                    var hash = v.IndexOf('#'); if (hash >= 0) v = v.Substring(0, hash).Trim();
                    if (k == "color_format") { format = v.Trim('"'); continue; }
                    if (TryColor(v, out var c)) d[k] = c;
                }
                if (format != "argb")
                {
                    // abgr (weasel default) -> argb ; rgba -> argb
                    foreach (var k in d.Keys.ToList())
                    {
                        uint v = d[k];
                        if (format == "rgba") d[k] = (v >> 8) | ((v & 0xFF) << 24);
                        else d[k] = (v & 0xFF00FF00) | ((v & 0xFF) << 16) | ((v >> 16) & 0xFF);
                    }
                }
            }
            catch { }
            return d;
        }

        public static bool TryColor(string v, out uint c)
        {
            c = 0; v = (v ?? "").Trim().Trim('"');
            if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || v.StartsWith("#")) v = v.TrimStart('#').Substring(v.StartsWith("#") ? 0 : 2);
            else return false;
            if (v.Length == 6) v = "FF" + v;
            return v.Length == 8 && uint.TryParse(v, System.Globalization.NumberStyles.HexNumber, null, out c);
        }
    }
}
