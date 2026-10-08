using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    /// <summary>「快捷输入」: 雾凇 Lua 功能开关 + 速查卡、模糊音、生僻字。</summary>
    sealed partial class SettingsWindow
    {
        static readonly FontFamily CodeFont = new FontFamily("Cascadia Mono, Consolas, Microsoft YaHei UI");

        StackPanel PageFeatures()
        {
            var f = RimeFeatures.Of(S);
            bool dbl = S.Schema != "rime_ice";
            var p = new StackPanel();
            p.Children.Add(K.PageTitle("快捷输入"));
            p.Children.Add(K.Text("在输入时直接打触发码就能用，不用切换模式。关掉的功能不再响应触发码；修改后会在后台重新部署。", 13, true));

            p.Children.Add(K.Section("功能开关与速查卡"));
            foreach (var ft in RimeFeatures.Features)
            {
                var feature = ft;
                var rows = dbl && feature.Double != null ? feature.Double : feature.Full;
                var card = K.Card(feature.Title, feature.Desc,
                    K.Toggle(feature.Get(f), v => { feature.Set(f, v); ApplyFeatures((v ? "已开启「" : "已关闭「") + feature.Title + "」"); }),
                    IconFor(feature.Key));
                AttachTable(card, rows);
                p.Children.Add(card);
            }

            var always = K.Block("一直可用", "这些不需要开关", CheatTable(RimeFeatures.AlwaysOn), Ui.SymbolRegular.Info24);
            p.Children.Add(always);

            // ---------- 模糊音 ----------
            p.Children.Add(K.Section("模糊音"));
            var wrap = new WrapPanel();
            foreach (var pair in RimeFeatures.FuzzyPairs)
            {
                var id = pair.Id;
                var cb = new CheckBox
                {
                    Content = pair.Label + "　" + pair.Example, IsChecked = f.Fuzzy.Contains(id),
                    MinWidth = 210, Margin = new Thickness(0, 0, 12, 6),
                };
                cb.Checked += (s, e) => { if (!f.Fuzzy.Contains(id)) f.Fuzzy.Add(id); ApplyFeatures("模糊音已更新"); };
                cb.Unchecked += (s, e) => { f.Fuzzy.Remove(id); ApplyFeatures("模糊音已更新"); };
                wrap.Children.Add(cb);
            }
            var fuzzyDesc = dbl
                ? "勾选的两种读音都能打出同样的字。目前只对全拼生效（双拼方案的键位规则不同），你现在用的是双拼"
                : "勾选的两种读音都能打出同样的字。规则追加在雾凇自带规则之后，不影响原有的简拼和纠错";
            p.Children.Add(K.Block("模糊音", fuzzyDesc, wrap, Ui.SymbolRegular.SoundWaveCircle24));

            // ---------- 生僻字 ----------
            p.Children.Add(K.Section("生僻字"));
            var bigOk = RimeFeatures.BigTableAvailable;
            var bigToggle = K.Toggle(f.BigCharset, v =>
            {
                f.BigCharset = v;
                ApplyFeatures(v ? "已挂载大字表，首次部署会慢一些（约半分钟）" : "已改回常用字表");
            });
            if (!bigOk && !f.BigCharset) bigToggle.IsEnabled = false;
            p.Children.Add(K.Card("大字表（41448 字）",
                bigOk ? "在常用 8105 字之外挂载雾凇的 41448 字表，生僻字也能用拼音打出；词库变大，部署更慢"
                      : "没有找到雾凇的 cn_dicts/41448.dict.yaml，更新词库后再试",
                bigToggle, Ui.SymbolRegular.TextFont24));

            var found = RimeFeatures.InstalledFallbacks();
            var fontDesc = "候选字体缺字时依次换用：" + (found.Count > 0 ? string.Join("、", found) : "（没有检测到可用的回退字体）")
                + "。想显示更多生僻字，可以安装遍黑体（Plangothic）、花园明朝或天珩全字库，装好后重新打开设置";
            p.Children.Add(K.Card("候选字体回退", fontDesc,
                K.Toggle(f.FontFallback, v => { f.FontFallback = v; ApplyFeatures(v ? "已开启字体回退" : "已关闭字体回退"); }),
                Ui.SymbolRegular.TextFont24));
            p.Children.Add(K.Block("不会读的字怎么打", "三种办法",
                CheatTable(new[]
                {
                    new[] { "uUmumumu", "拆字：按部件读音打出「森」" },
                    new[] { "U4e00", "Unicode 码位：直接打出「一」" },
                    new[] { "ni`r", "辅码：在「ni」的候选里只留带「亻」的字" },
                }), Ui.SymbolRegular.Search24));
            return p;
        }

        void ApplyFeatures(string message)
        {
            Save(true);
            Toast(message + "，正在后台部署…");
        }

        static Ui.SymbolRegular IconFor(string key)
        {
            switch (key)
            {
                case "date": return Ui.SymbolRegular.CalendarLtr24;
                case "lunar": return Ui.SymbolRegular.WeatherMoon24;
                case "calc": return Ui.SymbolRegular.Calculator24;
                case "number": return Ui.SymbolRegular.NumberSymbol24;
                case "unicode": return Ui.SymbolRegular.Code24;
                case "uuid": return Ui.SymbolRegular.Key24;
                case "select": return Ui.SymbolRegular.TextEditStyle24;
                case "radical": return Ui.SymbolRegular.PuzzlePiece24;
                case "corrector": return Ui.SymbolRegular.CheckmarkCircle24;
                default: return Ui.SymbolRegular.Flash24;
            }
        }

        /// <summary>Puts a 触发码 / 示例 table under a setting card's header row (keeps the card's search tag).</summary>
        static void AttachTable(Border card, string[][] rows)
        {
            if (rows == null || rows.Length == 0) return;
            var head = card.Child;
            card.Child = null;
            var sp = new StackPanel();
            sp.Children.Add(head);
            var table = CheatTable(rows);
            table.Margin = new Thickness(34, 8, 0, 2);
            sp.Children.Add(table);
            card.Child = sp;
            card.Tag = (card.Tag as string ?? "") + " " + string.Join(" ", rows.Select(r => r[0]));
        }

        static Grid CheatTable(string[][] rows)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < rows.Length; i++)
            {
                g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var code = K.Text(rows[i][0], 13, false, FontWeights.SemiBold);
                code.FontFamily = CodeFont;
                code.Margin = new Thickness(0, 2, 12, 2);
                Grid.SetRow(code, i); g.Children.Add(code);
                var ex = K.Text(rows[i][1], 13, true);
                ex.Margin = new Thickness(0, 2, 0, 2);
                Grid.SetRow(ex, i); Grid.SetColumn(ex, 1); g.Children.Add(ex);
            }
            return g;
        }
    }
}
