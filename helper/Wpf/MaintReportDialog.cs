using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Yiwei
{
    /// <summary>
    /// 「生成维护报告」前的确认弹窗：说明每一部分是什么，可以逐项取消勾选；全部取消时「生成」按钮变灰。
    /// 返回勾选的部分，取消/关闭返回 None。
    /// </summary>
    static class MaintReportDialog
    {
        public static MaintReport.Parts Ask(MaintReport.Parts initial)
        {
            var result = MaintReport.Parts.None;
            var w = new Window
            {
                Title = "生成维护报告", Width = 460, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen, Topmost = true, ShowInTaskbar = true, FontFamily = K.Font,
            };
            w.SetResourceReference(Window.BackgroundProperty, "ApplicationBackgroundBrush");
            w.SetResourceReference(Window.ForegroundProperty, "TextFillColorPrimaryBrush");

            var root = new StackPanel { Margin = new Thickness(24) };
            root.Children.Add(new TextBlock
            {
                Text = "将在桌面生成 yiwei-report-日期.zip。勾选要放进去的内容：", FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12),
            });
            var boxes = MaintReport.PartInfo.Select(x =>
            {
                var cb = new CheckBox { IsChecked = initial.HasFlag(x.part), Tag = x.part, Margin = new Thickness(0, 4, 0, 4) };
                var label = new StackPanel();
                label.Children.Add(new TextBlock { Text = x.title, FontSize = 14 });
                var d = new TextBlock { Text = x.desc, FontSize = 12, TextWrapping = TextWrapping.Wrap, Opacity = 0.75 };
                label.Children.Add(d);
                cb.Content = label;
                System.Windows.Automation.AutomationProperties.SetName(cb, x.title);
                root.Children.Add(cb);
                return cb;
            }).ToList();
            root.Children.Add(new TextBlock
            {
                Text = "不含原文句子、剪贴板、常用语和密码框输入；不会自动上传，你可以先打开看看再决定是否发给维护者。",
                FontSize = 12, TextWrapping = TextWrapping.Wrap, Opacity = 0.75, Margin = new Thickness(0, 12, 0, 16),
            });
            var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new Button { Content = "生成", MinWidth = 88, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancel = new Button { Content = "取消", MinWidth = 88, IsCancel = true };
            bar.Children.Add(ok); bar.Children.Add(cancel);
            root.Children.Add(bar);

            MaintReport.Parts Selected() => boxes.Where(b => b.IsChecked == true).Aggregate(MaintReport.Parts.None, (a, b) => a | (MaintReport.Parts)b.Tag);
            void Refresh() => ok.IsEnabled = Selected() != MaintReport.Parts.None;
            foreach (var b in boxes) { b.Checked += (s, e) => Refresh(); b.Unchecked += (s, e) => Refresh(); }
            Refresh();
            ok.Click += (s, e) => { result = Selected(); w.Close(); };
            cancel.Click += (s, e) => w.Close();

            w.Content = root;
            w.Loaded += (s, e) => { w.Activate(); w.Topmost = false; w.Topmost = true; };
            w.ShowDialog();
            return result;
        }
    }
}
