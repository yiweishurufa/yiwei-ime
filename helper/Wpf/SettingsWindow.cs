using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    /// <summary>「一维输入法设置」: Fluent window with left navigation, search, and instant-apply pages.</summary>
    sealed partial class SettingsWindow : Ui.FluentWindow
    {
        sealed class PageDef
        {
            public string Name, Group; public Ui.SymbolRegular Icon; public Func<StackPanel> Build;
            public ScrollViewer View; public ListBoxItem Nav; public Border Bar;
        }

        sealed class SearchEntry { public string Page, Title, Text; public Border Card; public override string ToString() => string.IsNullOrEmpty(Page) ? Title : Title + "　·　" + Page; }

        readonly Settings S = Settings.Current;
        readonly List<PageDef> _pages = new List<PageDef>();
        readonly List<SearchEntry> _index = new List<SearchEntry>();
        readonly ListBox _nav = new ListBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent };
        readonly ContentControl _host = new ContentControl();
        readonly ToastHost _toast = new ToastHost();
        readonly Ui.TextBox _search = new Ui.TextBox { PlaceholderText = "搜索设置（Ctrl+F）", Icon = new Ui.SymbolIcon { Symbol = Ui.SymbolRegular.Search24 } };
        readonly Popup _results = new Popup { StaysOpen = false, Placement = PlacementMode.Bottom, AllowsTransparency = true };
        readonly ListBox _resultList = new ListBox { MaxHeight = 360, MinWidth = 320 };
        string _buildingPage;

        public SettingsWindow()
        {
            Title = "一维输入法设置";
            Width = 1040; Height = 720; MinWidth = 820; MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = K.Font;
            try { Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(Program.AppIcon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()); } catch { }
            ExtendsContentIntoTitleBar = true;
            WindowBackdropType = UiTheme.Backdrop;
            WindowCornerPreference = Ui.WindowCornerPreference.Round;

            // v2 信息架构：13 页按任务分 4 组（DESIGN-v2 §7.1）；页名不变，GoTo / 托盘 / 管道命令照旧
            Add("常规", PageGeneral, "输入");
            Add("输入方案", PageSchema, "输入");
            Add("快捷输入", PageFeatures, "输入");
            Add("快捷键", PageHotkeys, "输入");
            Add("语音", PageVoice, "输入");
            Add("外观", PageAppearance, "个性化");
            Add("常用语", PageSnippets, "个性化");
            Add("词库", PageDicts, "个性化");
            Add("AI", PageAi, "智能与工具");
            Add("工具", PageTools, "智能与工具");
            Add("应用规则", PageApps, "智能与工具");
            Add("统计", PageStats, "数据与关于");
            Add("关于", PageAbout, "数据与关于");

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(248) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var title = new Ui.TitleBar { Title = "一维输入法设置", ShowMaximize = true };
            Grid.SetColumnSpan(title, 2);
            root.Children.Add(title);

            // 左栏：品牌标 → 搜索 → 分组导航
            var left = new DockPanel { Margin = new Thickness(Tok.S12, 0, Tok.S4, Tok.S12) };
            var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(Tok.S8, Tok.S4, 0, Tok.S16) };
            // 品牌标：渐变方块上的一笔横和一点，和安装包、任务栏图标是同一个。
            var dot = new Image { Width = 28, Height = 28, Margin = new Thickness(0, 0, Tok.S12, 0) };
            try { dot.Source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(Program.AppIcon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromWidthAndHeight(64, 64)); } catch { }
            RenderOptions.SetBitmapScalingMode(dot, BitmapScalingMode.HighQuality);
            brand.Children.Add(dot);
            var brandText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var brandName = K.Text("一维输入法", Tok.Subtitle, false, FontWeights.SemiBold); brandName.FontFamily = Tok.Display;
            brandText.Children.Add(brandName);
            brandText.Children.Add(K.Text("设置", Tok.Caption, true));
            brand.Children.Add(brandText);
            DockPanel.SetDock(brand, Dock.Top);
            left.Children.Add(brand);
            _search.Margin = new Thickness(Tok.S4, 0, Tok.S8, Tok.S12);
            System.Windows.Automation.AutomationProperties.SetName(_search, "搜索设置");
            DockPanel.SetDock(_search, Dock.Top);
            left.Children.Add(_search);
            ScrollViewer.SetHorizontalScrollBarVisibility(_nav, ScrollBarVisibility.Disabled);
            System.Windows.Automation.AutomationProperties.SetName(_nav, "设置分类");
            left.Children.Add(_nav);
            Grid.SetRow(left, 1);
            root.Children.Add(left);

            var right = new Grid();
            right.Children.Add(_host);
            right.Children.Add(_toast);
            Grid.SetRow(right, 1); Grid.SetColumn(right, 1);
            root.Children.Add(right);

            Content = root;

            string group = null;
            foreach (var p in _pages)
            {
                if (p.Group != group)
                {
                    group = p.Group;
                    // 组名：不可选、不可聚焦的小标题
                    var head = K.Text(group, Tok.Caption, true, FontWeights.SemiBold);
                    head.Margin = new Thickness(Tok.S12, _nav.Items.Count == 0 ? 0 : Tok.S16, 0, Tok.S4);
                    _nav.Items.Add(new ListBoxItem { Content = head, IsEnabled = false, Focusable = false, IsHitTestVisible = false, Padding = new Thickness(0), Background = Brushes.Transparent });
                }
                var item = new ListBoxItem { Padding = new Thickness(0), Tag = p, Margin = new Thickness(0, 1, 0, 1) };
                System.Windows.Automation.AutomationProperties.SetName(item, p.Name);
                System.Windows.Automation.AutomationProperties.SetHelpText(item, K.PageInfo.TryGetValue(p.Name, out var info) ? info.Desc : "");
                var g = new Grid { Height = 38 };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                // 选中条：强调色 3×16 竖条，选中时从中间展开（DESIGN-v2 §6）
                p.Bar = new Border { Width = 3, Height = 16, CornerRadius = new CornerRadius(1.5), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(1, 0) };
                p.Bar.SetResourceReference(Border.BackgroundProperty, "AccentFillColorDefaultBrush");
                g.Children.Add(p.Bar);
                var ic = new Ui.SymbolIcon { Symbol = p.Icon, FontSize = 18, Margin = new Thickness(0, 0, Tok.S12, 0), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(ic, 1); g.Children.Add(ic);
                var name = K.Text(p.Name, Tok.Body); name.VerticalAlignment = VerticalAlignment.Center; name.TextWrapping = TextWrapping.NoWrap;
                Grid.SetColumn(name, 2); g.Children.Add(name);
                item.Content = g;
                p.Nav = item;
                _nav.Items.Add(item);
            }
            _nav.SelectionChanged += (s, e) =>
            {
                if (_nav.SelectedItem is ListBoxItem it && it.Tag is PageDef p) ShowPage(p);
                else if (_nav.SelectedItem is ListBoxItem hdr && hdr.Tag == null && e.RemovedItems.Count > 0) _nav.SelectedItem = e.RemovedItems[0]; // 组名不能选中
            };

            // build every page once so search can see every setting
            foreach (var p in _pages) BuildPage(p);
            _nav.SelectedItem = _pages[0].Nav;

            _results.Child = K.CardBorder(_resultList, new Thickness(4));
            _results.PlacementTarget = _search;
            _search.TextChanged += (s, e) => UpdateSearch();
            _search.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Down && _resultList.Items.Count > 0) { _resultList.SelectedIndex = 0; (_resultList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus(); e.Handled = true; }
                if (e.Key == Key.Enter && _resultList.Items.Count > 0) { Jump(_resultList.Items[0] as SearchEntry); e.Handled = true; }
                if (e.Key == Key.Escape) { _results.IsOpen = false; }
            };
            _resultList.MouseLeftButtonUp += (s, e) => Jump(_resultList.SelectedItem as SearchEntry);
            _resultList.KeyDown += (s, e) => { if (e.Key == Key.Enter) Jump(_resultList.SelectedItem as SearchEntry); };

            _toast.TrackDeploy(this);
            PreviewKeyDown += (s, e) => { if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { _search.Focus(); e.Handled = true; } };
            Closing += (s, e) => FlushPending();
            UiTheme.Changed += RepaintVisuals;
            Closed += (s, e) => UiTheme.Changed -= RepaintVisuals;
        }

        void Add(string name, Func<StackPanel> build, string group) =>
            _pages.Add(new PageDef { Name = name, Group = group, Icon = K.PageInfo.TryGetValue(name, out var i) ? i.Icon : Ui.SymbolRegular.Settings24, Build = build });

        void BuildPage(PageDef p)
        {
            _buildingPage = p.Name;
            StackPanel content;
            try { content = p.Build(); }
            catch (Exception e)
            {
                Log.Write("page " + p.Name + ": " + e);
                content = new StackPanel();
                content.Children.Add(K.PageTitle(p.Name));
                content.Children.Add(K.ErrorState("这一页加载失败：" + e.Message + "。可以导出诊断信息反馈给我们。"));
            }
            p.View = K.Page(content);
            foreach (var card in FindCards(content))
            {
                var text = card.Tag as string ?? "";
                var title = text.Split('\n').FirstOrDefault() ?? "";
                _index.Add(new SearchEntry { Page = p.Name, Title = title, Text = text.Replace('\n', ' ').ToLowerInvariant() + " " + p.Name.ToLowerInvariant(), Card = card });
            }
        }

        static IEnumerable<Border> FindCards(Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Border b && b.Tag is string) yield return b;
                else if (child is Panel inner) foreach (var x in FindCards(inner)) yield return x;
            }
        }

        PageDef _current;

        void ShowPage(PageDef p)
        {
            bool changed = _current != p;
            _host.Content = p.View;
            p.View.ScrollToTop();
            if (changed)
            {
                Motion.FadeInUp(p.View, 12); // 页面切换：淡入 + 上移 12px（系统关了动画效果时直接显示）
                foreach (var x in _pages) SetBar(x, x == p);
                _current = p;
            }
            if (p.Name == "统计") RefreshStats();
            if (_nav.SelectedItem != p.Nav) _nav.SelectedItem = p.Nav;
        }

        static void SetBar(PageDef p, bool on)
        {
            if (p.Bar?.RenderTransform is ScaleTransform st)
            {
                if (!Motion.Enabled) { st.BeginAnimation(ScaleTransform.ScaleYProperty, null); st.ScaleY = on ? 1 : 0; return; }
                st.BeginAnimation(ScaleTransform.ScaleYProperty, new System.Windows.Media.Animation.DoubleAnimation(on ? 1 : 0, on ? Tok.Slow : Tok.Fast) { EasingFunction = Tok.Decelerate });
            }
        }

        public void GoTo(string page)
        {
            if (page == "AI 助手") page = "AI";
            if (page == "应用") page = "应用规则";
            if (page == "导入") page = "词库";
            var p = _pages.FirstOrDefault(x => x.Name == page);
            if (p != null) ShowPage(p);
        }

        void UpdateSearch()
        {
            var q = (_search.Text ?? "").Trim().ToLowerInvariant();
            _resultList.Items.Clear();
            if (q.Length == 0) { _results.IsOpen = false; return; }
            foreach (var e in _index.Where(x => x.Text.Contains(q)).OrderBy(x => x.Title.ToLowerInvariant().Contains(q) ? 0 : 1).Take(12))
                _resultList.Items.Add(e);
            if (_resultList.Items.Count == 0) _resultList.Items.Add(new SearchEntry { Title = "没有找到「" + _search.Text.Trim() + "」，换个说法试试（如：配色、双拼、剪贴板）", Page = "" });
            _results.IsOpen = true;
        }

        void Jump(SearchEntry e)
        {
            if (e == null || e.Card == null) return;
            _results.IsOpen = false;
            var p = _pages.First(x => x.Name == e.Page);
            ShowPage(p);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try { e.Card.BringIntoView(); K.Flash(e.Card); } catch { }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>Saves; when the RIME config changes too, writes it and redeploys in the background.</summary>
        void Save(bool rime = false)
        {
            try
            {
                if (rime) Rime.ApplySoon();
                else S.Save();
            }
            catch (Exception e) { Log.Write("save: " + e.Message); _toast.Show("保存失败：" + e.Message); }
        }

        void Toast(string text) => _toast.Show(text);

        readonly List<FrameworkElement> _themed = new List<FrameworkElement>();
        void RepaintVisuals() { foreach (var v in _themed) v.InvalidateVisual(); }
    }
}
