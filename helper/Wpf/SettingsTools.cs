using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ui = Wpf.Ui.Controls;
using WF = System.Windows.Forms;

namespace Yiwei
{
    /// <summary>「工具」页：文字工具、翻译、补全、同步备份、快捷键自定义（工具组的设置都在 tools.json）。</summary>
    sealed partial class SettingsWindow
    {
        ToolSettings T => ToolSettings.Current;

        void SaveTools() { try { T.Save(); } catch (Exception e) { _toast.Show("保存失败：" + e.Message); } }

        static KeyValuePair<string, string> KS(string k, string v) => new KeyValuePair<string, string>(k, v);

        StackPanel PageTools()
        {
            var p = new StackPanel();
            p.Children.Add(K.PageTitle("工具"));
            p.Children.Add(K.Card("长按 Alt 工具手势", "长按 Alt + T 文字工具菜单（对选中文字：翻译、拼音、繁简、大小写、Markdown…）；长按 Alt + S 截图翻译；长按 Alt + M Markdown 符号",
                K.Toggle(T.AltGestures, v => { T.AltGestures = v; SaveTools(); }), Ui.SymbolRegular.Toolbox24));
            foreach (var build in ToolSections()) { try { foreach (var e in build()) p.Children.Add(e); } catch (Exception ex) { Log.Write("tools page: " + ex); } }
            return p;
        }

        /// <summary>Sections of the 工具 page, in order.</summary>
        IEnumerable<Func<IEnumerable<UIElement>>> ToolSections()
        {
            yield return TranslateSection;
            yield return TextSection;
            yield return HotkeysSection;
        }

        IEnumerable<UIElement> TranslateSection()
        {
            yield return K.Section("翻译");
            yield return K.Card("翻译引擎", "「自动」：设置了 AI 就用 AI，否则用免费的 MyMemory 在线接口（无需账号，每天约 5000 字；文字会发送到该服务）",
                K.Combo(new[] { KS("auto", "自动"), KS("ai", "AI（设置 → AI）"), KS("free", "免费接口 MyMemory") }, T.TranslateEngine ?? "auto", v => { T.TranslateEngine = v; SaveTools(); }), Ui.SymbolRegular.Translate24);
            yield return K.Card("翻译成", "截图翻译和翻译选中文字的目标语言",
                K.Combo(Translator.Targets, T.TranslateTarget ?? "auto", v => { T.TranslateTarget = v; SaveTools(); }, 260));
            yield return K.Card("截图翻译", "长按 Alt + S，或下面给它设一个快捷键：框选屏幕文字 → 离线识别 → 翻译，可上屏或复制",
                K.Btn("现在试试", () => { Hide(); Dispatcher.BeginInvoke(new Action(() => { TextOut.Pump(250); ScreenTranslate.Start(); Show(); })); }), Ui.SymbolRegular.ScreenshotRecord24);
        }

        IEnumerable<UIElement> TextSection()
        {
            yield return K.Section("选中文字");
            yield return K.Card("加注拼音样式", "长按 Alt + T →「加注拼音」：多音字按雾凇词库里的词来读；词库不带声调，所以拼音不带声调",
                K.Combo(PinyinAnnotate.Styles, T.PinyinStyle ?? "after", v => { T.PinyinStyle = v; SaveTools(); }, 260), Ui.SymbolRegular.TextFont24);
        }

        /// <summary>W47：每个工具动作都可以绑一个全局快捷键（RegisterHotKey，被别的程序占用时会提示）。</summary>
        IEnumerable<UIElement> HotkeysSection()
        {
            ToolsHub.EnsureRegistered();
            yield return K.Section("自定义快捷键");
            var list = new StackPanel();
            var status = K.Text("点输入框后直接按组合键（至少带 Ctrl / Alt / Shift / Win 之一）；Backspace 或 Delete 清除。", 12, true);
            void Fill()
            {
                list.Children.Clear();
                foreach (var a in ToolsHub.Actions)
                {
                    var id = a.Id;
                    var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var label = new StackPanel();
                    label.Children.Add(K.Text(a.Name + (a.AltKey != WF.Keys.None ? "　（长按 Alt + " + TextToolsMenu.KeyName(a.AltKey) + "）" : ""), 13));
                    if (!string.IsNullOrEmpty(a.Help)) label.Children.Add(K.Text(a.Help, 11, true));
                    g.Children.Add(label);
                    var box = new TextBox { Width = 170, IsReadOnly = true, VerticalContentAlignment = VerticalAlignment.Center, Text = T.Hotkeys.TryGetValue(id, out var cur) ? cur : "" };
                    box.PreviewKeyDown += (s, e) =>
                    {
                        e.Handled = true;
                        var key = e.Key == Key.System ? e.SystemKey : e.Key;
                        if (key == Key.Back || key == Key.Delete) { T.Hotkeys.Remove(id); box.Text = ""; SaveTools(); Apply(); return; }
                        if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;
                        var m = Keyboard.Modifiers;
                        bool win = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);
                        if (m == ModifierKeys.None && !win) { status.Text = "要带 Ctrl、Alt、Shift 或 Win"; return; }
                        var wk = (WF.Keys)KeyInterop.VirtualKeyFromKey(key);
                        var text = ToolsHub.Format(wk, m.HasFlag(ModifierKeys.Control), m.HasFlag(ModifierKeys.Alt), m.HasFlag(ModifierKeys.Shift), win);
                        var clash = T.Hotkeys.FirstOrDefault(kv => kv.Key != id && string.Equals(kv.Value, text, StringComparison.OrdinalIgnoreCase));
                        if (clash.Key != null) { status.Text = text + " 已经给了「" + (ToolsHub.Find(clash.Key)?.Name ?? clash.Key) + "」"; return; }
                        T.Hotkeys[id] = text; box.Text = text; SaveTools(); Apply();
                    };
                    Grid.SetColumn(box, 1); g.Children.Add(box);
                    list.Children.Add(g);
                }
            }
            void Apply()
            {
                var failed = ToolsHub.ReapplyHotkeys();
                status.Text = failed.Count == 0 ? "已生效。" : "这些快捷键被别的程序占用了，换一个：" + string.Join("、", failed.Select(f => ToolsHub.Find(f)?.Name ?? f));
            }
            Fill();
            var body = new StackPanel();
            body.Children.Add(list);
            status.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(status);
            yield return K.Block("工具快捷键", "给下面每个功能单独设一个全局快捷键，按下就对选中的文字或屏幕执行", body, Ui.SymbolRegular.Keyboard24);
        }
    }
}
