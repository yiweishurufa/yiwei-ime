using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    sealed partial class SettingsWindow
    {
        /// <summary>「应用规则」页里的按应用中英列表：内置推荐 + 扫描到的已安装 / 正在运行程序 + 自定义 exe。</summary>
        Border AppModesBlock()
        {
            var list = new StackPanel();
            var status = K.Text("", 12, true); status.Margin = new Thickness(0, 6, 0, 0);
            Dictionary<string, string> scanned = null;   // exe → 来源
            bool showAll = false, scanRunningAll = false;

            UIElement Row(string exe, string label, string tag)
            {
                var g = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var cb = new CheckBox { Content = label + "　" + exe + (string.IsNullOrEmpty(tag) ? "" : "　（" + tag + "）"), IsChecked = AppModes.IsAscii(S, exe) };
                cb.Checked += (s, e) => { AppModes.SetAscii(S, exe, true); Save(true); };
                cb.Unchecked += (s, e) => { AppModes.SetAscii(S, exe, false); Save(true); };
                var vim = new CheckBox { Content = "vim 模式", IsChecked = AppModes.IsVim(exe), ToolTip = "按 Esc、Ctrl+[ 或 Ctrl+C 时自动回到英文", Margin = new Thickness(12, 0, 0, 0) };
                vim.Checked += (s, e) => { AppModes.SetVim(exe, true); Save(true); };
                vim.Unchecked += (s, e) => { AppModes.SetVim(exe, false); Save(true); };
                Grid.SetColumn(vim, 1);
                g.Children.Add(cb); g.Children.Add(vim);
                return g;
            }

            void Fill()
            {
                list.Children.Clear();
                var custom = new HashSet<string>(AppModes.CustomApps(S));
                var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var grp in new[] { "终端", "编辑器", "工具", "游戏" })
                {
                    var apps = AppModes.Catalog.Where(a => a.Group == grp && (showAll || scanned == null || scanned.ContainsKey(a.Exe) || (S.AppAscii != null && S.AppAscii.ContainsKey(a.Exe)))).ToList();
                    if (apps.Count == 0) continue;
                    list.Children.Add(K.Section(grp));
                    foreach (var a in apps)
                    {
                        string tag = scanned != null && scanned.TryGetValue(a.Exe, out var how) ? how : (scanned != null ? "未检测到" : "推荐");
                        list.Children.Add(Row(a.Exe, a.Name, tag)); shown.Add(a.Exe);
                    }
                }
                var others = new SortedSet<string>(custom, StringComparer.OrdinalIgnoreCase);
                if (scanned != null && scanRunningAll) foreach (var k in scanned.Keys) if (AppModes.Find(k) == null) others.Add(k);
                if (others.Count > 0)
                {
                    list.Children.Add(K.Section("其它应用"));
                    foreach (var exe in others)
                    {
                        if (!shown.Add(exe)) continue;
                        string tag = custom.Contains(exe) ? "自定义" : (scanned != null && scanned.TryGetValue(exe, out var how) ? how : "");
                        list.Children.Add(Row(exe, "", tag));
                    }
                }
                if (scanned == null) status.Text = "显示全部内置推荐。点「扫描本机应用」只列出这台电脑上装了的。";
                else status.Text = "扫描到 " + scanned.Keys.Count(k => AppModes.Find(k) != null) + " 个推荐应用" + (showAll ? "；也显示未检测到的推荐" : "");
            }

            void DoScan(bool all)
            {
                status.Text = "正在扫描开始菜单、已安装程序和正在运行的应用…";
                Task.Run(() => AppModes.Scan(all)).ContinueWith(t =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (t.IsFaulted) { status.Text = "扫描失败：" + t.Exception.GetBaseException().Message; return; }
                        scanned = t.Result; scanRunningAll = all; Fill();
                    }));
                });
            }

            Fill();
            var add = new Ui.TextBox { PlaceholderText = "例如 game.exe", MinWidth = 220 };
            void AddApp()
            {
                var n = AppModes.Norm(add.Text); if (n.Length == 0) return;
                AppModes.SetAscii(S, n, true); add.Text = ""; Save(true); Fill();
            }
            add.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) AddApp(); };
            var showAllBox = new CheckBox { Content = "显示未检测到的推荐", VerticalAlignment = VerticalAlignment.Center };
            showAllBox.Checked += (s, e) => { showAll = true; Fill(); };
            showAllBox.Unchecked += (s, e) => { showAll = false; Fill(); };

            var body = new StackPanel();
            body.Children.Add(new ScrollViewer { Content = list, MaxHeight = 380, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            body.Children.Add(status);
            var row = K.Row(add, K.Btn("添加", AddApp), K.Btn("扫描本机应用", () => DoScan(false)), K.Btn("列出所有正在运行的", () => DoScan(true)));
            row.Margin = new Thickness(0, 10, 0, 0);
            body.Children.Add(row);
            var row2 = K.Row(showAllBox); row2.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(row2);
            return K.Block("切换过去时自动用英文", "勾选的应用获得焦点时进入英文状态；「vim 模式」在按 Esc 回到普通模式时也切回英文。终端、编辑器、游戏默认已勾选，取消勾选即可改回中文", body, Ui.SymbolRegular.AppsList24);
        }
    }
}
