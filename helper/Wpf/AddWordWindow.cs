using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    /// <summary>「加词」小窗：选中的词 + 自动注音（可改），一键加入用户词库。长按 Alt + A 或托盘打开。</summary>
    sealed class AddWordWindow : Ui.FluentWindow
    {
        static AddWordWindow _open;

        public static void ShowFor(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length > 32) text = text.Substring(0, 32);
            if (_open != null) { _open.SetWord(text); _open.Activate(); return; }
            _open = new AddWordWindow();
            _open.Closed += (s, e) => _open = null;
            _open.SetWord(text);
            _open.Show();
            _open.Activate();
        }

        readonly Ui.TextBox _word = new Ui.TextBox { PlaceholderText = "要加的词，例如 一维输入法", FontSize = 16 };
        readonly Ui.TextBox _py = new Ui.TextBox { PlaceholderText = "拼音，音节之间空格，例如 yi wei shu ru fa" };
        readonly TextBlock _msg = K.Text("", 12, true);
        int _gen;

        AddWordWindow()
        {
            Title = "加词 · 一维输入法";
            Width = 460; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen; Topmost = true;
            FontFamily = K.Font; ExtendsContentIntoTitleBar = true; WindowBackdropType = UiTheme.Backdrop;
            WindowCornerPreference = Ui.WindowCornerPreference.Round;
            var root = new StackPanel();
            root.Children.Add(new Ui.TitleBar { Title = "加词", ShowMaximize = false, ShowMinimize = false });
            var body = new StackPanel { Margin = new Thickness(24, 4, 24, 20) };
            body.Children.Add(K.Text("加进用户词库后，打拼音时它会出现在前面", 13, true));
            _word.Margin = new Thickness(0, 12, 0, 0); _py.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(_word); body.Children.Add(_py);
            _msg.Margin = new Thickness(2, 8, 0, 0); _msg.TextWrapping = TextWrapping.Wrap;
            body.Children.Add(_msg);
            Ui.Button add = null;
            add = K.Btn("加入词库", async () =>
            {
                var err = AddWord.Validate(_word.Text, _py.Text);
                if (err != null) { _msg.Text = err; return; }
                add.IsEnabled = false; _msg.Text = "正在加入并同步…";
                string w = _word.Text.Trim(), p = _py.Text;
                try
                {
                    await Task.Run(() => AddWord.Add(w, p));
                    Tray.Balloon("加词", "已加入：" + w + "（" + AddWord.Normalize(p) + "）");
                    Close();
                }
                catch (Exception ex) { _msg.Text = "没加进去：" + ex.GetBaseException().Message; add.IsEnabled = true; }
            }, Ui.ControlAppearance.Primary, Ui.SymbolRegular.Add24);
            var cancel = K.Btn("取消", Close);
            cancel.Margin = new Thickness(8, 0, 0, 0);
            var row = K.Row(add, cancel); row.Margin = new Thickness(0, 14, 0, 0); row.HorizontalAlignment = HorizontalAlignment.Right;
            body.Children.Add(row);
            root.Children.Add(body);
            Content = root;
            _word.TextChanged += (s, e) => Reguess();
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape) Close();
                if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; add.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); }
            };
            Loaded += (s, e) => { _word.Focus(); _word.SelectAll(); };
        }

        void SetWord(string text) { _word.Text = text; Reguess(); }

        async void Reguess()
        {
            int gen = ++_gen;
            var w = _word.Text.Trim();
            if (w.Length == 0 || !AddWord.Cjk.IsMatch(w)) { _py.Text = ""; _msg.Text = w.Length == 0 ? "" : "只能加汉字词"; return; }
            _msg.Text = "正在注音…";
            var py = await Task.Run(() => AddWord.Guess(w));
            if (gen != _gen) return;
            _py.Text = py;
            _msg.Text = py.Length == 0 ? "有字找不到读音，请手动填写拼音" : "读音不对可以直接改（多音字请检查）";
        }
    }
}
