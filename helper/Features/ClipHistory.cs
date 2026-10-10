using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace Yiwei
{
    /// <summary>
    /// 剪贴板历史：最近 50 条文字，只存本机（DPAPI 加密，仅当前 Windows 用户可读），按设定天数自动过期。
    /// 不记录：密码管理器（KeePass、1Password、Bitwarden 等进程，以及带「不要记录」剪贴板标记的内容）、
    /// 浏览器无痕 / InPrivate 窗口、一维自己为 AI 取词或替换时临时用到的剪贴板。
    /// 在长按 Alt 的常用语面板里是最后一个分组「剪贴板」，长按 Alt + V 直接打开。
    /// </summary>
    public static class ClipHistory
    {
        public const int MaxItems = 50;
        const int MaxChars = 20000;

        public sealed class Entry
        {
            public string Text { get; set; } = "";
            public string At { get; set; } = "";
        }

        sealed class Store { public List<Entry> Items { get; set; } = new List<Entry>(); }

        static readonly List<Entry> Items = new List<Entry>();
        static Listener _listener;
        static long _suppressUntil;
        static bool _loaded;

        static string FilePath => Path.Combine(Paths.YiweiDir, "clipboard.dat");

        /// <summary>Process names whose clipboard content is never recorded (lower case, without .exe).</summary>
        public static readonly string[] PasswordManagers =
        {
            "keepass", "keepassxc", "keepassxc-proxy", "1password", "bitwarden", "lastpass", "enpass", "dashlane", "roboform",
            "nordpass", "keeper", "passwordsafe", "pwsafe", "proton pass", "protonpass", "authy", "authy desktop", "bitwarden-cli",
        };

        static readonly string[] PrivateTitles = { "InPrivate", "Incognito", "无痕", "隐私浏览", "隐身", "Private Browsing", "私密浏览" };

        public static void Start()
        {
            Load();
            if (_listener == null) { _listener = new Listener(); _listener.Changed += OnChanged; }
        }

        /// <summary>Called before the helper itself touches the clipboard (AI copy / paste), so that is not recorded.</summary>
        public static void Suppress(int ms = 1500) => _suppressUntil = Environment.TickCount + ms;

        public static List<Entry> Recent()
        {
            Load();
            Expire();
            return Items.ToList();
        }

        public static void Remove(string text)
        {
            Load();
            Items.RemoveAll(e => e.Text == text);
            Save();
        }

        /// <summary>Writes (or, when saving to disk is off, deletes) the encrypted file now.</summary>
        public static void Flush() { Load(); Save(); }

        public static void Clear()
        {
            Items.Clear();
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
        }

        static void OnChanged()
        {
            var s = Settings.Current;
            if (!s.ClipHistory) return;
            if (unchecked(_suppressUntil - Environment.TickCount) > 0) return;
            try
            {
                if (Excluded()) return;
                string text = null;
                for (int i = 0; i < 4 && text == null; i++)
                {
                    try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); else return; }
                    catch { System.Threading.Thread.Sleep(30); } // another app still holds the clipboard
                }
                if (string.IsNullOrWhiteSpace(text) || text.Length > MaxChars) return;
                Add(text);
            }
            catch (Exception e) { Log.Write("clipboard: " + e.Message); }
        }

        public static void Add(string text)
        {
            Load();
            Items.RemoveAll(e => e.Text == text);
            Items.Insert(0, new Entry { Text = text, At = DateTime.Now.ToString("o") });
            if (Items.Count > MaxItems) Items.RemoveRange(MaxItems, Items.Count - MaxItems);
            Save();
        }

        /// <summary>True when the current clipboard content must not be recorded.</summary>
        static bool Excluded()
        {
            // Formats password managers and Windows' own clipboard history use to say "do not keep this".
            if (HasFormat("ExcludeClipboardContentFromMonitorProcessing") || HasFormat("Clipboard Viewer Ignore")) return true;
            if (DwordFormatIsZero("CanIncludeInClipboardHistory")) return true;
            var owner = OwnerProcess();
            if (owner.Length > 0 && (PasswordManagers.Contains(owner) || Settings.Current.ClipExcludeApps.Any(a => string.Equals(a.Replace(".exe", ""), owner, StringComparison.OrdinalIgnoreCase))))
                return true;
            var title = ForegroundTitle();
            if (PrivateTitles.Any(t => title.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)) return true;
            return false;
        }

        static bool HasFormat(string name)
        {
            uint id = RegisterClipboardFormat(name);
            return id != 0 && IsClipboardFormatAvailable(id);
        }

        static bool DwordFormatIsZero(string name)
        {
            try
            {
                var d = Clipboard.GetData(name);
                if (d is MemoryStream ms && ms.Length >= 4) { var b = ms.ToArray(); return BitConverter.ToInt32(b, 0) == 0; }
            }
            catch { }
            return false;
        }

        static string OwnerProcess()
        {
            try
            {
                var hwnd = GetClipboardOwner();
                if (hwnd == IntPtr.Zero) hwnd = Native.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return "";
                Native.GetWindowThreadProcessId(hwnd, out var pid);
                var n = Native.ProcessName(pid) ?? "";
                return Path.GetFileNameWithoutExtension(n).ToLowerInvariant();
            }
            catch { return ""; }
        }

        static string ForegroundTitle()
        {
            var sb = new StringBuilder(512);
            try { GetWindowText(Native.GetForegroundWindow(), sb, sb.Capacity); } catch { }
            return sb.ToString();
        }

        static void Expire()
        {
            int days = Settings.Current.ClipKeepDays;
            if (days <= 0) return;
            var cut = DateTime.Now.AddDays(-days);
            int n = Items.RemoveAll(e => DateTime.TryParse(e.At, out var t) && t < cut);
            if (n > 0) Save();
        }

        // ---------- storage: DPAPI (CurrentUser) protected JSON ----------

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                var raw = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.CurrentUser);
                var st = Json.Read<Store>(Encoding.UTF8.GetString(raw));
                if (st?.Items != null) Items.AddRange(st.Items.Where(e => !string.IsNullOrEmpty(e.Text)).Take(MaxItems));
            }
            catch (Exception e) { Log.Write("clipboard load: " + e.Message); }
        }

        static void Save()
        {
            try
            {
                if (!Settings.Current.ClipHistorySaveToDisk) { if (File.Exists(FilePath)) File.Delete(FilePath); return; }
                Directory.CreateDirectory(Paths.YiweiDir);
                var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(Json.Write(new Store { Items = Items })), Entropy, DataProtectionScope.CurrentUser);
                var tmp = FilePath + ".tmp";
                File.WriteAllBytes(tmp, bytes);
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null); else File.Move(tmp, FilePath);
            }
            catch (Exception e) { Log.Write("clipboard save: " + e.Message); }
        }

        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("YiweiIME.ClipHistory.v1");

        /// <summary>A message-only window receiving WM_CLIPBOARDUPDATE.</summary>
        sealed class Listener : NativeWindow
        {
            const int WM_CLIPBOARDUPDATE = 0x031D;
            public event Action Changed;

            public Listener()
            {
                CreateHandle(new CreateParams { Parent = new IntPtr(-3) /* HWND_MESSAGE */ });
                if (!AddClipboardFormatListener(Handle)) Log.Write("clipboard listener failed " + Marshal.GetLastWin32Error());
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_CLIPBOARDUPDATE) { try { Changed?.Invoke(); } catch { } }
                base.WndProc(ref m);
            }
        }

        [DllImport("user32.dll", SetLastError = true)] static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterClipboardFormat(string name);
        [DllImport("user32.dll")] static extern bool IsClipboardFormatAvailable(uint format);
        [DllImport("user32.dll")] static extern IntPtr GetClipboardOwner();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
    }
}
