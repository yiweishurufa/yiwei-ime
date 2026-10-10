using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Yiwei
{
    /// <summary>
    /// 部署进度小窗：RIME 部署（编译词库）时在屏幕右下角显示「正在部署 · 已用 / 预计」和进度条，
    /// 部署期间暂时只能打英文，让用户知道在等什么。短部署（预计 6 秒内）只有超过 3 秒才出现。
    /// 也会发现安装包或开始菜单启动的部署程序（YiweiDeployer.exe）。
    /// 墨线视觉：纸色底、细边框、品牌蓝进度条；深色模式跟随系统。
    /// </summary>
    sealed class DeployProgress : FloatingWindow
    {
        readonly Border _card;
        readonly TextBlock _title = new TextBlock { FontSize = 13.5, FontWeight = FontWeights.SemiBold };
        readonly TextBlock _detail = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0), MaxWidth = 280 };
        readonly ProgressBar _bar = new ProgressBar { Height = 4, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 10, 0, 0), BorderThickness = new Thickness(0) };
        readonly DispatcherTimer _tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        readonly DispatcherTimer _hide = new DispatcherTimer();
        readonly DispatcherTimer _watch = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        double _estimate;
        bool _firstTime, _externalSeen;

        static readonly Color Blue = Color.FromRgb(0x2B, 0x5B, 0xD7);

        public DeployProgress() : base(clickThrough: true)
        {
            var stack = new StackPanel { Width = 300 };
            stack.Children.Add(_title);
            stack.Children.Add(_detail);
            stack.Children.Add(_bar);
            _card = new Border
            {
                CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 12, 16, 14), Margin = new Thickness(14),
                BorderThickness = new Thickness(1), Child = stack,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 2, Opacity = 0.22 },
            };
            Content = _card;
            _tick.Tick += (s, e) => Update();
            _hide.Tick += (s, e) =>
            {
                _hide.Stop();
                var a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
                a.Completed += (x, y) => { if (!_hide.IsEnabled && !Deploy.Running) Hide(); };
                _card.BeginAnimation(OpacityProperty, a);
            };
            _watch.Tick += (s, e) => WatchExternal();
            Deploy.StateChanged += (finished, ok) =>
            {
                try { Dispatcher.BeginInvoke(new Action(() => { if (finished) Finished(ok); else Started(); })); } catch { }
            };
            _watch.Start();
        }

        void Paint()
        {
            bool dark = SystemTheme.IsDark;
            _card.Background = new SolidColorBrush(dark ? Color.FromRgb(0x20, 0x23, 0x2C) : Color.FromRgb(0xFB, 0xFC, 0xFA));
            _card.BorderBrush = new SolidColorBrush(dark ? Color.FromRgb(0x36, 0x3A, 0x45) : Color.FromRgb(0xDD, 0xE3, 0xDF));
            _title.Foreground = new SolidColorBrush(dark ? Color.FromRgb(0xE8, 0xEC, 0xE9) : Color.FromRgb(0x1F, 0x24, 0x21));
            _detail.Foreground = new SolidColorBrush(dark ? Color.FromRgb(0x9A, 0xA3, 0x9E) : Color.FromRgb(0x6A, 0x73, 0x6E));
            _bar.Foreground = new SolidColorBrush(Blue);
            _bar.Background = new SolidColorBrush(dark ? Color.FromRgb(0x36, 0x3A, 0x45) : Color.FromRgb(0xE6, 0xEB, 0xF7));
        }

        void Started()
        {
            _firstTime = DeployTimes.FirstTime;
            _estimate = DeployTimes.Estimate();
            _hide.Stop();
            _tick.Start();
            Update();
        }

        void Update()
        {
            if (!Deploy.Running) { _tick.Stop(); return; }
            var elapsed = (DateTime.Now - Deploy.StartedAt).TotalSeconds;
            // short deployments (style changes) stay silent unless they turn out slow
            if (!IsVisible && _estimate < 6 && elapsed < 3) return;
            bool sync = Deploy.Kind == "/sync";
            _title.Text = sync ? "一维输入法 · 正在同步用户词库" : _firstTime ? "一维输入法 · 正在首次部署" : "一维输入法 · 正在部署";
            string what = sync ? "正在合并各设备的用户词库。" : _firstTime ? "第一次使用需要编译词库，大约需要 " + Human(_estimate) + "。" : "正在应用新的设置和词库。";
            string left;
            if (elapsed < _estimate)
            {
                left = "已用 " + Human(elapsed) + " · 预计还需约 " + Human(Math.Max(1, _estimate - elapsed));
                _bar.IsIndeterminate = false;
                _bar.Value = Math.Min(0.95, elapsed / Math.Max(1, _estimate));
            }
            else
            {
                left = "已用 " + Human(elapsed) + " · 比预计慢一些，请稍候";
                _bar.IsIndeterminate = true;
            }
            _detail.Text = what + "\n" + left + (sync ? "" : "\n部署完成前暂时只能输入英文。");
            ShowCorner();
        }

        void Finished(bool ok)
        {
            _tick.Stop();
            if (!IsVisible) return; // it never needed to show
            _bar.IsIndeterminate = false;
            _bar.Value = ok ? 1 : 0;
            bool sync = Deploy.Kind == "/sync";
            _title.Text = ok ? (sync ? "同步完成" : "部署完成，可以打字了") : (sync ? "同步没有完成" : "部署没有完成");
            _detail.Text = ok ? "用时 " + Human((DateTime.Now - Deploy.StartedAt).TotalSeconds) + "。"
                              : "可以在托盘菜单里点「重新部署」再试一次；仍不行请用「导出诊断信息」反馈给我们。";
            ShowCorner();
            _hide.Interval = TimeSpan.FromMilliseconds(ok ? 2200 : 7000);
            _hide.Start();
        }

        void ShowCorner()
        {
            Paint();
            _card.BeginAnimation(OpacityProperty, null);
            _card.Opacity = 1;
            Native.GetCursorPos(out var c);
            var wa = Native.WorkArea(c.X, c.Y);
            // FloatingWindow places below the anchor or, when there is no room, above it: anchor at the bottom-right corner
            ShowAt(new Native.RECT { Left = wa.Right, Right = wa.Right, Top = wa.Bottom, Bottom = wa.Bottom });
        }

        /// <summary>A deployer we did not start (installer, 开始菜单「重新部署」, WeaselDeployer by hand).</summary>
        void WatchExternal()
        {
            if (Deploy.Running && Deploy.Kind != "external") { _externalSeen = false; return; }
            bool running = false;
            foreach (var name in new[] { "YiweiDeployer", "WeaselDeployer" })
            {
                var ps = Process.GetProcessesByName(name);
                foreach (var p in ps) p.Dispose();
                if (ps.Length > 0) { running = true; break; }
            }
            if (running && !_externalSeen) { _externalSeen = true; Deploy.External(false, true); }
            else if (!running && _externalSeen) { _externalSeen = false; Deploy.External(true, true); }
        }

        static string Human(double seconds)
        {
            if (seconds < 60) return Math.Max(1, (int)Math.Round(seconds)) + " 秒";
            var m = (int)(seconds / 60); var s = (int)Math.Round(seconds - m * 60);
            return s >= 5 ? m + " 分 " + s + " 秒" : m + " 分钟";
        }
    }
}
