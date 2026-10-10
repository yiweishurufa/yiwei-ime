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
                            foreach (var name in new[] { "YiweiRoot", "WeaselRoot" })
                            {
                                var v = k?.GetValue(name) as string;
                                if (!string.IsNullOrWhiteSpace(v) && Directory.Exists(v)) return v;
                            }
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
        public static string AutoPairOff => Path.Combine(YiweiDir, "autopair.disabled");
        public static string RegretOff => Path.Combine(YiweiDir, "regret.disabled");
        public static string PanguFlag => Path.Combine(YiweiDir, "pangu.enabled");
        public static string WeaselCustom => Path.Combine(UserDir, "weasel.custom.yaml");
        public static string DefaultCustom => Path.Combine(UserDir, "default.custom.yaml");
        public static string Deployer => Pick("YiweiDeployer.exe", "WeaselDeployer.exe");
        public static string Server => Pick("YiweiServer.exe", "WeaselServer.exe");

        /// <summary>The renamed executable when it exists, otherwise the original Weasel name.</summary>
        static string Pick(string preferred, string legacy)
        {
            var dir = InstallDir;
            var p = Path.Combine(dir, preferred);
            if (File.Exists(p)) return p;
            var l = Path.Combine(dir, legacy);
            return File.Exists(l) ? l : p;
        }
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

        public bool AiEnabled { get; set; } = false;
        /// <summary>Instruction typed for the 自定义 mode last time.</summary>
        public string AiCustomPrompt { get; set; } = "";

        // 版本与首次引导
        public int SettingsVersion { get; set; } = 0;
        public bool FirstRunDone { get; set; } = false;

        // 常规
        public bool ImeToast { get; set; } = true;
        public bool ImeToastPolling { get; set; } = false;

        // 输入方案（雾凇拼音 schema id）
        public string Schema { get; set; } = "rime_ice";

        // 快捷键
        public bool AltHotkeys { get; set; } = true;
        public int HoldMs { get; set; } = 300;
        public bool SnippetsHotkey { get; set; } = true;
        public bool AiHotkey { get; set; } = true;
        public bool OcrHotkey { get; set; } = true;
        public bool SymbolsHotkey { get; set; } = true;
        public bool AddWordHotkey { get; set; } = true;
        public bool HandwritingHotkey { get; set; } = true;
        public List<string> AltBlocklist { get; set; } = DefaultBlocklist();
        public bool AltSkipBorderlessFullscreen { get; set; } = false;

        // 外观
        public string Accent { get; set; } = "pill";
        public bool FollowSystemDark { get; set; } = true;
        public int CandidateCount { get; set; } = 5;
        public string ColorScheme { get; set; } = "yiwei_pill";
        public string ColorSchemeDark { get; set; } = "yiwei_pill_dark";
        public bool Horizontal { get; set; } = true;
        public bool VerticalText { get; set; } = false;
        public int FontPoint { get; set; } = 12;
        public string FontFace { get; set; } = "Microsoft YaHei UI";
        public int CornerRadius { get; set; } = 8;
        public int HilitedCornerRadius { get; set; } = 6;
        public bool InlinePreedit { get; set; } = true;

        // 应用
        public bool GlobalAscii { get; set; } = false;
        public Dictionary<string, bool> AppAscii { get; set; } = new Dictionary<string, bool>();
        public bool AppsScanned { get; set; } = false;

        // 词库
        public bool DictAutoUpdate { get; set; } = true;
        public bool AppAutoUpdate { get; set; } = true;
        public string DictTag { get; set; } = "";
        public string DictCheckedAt { get; set; } = "";

        // 剪贴板历史（Features/ClipHistory.cs）：保留天数 0 = 不过期
        public bool ClipHistory { get; set; } = true;
        public bool ClipHistorySaveToDisk { get; set; } = true;
        public int ClipKeepDays { get; set; } = 7;
        public List<string> ClipExcludeApps { get; set; } = new List<string>();

        // 隐私（Features/PrivacyGuard.cs）
        public bool PasswordAscii { get; set; } = true;
        public bool PrivateNoLearn { get; set; } = true;

        // 游戏模式（Features/GameMode.cs）
        public bool GameMode { get; set; } = true;
        public List<string> GameApps { get; set; } = new List<string>();

        // 语音输入（Features/Voice.cs）：rctrl / ralt / off；auto / zh / en / yue
        public string VoiceKey { get; set; } = "rctrl";
        public string VoiceLanguage { get; set; } = "auto";
        public bool VoiceRemoveFillers { get; set; } = true;
        public bool VoicePanguSpacing { get; set; } = true;

        // 简繁
        public bool Traditional { get; set; } = false;

        // 快捷输入、模糊音、生僻字（Features/RimeFeatures.cs）
        public RimeFeatureSettings Features { get; set; } = new RimeFeatureSettings();

        // 主题导入
        public Dictionary<string, Dictionary<string, string>> ImportedThemes { get; set; } = new Dictionary<string, Dictionary<string, string>>();
        public Dictionary<string, string> CustomTheme { get; set; } = null;

        public static List<string> DefaultBlocklist() => new List<string>
        {
            "mstsc.exe", "vmconnect.exe", "vmware.exe", "vmware-vmx.exe", "virtualboxvm.exe", "parsecd.exe", "moonlight.exe",
            "anydesk.exe", "teamviewer.exe", "todesk.exe", "sunloginclient.exe",
            "steam.exe", "steamwebhelper.exe", "epicgameslauncher.exe", "battle.net.exe", "riotclientservices.exe", "eadesktop.exe", "wegame.exe",
        };

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
        static readonly object Gate = new object();
        public static Settings Current
        {
            get
            {
                if (_current != null) return _current;
                lock (Gate)
                {
                    if (_current != null) return _current;
                    bool existed = File.Exists(Paths.SettingsFile);
                    var s = Json.Load<Settings>(Paths.SettingsFile);
                    if (s.SettingsVersion < 2)
                    {
                        // Settings written by 0.1: the user has used the helper already, keep AI on if it was set up.
                        if (existed)
                        {
                            s.FirstRunDone = true;
                            s.AiEnabled = !string.IsNullOrEmpty(s.AiKeyProtected) || (s.AiBaseUrl ?? "").Contains("localhost") || (s.AiBaseUrl ?? "").Contains("127.0.0.1");
                        }
                        s.SettingsVersion = 2;
                        if (existed) try { s.Save(); } catch { }
                    }
                    if (s.SettingsVersion < 3)
                    {
                        // 0.1.0.20: smaller candidate font by default; keep a size the user picked themselves.
                        if (s.FontPoint == 14) s.FontPoint = 12;
                        s.SettingsVersion = 3;
                        if (existed) { try { s.Save(); } catch { } Rime.ApplySoon(4000); }
                    }
                    if (s.SettingsVersion < 4)
                    {
                        // 0.1.0.3x（2026-10 视觉改版）：默认候选框改为「蓝色胶囊」。仍是旧默认（青碧）的换成新默认，挑过别的配色的不动。
                        if (s.ColorScheme == "yiwei_qingbi" && s.Accent == "qingbi")
                        {
                            s.ColorScheme = "yiwei_pill"; s.Accent = "pill";
                            if (s.ColorSchemeDark == "yiwei_qingbi_dark") s.ColorSchemeDark = "yiwei_pill_dark";
                        }
                        s.SettingsVersion = 4;
                        if (existed) { try { s.Save(); } catch { } Rime.ApplyWhenIdle(); }
                    }
                    if (s.AltBlocklist == null) s.AltBlocklist = DefaultBlocklist();
                    if (s.AiActions == null || s.AiActions.Count == 0) s.AiActions = DefaultActions();
                    if (s.AppAscii == null) s.AppAscii = new Dictionary<string, bool>();
                    if (s.ImportedThemes == null) s.ImportedThemes = new Dictionary<string, Dictionary<string, string>>();
                    if (s.CandidateCount != 5 && s.CandidateCount != 7 && s.CandidateCount != 9) s.CandidateCount = 5;
                    if (Brand.Find(s.Accent) == null) s.Accent = "pill";
                    return _current = s;
                }
            }
        }
        public static void Reload() { _current = null; }
        public void Save() { lock (Gate) Json.Save(Paths.SettingsFile, this); }

        /// <summary>Back to defaults, keeping the encrypted key out of it (the user asked for a reset).</summary>
        public static void Reset()
        {
            lock (Gate)
            {
                var s = new Settings { SettingsVersion = 3, FirstRunDone = true };
                s.Save();
                _current = s;
            }
        }

        /// <summary>上屏后 Ctrl+Backspace 反悔重选（yiwei_regret.lua 读这个标记文件，默认开启）。</summary>
        [ScriptIgnore]
        public bool Regret
        {
            get => !File.Exists(Paths.RegretOff);
            set { if (!value) { Directory.CreateDirectory(Paths.YiweiDir); File.WriteAllText(Paths.RegretOff, "1"); } else if (File.Exists(Paths.RegretOff)) File.Delete(Paths.RegretOff); }
        }

        /// <summary>括号自动配对（yiwei_autopair.lua 读这个标记文件，默认开启）。</summary>
        public bool AutoPair
        {
            get => !File.Exists(Paths.AutoPairOff);
            set { if (!value) { Directory.CreateDirectory(Paths.YiweiDir); File.WriteAllText(Paths.AutoPairOff, "1"); } else if (File.Exists(Paths.AutoPairOff)) File.Delete(Paths.AutoPairOff); }
        }

        /// <summary>中英之间自动加空格（yiwei_pangu*.lua 读这个标记文件，默认关闭）。</summary>
        public bool PanguSpacing
        {
            get => File.Exists(Paths.PanguFlag);
            set { if (value) File.WriteAllText(Paths.PanguFlag, "1"); else if (File.Exists(Paths.PanguFlag)) File.Delete(Paths.PanguFlag); }
        }

        public bool StatsEnabled
        {
            get => File.Exists(Paths.StatsFlag);
            set { if (value) File.WriteAllText(Paths.StatsFlag, "1"); else if (File.Exists(Paths.StatsFlag)) File.Delete(Paths.StatsFlag); }
        }
    }

    /// <summary>Runs the RIME deployer so a changed configuration takes effect.</summary>
    public static class Deploy
    {
        /// <summary>Raised (on a worker thread) when a deployment starts / finishes: (finished, ok).</summary>
        public static event Action<bool, bool> StateChanged;

        /// <summary>What is running now: "/deploy", "/sync" or "external" (a deployer started by the installer / by hand).</summary>
        public static string Kind { get; private set; } = "";
        public static DateTime StartedAt { get; private set; }
        public static bool Running { get; private set; }

        public static void Run(bool wait = false) => Run("/deploy", wait);

        public static void Run(string args, bool wait) => Start(args, wait, 10 * 60 * 1000);

        /// <summary>Runs the deployer and waits; false when it could not start or did not finish in time.</summary>
        public static bool RunAndWait(string args = "/deploy", int timeoutMs = 10 * 60 * 1000) => Start(args, true, timeoutMs);

        static bool Start(string args, bool wait, int timeoutMs)
        {
            try
            {
                if (!File.Exists(Paths.Deployer)) { Log.Write("deploy: deployer not found " + Paths.Deployer); return false; }
                var p = Process.Start(new ProcessStartInfo(Paths.Deployer, args) { UseShellExecute = false, CreateNoWindow = true });
                if (p == null) return false;
                Began(args);
                if (wait)
                {
                    bool ok = false;
                    try { ok = p.WaitForExit(timeoutMs); if (!ok) Log.Write("deploy: timeout"); } catch { }
                    Ended(args, ok);
                    return ok;
                }
                System.Threading.Tasks.Task.Run(() => { bool ok = false; try { ok = p.WaitForExit(timeoutMs); } catch { } Ended(args, ok); });
                return true;
            }
            catch (Exception e) { Log.Write("deploy: " + e.Message); Fire(true, false); return false; }
        }

        /// <summary>Lets the watcher report a deployer someone else started (installer, 开始菜单「重新部署」).</summary>
        public static void External(bool finished, bool ok) { if (finished) Ended("external", ok); else Began("external"); }

        static void Began(string kind)
        {
            Kind = kind; StartedAt = DateTime.Now; Running = true;
            Fire(false, true);
        }

        static void Ended(string kind, bool ok)
        {
            var took = DateTime.Now - StartedAt;
            Running = false;
            if (ok && kind != "/sync") DeployTimes.Record(took);
            Fire(true, ok);
        }

        static void Fire(bool finished, bool ok) { try { StateChanged?.Invoke(finished, ok); } catch { } }
    }

    /// <summary>
    /// How long deployments take on this machine, to show a realistic estimate. Kept per "profile" (schema + 大字表),
    /// since only a changed dictionary makes the deployer recompile tables.
    /// </summary>
    public static class DeployTimes
    {
        public class Data { public Dictionary<string, List<double>> Seconds { get; set; } = new Dictionary<string, List<double>>(); }
        static string FilePath => Path.Combine(Paths.YiweiDir, "deploy-times.json");
        static readonly object Gate = new object();

        static string Profile
        {
            get { var s = Settings.Current; return s.Schema + (RimeFeatures.Of(s).BigCharset ? "+big" : ""); }
        }

        /// <summary>No compiled tables yet: the first deployment compiles the whole dictionary.</summary>
        public static bool FirstTime
        {
            get
            {
                try
                {
                    var build = Path.Combine(Paths.UserDir, "build");
                    return !Directory.Exists(build) || Directory.GetFiles(build, "*.table.bin").Length == 0;
                }
                catch { return false; }
            }
        }

        public static void Record(TimeSpan took)
        {
            lock (Gate)
            {
                try
                {
                    var d = Json.Load<Data>(FilePath);
                    if (d.Seconds == null) d.Seconds = new Dictionary<string, List<double>>();
                    if (!d.Seconds.TryGetValue(Profile, out var list) || list == null) d.Seconds[Profile] = list = new List<double>();
                    list.Add(Math.Round(took.TotalSeconds, 1));
                    while (list.Count > 8) list.RemoveAt(0);
                    Json.Save(FilePath, d);
                }
                catch (Exception e) { Log.Write("deploy times: " + e.Message); }
            }
        }

        /// <summary>Expected duration in seconds: first-time guess, else the slower half of recent runs.</summary>
        public static double Estimate()
        {
            bool big = RimeFeatures.Of(Settings.Current).BigCharset;
            if (FirstTime) return big ? 120 : 75;
            lock (Gate)
            {
                try
                {
                    var d = Json.Load<Data>(FilePath);
                    if (d.Seconds != null && d.Seconds.TryGetValue(Profile, out var list) && list != null && list.Count > 0)
                    {
                        var sorted = list.OrderBy(x => x).ToList();
                        return Math.Max(3, sorted[Math.Min(sorted.Count - 1, sorted.Count * 3 / 4)]);
                    }
                }
                catch { }
            }
            return big ? 30 : 10;
        }
    }

    /// <summary>How long the user has not touched keyboard or mouse; used to run heavy deployments when nobody is typing.</summary>
    public static class Idle
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        public static TimeSpan For
        {
            get
            {
                var li = new LASTINPUTINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(LASTINPUTINFO)) };
                if (!GetLastInputInfo(ref li)) return TimeSpan.Zero;
                return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - li.dwTime));
            }
        }

        /// <summary>Waits until nobody has typed for <paramref name="quiet"/>, but never longer than <paramref name="max"/>.</summary>
        public static async System.Threading.Tasks.Task WaitAsync(TimeSpan quiet, TimeSpan max, System.Threading.CancellationToken ct = default(System.Threading.CancellationToken))
        {
            var until = DateTime.Now + max;
            while (DateTime.Now < until && For < quiet)
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes weasel.custom.yaml from the settings model. The file is owned by 一维输入法设置;
    /// anything the user wrote by hand is kept in a backup the first time.
    /// </summary>
    public static class WeaselConfig
    {
        const string Marker = "# 由「一维输入法设置」生成";

        /// <summary>Whether the generated file already names the light / dark schemes the current settings want.</summary>
        public static bool UpToDateForDarkMode(Settings s)
        {
            try
            {
                if (!File.Exists(Paths.WeaselCustom)) return true;
                var y = File.ReadAllText(Paths.WeaselCustom, Encoding.UTF8);
                if (!y.Contains(Marker)) return true; // not ours
                return y.Contains("\"style/color_scheme\": " + Q(s.ColorScheme)) && y.Contains("\"style/color_scheme_dark\": " + Q(DarkSchemeFor(s)));
            }
            catch { return true; }
        }

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
            // 深浅色由小狼毫自己跟随系统切换（WM_SETTINGCHANGE → color_scheme / color_scheme_dark），不需要重新部署
            var light = s.ColorScheme;
            var dark = DarkSchemeFor(s);
            P("style/color_scheme", Q(light));
            P("style/color_scheme_dark", Q(dark));
            if (dark == AutoDarkId) P("preset_color_schemes/" + AutoDarkId, AutoDarkYaml(light));
            P("style/display_tray_icon", "false"); // 托盘只显示一维助手的合并图标
            P("style/horizontal", B(s.Horizontal));
            P("style/vertical_text", B(s.VerticalText));
            P("style/candidate_list_layout", Q(s.Horizontal ? "linear" : "stacked"));
            P("style/text_orientation", Q(s.VerticalText ? "vertical" : "horizontal"));
            P("style/inline_preedit", B(s.InlinePreedit));
            P("style/font_point", s.FontPoint.ToString());
            bool pill = Brand.IsPill(light);
            P("style/label_font_point", Math.Max(8, s.FontPoint - (pill ? 3 : 2)).ToString()); // 胶囊：序号小号灰色
            P("style/comment_font_point", Math.Max(8, s.FontPoint - 2).ToString());
            P("style/font_face", Q(RimeFeatures.FontFaceChain(s)));
            P("style/comment_font_face", Q(RimeFeatures.FontFaceChain(s)));
            P("style/layout/corner_radius", s.CornerRadius.ToString());
            P("style/layout/round_corner", HilitedRadius(s).ToString()); // 小狼毫把超过半高的圆角收成半高，99 = 胶囊
            if (pill) P("style/layout/hilite_padding_x", "8");
            foreach (var b in Brand.Palette)
                foreach (var d in new[] { false, true })
                    P("preset_color_schemes/" + Brand.SchemeId(b.Id, d), Brand.WeaselScheme(b, d));
            P("global_ascii", B(s.GlobalAscii));
            AppModes.WritePatches(s, P); // app_options/<exe>: {ascii_mode, vim_mode}
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

            // Simplified / Traditional default + 快捷输入 / 模糊音 / 大字表 for rime-ice schemas
            RimeFeatures.EnsureBigDict(s);
            foreach (var schema in RimeFeatures.PatchedSchemas) WriteSchemaPatch(schema, s);
        }

        /// <summary>Generated dark version of a light scheme that has none (imported themes, 我的配色, classic schemes).</summary>
        public const string AutoDarkId = "yiwei_auto_dark";
        public const string AutoChoice = "auto";

        /// <summary>
        /// The scheme used in dark mode. Following the system: the brand's own dark variant (墨线五色), yiwei_light → yiwei_dark,
        /// a scheme that is dark already stays, otherwise a dark version is generated from its accent — unless the user picked
        /// a non-brand dark scheme by hand. Not following: the chosen dark scheme.
        /// </summary>
        /// <summary>Highlight corner radius actually written: 蓝色胶囊 is always a capsule.</summary>
        public static int HilitedRadius(Settings s) => Brand.IsPill(s.ColorScheme) ? 99 : s.HilitedCornerRadius;

        public static string DarkSchemeFor(Settings s)
        {
            var chosen = string.IsNullOrEmpty(s.ColorSchemeDark) ? AutoChoice : s.ColorSchemeDark;
            if (s.FollowSystemDark)
            {
                var b = Brand.FromScheme(s.ColorScheme);
                if (b != null) return Brand.SchemeId(b.Id, true);
                if (s.ColorScheme == "yiwei_light") return "yiwei_dark";
                if (chosen == AutoChoice || Brand.FromScheme(chosen) != null) return IsDarkScheme(s.ColorScheme) ? s.ColorScheme : AutoDarkId;
                return chosen;
            }
            return chosen == AutoChoice ? (IsDarkScheme(s.ColorScheme) ? s.ColorScheme : AutoDarkId) : chosen;
        }

        static double Luma(uint argb) => (0.2126 * ((argb >> 16) & 0xFF) + 0.7152 * ((argb >> 8) & 0xFF) + 0.0722 * (argb & 0xFF)) / 255.0;

        static double Saturation(uint argb)
        {
            double r = ((argb >> 16) & 0xFF) / 255.0, g = ((argb >> 8) & 0xFF) / 255.0, b = (argb & 0xFF) / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            return max <= 0 ? 0 : (max - min) / max;
        }

        public static bool IsDarkScheme(string id)
        {
            if (id == AutoDarkId) return true;
            var c = SchemeColors(id);
            return c.TryGetValue("back_color", out var back) && Luma(back) < 0.45;
        }

        /// <summary>The accent of a light scheme: its most saturated highlight colour, or null for a grey scheme.</summary>
        static BrandColor AccentOf(string lightId)
        {
            var c = SchemeColors(lightId);
            uint best = 0; double sat = 0.25;
            foreach (var k in new[] { "hilited_candidate_back_color", "hilited_label_color", "hilited_candidate_label_color", "hilited_candidate_text_color", "hilited_back_color", "border_color" })
                if (c.TryGetValue(k, out var v) && Saturation(v) > sat && Luma(v) > 0.08) { sat = Saturation(v); best = v; }
            if (best == 0) return null;
            // a pale tint (light highlight) → deepen it so it reads on a dark background
            if (Luma(best) > 0.7) best = Brand.Mix(best, 0xFF000000, 0.45);
            return new BrandColor("auto", "自动", best & 0xFFFFFF);
        }

        /// <summary>Colours of the generated dark scheme (ARGB), in the 墨线 dark style with the light scheme's accent.</summary>
        public static Dictionary<string, uint> AutoDarkColors(string lightId) =>
            Brand.SchemeArgb(AccentOf(lightId) ?? Brand.Find("shimo"), true);

        static string AutoDarkYaml(string lightId)
        {
            var parts = new List<string> { "name: \"自动深色\"", "author: \"一维输入法\"", "color_format: abgr" };
            foreach (var kv in AutoDarkColors(lightId)) parts.Add(kv.Key + ": " + Brand.ToWeasel(kv.Value));
            return "{" + string.Join(", ", parts) + "}";
        }

        /// <summary>The candidate-window scheme that is showing right now.</summary>
        public static string ActiveScheme(Settings s) => s.FollowSystemDark && SystemTheme.IsDark ? DarkSchemeFor(s) : s.ColorScheme;

        static void WriteSchemaPatch(string schema, Settings s)
        {
            var path = Path.Combine(Paths.UserDir, schema + ".custom.yaml");
            if (File.Exists(path) && !File.ReadAllText(path).Contains(Marker)) return; // user owns it
            var y = Marker + "\npatch:\n  \"switches/@2/reset\": " + (s.Traditional ? "1" : "0") + "\n" + RimeFeatures.PatchLines(schema, s);
            y += Grammar.PatchBlock(); // 语法模型（Features/Grammar.cs）
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
            if (list.Count == 0) { list.Add(new KeyValuePair<string, string>("yiwei_light", "一维 · 浅色")); list.Add(new KeyValuePair<string, string>("yiwei_dark", "一维 · 深色")); }
            list.RemoveAll(kv => Brand.FromScheme(kv.Key) != null);
            var brand = new List<KeyValuePair<string, string>>();
            foreach (var b in Brand.Palette)
            {
                brand.Add(new KeyValuePair<string, string>(Brand.SchemeId(b.Id, false), "一维 · " + b.Name));
                brand.Add(new KeyValuePair<string, string>(Brand.SchemeId(b.Id, true), "一维 · " + b.Name + "（深色）"));
            }
            list.InsertRange(0, brand);
            foreach (var t in Settings.Current.ImportedThemes)
                list.Add(new KeyValuePair<string, string>(t.Key, (t.Value.TryGetValue("name", out var n) ? n : t.Key) + "（导入）"));
            if (Settings.Current.CustomTheme != null) list.Add(new KeyValuePair<string, string>("yiwei_custom", "我的配色"));
            return list;
        }

        /// <summary>Reads colours of a builtin scheme for the preview.</summary>
        public static Dictionary<string, uint> SchemeColors(string id)
        {
            var d = new Dictionary<string, uint>();
            if (id == AutoDarkId) return AutoDarkColors(Settings.Current.ColorScheme);
            var brand = Brand.FromScheme(id);
            if (brand != null) return Brand.SchemeArgb(brand, id.EndsWith("_dark"));
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
