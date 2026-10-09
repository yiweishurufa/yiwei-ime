using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    /// <summary>首次引导: 欢迎 → 选方案 → 选外观 → 导入旧习惯 → AI 与隐私 → 试一试 → 完成. Every step can be skipped.</summary>
    sealed class WizardWindow : Ui.FluentWindow
    {
        readonly Settings S = Settings.Current;
        readonly string[] _titles = { "欢迎", "选方案", "选外观", "导入旧习惯", "AI 与隐私", "试一试", "完成" };
        readonly Func<UIElement>[] _steps;
        readonly ContentControl _host = new ContentControl();
        readonly StackPanel _dots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        readonly Ui.Button _back, _skip, _next;
        readonly ToastHost _toast = new ToastHost();
        int _step;
        bool _changed;

        public WizardWindow()
        {
            Title = "欢迎使用一维输入法";
            Width = 820; Height = 640; MinWidth = 720; MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = K.Font;
            try { Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(Program.AppIcon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()); } catch { }
            ExtendsContentIntoTitleBar = true;
            WindowBackdropType = UiTheme.Backdrop;
            WindowCornerPreference = Ui.WindowCornerPreference.Round;

            _steps = new Func<UIElement>[] { Welcome, Schema, Look, Import, AiStep, Practice, Done };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.Children.Add(new Ui.TitleBar { Title = "一维输入法 · 首次引导", ShowMaximize = false, ShowMinimize = false });
            var scroll = new ScrollViewer { Content = _host, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(40, 8, 40, 8) };
            Grid.SetRow(scroll, 1); root.Children.Add(scroll);
            Grid.SetRow(_toast, 1); root.Children.Add(_toast);

            var footer = new Grid { Margin = new Thickness(32, 8, 32, 20) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _back = K.Btn("上一步", () => Go(_step - 1));
            _skip = K.Btn("跳过", () => Go(_step + 1), Ui.ControlAppearance.Transparent);
            _next = K.Btn("下一步", () => { if (_step == _steps.Length - 1) Finish(); else Go(_step + 1); }, Ui.ControlAppearance.Primary);
            footer.Children.Add(_back);
            Grid.SetColumn(_dots, 1); footer.Children.Add(_dots);
            var right = K.Row(_skip, _next); Grid.SetColumn(right, 2); footer.Children.Add(right);
            Grid.SetRow(footer, 2); root.Children.Add(footer);
            Content = root;

            _toast.TrackDeploy(this);
            Closed += (s, e) =>
            {
                if (!S.FirstRunDone) { S.FirstRunDone = true; try { S.Save(); } catch { } }
                if (_changed) Rime.ApplySoon(100);
            };
            Go(0);
        }

        void Go(int step)
        {
            _step = Math.Max(0, Math.Min(_steps.Length - 1, step));
            UIElement content;
            try { content = _steps[_step](); }
            catch (Exception e) { Log.Write("wizard step: " + e); content = K.Text("这一步加载失败：" + e.Message, 13, true); }
            _host.Content = content;
            _back.Visibility = _step == 0 ? Visibility.Hidden : Visibility.Visible;
            _skip.Visibility = _step == 0 || _step == _steps.Length - 1 ? Visibility.Hidden : Visibility.Visible;
            _next.Content = _step == 0 ? "开始设置" : _step == _steps.Length - 1 ? "完成" : "下一步";
            _dots.Children.Clear();
            for (int i = 0; i < _steps.Length; i++)
            {
                var d = new Border { Width = i == _step ? 22 : 8, Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(3, 0, 3, 0), ToolTip = _titles[i] };
                if (i == _step) d.Background = new SolidColorBrush(UiTheme.Accent); else d.SetResourceReference(Border.BackgroundProperty, "ControlStrongFillColorDisabledBrush");
                _dots.Children.Add(d);
            }
        }

        void Changed() { _changed = true; try { S.Save(); } catch { } }

        void Finish()
        {
            S.FirstRunDone = true;
            Changed();
            Close();
        }

        static StackPanel Head(string title, string sub)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 12, 0, 18) };
            sp.Children.Add(K.Text(title, 26, false, FontWeights.SemiBold));
            var s = K.Text(sub, 14, true); s.Margin = new Thickness(0, 6, 0, 0);
            sp.Children.Add(s);
            return sp;
        }

        static Border Pick(UIElement content, bool selected)
        {
            var b = K.CardBorder(content, new Thickness(16));
            b.Margin = new Thickness(0, 0, 10, 10);
            b.Cursor = Cursors.Hand;
            Select(b, selected);
            return b;
        }

        static void Select(Border b, bool on)
        {
            if (on) { b.BorderBrush = new SolidColorBrush(UiTheme.Accent); b.BorderThickness = new Thickness(2); }
            else { b.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush"); b.BorderThickness = new Thickness(1); }
        }

        // ---------- steps ----------

        UIElement Welcome()
        {
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 40, 0, 0) };
            var logo = new Image { Width = 72, Height = 72, HorizontalAlignment = HorizontalAlignment.Left };
            try
            {
                using (var big = new System.Drawing.Icon(Program.AppIcon, 256, 256))
                    logo.Source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(big.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            }
            catch { }
            RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
            sp.Children.Add(logo);
            sp.Children.Add(Head("欢迎使用一维输入法", "中文常新，自在表达。基于 RIME 与雾凇拼音，组字、候选和个人词频都在本机处理，不含遥测。"));
            sp.Children.Add(K.Text("接下来用一分钟，选好输入方案和外观，再把旧输入法里的词带过来。每一步都可以跳过，之后也能在托盘菜单「首次引导」里重新打开。", 14, true));
            return sp;
        }

        UIElement Schema()
        {
            var sp = new StackPanel();
            sp.Children.Add(Head("你习惯怎么打拼音？", "选一个默认方案，以后可随时在设置或托盘菜单里切换。"));
            var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
            var cards = new List<Border>();
            foreach (var sc in Rime.Schemas)
            {
                var id = sc.Id;
                var inner = new StackPanel();
                inner.Children.Add(K.Text(sc.Name, 16, false, FontWeights.SemiBold));
                inner.Children.Add(K.Text(sc.Desc, 12, true));
                if (id != "rime_ice") inner.Children.Add(new KeyboardDiagram(id) { Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Left });
                else { var t = K.Text("yi wei → 一维\nzhong wen → 中文", 13, true); t.Margin = new Thickness(0, 10, 0, 0); inner.Children.Add(t); }
                var card = Pick(inner, S.Schema == id);
                card.Tag = id;
                card.MouseLeftButtonUp += (s, e) => { S.Schema = id; Changed(); foreach (var c in cards) Select(c, (string)c.Tag == id); };
                cards.Add(card); grid.Children.Add(card);
            }
            sp.Children.Add(grid);
            return sp;
        }

        UIElement Look()
        {
            var sp = new StackPanel();
            sp.Children.Add(Head("挑一个顺眼的候选窗", "深色模式下会自动换成对应的深色版。"));
            var preview = new CandidatePreview();
            void Refresh()
            {
                preview.Scheme = WeaselConfig.ActiveScheme(S); preview.Horizontal = S.Horizontal;
                preview.FontName = S.FontFace; preview.FontPoint = S.FontPoint; preview.Radius = S.CornerRadius; preview.HRadius = S.HilitedCornerRadius;
                preview.Refresh();
            }
            var wrap = new WrapPanel();
            var cards = new List<Border>();
            foreach (var b in Brand.Palette)
            {
                var id = Brand.SchemeId(b.Id, false);
                var inner = new StackPanel { Orientation = Orientation.Horizontal };
                inner.Children.Add(new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), Background = new SolidColorBrush(Color.FromRgb(b.R, b.G, b.B)), Margin = new Thickness(0, 0, 8, 0) });
                inner.Children.Add(K.Text(b.Name, 14));
                var card = Pick(inner, S.ColorScheme == id);
                card.Tag = id; card.Padding = new Thickness(14, 10, 18, 10);
                card.MouseLeftButtonUp += (s, e) =>
                {
                    S.ColorScheme = id; S.Accent = b.Id; S.FollowSystemDark = true; Changed();
                    foreach (var c in cards) Select(c, (string)c.Tag == id);
                    UiTheme.Apply(); Refresh(); Go(_step);
                };
                cards.Add(card); wrap.Children.Add(card);
            }
            sp.Children.Add(wrap);
            var pv = K.CardBorder(preview, new Thickness(8)); pv.Margin = new Thickness(0, 4, 0, 12);
            sp.Children.Add(pv);
            sp.Children.Add(K.Card("候选排列", "横排一行，或竖排列表",
                K.Segmented(new[] { new KeyValuePair<bool, string>(true, "横排"), new KeyValuePair<bool, string>(false, "竖排") }, S.Horizontal, v => { S.Horizontal = v; S.VerticalText = false; Changed(); Refresh(); })));
            sp.Children.Add(K.Card("候选个数", "每页显示几个候选",
                K.Segmented(new[] { new KeyValuePair<int, string>(5, "5"), new KeyValuePair<int, string>(7, "7"), new KeyValuePair<int, string>(9, "9") }, S.CandidateCount, v => { S.CandidateCount = v; Changed(); })));
            Refresh();
            return sp;
        }

        UIElement Import()
        {
            var sp = new StackPanel();
            sp.Children.Add(Head("把旧习惯带过来", "导入以前积累的词，打字一开始就顺手。没有找到的可以跳过。"));
            List<HabitImport.Source> sources;
            try { sources = HabitImport.Detect(); } catch (Exception e) { Log.Write("detect: " + e.Message); sources = new List<HabitImport.Source>(); }
            foreach (var src in sources)
            {
                var s = src;
                Ui.Button btn;
                if (s.Name == "小狼毫")
                {
                    btn = K.Btn(s.Found ? "导入" : "未找到", () =>
                    {
                        try { var n = HabitImport.ImportWeasel(); _toast.Show($"已从小狼毫导入 {n} 个文件"); }
                        catch (Exception ex) { Dialogs.Error(ex.Message); }
                    }, s.Found ? Ui.ControlAppearance.Primary : Ui.ControlAppearance.Secondary);
                    btn.IsEnabled = s.Found;
                }
                else
                {
                    btn = K.Btn("选择导出的文件…", () =>
                    {
                        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "文本词库 (*.txt)|*.txt|所有文件|*.*", Title = "选择从" + s.Name + "导出的词库" };
                        if (dlg.ShowDialog(this) != true) return;
                        try
                        {
                            int n = HabitImport.ImportTextFile(dlg.FileName);
                            if (n == 0) Dialogs.Error("没有读到带拼音的词。请确认导出的是文本格式。"); else _toast.Show($"已导入 {n} 个词");
                        }
                        catch (Exception ex) { Dialogs.Error("导入失败：" + ex.Message); }
                    });
                }
                var desc = (s.Found ? "已检测到。" : "这台电脑上没有检测到。") + s.Desc;
                sp.Children.Add(K.Card(s.Name, desc, btn, s.Found ? Ui.SymbolRegular.Checkmark24 : Ui.SymbolRegular.Dismiss24));
            }
            return sp;
        }

        UIElement AiStep()
        {
            var sp = new StackPanel();
            sp.Children.Add(Head("AI 与隐私", "AI 助手默认关闭。打字本身永远在本机完成。"));
            var note = K.CardBorder(K.Text("🔒  只有你选中并按快捷键的文字才会发出去。\n选中一段话，长按 Alt 再按空格，才会把这一段发给你配置的接口，用于翻译、润色或改写成粤语。其他任何输入都不会离开这台电脑。用本机 Ollama 则完全离线。", 13));
            note.Margin = new Thickness(0, 0, 0, 12);
            sp.Children.Add(note);
            var fields = new StackPanel { IsEnabled = S.AiEnabled, Opacity = S.AiEnabled ? 1 : 0.5 };
            sp.Children.Add(K.Card("启用 AI 助手", "可以稍后在设置里开启", K.Toggle(S.AiEnabled, v => { S.AiEnabled = v; fields.IsEnabled = v; fields.Opacity = v ? 1 : 0.5; Changed(); }), Ui.SymbolRegular.Sparkle24));
            var url = new Ui.TextBox { Text = S.AiBaseUrl, MinWidth = 340 };
            var model = new Ui.TextBox { Text = S.AiModel, MinWidth = 200 };
            var key = new Ui.PasswordBox { Password = S.AiKey, MinWidth = 340, PlaceholderText = "可选；本机 Ollama 不需要" };
            url.TextChanged += (s, e) => { S.AiBaseUrl = url.Text.Trim(); Changed(); };
            model.TextChanged += (s, e) => { S.AiModel = model.Text.Trim(); Changed(); };
            key.PasswordChanged += (s, e) => { S.AiKey = key.Password.Trim(); Changed(); };
            fields.Children.Add(K.Card("接口地址", "任何 OpenAI 兼容接口，如 DeepSeek、通义、Kimi、Ollama", url));
            fields.Children.Add(K.Card("模型", "接口里的模型名称", model));
            fields.Children.Add(K.Card("API 密钥", "加密后只保存在这台电脑上", key, Ui.SymbolRegular.Shield24));
            sp.Children.Add(fields);
            return sp;
        }

        UIElement Practice()
        {
            var sp = new StackPanel();
            sp.Children.Add(Head("试一试", "在下面的框里打几个字。设置正在后台生效，稍等几秒就能用上新方案和配色。"));
            var box = new TextBox { MinHeight = 140, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 16, Padding = new Thickness(10) };
            sp.Children.Add(box);
            var tips = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            foreach (var t in new[]
            {
                "试着打 yiwei，按空格上屏「一维」",
                "按一下 Shift 切换中 / 英文，光标旁会出现「中」或「A」",
                "长按 Alt 再按 1，打开常用语面板",
                "打 rq 看看今天的日期",
            })
            {
                var row = K.Text("•  " + t, 13, true); row.Margin = new Thickness(0, 3, 0, 3);
                tips.Children.Add(row);
            }
            sp.Children.Add(tips);
            if (_changed) { Rime.ApplySoon(100); _changed = false; }
            Loaded += (s, e) => box.Focus();
            Dispatcher.BeginInvoke(new Action(() => box.Focus()), System.Windows.Threading.DispatcherPriority.Input);
            return sp;
        }

        UIElement Done()
        {
            var sp = new StackPanel();
            sp.Children.Add(Head("一切就绪", "这些快捷键值得记住。随时可在托盘图标里打开设置。"));
            sp.Children.Add(K.CardBorder(SettingsWindow.CheatSheet()));
            return sp;
        }
    }
}
