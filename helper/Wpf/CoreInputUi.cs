using System.Windows.Controls;
using Ui = Wpf.Ui.Controls;

namespace Yiwei
{
    /// <summary>「快捷输入」页末尾：输入核心组的设置（Features/CoreInput.cs）。</summary>
    sealed partial class SettingsWindow
    {
        void AddCoreInputSection(StackPanel p)
        {
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
