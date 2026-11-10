using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Yiwei
{
    /// <summary>
    /// 用户词库体检（#10）：检查屏蔽词、个人纠错规则两个文本文件里的问题行——
    /// 重复、格式不对（纠错规则要「错码 正码」）、错码含非小写字母、错码等于正码、屏蔽词过长。只读，不改文件。
    /// </summary>
    static class DictHealth
    {
        public static List<string> Check(string blockedText, string correctionsText)
        {
            var issues = new List<string>();
            var seen = new HashSet<string>();
            int ln = 0;
            foreach (var raw in (blockedText ?? "").Split('\n'))
            {
                ln++;
                var s = raw.Trim();
                if (s.Length == 0 || s.StartsWith("#")) continue;
                if (!seen.Add(s)) issues.Add("屏蔽词第 " + ln + " 行「" + s + "」重复");
                else if (s.Length > 20) issues.Add("屏蔽词第 " + ln + " 行过长（" + s.Length + " 字），可能是误粘贴");
            }
            seen.Clear(); ln = 0;
            foreach (var raw in (correctionsText ?? "").Split('\n'))
            {
                ln++;
                var s = raw.Trim();
                if (s.Length == 0 || s.StartsWith("#")) continue;
                var p = s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length != 2) { issues.Add("纠错规则第 " + ln + " 行格式不对，应为「错码 正码」"); continue; }
                if (p[0].Any(c => c < 'a' || c > 'z')) issues.Add("纠错规则第 " + ln + " 行：错码「" + p[0] + "」只能是小写字母");
                else if (p[0] == p[1]) issues.Add("纠错规则第 " + ln + " 行：错码和正码相同");
                else if (!seen.Add(p[0] + " " + p[1])) issues.Add("纠错规则第 " + ln + " 行「" + s + "」重复");
            }
            return issues;
        }

        public static string Run()
        {
            string Read(string f) { try { return File.Exists(f) ? File.ReadAllText(f, Encoding.UTF8) : ""; } catch { return ""; } }
            var issues = Check(Read(Paths.BlockedFile), Read(Paths.CorrectionsFile));
            return issues.Count == 0 ? "屏蔽词和纠错规则没有发现问题 👍"
                : "发现 " + issues.Count + " 处问题：\n· " + string.Join("\n· ", issues.Take(30));
        }
    }
}
