using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WinKeys = System.Windows.Forms.Keys;

namespace Yiwei
{
    /// <summary>
    /// 符号与表情点选面板（雾凇 v 模式的可视化版）：分组标签 + 符号格子，点一下就上屏，面板不抢焦点。
    /// 长按 Alt + E、托盘「符号与表情」或 /symbols 打开；Tab / ← → 换组，Esc 关闭。最近用过的排在「常用」组。
    /// </summary>
    sealed class SymbolPanel : FloatingWindow
    {
        static readonly (string Name, string Items)[] Groups =
        {
            ("标点", "， 。 、 ； ： ？ ！ … — · “ ” ‘ ’ 「 」 『 』 （ ） 【 】 《 》 〈 〉 〔 〕 ［ ］ ｛ ｝ ～ ＠ ＃ ％ ＆ ＊ ＋ ＝ ／ ＼ ｜ ＿ ¦ ‖ ‰ ※ § ¶"),
            ("数学", "± × ÷ ≈ ≠ ≡ ≤ ≥ ＜ ＞ ∞ √ ∛ ∑ ∏ ∫ ∮ ∂ ∆ ∇ ∈ ∉ ⊂ ⊃ ⊆ ⊇ ∪ ∩ ∧ ∨ ¬ ∀ ∃ ∅ ∠ ⊥ ∥ ° ′ ″ π ½ ⅓ ¼ ¾ ² ³ ⁿ ₁ ₂ ₃"),
            ("箭头", "→ ← ↑ ↓ ↔ ↕ ↖ ↗ ↘ ↙ ⇒ ⇐ ⇑ ⇓ ⇔ ⟶ ⟵ ➜ ➔ ➤ ▶ ◀ ▲ ▼ ► ◄ △ ▽ ↩ ↪ ⤴ ⤵ ↻ ↺"),
            ("单位", "℃ ℉ ° ¥ ￥ $ € £ ¢ ₩ ₽ ₹ ㎡ ㎥ ㎝ ㎜ ㎞ ㎏ ㎎ ㏄ ㎖ ㎗ ㎘ ℓ ㏒ ㏑ % ‰ № ™ © ® ℗"),
            ("序号", "① ② ③ ④ ⑤ ⑥ ⑦ ⑧ ⑨ ⑩ ⑪ ⑫ ⑬ ⑭ ⑮ ⑯ ⑰ ⑱ ⑲ ⑳ ⑴ ⑵ ⑶ ⑷ ⑸ ⑹ ⑺ ⑻ ⑼ ⑽ ㈠ ㈡ ㈢ ㈣ ㈤ ㈥ ㈦ ㈧ ㈨ ㈩ Ⅰ Ⅱ Ⅲ Ⅳ Ⅴ Ⅵ Ⅶ Ⅷ Ⅸ Ⅹ ❶ ❷ ❸ ❹ ❺"),
            ("图形", "★ ☆ ● ○ ◎ ◆ ◇ ■ □ ▪ ▫ ♠ ♣ ♥ ♦ ♤ ♧ ♡ ♢ ✓ ✔ ✗ ✘ ☑ ☒ ☐ ✦ ✧ ❖ ☀ ☁ ☂ ☃ ❄ ♪ ♫ ☎ ✉ ✈ ⚑ ⚐ ⌘ ⌥ ⏎ ⌫"),
            ("希腊", "α β γ δ ε ζ η θ ι κ λ μ ν ξ ο π ρ σ τ υ φ χ ψ ω Α Β Γ Δ Ε Ζ Η Θ Ι Κ Λ Μ Ν Ξ Ο Π Ρ Σ Τ Υ Φ Χ Ψ Ω"),
            ("拼音", "ā á ǎ à ō ó ǒ ò ē é ě è ī í ǐ ì ū ú ǔ ù ǖ ǘ ǚ ǜ ü ê ń ň ǹ ḿ"),
            ("表情", "😀 😂 🤣 😊 😍 🥰 😘 😎 🤔 😅 😭 😡 😱 🥺 😴 🙄 😏 🤗 🤝 👍 👎 👏 🙏 💪 👌 ✌️ 🤞 👋 🎉 🎂 🎁 ❤️ 💔 🔥 ✨ ⭐ 🌹 🌈 ☕ 🍺 🍉 🐶 🐱 🐼 💯 ✅ ❌ ⚠️ 💡 📌 📎 📅 ⏰ 🚀"),
            ("颜文字", "(＾▽＾) | (๑•̀ㅂ•́)و✧ | (╯°□°）╯︵ ┻━┻ | ┬─┬ノ( º _ ºノ) | ¯\\_(ツ)_/¯ | (｡･ω･｡) | (⊙ˍ⊙) | (ಥ_ಥ) | (•‾⌣‾•) | ヾ(≧▽≦*)o | (*/ω＼*) | (￣▽￣)ノ | (｀・ω・´) | (=^･ω･^=) | ʕ•ᴥ•ʔ | (づ｡◕‿‿◕｡)づ"),
        };

