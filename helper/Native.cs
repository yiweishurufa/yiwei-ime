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

        public Func<Keys, bool> PanelKey;            // returns true when the panel consumed the key
        public Func<bool> PanelOpen;
        public Action<int> OnSnippets;               // category index
        public Action OnAi;

        public KeyHook() { _proc = Proc; }

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

        bool Handle(Keys vk, bool down)
        {
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

            // An open panel takes the keyboard (except modifiers).
            if (PanelOpen != null && PanelOpen())
            {
                if (vk == Keys.LShiftKey || vk == Keys.RShiftKey || vk == Keys.LControlKey || vk == Keys.RControlKey) return false;
                if (!down) return true;
                bool used = PanelKey != null && PanelKey(vk);
                if (_altDownAt != 0) _swallowedChord = true;
                return used;
            }

            if (_altDownAt != 0 && down)
            {
                var s = Settings.Current;
                bool held = unchecked(Environment.TickCount - _altDownAt) >= s.HoldMs && !_altUsed;
                if (held && s.SnippetsHotkey && vk >= Keys.D1 && vk <= Keys.D9)
                {
                    _swallowedChord = true; _altUsed = true;
                    int cat = vk - Keys.D1;
                    Program.Ui.BeginInvoke(new Action(() => OnSnippets?.Invoke(cat)));
                    return true;
                }
                if (held && s.AiHotkey && vk == Keys.Space)
                {
                    _swallowedChord = true; _altUsed = true;
                    Program.Ui.BeginInvoke(new Action(() => OnAi?.Invoke()));
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
