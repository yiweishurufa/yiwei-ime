using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    /// <summary>「常用语」页底部：剪贴板历史的开关、保留时长、排除应用与清空（逻辑在 Features/ClipHistory.cs）。</summary>
    sealed partial class SettingsWindow
    {
        void AddClipHistoryCards(StackPanel p)
        {
            p.Children.Add(K.Section("剪贴板历史"));
            p.Children.Add(K.Card("记录剪贴板历史", "最近 " + ClipHistory.MaxItems + " 条复制过的文字，在长按 Alt 面板的「剪贴板」分组里（长按 Alt + V 直接打开），数字键上屏、Del 删除",
                K.Toggle(S.ClipHistory, v => { S.ClipHistory = v; Save(); }), Ui.SymbolRegular.ClipboardPaste24));
            p.Children.Add(K.Card("保留时长", "超过时长的记录自动删除",
                K.Combo(new[]
                {
                    new KeyValuePair<int, string>(1, "1 天"),
                    new KeyValuePair<int, string>(7, "7 天"),
                    new KeyValuePair<int, string>(30, "30 天"),
                    new KeyValuePair<int, string>(0, "一直保留（最多 " + ClipHistory.MaxItems + " 条）"),
                }, S.ClipKeepDays, v => { S.ClipKeepDays = v; Save(); }, 180), Ui.SymbolRegular.History24));
            p.Children.Add(K.Card("重启后保留", "开启时加密保存在本机（只有当前 Windows 账户能读）；关闭则只在内存里，退出即清空",
                K.Toggle(S.ClipHistorySaveToDisk, v => { S.ClipHistorySaveToDisk = v; Save(); ClipHistory.Flush(); }), Ui.SymbolRegular.LockClosed24));

            var box = new Ui.TextBox { PlaceholderText = "例如 wechat.exe, notepad.exe", MinWidth = 320, Text = string.Join(", ", S.ClipExcludeApps) };
            box.LostFocus += (s, e) =>
            {
                S.ClipExcludeApps = box.Text.Split(new[] { ',', '，', ' ', ';', '；' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim().ToLowerInvariant()).Select(x => x.EndsWith(".exe") ? x : x + ".exe").Distinct().ToList();
                Save();
            };
            p.Children.Add(K.Card("不记录这些应用", "KeePass、1Password、Bitwarden 等密码管理器和浏览器无痕窗口总是不记录；这里可以再加",
                box, Ui.SymbolRegular.Shield24));
            p.Children.Add(K.Card("清空剪贴板历史", "立即删除全部记录（包括本机加密文件）",
                K.Btn("清空", () => { if (Dialogs.Confirm("清空全部剪贴板历史？")) { ClipHistory.Clear(); Toast("已清空剪贴板历史"); } }), Ui.SymbolRegular.Delete24));
        }
    }
}
