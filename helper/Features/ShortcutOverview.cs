using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Yiwei
{
    /// <summary>W25 快捷键总览：输入法按键、长按 Alt 手势、语音、工具快捷键，按当前设置列出（关掉的标「已关闭」）。</summary>
    public static class ShortcutOverview
    {
        public sealed class Row { public string Group, Keys, What; public bool On = true; }

        public static List<Row> Build()
        {
            var s = Settings.Current; var t = ToolSettings.Current;
            var rows = new List<Row>();
            void Add(string g, string k, string w, bool on = true) => rows.Add(new Row { Group = g, Keys = k, What = w, On = on });
            Add("打字", "Shift", "切换中文 / 英文");
            Add("打字", "Ctrl+` 或 F4", "方案选单：切换输入方案、简繁、全半角等");
            Add("打字", "Shift+空格", "全角 / 半角");
            Add("打字", "Ctrl+.", "中文标点 / 英文标点");
            Add("打字", "- =  或  , .  或  PgUp PgDn", "候选翻页");
            Add("打字", "1–" + (s.CandidateCount >= 10 ? "0" : s.CandidateCount.ToString()) + " / 空格", "选择候选（每页 " + s.CandidateCount + " 个）");
            Add("打字", "Ctrl+Backspace（刚上屏后）", "反悔重选：删掉刚上屏的词，拼音回到输入框");
            Add("打字", "Ctrl+T / Ctrl+Delete（候选高亮时）", "置顶 / 隐藏这个候选");
            Add("打字", "Esc", "清空输入");
            bool alt = s.AltHotkeys;
            Add("长按 Alt", "Alt + 1–9", "常用语分组", alt && s.SnippetsHotkey);
            Add("长按 Alt", "Alt + V", "剪贴板历史", alt && s.SnippetsHotkey && s.ClipHistory);
            Add("长按 Alt", "Alt + 空格", "AI：翻译、润色、粤语…（先选中文字）", alt && s.AiHotkey);
            Add("长按 Alt", "Alt + O", "截图识字", alt && s.OcrHotkey);
            Add("长按 Alt", "Alt + H", "手写输入", alt && s.HandwritingHotkey);
            Add("长按 Alt", "Alt + A", "选中文字加词", alt && s.AddWordHotkey);
            Add("长按 Alt", "Alt + E", "符号与表情", alt && s.SymbolsHotkey);
            ToolsHub.EnsureRegistered();
            foreach (var a in ToolsHub.Actions.Where(a => a.AltKey != Keys.None))
                Add("长按 Alt", "Alt + " + TextToolsMenu.KeyName(a.AltKey), a.Name, alt && t.AltGestures);
            Add("语音", s.VoiceKey == "ralt" ? "按住右 Alt" : s.VoiceKey == "rctrl" ? "按住右 Ctrl" : "（未设置）", "说话，松开上屏（需先下载语音模型）", s.VoiceKey == "ralt" || s.VoiceKey == "rctrl");
            foreach (var kv in t.Hotkeys.Where(kv => !string.IsNullOrEmpty(kv.Value)))
                Add("自定义", kv.Value, ToolsHub.Find(kv.Key)?.Name ?? kv.Key);
            return rows;
        }

        public static string AsText()
        {
            var sb = new StringBuilder("一维输入法 快捷键\r\n");
            foreach (var g in Build().GroupBy(r => r.Group))
            {
                sb.Append("\r\n【").Append(g.Key).Append("】\r\n");
                foreach (var r in g) sb.Append(r.Keys.PadRight(22)).Append(r.What).Append(r.On ? "" : "（已关闭）").Append("\r\n");
            }
            return sb.ToString();
        }
    }
}
