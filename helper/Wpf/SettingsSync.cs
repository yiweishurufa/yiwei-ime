using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    sealed partial class SettingsWindow
    {
        /// <summary>「常规」页的「同步」小节：选网盘文件夹、立即同步、每日自动同步、上次同步时间。</summary>
        void AddSyncSection(StackPanel p)
        {
            p.Children.Add(K.Section("同步"));

            var current = K.Text("", 12, true);
            var last = K.Text("", 12, true);
            void Refresh()
            {
                var d = Sync.SyncDir;
                current.Text = d == null ? "未设置：只在本机保存（" + Sync.EffectiveDir + "）" : "同步文件夹：" + d + (Sync.InstallationId != null ? "　本机：" + Sync.InstallationId : "");
                last.Text = Sync.Describe();
            }
            Action onChanged = () => Dispatcher.BeginInvoke(new Action(Refresh));
            Sync.Changed += onChanged;
            Closed += (s, e) => Sync.Changed -= onChanged;

            var combo = new ComboBox { MinWidth = 320 };
            void FillCombo()
            {
                combo.Items.Clear();
                combo.Items.Add(new ComboBoxItem { Content = "不同步（只在本机）", Tag = "" });
                foreach (var c in Sync.Detect())
                    combo.Items.Add(new ComboBoxItem { Content = c.Name + "　" + c.Target, Tag = c.Target });
                var cur = Sync.SyncDir;
                if (cur != null && !combo.Items.Cast<ComboBoxItem>().Any(i => string.Equals((string)i.Tag, cur, StringComparison.OrdinalIgnoreCase)))
                    combo.Items.Add(new ComboBoxItem { Content = "自选　" + cur, Tag = cur });
                combo.SelectedItem = combo.Items.Cast<ComboBoxItem>().FirstOrDefault(i => string.Equals((string)i.Tag, cur ?? "", StringComparison.OrdinalIgnoreCase)) ?? combo.Items[0];
            }
            FillCombo();
            bool filling = false;
            combo.SelectionChanged += (s, e) =>
            {
                if (filling || !(combo.SelectedItem is ComboBoxItem it)) return;
                var dir = (string)it.Tag;
                try
                {
                    Sync.SetSyncDir(dir.Length == 0 ? null : dir);
                    Toast(dir.Length == 0 ? "已关闭同步" : "同步文件夹已设置，点「立即同步」开始");
                }
                catch (Exception ex) { Dialogs.Error("无法使用这个文件夹：" + ex.Message); }
                Refresh();
            };

            void Pick()
            {
                using (var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "选择网盘里的同步文件夹（会在里面为每台电脑建一个子文件夹）", ShowNewFolderButton = true })
                {
                    var cur = Sync.SyncDir; if (cur != null && Directory.Exists(cur)) dlg.SelectedPath = cur;
                    if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    Sync.SetSyncDir(dlg.SelectedPath);
                }
                filling = true; try { FillCombo(); } finally { filling = false; }
                Refresh();
                Toast("同步文件夹已设置，点「立即同步」开始");
            }

            var choose = new StackPanel();
            choose.Children.Add(K.Row(combo, K.Btn("手动选择…", Pick), K.Btn("打开", () =>
            {
                var d = Sync.EffectiveDir; Directory.CreateDirectory(d); Dialogs.Open(d);
            })));
            current.Margin = new Thickness(0, 8, 0, 0);
            choose.Children.Add(current);
            p.Children.Add(K.Block("同步到自己的网盘", "不需要账号：用户词库存到你选的 OneDrive、坚果云、百度网盘同步盘等文件夹，各台电脑在同一个文件夹里互相合并", choose, Ui.SymbolRegular.CloudSync24));

            var right = new StackPanel { Orientation = Orientation.Horizontal };
            var now = K.Btn("立即同步", () =>
            {
                if (Sync.Running) { Toast("正在同步…"); return; }
                if (Sync.SyncDir == null && !Dialogs.Confirm("还没有选择同步文件夹，现在只会把词库快照存在本机的 sync 文件夹。继续？")) return;
                Toast("正在同步用户词库…");
                Sync.RunAsync().ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
                {
                    Refresh();
                    Toast(t.Result ? "同步完成" : "同步可能没有成功，详情见「上次同步」");
                })));
            }, Ui.ControlAppearance.Primary);
            right.Children.Add(now);
            p.Children.Add(K.Card("立即同步", "在后台合并本机和网盘里其它电脑的用户词库", right, Ui.SymbolRegular.ArrowSync24));
            last.Margin = new Thickness(0, -6, 0, 0);

            p.Children.Add(K.Card("每天自动同步", "助手运行时每天同步一次（需要先选择同步文件夹）",
                K.Toggle(Sync.Current.AutoDaily, v => { Sync.Current.AutoDaily = v; Sync.Save(); }), Ui.SymbolRegular.CalendarClock24));
            var lastWrap = new Border { Padding = new Thickness(18, 0, 18, 4), Child = last };
            p.Children.Add(lastWrap);
            Refresh();
            AddBackupSection(p);
        }

        /// <summary>「备份与恢复」：一个 .yiwei-backup 文件装下词库、常用语、设置和自定义配置。</summary>
        void AddBackupSection(StackPanel p)
        {
            p.Children.Add(K.Section("备份与恢复"));
            p.Children.Add(K.Card("备份到文件", "把用户词库、常用语、配色和全部设置存成一个文件，换电脑时带走",
                K.Btn("备份…", () =>
                {
                    using (var dlg = new System.Windows.Forms.SaveFileDialog { Filter = "一维输入法备份|*" + Backup.Extension, FileName = "一维输入法备份-" + DateTime.Now.ToString("yyyyMMdd") + Backup.Extension })
                    {
                        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        var path = dlg.FileName;
                        Toast("正在备份…");
                        System.Threading.Tasks.Task.Run(() => Backup.Export(path)).ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
                            Toast(t.IsFaulted ? "备份失败：" + t.Exception.GetBaseException().Message : "已备份 " + t.Result + " 个文件"))));
                    }
                }), Ui.SymbolRegular.ArrowDownload24));
            p.Children.Add(K.Card("从备份恢复", "词库会合并进来，不会冲掉本机新学的词；被替换的设置文件会留一份 .bak",
                K.Btn("恢复…", () =>
                {
                    using (var dlg = new System.Windows.Forms.OpenFileDialog { Filter = "一维输入法备份|*" + Backup.Extension })
                    {
                        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                        var path = dlg.FileName;
                        Toast("正在恢复…");
                        System.Threading.Tasks.Task.Run(() => Backup.Import(path)).ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
                            Toast(t.IsFaulted ? "恢复失败：" + t.Exception.GetBaseException().Message : "已恢复，重新打开设置后可以看到恢复的选项"))));
                    }
                }), Ui.SymbolRegular.History24));
        }
    }
}
