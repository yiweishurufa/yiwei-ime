using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using QRCoder;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    /// <summary>「局域网互传」窗口：二维码 + 地址；收到的手机文字自动复制；可以把文字发到手机。关窗即停止服务。</summary>
    sealed class LanShareWindow : Ui.FluentWindow
    {
        static readonly Color Blue = Color.FromRgb(0x2B, 0x5B, 0xD7);
        readonly LanShare _share = new LanShare();
        readonly StackPanel _inbox = new StackPanel();
        readonly TextBlock _status = K.Text("", 12, true);

        public LanShareWindow()
        {
            Title = "局域网互传 · 一维输入法";
            Width = 720; Height = 560; MinWidth = 600; MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = K.Font;
            ExtendsContentIntoTitleBar = true;
            WindowBackdropType = UiTheme.Backdrop;
            WindowCornerPreference = Ui.WindowCornerPreference.Round;
            try { Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(Program.AppIcon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()); } catch { }

            var root = new DockPanel();
            var title = new Ui.TitleBar { Title = "局域网互传", ShowMaximize = false };
            DockPanel.SetDock(title, Dock.Top);
            root.Children.Add(title);

            var grid = new Grid { Margin = new Thickness(24, 8, 24, 20) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var left = new StackPanel { Margin = new Thickness(0, 0, 24, 0) };
            var qrHost = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(16), Padding = new Thickness(14), Width = 240, Height = 240 };
            left.Children.Add(qrHost);
            var url = K.Text("", 12, true); url.TextWrapping = TextWrapping.Wrap; url.Margin = new Thickness(2, 10, 0, 0);
            left.Children.Add(K.Text("手机连同一个 Wi-Fi，扫码打开网页", 14, false, FontWeights.SemiBold));
            left.Children[1].SetValue(MarginProperty, new Thickness(2, 12, 0, 0));
            left.Children.Add(url);
            _status.Margin = new Thickness(2, 6, 0, 0); _status.TextWrapping = TextWrapping.Wrap;
            left.Children.Add(_status);
            left.Children.Add(new TextBlock
            {
                Text = "只在这个窗口打开时运行，地址带一次性口令；文字不经过任何服务器。第一次使用时 Windows 防火墙会询问，请允许「专用网络」。",
                FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 10, 0, 0), Opacity = 0.7,
            });
            grid.Children.Add(left);

            var right = new DockPanel();
            var sendBox = new Ui.TextBox { PlaceholderText = "要发到手机的文字", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70, MaxHeight = 140 };
            var send = K.Btn("发到手机", () =>
            {
                var t = sendBox.Text.Trim(); if (t.Length == 0) return;
                _share.SendToPhone(t); sendBox.Text = ""; _status.Text = "已发出，手机网页几秒内显示";
            }, Ui.ControlAppearance.Primary, Ui.SymbolRegular.Send24);
            var fromClip = K.Btn("发送剪贴板", () =>
            {
                try { var t = System.Windows.Forms.Clipboard.GetText(); if (!string.IsNullOrWhiteSpace(t)) { _share.SendToPhone(t); _status.Text = "已把剪贴板发到手机"; } } catch { }
            });
            var dict = K.Btn("发词库到手机", () =>
            {
                _status.Text = "正在打包词库与常用语…";
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "yiwei-to-phone-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".zip");
                System.Threading.Tasks.Task.Run(() => Backup.ExportForAndroid(path)).ContinueWith(t => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (t.IsFaulted) { _status.Text = "打包失败：" + t.Exception.GetBaseException().Message; return; }
                    _share.OfferFile(path, "一维词库-给手机-" + DateTime.Now.ToString("yyyyMMdd") + ".zip");
                    _status.Text = "手机网页上点「下载」，解压到 rime 文件夹后「同步用户数据」";
                })));
            });
            dict.Margin = new Thickness(8, 0, 0, 0);
            var sendRow = K.Row(send, fromClip, dict); sendRow.Margin = new Thickness(0, 8, 0, 0);
            ((FrameworkElement)sendRow.Children[0]).Margin = new Thickness(0, 0, 8, 0);
            var sendPanel = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            sendPanel.Children.Add(sendBox); sendPanel.Children.Add(sendRow);
            DockPanel.SetDock(sendPanel, Dock.Bottom);
            right.Children.Add(sendPanel);
            var head = K.Text("手机发来的（自动复制到剪贴板）", 13, true, FontWeights.SemiBold);
            DockPanel.SetDock(head, Dock.Top);
            right.Children.Add(head);
            _inbox.Children.Add(K.Text("还没有收到。", 13, true));
            right.Children.Add(new ScrollViewer { Content = _inbox, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 8, 0, 0) });
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);
            root.Children.Add(grid);
            Content = root;

            try
            {
                _share.Start();
                url.Text = _share.Url;
                qrHost.Child = new Image { Source = Qr(_share.Url), Stretch = Stretch.Uniform };
                RenderOptions.SetBitmapScalingMode(qrHost.Child, BitmapScalingMode.NearestNeighbor);
                _status.Text = "等待手机连接…";
            }
            catch (Exception e)
            {
                url.Text = "";
                _status.Text = "无法开启互传：" + e.Message;
                qrHost.Child = K.Text("未开启", 14, true);
            }
            _share.Connected += ip => Dispatcher.BeginInvoke(new Action(() => _status.Text = "手机已连接（" + ip + "）"));
            _share.Received += text => Dispatcher.BeginInvoke(new Action(() => OnReceived(text)));
            Closed += (s, e) => _share.Dispose();
        }

        void OnReceived(string text)
        {
            try { System.Windows.Forms.Clipboard.SetText(text); } catch { }
            if (_inbox.Children.Count == 1 && _inbox.Children[0] is TextBlock) _inbox.Children.Clear();
            var body = K.Text(text, 14);
            body.TextWrapping = TextWrapping.Wrap;
            var copy = K.Btn("复制", () => { try { System.Windows.Forms.Clipboard.SetText(text); _status.Text = "已复制"; } catch { } });
            copy.Margin = new Thickness(0, 6, 0, 0);
            var stack = new StackPanel();
            stack.Children.Add(K.Text(DateTime.Now.ToString("HH:mm:ss"), 11, true));
            stack.Children.Add(body);
            stack.Children.Add(copy);
            var card = K.CardBorder(stack);
            card.Margin = new Thickness(0, 0, 0, 8);
            _inbox.Children.Insert(0, card);
            _status.Text = "收到 " + text.Length + " 个字，已复制到剪贴板";
            if (!IsActive) Tray.Balloon("局域网互传", "收到手机文字，已复制：" + (text.Length > 40 ? text.Substring(0, 40) + "…" : text));
        }

        /// <summary>QR code drawn as vector modules in the brand blue.</summary>
        static DrawingImage Qr(string text)
        {
            using (var gen = new QRCodeGenerator())
            using (var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.M))
            {
                var m = data.ModuleMatrix;
                int n = m.Count;
                var geo = new GeometryGroup();
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                        if (m[y][x]) geo.Children.Add(new RectangleGeometry(new Rect(x, y, 1.02, 1.02)));
                var dg = new DrawingGroup();
                dg.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, n, n))));
                dg.Children.Add(new GeometryDrawing(new SolidColorBrush(Blue), null, geo));
                var img = new DrawingImage(dg);
                img.Freeze();
                return img;
            }
        }
    }
}
