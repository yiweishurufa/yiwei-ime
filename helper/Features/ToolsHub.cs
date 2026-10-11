using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Yiwei
{
    /// <summary>
    /// 「工具」组的设置，单独存在 &lt;user&gt;/yiwei/tools.json（不动主 settings.json，免得和其它功能抢字段）。
    /// </summary>
    public class ToolSettings
    {
        /// <summary>长按 Alt + 字母的工具手势（T 文字工具、S 截图翻译、M Markdown、/ 补全）。</summary>
        public bool AltGestures { get; set; } = true;
        /// <summary>动作 id → 全局快捷键（"Ctrl+Alt+F" 这种写法）。空 = 没绑定。</summary>
        public Dictionary<string, string> Hotkeys { get; set; } = new Dictionary<string, string>();
        /// <summary>翻译：auto（配了 AI 用 AI，否则免费接口）/ ai / free。</summary>
        public string TranslateEngine { get; set; } = "auto";
        /// <summary>翻译目标：auto（中文→英文，其它→中文）/ zh / en / ja / ko。</summary>
        public string TranslateTarget { get; set; } = "auto";
        /// <summary>W30 加注拼音样式：after / line / only / ruby。</summary>
        public string PinyinStyle { get; set; } = "after";
        /// <summary>W34 把 Windows 剪贴板历史（Win+V）并入一维剪贴板历史。</summary>
        public bool MergeWinClipboard { get; set; } = false;
        /// <summary>W35 邮箱后缀（空 = 内置列表）和各后缀的使用次数（常用的排前面）。</summary>
        public List<string> EmailSuffixes { get; set; } = new List<string>();
        public Dictionary<string, int> EmailUse { get; set; } = new Dictionary<string, int>();
        /// <summary>W36 WebDAV（坚果云等）：地址、账号、DPAPI 加密的密码、自动同步、上次结果。</summary>
        public string DavUrl { get; set; } = "";
        public string DavUser { get; set; } = "";
        public string DavPass { get; set; } = "";
        public bool DavAuto { get; set; } = true;
        public string DavLast { get; set; } = "";
        /// <summary>W37 自动备份：开关、位置（local / onedrive / custom）、自定义目录、保留份数、上次时间。</summary>
        public bool AutoBackup { get; set; } = true;
        public string BackupWhere { get; set; } = "local";
        public string BackupDir { get; set; } = "";
        public int BackupKeep { get; set; } = 10;
        public string BackupLast { get; set; } = "";
        /// <summary>W22/W23 取色皮肤：跟随壁纸、上次壁纸（路径|时间）、当前取到的颜色。</summary>
        public bool FollowWallpaper { get; set; } = false;
        public string WallpaperStamp { get; set; } = "";
        public string ThemeRgb { get; set; } = "";
        /// <summary>W40 低配机模式；开启前的设置（关闭时恢复）；是否已提示过。</summary>
        public bool LowSpec { get; set; } = false;
        public string LowSpecSaved { get; set; } = "";
        public bool LowSpecHinted { get; set; } = false;

        static ToolSettings _current;
        static string FilePath => System.IO.Path.Combine(Paths.YiweiDir, "tools.json");

        public static ToolSettings Current
        {
            get
            {
                if (_current != null) return _current;
                try { _current = Json.Load<ToolSettings>(FilePath); } catch (Exception e) { Log.Write("tools.json: " + e.Message); }
                if (_current == null) _current = new ToolSettings();
                if (_current.Hotkeys == null) _current.Hotkeys = new Dictionary<string, string>();
                if (_current.EmailSuffixes == null) _current.EmailSuffixes = new List<string>();
                if (_current.EmailUse == null) _current.EmailUse = new Dictionary<string, int>();
                return _current;
            }
        }

        public void Save()
        {
            try { Json.Save(FilePath, this); } catch (Exception e) { Log.Write("tools.json save: " + e.Message); }
        }
    }

    /// <summary>One tool the user can trigger from 文字工具菜单 / a hotkey / the pipe (/tool &lt;id&gt;).</summary>
    public sealed class ToolAction
    {
        public string Id, Name, Group, Help;
        public Keys AltKey = Keys.None;   // 长按 Alt + 这个键
        public Action Run;
        public bool InMenu = true;        // 出现在长按 Alt + T 的文字工具菜单里
    }

    /// <summary>
    /// 工具组的入口：动作注册表、命令（/tool …）、长按 Alt 手势、可自定义的全局快捷键（RegisterHotKey）。
    /// Program.cs 只调用 Start() 和 TryExecute()，Native.cs 的 Alt 手势只调用 AltHold()。
    /// </summary>
    public static class ToolsHub
    {
        public static readonly List<ToolAction> Actions = new List<ToolAction>();
        static readonly List<Action> _starters = new List<Action>();
        static HotkeyWindow _hk;

        public static void Register(ToolAction a) { if (!Actions.Any(x => x.Id == a.Id)) Actions.Add(a); }
        public static ToolAction Find(string id) => Actions.FirstOrDefault(a => a.Id == id);

        static bool _registered;
        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            ToolsRegistry.RegisterAll(_starters);
        }

        public static void Start()
        {
            EnsureRegistered();
            try { _hk = new HotkeyWindow(); _hk.Apply(); } catch (Exception e) { Log.Write("tools hotkeys: " + e.Message); }
            foreach (var s in _starters) { try { s(); } catch (Exception e) { Log.Write("tools start: " + e.Message); } }
        }

        /// <summary>Re-register the global hotkeys after the user changed them. Returns ids that could not be registered.</summary>
        public static List<string> ReapplyHotkeys() => _hk?.Apply() ?? new List<string>();

        public static void Run(string id)
        {
            EnsureRegistered();
            var a = Find(id);
            if (a == null) { Log.Write("tool not found: " + id); return; }
            try { a.Run(); } catch (Exception e) { Log.Write("tool " + id + ": " + e); Tray.Balloon(a.Name, e.Message); }
        }

        /// <summary>Pipe / command line: "/tool &lt;id&gt;", "/tools" (menu). Returns true when handled.</summary>
        public static bool TryExecute(string command)
        {
            if (command == null) return false;
            if (command == "/tools") { EnsureRegistered(); TextToolsMenu.Show(); return true; }
            if (command.StartsWith("/tool "))
            {
                Run(command.Substring(6).Trim());
                return true;
            }
            return false;
        }

        /// <summary>Called by the Alt-hold gesture in the keyboard hook. True = consumed (the action is queued on the UI thread).</summary>
        public static bool AltHold(Keys vk)
        {
            if (!ToolSettings.Current.AltGestures) return false;
            EnsureRegistered();
            var a = Actions.FirstOrDefault(x => x.AltKey == vk && x.AltKey != Keys.None);
            if (a == null) return false;
            Program.Ui.BeginInvoke(new Action(() => Run(a.Id)));
            return true;
        }

        // ---------- hotkey text ("Ctrl+Alt+F") ----------

        public static bool TryParse(string text, out uint mods, out Keys key)
        {
            mods = 0; key = Keys.None;
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (var raw in text.Split('+'))
            {
                var p = raw.Trim();
                switch (p.ToLowerInvariant())
                {
                    case "ctrl": case "control": mods |= 0x2; continue;
                    case "alt": mods |= 0x1; continue;
                    case "shift": mods |= 0x4; continue;
                    case "win": mods |= 0x8; continue;
                }
                if (p.Length == 1 && char.IsLetterOrDigit(p[0])) { key = char.IsDigit(p[0]) ? Keys.D0 + (p[0] - '0') : Keys.A + (char.ToUpperInvariant(p[0]) - 'A'); continue; }
                if (Enum.TryParse(p, true, out Keys k)) { key = k; continue; }
                return false;
            }
            return key != Keys.None && mods != 0;
        }

        public static string Format(Keys key, bool ctrl, bool alt, bool shift, bool win)
        {
            var parts = new List<string>();
            if (ctrl) parts.Add("Ctrl"); if (alt) parts.Add("Alt"); if (shift) parts.Add("Shift"); if (win) parts.Add("Win");
            string k = key >= Keys.D0 && key <= Keys.D9 ? ((char)('0' + (key - Keys.D0))).ToString() : key.ToString();
            parts.Add(k);
            return string.Join("+", parts);
        }

        sealed class HotkeyWindow : NativeWindow
        {
            [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
            [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
            const int WM_HOTKEY = 0x0312;
            const uint MOD_NOREPEAT = 0x4000;
            readonly Dictionary<int, string> _ids = new Dictionary<int, string>();

            public HotkeyWindow() { CreateHandle(new CreateParams { Parent = new IntPtr(-3) /* HWND_MESSAGE */ }); }

            public List<string> Apply()
            {
                foreach (var id in _ids.Keys) UnregisterHotKey(Handle, id);
                _ids.Clear();
                var failed = new List<string>();
                int n = 0x5900;
                foreach (var kv in ToolSettings.Current.Hotkeys)
                {
                    if (!TryParse(kv.Value, out var mods, out var key)) continue;
                    if (RegisterHotKey(Handle, ++n, mods | MOD_NOREPEAT, (uint)key)) _ids[n] = kv.Key;
                    else { failed.Add(kv.Key); Log.Write("hotkey taken: " + kv.Value + " (" + kv.Key + ")"); }
                }
                return failed;
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY && _ids.TryGetValue(m.WParam.ToInt32(), out var id))
                {
                    // wait for the user to let go of the chord, so Ctrl/Alt do not leak into what we send next
                    Program.Ui.BeginInvoke(new Action(() => { WaitKeysUp(); Run(id); }));
                    return;
                }
                base.WndProc(ref m);
            }
        }

        /// <summary>Waits (≤ 1 s) until Ctrl / Alt / Shift / Win are released.</summary>
        public static void WaitKeysUp()
        {
            for (int i = 0; i < 100; i++)
            {
                bool any = false;
                foreach (var k in new[] { Keys.ControlKey, Keys.Menu, Keys.ShiftKey, Keys.LWin, Keys.RWin })
                    if ((Native.GetAsyncKeyState((int)k) & 0x8000) != 0) any = true;
                if (!any) return;
                TextOut.Pump(10);
            }
        }

        // ---------- selection helpers shared by the text tools ----------

        /// <summary>Copies the selection, transforms it, pastes the result back over it.</summary>
        public static void TransformSelection(string title, Func<string, string> f)
        {
            string sel = "";
            try { sel = TextOut.CopySelection(); } catch { }
            if (string.IsNullOrEmpty(sel)) { Tray.Balloon(title, "先选中要处理的文字，再用这个功能"); return; }
            string result;
            try { result = f(sel); }
            catch (Exception e) { Tray.Balloon(title, e.Message); return; }
            if (result == null || result == sel) { if (result == sel) Tray.Balloon(title, "选中的文字不需要改动"); return; }
            TextOut.Replace(result);
        }
    }
}
