using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    sealed partial class SettingsWindow
    {
        // ================= 常用语 =================

        DispatcherTimer _snippetSave;
        bool _snippetDirty;

        void SnippetsChanged()
        {
            _snippetDirty = true;
            if (_snippetSave == null)
            {
                _snippetSave = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
                _snippetSave.Tick += (s, e) => { _snippetSave.Stop(); SaveSnippets(); };
            }
            _snippetSave.Stop(); _snippetSave.Start();
        }

        void SaveSnippets()
        {
            if (!_snippetDirty) return;
            _snippetDirty = false;
            try { SnippetBook.Current.Save(); }
            catch (Exception e) { Toast("常用语保存失败：" + e.Message); }
        }

        void FlushPending() { _snippetSave?.Stop(); SaveSnippets(); }

        /// <summary>Drag-to-reorder for a ListBox whose items are bound to a list.</summary>
        static void EnableDrag<T>(ListBox box, Func<List<T>> source, Action reordered)
        {
            Point start = default(Point);
            int from = -1;
            box.PreviewMouseLeftButtonDown += (s, e) =>
            {
                start = e.GetPosition(box);
                from = IndexAt(box, e.OriginalSource as DependencyObject);
                // don't start drags from text boxes
                if (e.OriginalSource is DependencyObject d && FindParent<TextBox>(d) != null) from = -1;
            };
            box.PreviewMouseMove += (s, e) =>
            {
                if (from < 0 || e.LeftButton != MouseButtonState.Pressed) return;
                var pos = e.GetPosition(box);
                if (Math.Abs(pos.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                int f = from; from = -1;
                DragDrop.DoDragDrop(box, new DataObject("yiwei-reorder", f), DragDropEffects.Move);
            };
            box.AllowDrop = true;
            box.Drop += (s, e) =>
            {
                if (!e.Data.GetDataPresent("yiwei-reorder")) return;
                int f = (int)e.Data.GetData("yiwei-reorder");
                int to = IndexAt(box, e.OriginalSource as DependencyObject);
                var list = source();
                if (to < 0) to = list.Count - 1;
                if (f < 0 || f >= list.Count || to == f) return;
                var item = list[f]; list.RemoveAt(f); list.Insert(Math.Min(to, list.Count), item);
                reordered();
            };
        }

        static int IndexAt(ListBox box, DependencyObject d)
        {
            var item = d == null ? null : FindParent<ListBoxItem>(d);
            return item == null ? -1 : box.ItemContainerGenerator.IndexFromContainer(item);
        }

        static T FindParent<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null && !(d is T)) d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            return d as T;
        }

        StackPanel PageSnippets()
        {
            var book = SnippetBook.Current;
            var p = new StackPanel();
            p.Children.Add(K.PageTitle("常用语"));
            p.Children.Add(K.Text("长按 Alt 再按分组序号打开面板，按 1–9 直接上屏。填了「编码」的条目，打拼音时也会出现在候选里。拖动可以排序，修改自动保存。", 13, true));

            var groups = new ListBox { Width = 200, MinHeight = 320, Margin = new Thickness(0, 0, 12, 0) };
            var items = new ListBox { MinHeight = 320, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            int current = 0;

            void FillGroups()
            {
                groups.Items.Clear();
                for (int i = 0; i < book.Categories.Count; i++)
                    groups.Items.Add(new ListBoxItem { Content = (i < 9 ? (i + 1) + "  " : "    ") + book.Categories[i].Name, ToolTip = "拖动排序；前 9 个分组对应 Alt+1…9" });
                if (book.Categories.Count > 0) groups.SelectedIndex = Math.Max(0, Math.Min(current, book.Categories.Count - 1));
            }
            void FillItems()
            {
                items.Items.Clear();
                if (current < 0 || current >= book.Categories.Count) return;
                var list = book.Categories[current].Items;
                for (int i = 0; i < list.Count; i++)
                {
                    var sn = list[i];
                    var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var num = K.Text(i < 9 ? (i + 1).ToString() : "⋮", 13, true); num.VerticalAlignment = VerticalAlignment.Center; num.Cursor = Cursors.SizeAll; num.ToolTip = "拖动排序";
                    var text = new TextBox { Text = sn.Text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 80, Margin = new Thickness(0, 0, 6, 0) };
                    text.TextChanged += (s, e) => { sn.Text = text.Text; SnippetsChanged(); };
                    var code = new Ui.TextBox { Text = sn.Code, PlaceholderText = "编码（可选）", Margin = new Thickness(0, 0, 6, 0) };
                    code.TextChanged += (s, e) => { sn.Code = code.Text.Trim(); SnippetsChanged(); };
                    var del = new Ui.Button { Icon = new Ui.SymbolIcon { Symbol = Ui.SymbolRegular.Delete24 }, Appearance = Ui.ControlAppearance.Transparent, ToolTip = "删除" };
                    del.Click += (s, e) => { list.Remove(sn); SnippetsChanged(); FillItems(); };
                    Grid.SetColumn(text, 1); Grid.SetColumn(code, 2); Grid.SetColumn(del, 3);
                    row.Children.Add(num); row.Children.Add(text); row.Children.Add(code); row.Children.Add(del);
                    items.Items.Add(new ListBoxItem { Content = row });
                }
            }
            groups.SelectionChanged += (s, e) => { if (groups.SelectedIndex >= 0 && groups.SelectedIndex != current) { current = groups.SelectedIndex; FillItems(); } };
            EnableDrag(groups, () => book.Categories, () => { SnippetsChanged(); FillGroups(); FillItems(); });
            EnableDrag(items, () => book.Categories[current].Items, () => { SnippetsChanged(); FillItems(); });
            FillGroups(); FillItems();

            var newName = new Ui.TextBox { PlaceholderText = "分组名称", MinWidth = 140 };
            var groupButtons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0), Width = 200 };
            groupButtons.Children.Add(newName);
            var addG = K.Btn("添加分组", () =>
            {
                var n = newName.Text.Trim(); if (n.Length == 0) { newName.Focus(); return; }
                book.Categories.Add(new SnippetCategory { Name = n }); newName.Text = "";
                current = book.Categories.Count - 1; SnippetsChanged(); FillGroups(); FillItems();
            });
            var renG = K.Btn("重命名", () =>
            {
                var n = newName.Text.Trim(); if (n.Length == 0 || current < 0) { newName.Focus(); Toast("在框里输入新名称，再点「重命名」"); return; }
                book.Categories[current].Name = n; newName.Text = ""; SnippetsChanged(); FillGroups();
            });
            var delG = K.Btn("删除分组", () =>
            {
                if (current < 0 || current >= book.Categories.Count) return;
                if (!Dialogs.Confirm("删除分组「" + book.Categories[current].Name + "」和其中的全部内容？")) return;
                book.Categories.RemoveAt(current); current = 0; SnippetsChanged(); FillGroups(); FillItems();
            });
            foreach (var b in new[] { addG, renG, delG }) { b.Margin = new Thickness(0, 6, 6, 0); b.MinWidth = 0; groupButtons.Children.Add(b); }

            var addItem = K.Btn("添加一条", () =>
            {
                if (current < 0 || current >= book.Categories.Count) return;
                book.Categories[current].Items.Add(new Snippet { Text = "" }); FillItems();
                if (items.Items.Count > 0) items.ScrollIntoView(items.Items[items.Items.Count - 1]);
            }, Ui.ControlAppearance.Primary, Ui.SymbolRegular.Add24);
            addItem.Margin = new Thickness(0, 8, 0, 0); addItem.HorizontalAlignment = HorizontalAlignment.Left;

            var left = new StackPanel(); left.Children.Add(groups); left.Children.Add(groupButtons);
            var right = new StackPanel(); right.Children.Add(items); right.Children.Add(addItem);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(right, 1);
            grid.Children.Add(left); grid.Children.Add(right);
            p.Children.Add(K.Block("分组与条目", "手机号、邮箱、地址、符号……每组最多 9 条一页，超过可在面板里翻页", grid, Ui.SymbolRegular.TextBulletListSquare24));
            return p;
        }

        // ================= AI =================

        static readonly (string Name, string Url, string Model)[] Presets =
        {
            ("OpenAI", "https://api.openai.com/v1", "gpt-4o-mini"),
            ("DeepSeek", "https://api.deepseek.com/v1", "deepseek-chat"),
            ("通义千问（阿里云百炼）", "https://dashscope.aliyuncs.com/compatible-mode/v1", "qwen-plus"),
            ("Kimi（月之暗面）", "https://api.moonshot.cn/v1", "moonshot-v1-8k"),
            ("智谱 GLM", "https://open.bigmodel.cn/api/paas/v4", "glm-4-flash"),
            ("本机 Ollama（免费、离线）", "http://localhost:11434/v1", "qwen2.5:7b"),
            ("本机 LM Studio", "http://localhost:1234/v1", "local-model"),
        };

        StackPanel PageAi()
        {
            var p = new StackPanel();
            p.Children.Add(K.PageTitle("AI"));
            var privacy = K.Text("只有你选中并按快捷键的文字才会发出去，只发给你在这里配置的接口（可能计费）。用本机 Ollama 则完全不出电脑。", 13, true);
            privacy.Margin = new Thickness(0, 0, 0, 10);
            p.Children.Add(privacy);

            var details = new StackPanel { IsEnabled = S.AiEnabled, Opacity = S.AiEnabled ? 1 : 0.5 };
            p.Children.Add(K.Card("启用 AI 助手", "关闭时 Alt+空格 不会发送任何内容",
                K.Toggle(S.AiEnabled, v => { S.AiEnabled = v; details.IsEnabled = v; details.Opacity = v ? 1 : 0.5; Save(); }), Ui.SymbolRegular.Sparkle24));

            var url = new Ui.TextBox { Text = S.AiBaseUrl, MinWidth = 380 };
            var model = new Ui.TextBox { Text = S.AiModel, MinWidth = 220 };
            var key = new Ui.PasswordBox { Password = S.AiKey, MinWidth = 380, PlaceholderText = "sk-…" };
            var preset = K.Combo(new[] { KV(-1, "选择服务商快速填写…") }.Concat(Presets.Select((x, i) => KV(i, x.Name))), -1, i =>
            {
                if (i < 0) return;
                url.Text = Presets[i].Url; model.Text = Presets[i].Model;
            }, 260);
            url.TextChanged += (s, e) => { S.AiBaseUrl = url.Text.Trim(); Save(); };
            model.TextChanged += (s, e) => { S.AiModel = model.Text.Trim(); Save(); };
            key.PasswordChanged += (s, e) => { S.AiKey = key.Password.Trim(); Save(); };
            details.Children.Add(K.Card("服务商", "任何 OpenAI 兼容接口都可以", preset, Ui.SymbolRegular.Apps24));
            details.Children.Add(K.Card("接口地址", "例如 https://api.deepseek.com/v1", url));
            details.Children.Add(K.Card("模型", "接口里的模型名称", model));
            details.Children.Add(K.Card("API 密钥", "用 Windows 数据保护加密后只存在这台电脑上", key, Ui.SymbolRegular.Shield24));
            details.Children.Add(K.Card("测试连接", "发送「你好，世界」请求翻译，确认接口可用", K.Btn("测试", async () =>
            {
                Toast("正在测试…");
                try { var r = await Ai.Run("把用户的话翻译成英文，只输出译文。", "你好，世界"); Toast("连接成功：" + r); }
                catch (Exception ex) { Toast("连接失败：" + ex.GetBaseException().Message); }
            }), Ui.SymbolRegular.ArrowSync24));

            // actions
            var actions = new StackPanel();
            void FillActions()
            {
                actions.Children.Clear();
                foreach (var a in S.AiActions.ToList())
                {
                    var act = a;
                    var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var n = new TextBox { Text = act.Name, Margin = new Thickness(0, 0, 6, 0) };
                    var pr = new TextBox { Text = act.Prompt, TextWrapping = TextWrapping.Wrap, AcceptsReturn = false };
                    n.TextChanged += (s, e) => { act.Name = n.Text.Trim(); Save(); };
                    pr.TextChanged += (s, e) => { act.Prompt = pr.Text.Trim(); Save(); };
                    var del = new Ui.Button { Icon = new Ui.SymbolIcon { Symbol = Ui.SymbolRegular.Delete24 }, Appearance = Ui.ControlAppearance.Transparent, Margin = new Thickness(6, 0, 0, 0) };
                    del.Click += (s, e) => { S.AiActions.Remove(act); Save(); FillActions(); };
                    Grid.SetColumn(pr, 1); Grid.SetColumn(del, 2);
                    g.Children.Add(n); g.Children.Add(pr); g.Children.Add(del);
                    actions.Children.Add(g);
                }
                var row = K.Row(K.Btn("添加模式", () => { if (S.AiActions.Count >= 8) { Toast("最多 8 个模式（另有「自定义」）"); return; } S.AiActions.Add(new AiAction { Name = "新模式", Prompt = "" }); Save(); FillActions(); }, Ui.ControlAppearance.Secondary, Ui.SymbolRegular.Add24),
                    K.Btn("恢复默认", () => { S.AiActions = Settings.DefaultActions(); Save(); FillActions(); }));
                row.Margin = new Thickness(0, 8, 0, 0);
                actions.Children.Add(row);
            }
            FillActions();
            details.Children.Add(K.Block("模式", "面板里按数字切换；名称含「润色」的模式会标出改动（红删绿增）。最后还有一个「自定义」", actions, Ui.SymbolRegular.TextGrammarWand24));
            p.Children.Add(details);
            return p;
        }

        // ================= 词库 =================

        StackPanel PageDicts()
        {
            var p = new StackPanel();
            p.Children.Add(K.PageTitle("词库"));
            AddDictUpdateCards(p);   // Wpf/DictFeaturesUi.cs
            AddGrammarCards(p);
            p.Children.Add(K.Card("用户词典管理", "导出、导入或备份 RIME 用户词典", K.Btn("打开", () => Dialogs.Open(Paths.Deployer, "/dict")), Ui.SymbolRegular.Book24));

            p.Children.Add(K.Section("导入旧习惯"));
            p.Children.Add(K.Card("从小狼毫导入", "复制 " + Paths.LegacyWeaselUserDir + " 里的方案、补丁和用户词库", K.Btn("导入", () =>
            {
                try { var n = HabitImport.ImportWeasel(); Toast($"已导入 {n} 个文件，正在部署…"); }
                catch (Exception ex) { Dialogs.Error(ex.Message); }
            }), Ui.SymbolRegular.ArrowDownload24));
            p.Children.Add(K.Card("从文本词库导入", "搜狗、微软拼音等导出的 .txt 词库（每行一个词和拼音）", K.Btn("选择文件…", ImportTextDict), Ui.SymbolRegular.Document24));
            return p;
        }

        void ImportTextDict()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "文本词库 (*.txt)|*.txt|所有文件|*.*", Title = "选择导出的词库文件" };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                int n = HabitImport.ImportTextFile(dlg.FileName);
                if (n == 0) Dialogs.Error("没有从这个文件里读到带拼音的词。请确认导出的是文本格式（每行包含词和拼音）。");
                else Toast($"已导入 {n} 个词，正在合并到用户词典…");
            }
            catch (Exception e) { Dialogs.Error("导入失败：" + e.Message); }
        }

        // ================= 统计 =================

        TextBlock _statToday, _statWeek, _statYear, _statSaved;
        LineChart _week;
        Heatmap _heat;
        BarList _words, _apps;

        StackPanel PageStats()
        {
            var p = new StackPanel();
            p.Children.Add(K.PageTitle("统计"));
            p.Children.Add(K.Card("记录输入统计", "字数、时段、常用词和应用。只存在这台电脑上，可随时清空",
                K.Toggle(S.StatsEnabled, v => { S.StatsEnabled = v; RefreshStats(); }), Ui.SymbolRegular.DataTrending24));

            TextBlock Num(out TextBlock t) { t = K.Text("0", 24, false, FontWeights.SemiBold); return t; }
            var tiles = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4 };
            foreach (var (label, make) in new (string, Func<TextBlock>)[]
            {
                ("今天", () => Num(out _statToday)), ("本周", () => Num(out _statWeek)), ("今年", () => Num(out _statYear)), ("省下的按键（估算）", () => Num(out _statSaved)),
            })
            {
                var sp = new StackPanel();
                sp.Children.Add(K.Text(label, 12, true));
                sp.Children.Add(make());
                var b = K.CardBorder(sp); b.Margin = new Thickness(0, 0, 8, 8);
                tiles.Children.Add(b);
            }
            p.Children.Add(tiles);

            _week = new LineChart(); _themed.Add(_week);
            p.Children.Add(K.Block("最近 7 天", "每天上屏的字数", _week, Ui.SymbolRegular.DataLine24));
            _heat = new Heatmap(); _themed.Add(_heat);
            p.Children.Add(K.Block("这一年", "颜色越深，那天打字越多", _heat, Ui.SymbolRegular.DataTrending24));
            _words = new BarList { Unit = " 次" }; _themed.Add(_words);
            _apps = new BarList { Unit = " 字" }; _themed.Add(_apps);
            var two = new Grid();
            two.ColumnDefinitions.Add(new ColumnDefinition()); two.ColumnDefinitions.Add(new ColumnDefinition());
            var w = K.Block("常用词", "上屏次数最多的词", _words); w.Margin = new Thickness(0, 0, 4, 4);
            var a = K.Block("常用应用", "在哪些应用里打字最多", _apps); a.Margin = new Thickness(4, 0, 0, 4);
            Grid.SetColumn(a, 1);
            two.Children.Add(w); two.Children.Add(a);
            p.Children.Add(two);
            p.Children.Add(K.Card("刷新", "重新读取统计数据", K.Btn("刷新", RefreshStats), Ui.SymbolRegular.ArrowClockwise24));

            p.Children.Add(K.Section("危险操作"));
            p.Children.Add(Danger("清空统计", "删除所有输入统计记录，无法恢复", "清空…", () =>
            {
                if (!Dialogs.Confirm("清空所有输入统计？此操作无法恢复。")) return;
                Stats.Clear(); RefreshStats(); Toast("统计已清空");
            }));
            return p;
        }

        void RefreshStats()
        {
            if (_week == null) return;
            Stats.Report r;
            try { r = Stats.Build(); }
            catch (Exception e) { Log.Write("stats: " + e.Message); return; }
            _statToday.Text = r.Today.ToString("N0") + " 字";
            _statWeek.Text = r.ThisWeek.ToString("N0") + " 字";
            _statYear.Text = r.ThisYear.ToString("N0") + " 字";
            _statSaved.Text = Stats.KeystrokesSaved(r, S.Schema).ToString("N0");
            var days = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(i - 6)).ToList();
            _week.Values = days.Select(d => r.ByDay.TryGetValue(d, out var v) ? v : 0).ToArray();
            string[] wd = { "日", "一", "二", "三", "四", "五", "六" };
            _week.Labels = days.Select(d => d == DateTime.Today ? "今天" : "周" + wd[(int)d.DayOfWeek]).ToArray();
            _heat.Days = r.ByDay;
            _words.Items = r.TopWords.Take(10).ToList();
            _apps.Items = r.TopApps.Take(8).ToList();
            foreach (var v in new FrameworkElement[] { _week, _heat, _words, _apps }) { v.InvalidateMeasure(); v.InvalidateVisual(); }
        }
    }
}