        PanelPalette _p;
        int _group;
        static readonly List<string> Recent = new List<string>();

        public bool IsOpen => IsVisible;

        public void Open()
        {
            _p = PanelPalette.Now();
            _group = Recent.Count > 0 ? -1 : 0;
            Build();
            ShowAtCaret();
        }

        public void Close2() { Hide(); }

        /// <summary>Items are separated by spaces; groups whose items contain spaces (颜文字) use " | ".</summary>
        static string[] Split(string items) => items.Contains(" | ")
            ? items.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries)
            : items.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        string[] Current => _group < 0 ? Recent.ToArray() : Split(Groups[_group].Items);

        void Build()
        {
            var root = new StackPanel { Width = 520 };
            var tabs = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            if (Recent.Count > 0)
            {
                var c = PanelParts.Chip(_p, "常用", _group < 0);
                c.MouseLeftButtonUp += (s, e) => { _group = -1; Build(); };
                tabs.Children.Add(c);
            }
            for (int i = 0; i < Groups.Length; i++)
            {
                int idx = i;
                var c = PanelParts.Chip(_p, Groups[i].Name, i == _group);
                c.MouseLeftButtonUp += (s, e) => { _group = idx; Build(); };
                c.Margin = new Thickness(0, 0, 4, 4);
                tabs.Children.Add(c);
            }
            root.Children.Add(tabs);
            var items = Current;
            bool wide = _group >= 0 && Groups[_group].Name == "颜文字";
            var grid = new UniformGrid { Columns = wide ? 3 : 10 };
            foreach (var it in items)
            {
                var sym = it;
                var b = new Border
                {
                    Background = _p.Card, BorderBrush = _p.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(2), Height = 38, Cursor = Cursors.Hand, ToolTip = sym,
                    Child = new TextBlock { Text = sym, FontSize = wide ? 13 : 18, Foreground = _p.Text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis },
                };
                b.MouseEnter += (s, e) => b.Background = _p.Accent;
                b.MouseLeave += (s, e) => b.Background = _p.Card;
                b.MouseEnter += (s, e) => ((TextBlock)b.Child).Foreground = _p.AccentText;
                b.MouseLeave += (s, e) => ((TextBlock)b.Child).Foreground = _p.Text;
                b.MouseLeftButtonUp += (s, e) => Commit(sym);
                grid.Children.Add(b);
            }
            root.Children.Add(new ScrollViewer { Content = grid, MaxHeight = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            root.Children.Add(PanelParts.Hint(_p, "点击上屏（面板不关，可以连点）· Tab / ← → 换组 · Esc 关闭 · 打字时也可以用 v 开头输入符号"));
            Content = PanelParts.Shell(_p, root);
        }

        void Commit(string sym)
        {
            Recent.Remove(sym); Recent.Insert(0, sym);
            if (Recent.Count > 30) Recent.RemoveAt(Recent.Count - 1);
            Program.Ui.BeginInvoke(new Action(() => TextOut.Type(sym)));
        }

        public bool HandleKey(WinKeys k)
        {
            int first = Recent.Count > 0 ? -1 : 0;
            if (k == WinKeys.Escape) { Close2(); return true; }
            if (k == WinKeys.Tab || k == WinKeys.Right) { _group = _group + 1 >= Groups.Length ? first : _group + 1; Build(); return true; }
            if (k == WinKeys.Left) { _group = _group - 1 < first ? Groups.Length - 1 : _group - 1; Build(); return true; }
            return true;
        }
    }
}
