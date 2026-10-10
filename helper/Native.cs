using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Yiwei
{
    static class Native
    {
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;
        public const int LLKHF_INJECTED = 0x10;
        public const uint INPUT_KEYBOARD = 1, KEYEVENTF_KEYUP = 2, KEYEVENTF_UNICODE = 4;
        public const int WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOPMOST = 0x8, WS_EX_TOOLWINDOW = 0x80;
        public static readonly IntPtr InjectedTag = new IntPtr(0x59495745); // "YIWE"

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT { public uint type; public InputUnion u; }
        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public int cbSize, flags; public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret; public RECT rcCaret;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern bool GetGUIThreadInfo(uint tid, ref GUITHREADINFO info);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT p);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] public static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] public static extern int GetWindowLong(IntPtr hWnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] public static extern int SetWindowLong(IntPtr hWnd, int index, int value);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr mon, ref MONITORINFO info);
        [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
        [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder sb, ref int size);
        [DllImport("imm32.dll")] public static extern IntPtr ImmGetDefaultIMEWnd(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }

        public const int GWL_EXSTYLE = -20;
        public const uint SWP_NOSIZE = 0x1, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const int WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000;

        /// <summary>Executable name (lower case, with .exe) of a process id, cached; cheap enough for the keyboard hook.</summary>
        static readonly System.Collections.Generic.Dictionary<uint, string> _names = new System.Collections.Generic.Dictionary<uint, string>();
        public static string ProcessName(uint pid)
        {
            if (pid == 0) return "";
            lock (_names)
            {
                if (_names.TryGetValue(pid, out var cached)) return cached;
                string name = "";
                var h = OpenProcess(0x1000 /* QUERY_LIMITED_INFORMATION */, false, pid);
                if (h != IntPtr.Zero)
                {
                    try
                    {
                        var sb = new StringBuilder(1024); int size = sb.Capacity;
                        if (QueryFullProcessImageName(h, 0, sb, ref size)) name = System.IO.Path.GetFileName(sb.ToString()).ToLowerInvariant();
                    }
                    finally { CloseHandle(h); }
                }
                if (_names.Count > 512) _names.Clear();
                _names[pid] = name;
                return name;
            }
        }

        public static string ForegroundExe()
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
            return ProcessName(pid);
        }

        /// <summary>A Direct3D exclusive full-screen app or presentation mode is running.</summary>
        public static bool ExclusiveFullscreen()
        {
            try { return SHQueryUserNotificationState(out var st) == 0 && (st == 3 || st == 4); }
            catch { return false; }
        }

        /// <summary>The foreground window covers its whole monitor (borderless full screen), and is not the desktop.</summary>
        public static bool ForegroundCoversMonitor()
        {
            var fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == GetShellWindow() || fg == GetDesktopWindow()) return false;
            var cls = new StringBuilder(64); GetClassName(fg, cls, 64);
            var c = cls.ToString();
            if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd") return false;
            if (!GetWindowRect(fg, out var r)) return false;
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (!GetMonitorInfo(MonitorFromWindow(fg, 2), ref mi)) return false;
            return r.Left <= mi.rcMonitor.Left && r.Top <= mi.rcMonitor.Top && r.Right >= mi.rcMonitor.Right && r.Bottom >= mi.rcMonitor.Bottom;
        }

        /// <summary>Caret rectangle in screen pixels, or null when the app does not expose a caret.</summary>
        public static RECT? CaretRect()
        {
            var fg = GetForegroundWindow();
            var tid = GetWindowThreadProcessId(fg, out _);
            var info = new GUITHREADINFO { cbSize = Marshal.SizeOf(typeof(GUITHREADINFO)) };
            if (GetGUIThreadInfo(tid, ref info) && info.hwndCaret != IntPtr.Zero)
            {
                var a = new POINT { X = info.rcCaret.Left, Y = info.rcCaret.Top };
                var b = new POINT { X = info.rcCaret.Right, Y = info.rcCaret.Bottom };
                ClientToScreen(info.hwndCaret, ref a); ClientToScreen(info.hwndCaret, ref b);
                if (a.X != 0 || a.Y != 0) return new RECT { Left = a.X, Top = a.Y, Right = Math.Max(b.X, a.X + 1), Bottom = Math.Max(b.Y, a.Y + 16) };
            }
            return null;
        }

        /// <summary>Work area (pixels) of the monitor containing a point.</summary>
        public static RECT WorkArea(int x, int y)
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            if (GetMonitorInfo(MonitorFromPoint(new POINT { X = x, Y = y }, 2), ref mi)) return mi.rcWork;
            var wa = Screen.PrimaryScreen.WorkingArea;
            return new RECT { Left = wa.Left, Top = wa.Top, Right = wa.Right, Bottom = wa.Bottom };
        }

        public static string ForegroundProcessName()
        {
            try
            {
                GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
                if (pid == 0) return "";
                using (var p = Process.GetProcessById((int)pid)) return p.ProcessName + ".exe";
            }
            catch { return ""; }
        }

        /// <summary>Screen position of the text caret in the foreground window, or the mouse cursor.</summary>
        public static Point CaretPosition()
        {
            var fg = GetForegroundWindow();
            var tid = GetWindowThreadProcessId(fg, out _);
            var info = new GUITHREADINFO { cbSize = Marshal.SizeOf(typeof(GUITHREADINFO)) };
            if (GetGUIThreadInfo(tid, ref info) && info.hwndCaret != IntPtr.Zero)
            {
                var p = new POINT { X = info.rcCaret.Left, Y = info.rcCaret.Bottom };
                ClientToScreen(info.hwndCaret, ref p);
                if (p.X != 0 || p.Y != 0) return new Point(p.X, p.Y + 4);
            }
            GetCursorPos(out var c);
            return new Point(c.X, c.Y + 18);
        }
    }

    /// <summary>Puts text where the cursor is, without touching the clipboard when possible.</summary>
    static class TextOut
    {
        public static void Type(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            ReleaseModifiers();
            var list = new System.Collections.Generic.List<Native.INPUT>();
            foreach (char ch in text.Replace("\r\n", "\n"))
            {
                if (ch == '\n') { AddVk(list, (ushort)Keys.Enter); continue; }
                list.Add(Key(0, ch, Native.KEYEVENTF_UNICODE));
                list.Add(Key(0, ch, Native.KEYEVENTF_UNICODE | Native.KEYEVENTF_KEYUP));
            }
            Native.SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf(typeof(Native.INPUT)));
        }

        /// <summary>One Left arrow, tagged as ours so the keyboard hook ignores it.</summary>
        public static void Left()
        {
            var list = new System.Collections.Generic.List<Native.INPUT>();
            AddVk(list, (ushort)Keys.Left);
            Native.SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf(typeof(Native.INPUT)));
        }

        /// <summary>Replaces the current selection: paste through the clipboard, then restore it.</summary>
        public static void Replace(string text)
        {
            IDataObject saved = null;
            try { saved = Clipboard.GetDataObject(); } catch { }
            try
            {
                Clipboard.SetText(text);
                ReleaseModifiers();
                Chord(Keys.ControlKey, Keys.V);
                Pump(200);
            }
            catch { Type(text); }
            finally { Restore(saved); }
        }

        /// <summary>Copies the current selection of the foreground app (Ctrl+C) and returns it.</summary>
        public static string CopySelection()
        {
            IDataObject saved = null;
            try { saved = Clipboard.GetDataObject(); } catch { }
            string result = "";
            try
            {
                Clipboard.Clear();
                ReleaseModifiers();
                Chord(Keys.ControlKey, Keys.C);
                for (int i = 0; i < 20 && result.Length == 0; i++)
                {
                    Pump(30);
                    try { if (Clipboard.ContainsText()) result = Clipboard.GetText(); } catch { }
                }
            }
            finally { Restore(saved); }
            return result;
        }

        /// <summary>Waits while keeping the message loop (and so the keyboard hook) responsive.</summary>
        public static void Pump(int ms)
        {
            var end = Environment.TickCount + ms;
            while (unchecked(end - Environment.TickCount) > 0) { Application.DoEvents(); Thread.Sleep(5); }
        }

        static void Restore(IDataObject saved)
        {
            if (saved == null) return;
            try
            {
                var copy = new DataObject();
                foreach (var f in saved.GetFormats(false))
                {
                    try { var d = saved.GetData(f, false); if (d != null) copy.SetData(f, d); } catch { }
                }
                Clipboard.SetDataObject(copy, true);
            }
            catch { }
        }

        public static void ReleaseModifiers()
        {
            var list = new System.Collections.Generic.List<Native.INPUT>();
            foreach (var vk in new[] { Keys.LMenu, Keys.RMenu, Keys.LControlKey, Keys.RControlKey, Keys.LShiftKey, Keys.RShiftKey, Keys.LWin, Keys.RWin })
                if ((Native.GetAsyncKeyState((int)vk) & 0x8000) != 0) list.Add(Key((ushort)vk, 0, Native.KEYEVENTF_KEYUP));
            if (list.Count > 0) Native.SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf(typeof(Native.INPUT)));
        }

        /// <summary>A Ctrl tap stops Windows from opening the menu bar when Alt is released after a swallowed chord.</summary>
        public static void MaskAlt()
        {
            var list = new System.Collections.Generic.List<Native.INPUT>();
            AddVk(list, 0xE8); // unassigned virtual key
            Native.SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf(typeof(Native.INPUT)));
        }

        static void Chord(Keys mod, Keys key)
        {
            var list = new System.Collections.Generic.List<Native.INPUT>
            {
                Key((ushort)mod, 0, 0), Key((ushort)key, 0, 0), Key((ushort)key, 0, Native.KEYEVENTF_KEYUP), Key((ushort)mod, 0, Native.KEYEVENTF_KEYUP)
            };
            Native.SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf(typeof(Native.INPUT)));
        }

        static void AddVk(System.Collections.Generic.List<Native.INPUT> list, ushort vk)
        {
            list.Add(Key(vk, 0, 0)); list.Add(Key(vk, 0, Native.KEYEVENTF_KEYUP));
        }

        static Native.INPUT Key(ushort vk, int scan, uint flags) => new Native.INPUT
        {
            type = Native.INPUT_KEYBOARD,
            u = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = vk, wScan = (ushort)scan, dwFlags = flags, dwExtraInfo = Native.InjectedTag } }
        };
    }

    /// <summary>
    /// Global keyboard hook implementing AIME's "hold Alt, then press a key" gestures:
    /// hold Alt + 1..9 opens the snippet panel on that category, hold Alt + Space runs AI on the selection.
    /// While a panel is open, keys are routed to it instead of the focused app.
    /// </summary>
    sealed class KeyHook : IDisposable
    {
        readonly Native.HookProc _proc;
        IntPtr _hook;
        long _altDownAt;      // Environment.TickCount when Alt went down (0 = up)
        bool _altUsed;        // another key was pressed while Alt was down
        bool _swallowedChord; // we consumed a chord; mask the Alt release

        public Func<Keys, bool, bool> PanelKey;      // (key, alt held) → true when the panel consumed the key
        public Func<bool> PanelOpen;
        public Action ClosePanel;
        public Action<int> OnSnippets;               // category index
        public Action OnAi;

        // 按住说话（Features/Voice.cs）：按住右 Ctrl / 右 Alt 超过判定时间开始录音，松开识别上屏。
        public Action OnVoiceStart, OnVoiceEnd, OnVoiceCancel;
        long _voiceDownAt;    // TickCount when the voice key went down (0 = up)
        bool _voiceUsed;      // another key was pressed while it was held: an ordinary shortcut
        bool _voiceActive;    // recording started
        readonly System.Windows.Forms.Timer _voiceTimer = new System.Windows.Forms.Timer { Interval = 50 };

        public KeyHook()
        {
            _proc = Proc;
            _voiceTimer.Tick += (s, e) =>
            {
                if (_voiceDownAt == 0 || _voiceUsed || _voiceActive) { _voiceTimer.Stop(); return; }
                var vk = VoiceVk(Settings.Current);
                if (vk == Keys.None || !IsDown(vk)) { _voiceDownAt = 0; _voiceTimer.Stop(); return; }
                if (unchecked(Environment.TickCount - _voiceDownAt) < VoiceHoldMs(Settings.Current)) return;
                _voiceTimer.Stop();
                _voiceActive = true;
                OnVoiceStart?.Invoke();
            };
        }

        static Keys VoiceVk(Settings s) =>
            s.VoiceKey == "ralt" ? Keys.RMenu : s.VoiceKey == "rctrl" ? Keys.RControlKey : Keys.None;

        /// <summary>右 Alt 要比 Alt 手势的判定时间更久，免得和「长按 Alt + 数字」抢。</summary>
        static int VoiceHoldMs(Settings s) => s.VoiceKey == "ralt" ? Math.Max(600, s.HoldMs + 250) : 350;

        /// <summary>Voice key bookkeeping. Returns true when the key event must be swallowed.</summary>
        bool HandleVoice(Keys vk, bool down)
        {
            var s = Settings.Current;
            var voiceVk = VoiceVk(s);
            if (voiceVk == Keys.None || OnVoiceStart == null || !VoiceModel.Ready) return false;
            bool isVoiceKey = vk == voiceVk;
            if (isVoiceKey)
            {
                if (down)
                {
                    if (_voiceDownAt != 0) return _voiceActive; // auto-repeat
                    bool otherMods = IsDown(Keys.LControlKey) || IsDown(Keys.LMenu) || IsDown(Keys.LShiftKey) || IsDown(Keys.RShiftKey) || IsDown(Keys.LWin) || IsDown(Keys.RWin)
                                     || (voiceVk == Keys.RMenu ? IsDown(Keys.RControlKey) : IsDown(Keys.RMenu));
                    if (otherMods || VoiceBlocked(s)) return false;
                    _voiceDownAt = Environment.TickCount; _voiceUsed = false; _voiceActive = false;
                    _voiceTimer.Start();
                    return false;
                }
                _voiceDownAt = 0; _voiceTimer.Stop();
                if (_voiceActive)
                {
                    _voiceActive = false;
                    Program.Ui.BeginInvoke(new Action(() => OnVoiceEnd?.Invoke()));
                    if (voiceVk == Keys.RMenu) TextOut.MaskAlt(); // no menu bar after a lone Alt release
                }
                return false;
            }
            if (!down || _voiceDownAt == 0) return false;
            if (_voiceActive)
            {
                // Esc cancels (and is swallowed); any other key cancels and goes through as usual.
                _voiceActive = false; _voiceUsed = true;
                Program.Ui.BeginInvoke(new Action(() => OnVoiceCancel?.Invoke()));
                return vk == Keys.Escape;
            }
            _voiceUsed = true;
            return false;
        }

        /// <summary>Voice input stays out of exclusive full screen and the apps blocked for Alt gestures (games, remote desktops).</summary>
        static bool VoiceBlocked(Settings s)
        {
            if (Native.ExclusiveFullscreen()) return true;
            var exe = Native.ForegroundExe();
            if (exe.Length > 0 && s.AltBlocklist != null)
                foreach (var b in s.AltBlocklist)
                    if (string.Equals(b?.Trim(), exe, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public void Install()
        {
            using (var cur = Process.GetCurrentProcess())
            using (var mod = cur.MainModule)
                _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc, Native.GetModuleHandle(mod.ModuleName), 0);
            if (_hook == IntPtr.Zero) Log.Write("hook install failed " + Marshal.GetLastWin32Error());
        }

        IntPtr Proc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var k = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                bool injected = (k.flags & Native.LLKHF_INJECTED) != 0;
                if (!(injected && k.dwExtraInfo == Native.InjectedTag))
                {
                    int msg = wParam.ToInt32();
                    bool down = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
                    if (Handle((Keys)k.vkCode, down)) return new IntPtr(1);
                }
            }
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        static bool IsDown(Keys k) => (Native.GetAsyncKeyState((int)k) & 0x8000) != 0;
        static bool CtrlOrWinDown() => IsDown(Keys.LControlKey) || IsDown(Keys.RControlKey) || IsDown(Keys.LWin) || IsDown(Keys.RWin);

        /// <summary>Alt gestures are off in blocked apps, exclusive full screen, and (optionally) borderless full screen.</summary>
        static bool GestureBlocked(Settings s)
        {
            if (!s.AltHotkeys) return true;
            if (Native.ExclusiveFullscreen()) return true;
            if (s.AltSkipBorderlessFullscreen && Native.ForegroundCoversMonitor()) return true;
            var exe = Native.ForegroundExe();
            if (exe.Length > 0 && s.AltBlocklist != null)
                foreach (var b in s.AltBlocklist)
                    if (string.Equals(b?.Trim(), exe, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        bool Handle(Keys vk, bool down)
        {
            if (HandleVoice(vk, down)) return true;
            bool isAlt = vk == Keys.LMenu || vk == Keys.RMenu || vk == Keys.Menu;
            if (isAlt)
            {
                if (down) { if (_altDownAt == 0) { _altDownAt = Environment.TickCount; _altUsed = false; } }
                else
                {
                    _altDownAt = 0;
                    if (_swallowedChord) { _swallowedChord = false; TextOut.MaskAlt(); }
                }
                return false;
            }
            bool modifier = vk == Keys.LShiftKey || vk == Keys.RShiftKey || vk == Keys.LControlKey || vk == Keys.RControlKey
                            || vk == Keys.LWin || vk == Keys.RWin || vk == Keys.ShiftKey || vk == Keys.ControlKey;

            // An open panel takes the keyboard, but never system chords (Alt+Tab, Alt+F4, Ctrl/Win combos).
            if (PanelOpen != null && PanelOpen())
            {
                if (modifier) return false;
                if (CtrlOrWinDown()) return false;
                if (_altDownAt != 0 && (vk == Keys.Tab || vk == Keys.F4 || vk == Keys.Escape))
                {
                    _swallowedChord = false;
                    if (down) Program.Ui.BeginInvoke(new Action(() => ClosePanel?.Invoke()));
                    return false;
                }
                if (!down) return true;
                bool used = PanelKey != null && PanelKey(vk, _altDownAt != 0);
                if (_altDownAt != 0 && used) _swallowedChord = true;
                return used;
            }

            if (_altDownAt != 0 && down)
            {
                if (modifier || CtrlOrWinDown() || IsDown(Keys.LShiftKey) || IsDown(Keys.RShiftKey)) { _altUsed = true; return false; }
                var s = Settings.Current;
                bool held = unchecked(Environment.TickCount - _altDownAt) >= s.HoldMs && !_altUsed;
                bool wanted = held && ((s.SnippetsHotkey && vk >= Keys.D1 && vk <= Keys.D9) || (s.AiHotkey && vk == Keys.Space));
                if (wanted && !GestureBlocked(s))
                {
                    _swallowedChord = true; _altUsed = true;
                    if (vk == Keys.Space) Program.Ui.BeginInvoke(new Action(() => OnAi?.Invoke()));
                    else { int cat = vk - Keys.D1; Program.Ui.BeginInvoke(new Action(() => OnSnippets?.Invoke(cat))); }
                    return true;
                }
                _altUsed = true;
            }
            return false;
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero) Native.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
