using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yiwei
{
    /// <summary>
    /// W47（余下部分）：输入法开关的快捷键——简繁、全半角、中英标点、Emoji、切换方案。
    /// 写进 default.custom.yaml 的 key_binder/bindings/+（雾凇各方案 import_preset: default，全拼双拼都生效），
    /// 由 RIME 自己处理（不需要助手常驻）。雾凇自带的 Ctrl+Shift+3 中英标点、Ctrl+Shift+4 简繁 保留。
    /// </summary>
    static class RimeToggleKeys
    {
        public static readonly (string Id, string Name, string Builtin)[] Switches =
        {
            ("traditionalization", "简体 / 繁体", "Ctrl+Shift+4"),
            ("full_shape", "半角 / 全角", ""),
            ("ascii_punct", "中文 / 英文标点", "Ctrl+Shift+3"),
            ("emoji", "Emoji 候选", ""),
            (".next", "切换输入方案（全拼 / 双拼）", "Ctrl+`"),
        };

        /// <summary>可选的按键：Ctrl+Shift+数字很少被程序占用；Ctrl+逗号/句号/斜杠 在任何时候都会被输入法拿走。</summary>
        public static readonly (string Id, string Name, string[] Rime)[] Keys =
        {
            ("", "不设", new string[0]),
            ("cs5", "Ctrl+Shift+5", new[] { "Control+Shift+5", "Control+Shift+percent" }),
            ("cs6", "Ctrl+Shift+6", new[] { "Control+Shift+6", "Control+Shift+asciicircum" }),
            ("cs7", "Ctrl+Shift+7", new[] { "Control+Shift+7", "Control+Shift+ampersand" }),
            ("cs8", "Ctrl+Shift+8", new[] { "Control+Shift+8", "Control+Shift+asterisk" }),
            ("cs9", "Ctrl+Shift+9", new[] { "Control+Shift+9", "Control+Shift+parenleft" }),
            ("c,", "Ctrl+,（逗号）", new[] { "Control+comma" }),
            ("c.", "Ctrl+.（句号）", new[] { "Control+period" }),
            ("c/", "Ctrl+/（斜杠）", new[] { "Control+slash" }),
        };

        /// <summary>patch: 下的行（没设任何键时为空）。</summary>
        public static string PatchLines(IDictionary<string, string> chosen)
        {
            if (chosen == null) return "";
            var y = new StringBuilder();
            foreach (var sw in Switches)
            {
                if (!chosen.TryGetValue(sw.Id, out var k) || string.IsNullOrEmpty(k)) continue;
                var key = Keys.FirstOrDefault(x => x.Id == k);
                if (key.Rime == null) continue;
                foreach (var accept in key.Rime)
                    y.Append(sw.Id == ".next"
                        ? "    - { when: always, select: .next, accept: " + accept + " }\n"
                        : "    - { when: always, toggle: " + sw.Id + ", accept: " + accept + " }\n");
            }
            return y.Length == 0 ? "" : "  \"key_binder/bindings/+\":\n" + y;
        }

        public static string Describe(IDictionary<string, string> chosen) =>
            string.Join("、", Switches.Where(s => chosen != null && chosen.TryGetValue(s.Id, out var k) && !string.IsNullOrEmpty(k))
                .Select(s => s.Name + " " + Keys.First(x => x.Id == chosen[s.Id]).Name));
    }
}
