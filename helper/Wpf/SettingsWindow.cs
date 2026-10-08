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
            public string Name; public Ui.SymbolRegular Icon; public Func<StackPanel> Build;
            public ScrollViewer View; public ListBoxItem Nav;
        }

        sealed class SearchEntry { public string Page, Title, Text; public Border Card; public override string ToString() => Title + "　·　" + Page; }

        readonly Settings S = Settings.Current;
        readonly List<PageDef> _pages = new List<PageDef>();
        readonly List<SearchEntry> _index = new List<SearchEntry>();
        readonly ListBox _nav = new ListBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent };
        readonly ContentControl _host = new ContentControl();
        readonly ToastHost _toast = new ToastHost();
        readonly Ui.TextBox _search = new Ui.TextBox { PlaceholderText = "搜索设置", Width = 300, Icon = new Ui.SymbolIcon { Symbol = Ui.SymbolRegular.Search24 } };
        readonly Popup _results = new Popup { StaysOpen = false, Placement = PlacementMode.Bottom, AllowsTransparency = true };
        readonly ListBox _resultList = new ListBox { MaxHeight = 320, MinWidth = 300 };
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

            Add("常规", Ui.SymbolRegular.Settings24, PageGeneral);
            Add("输入方案", Ui.SymbolRegular.Keyboard24, PageSchema);
            Add("快捷输入", Ui.SymbolRegular.Flash24, PageFeatures);
            Add("外观", Ui.SymbolRegular.PaintBrush24, PageAppearance);
            Add("快捷键", Ui.SymbolRegular.KeyboardShift24, PageHotkeys);
            Add("常用语", Ui.SymbolRegular.TextBulletListSquare24, PageSnippets);
            Add("AI", Ui.SymbolRegular.Sparkle24, PageAi);
            Add("词库", Ui.SymbolRegular.Library24, PageDicts);
            Add("应用规则", Ui.SymbolRegular.AppsList24, PageApps);
            Add("统计", Ui.SymbolRegular.DataTrending24, PageStats);
            Add("关于", Ui.SymbolRegular.Info24, PageAbout);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var title = new Ui.TitleBar { Title = "一维输入法设置", ShowMaximize = true };
            Grid.SetColumnSpan(title, 2);
            root.Children.Add(title);

            var left = new DockPanel { Margin = new Thickness(8, 4, 4, 8) };
            var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 4, 0, 14) };
            var dot = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 10, 0) };
            dot.Background = new SolidColorBrush(UiTheme.Accent);
            Action recolor = () => dot.Background = new SolidColorBrush(UiTheme.Accent);
            UiTheme.Changed += recolor;
            Closed += (s, e) => UiTheme.Changed -= recolor;
            dot.Child = new TextBlock { Text = "一", Foreground = Brushes.White, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 };
            brand.Children.Add(dot);
            brand.Children.Add(K.Text("一维输入法", 15, false, FontWeights.SemiBold));
            DockPanel.SetDock(brand, Dock.Top);
            left.Children.Add(brand);
            left.Children.Add(_nav);
            Grid.SetRow(left, 1);
            root.Children.Add(left);

            var right = new Grid();
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var searchRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(28, 4, 28, 0) };
            searchRow.Children.Add(_search);
            right.Children.Add(searchRow);
            Grid.SetRow(_host, 1);
            right.Children.Add(_host);
            Grid.SetRow(_toast, 1);
            right.Children.Add(_toast);
            Grid.SetRow(right, 1); Grid.SetColumn(right, 1);
            root.Children.Add(right);

            Content = root;

            foreach (var p in _pages)
            {
                var item = new ListBoxItem { Padding = new Thickness(10, 8, 10, 8), Tag = p };
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new Ui.SymbolIcon { Symbol = p.Icon, FontSize = 18, Margin = new Thickness(0, 0, 12, 0) });
                row.Children.Add(K.Text(p.Name, 14));
                item.Content = row;
                p.Nav = item;
                _nav.Items.Add(item);
            }
            _nav.SelectionChanged += (s, e) => { if (_nav.SelectedItem is ListBoxItem it && it.Tag is PageDef p) ShowPage(p); };

            // build every page once so search can see every setting
            foreach (var p in _pages) BuildPage(p);
            _nav.SelectedIndex = 0;

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

        void Add(string name, Ui.SymbolRegular icon, Func<StackPanel> build) => _pages.Add(new PageDef { Name = name, Icon = icon, Build = build });

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
                content.Children.Add(K.Text("这一页加载失败：" + e.Message, 13, true));
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

        void ShowPage(PageDef p)
        {
            _host.Content = p.View;
            p.View.ScrollToTop();
            if (p.Name == "统计") RefreshStats();
            if (_nav.SelectedItem != p.Nav) _nav.SelectedItem = p.Nav;
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
            if (_resultList.Items.Count == 0) _resultList.Items.Add(new SearchEntry { Title = "没有找到相关设置", Page = "" });
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
