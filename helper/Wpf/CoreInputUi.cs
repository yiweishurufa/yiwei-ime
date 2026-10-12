using System.Windows.Controls;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    /// <summary>「快捷输入」页末尾：输入核心组的设置（Features/CoreInput.cs）。</summary>
    sealed partial class SettingsWindow
    {
        void AddCoreInputSection(StackPanel p)
        {
            p.Children.Add(K.Section("模糊音地区预设"));
            var presets = new WrapPanel();
            foreach (var ps in CoreInput.FuzzyPresets)
            {
                var preset = ps;
                var b = K.Btn(preset.Name, () =>
                {
                    var f = RimeFeatures.Of(S);
                    f.Fuzzy = new System.Collections.Generic.List<string>(preset.Ids);
                    ApplyFeatures("模糊音已换成「" + preset.Name + "」（全拼和双拼都生效），重新打开本页可看到勾选");
                });
                b.ToolTip = preset.Desc;
                b.Margin = new System.Windows.Thickness(0, 0, 8, 8);
                presets.Children.Add(b);
            }
            p.Children.Add(K.Block("一键套用", "按口音选一组模糊音，之后还可以在上面逐项增减；全拼、双拼都生效", presets, Ui.SymbolRegular.SoundWaveCircle24));

            p.Children.Add(K.Section("候选"));
            p.Children.Add(K.Card("Emoji 候选", "打 xiaolian、haha 等词时在候选里带上 😀 😂；关掉后只出文字（Ctrl+` 菜单里也能临时切换）",
                K.Toggle(CoreInput.Emoji, v => { CoreInput.Emoji = v; ApplyFeatures(v ? "已开启 Emoji 候选" : "已关闭 Emoji 候选"); }),
                Ui.SymbolRegular.Emoji24));

            p.Children.Add(K.Card("自定义标点", "改某个键在中文状态下打出的标点，例如 / 打「、」、` 打「·」、~ 打「～」；一行一个，保存后点「应用」",
                K.Row(
                    K.Btn("编辑…", () => { CoreInput.EnsurePunctFile(); Dialogs.Open(CoreInput.PunctFile); }),
                    K.Btn("应用", () => ApplyFeatures("自定义标点已更新"), Ui.ControlAppearance.Primary)),
                Ui.SymbolRegular.NumberSymbol24));

            p.Children.Add(K.Card("人名候选（全拼）",
                "打「姓 + 名」的拼音（如 liuxinyi、ouyangxue）时，在第 5 位起补几个常见的人名写法，注释「人名」；日常词语不受影响",
                K.Toggle(CoreInput.Names, v => { CoreInput.Names = v; Toast(v ? "已开启人名候选" : "已关闭人名候选"); }),
                Ui.SymbolRegular.Person24));
            p.Children.Add(K.Section("按习惯调整候选"));
            p.Children.Add(K.Card("按习惯调整候选",
                "分几次选出来的词（如 yuanyiwei 依次选 苑、祎、玮）会记成一个词，下次排第一；常选的字词持续靠前。只在本机记录，无痕窗口不记",
                K.Toggle(CoreInput.Learn, v => { CoreInput.Learn = v; Toast(v ? "已开启按习惯调整候选" : "已关闭按习惯调整候选"); }),
                Ui.SymbolRegular.Lightbulb24));
            p.Children.Add(K.Card("学习强度", "低：选 2–3 次才前移、两周淡忘；中：词组 1 次、单字 2 次；高：选 1 次就前移、一年才淡忘",
                K.Combo(new[] { KV("1", "低"), KV("2", "中（推荐）"), KV("3", "高") }, CoreInput.LearnLevel,
                    v => { CoreInput.LearnLevel = v; Toast("学习强度已更新"); }, 140),
                Ui.SymbolRegular.DataTrending24));
            p.Children.Add(K.Card("清除学到的词", "把最近学到的词从习惯和用户词库里删掉（下次打字时生效）",
                K.Row(
                    K.Btn("最近 1 小时", () => { CoreInput.ClearLearned("3600"); Toast("已清除最近 1 小时学到的词"); }),
                    K.Btn("最近 1 天", () => { CoreInput.ClearLearned("86400"); Toast("已清除最近 1 天学到的词"); }),
                    K.Btn("全部…", () =>
                    {
                        if (!Dialogs.Confirm("清除全部学到的词？\n\n习惯记录和 RIME 用户词库里的词频、自造词都会删除，候选恢复成刚安装时的顺序。建议先在「常规 → 备份」里备份。")) return;
                        CoreInput.ClearLearned("all"); Toast("已清除全部学到的词");
                    }, Ui.ControlAppearance.Danger)),
                Ui.SymbolRegular.Delete24));
        }
    }
}
