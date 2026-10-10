using System;
using System.IO;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace Yiwei
{
    /// <summary>
    /// 密码框自动英文 + 无痕窗口不学词不统计。
    /// · 密码框：UI Automation 焦点事件里 IsPassword 为真时，若当前是中文就补一次 Shift 切到英文；离开密码框再切回。
    ///   （多数程序的密码框 Windows 本来就会关掉输入法，这里补上浏览器网页、自绘界面等漏网的。）
    /// · 无痕 / InPrivate 窗口：前台窗口标题带「InPrivate / 无痕 / Incognito …」时写标记文件 yiwei/private.now，
    ///   yiwei_private.lua 据此直接上屏不进用户词库，yiwei_stats.lua 不计数；离开即删除标记。
    /// </summary>
    public static class PrivacyGuard
    {
        static readonly string[] PrivateTitles = { "InPrivate", "Incognito", "无痕", "隐私浏览", "隐身", "Private Browsing", "私密浏览", "隐私窗口" };
        static System.Windows.Forms.Timer _timer;
        static bool _private, _switchedForPassword, _inPassword;
        static bool? _mode; // last known 中(true)/英(false) from the IME's toast messages

        public static string FlagFile => Path.Combine(Paths.YiweiDir, "private.now");

        public static void Start()
        {
            try { if (File.Exists(FlagFile)) File.Delete(FlagFile); } catch { }
            _timer = new System.Windows.Forms.Timer { Interval = 700 };
            _timer.Tick += (s, e) => CheckPrivate();
            _timer.Start();
            var t = new Thread(() =>
            {
                try { Automation.AddAutomationFocusChangedEventHandler(OnFocus); }
                catch (Exception e) { Log.Write("privacy: uia " + e.Message); }
            }) { IsBackground = true };
            t.SetApartmentState(ApartmentState.MTA);
            t.Start();
        }

        /// <summary>Told by the toast pipe whenever the IME switches 中/英.</summary>
        public static void ModeChanged(bool chinese) => _mode = chinese;

        public static bool InPrivateWindow => _private;

        static void CheckPrivate()
        {
            bool on = false;
            if (Settings.Current.PrivateNoLearn)
            {
                var title = Native.ForegroundTitle();
                foreach (var t in PrivateTitles) if (title.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) { on = true; break; }
            }
            if (on == _private) return;
            _private = on;
            try
            {
                if (on) { Directory.CreateDirectory(Paths.YiweiDir); File.WriteAllText(FlagFile, "1"); }
                else if (File.Exists(FlagFile)) File.Delete(FlagFile);
            }
            catch (Exception e) { Log.Write("privacy flag: " + e.Message); }
        }

        static void OnFocus(object sender, AutomationFocusChangedEventArgs e)
        {
            if (!Settings.Current.PasswordAscii) return;
            bool pwd = false;
            try
            {
                var el = sender as AutomationElement;
                if (el != null) pwd = (bool)el.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true) == true;
            }
            catch { }
            if (pwd == _inPassword) return;
            _inPassword = pwd;
            Program.Ui.BeginInvoke(new Action(() =>
            {
                if (pwd && _mode != false) { _switchedForPassword = true; TapShift(); }
                else if (!pwd && _switchedForPassword) { _switchedForPassword = false; if (_mode == false) TapShift(); }
            }));
        }

        /// <summary>A lone Shift tap toggles 中/英 in RIME (ascii_composer); tagged so our hook ignores it.</summary>
        static void TapShift()
        {
            if ((Native.GetAsyncKeyState((int)Keys.ShiftKey) & 0x8000) != 0) return;
            var list = new[]
            {
                new Native.INPUT { type = Native.INPUT_KEYBOARD, u = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = (ushort)Keys.LShiftKey, dwExtraInfo = Native.InjectedTag } } },
                new Native.INPUT { type = Native.INPUT_KEYBOARD, u = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = (ushort)Keys.LShiftKey, dwFlags = Native.KEYEVENTF_KEYUP, dwExtraInfo = Native.InjectedTag } } },
            };
            Native.SendInput((uint)list.Length, list, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.INPUT)));
        }
    }
}
