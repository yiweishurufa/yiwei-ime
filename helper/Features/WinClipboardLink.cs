using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Yiwei
{
    /// <summary>
    /// W34 与 Win+V 剪贴板历史联动：
    /// · 「系统剪贴板历史」动作：打开 Windows 的 Win+V 面板（没开启时可一键开启）；
    /// · 把 Windows 剪贴板历史里的文字并入一维的剪贴板历史（长按 Alt+V 面板），手动或每 10 分钟一次；
    /// · 一维自己的剪贴板历史本来就不会把密码管理器内容写进系统历史（ClipHistory 里已经遵守 CanIncludeInClipboardHistory）。
    /// Windows 只允许前台程序读取剪贴板历史，后台同步可能被拒（静默跳过），所以设置页按钮最可靠。
    /// </summary>
    public static class WinClipboardLink
    {
        const string Key = @"Software\Microsoft\Clipboard";

        public static bool SystemHistoryEnabled
        {
            get { try { using (var k = Registry.CurrentUser.OpenSubKey(Key)) return (k?.GetValue("EnableClipboardHistory") as int? ?? 0) == 1; } catch { return false; } }
            set { try { using (var k = Registry.CurrentUser.CreateSubKey(Key)) k.SetValue("EnableClipboardHistory", value ? 1 : 0, RegistryValueKind.DWord); } catch (Exception e) { Log.Write("clip history reg: " + e.Message); } }
        }

        [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

        /// <summary>Opens the Windows clipboard history (Win+V).</summary>
        public static void OpenSystemPanel()
        {
            if (!SystemHistoryEnabled)
            {
                var r = MessageBox.Show("Windows 剪贴板历史（Win+V）还没开启。现在开启吗？\n开启后复制过的内容会保存在系统里，Win+V 可以随时选。", "一维输入法", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return;
                SystemHistoryEnabled = true;
            }
            ToolsHub.WaitKeysUp();
            var tag = (UIntPtr)(ulong)Native.InjectedTag.ToInt64();
            keybd_event(0x5B, 0, 0, tag); keybd_event((byte)Keys.V, 0, 0, tag);
            keybd_event((byte)Keys.V, 0, 2, tag); keybd_event(0x5B, 0, 2, tag);
        }

        /// <summary>Copies the text items of the Windows clipboard history into 一维's own history. Returns how many were new.</summary>
        public static async Task<int> ImportAsync()
        {
            if (!Windows.ApplicationModel.DataTransfer.Clipboard.IsHistoryEnabled()) throw new Exception("Windows 剪贴板历史没有开启");
            var r = await Windows.ApplicationModel.DataTransfer.Clipboard.GetHistoryItemsAsync();
            if (r.Status != Windows.ApplicationModel.DataTransfer.ClipboardHistoryItemsResultStatus.Success)
                throw new Exception(r.Status == Windows.ApplicationModel.DataTransfer.ClipboardHistoryItemsResultStatus.AccessDenied ? "Windows 拒绝了读取（只有前台窗口能读）" : "剪贴板历史不可用");
            var have = ClipHistory.Recent().Select(e => e.Text).ToList();
            int added = 0;
            foreach (var item in r.Items.Reverse()) // oldest first, so the newest ends on top
            {
                try
                {
                    if (!item.Content.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text)) continue;
                    var text = await item.Content.GetTextAsync();
                    if (string.IsNullOrWhiteSpace(text) || text.Length > 20000 || have.Contains(text)) continue;
                    ClipHistory.Add(text); added++;
                }
                catch { }
            }
            return added;
        }

        static Timer _timer;

        public static void Start()
        {
            if (_timer != null) return;
            _timer = new Timer { Interval = 10 * 60 * 1000 };
            _timer.Tick += async (s, e) =>
            {
                if (!ToolSettings.Current.MergeWinClipboard || !Settings.Current.ClipHistory) return;
                try { await ImportAsync(); } catch { /* not foreground or disabled: try again later */ }
            };
            _timer.Start();
        }
    }
}
