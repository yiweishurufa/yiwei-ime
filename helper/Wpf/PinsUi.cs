using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    /// <summary>「词库」页：置顶与隐藏的候选（yiwei_pin.lua 写 yiwei/pins.tsv；这里改完写 pins.reload 让输入法重读）。</summary>
    sealed partial class SettingsWindow
    {
        static string PinsFile => Path.Combine(Paths.YiweiDir, "pins.tsv");

        void AddPinsCard(StackPanel p)
        {
            var list = new StackPanel();
            void Fill()
            {
                list.Children.Clear();
                var rows = File.Exists(PinsFile) ? File.ReadAllLines(PinsFile, Encoding.UTF8).Where(l => l.Length > 4).ToList() : new List<string>();
                if (rows.Count == 0) { list.Children.Add(K.Text("还没有。打字时选中候选按 Ctrl+T 置顶，按 Ctrl+Delete 隐藏。", 12, true)); return; }
                foreach (var row in rows.OrderBy(r => r.Split('\t')[1]))
                {
                    var parts = row.Split('\t');
                    if (parts.Length < 3) continue;
                    var line = row;
                    var dp = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
                    var del = new Ui.Button { Content = "撤销", Appearance = Ui.ControlAppearance.Transparent, Padding = new Thickness(8, 2, 8, 2) };
                    del.Click += (s, e) =>
                    {
                        var keep = File.ReadAllLines(PinsFile, Encoding.UTF8).Where(l => l != line).ToArray();
                        File.WriteAllLines(PinsFile, keep, new UTF8Encoding(false));
                        File.WriteAllText(Path.Combine(Paths.YiweiDir, "pins.reload"), "1");
                        Fill();
                    };
                    DockPanel.SetDock(del, Dock.Right);
                    dp.Children.Add(del);
                    dp.Children.Add(new TextBlock { Text = (parts[0] == "P" ? "📌 置顶　" : "🚫 隐藏　") + parts[1] + "　→　" + parts[2], VerticalAlignment = VerticalAlignment.Center });
                    list.Children.Add(dp);
                }
            }
            Fill();
            p.Children.Add(K.Block("置顶与隐藏的候选", "打字时：Ctrl+T 把高亮的候选钉在第一位（再按取消），Ctrl+Delete 让它不再出现（学来的词同时从词库删除）",
                new ScrollViewer { Content = list, MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, Ui.SymbolRegular.Pin24));
        }
    }
}
