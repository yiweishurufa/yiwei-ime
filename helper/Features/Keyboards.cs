using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yiwei
{
    /// <summary>
    /// 任务栏只留一个图标。Windows 输入指示器旁的 中/英 是小狼毫语言栏的输入模式按钮（GUID_LBI_INPUTMODE）；
    /// 只要系统里还装了别的键盘（中文版 Windows 默认带「微软拼音」，或者「英语(美国)」键盘），Windows 会在它旁边
    /// 再放一个键盘切换按钮，显示当前输入法的图标（一维的蓝色「一」）。这个按钮由 Windows 控制，输入法无法隐藏，
    /// 只能少装键盘：这里列出其他键盘，并在用户确认后只保留一维输入法（Windows 显示语言本身保留）。
    /// </summary>
    static class Keyboards
    {
        /// <summary>一维输入法的 TSF CLSID（scripts/rebrand.py 的 GUIDS）。</summary>
        public const string OurClsid = "54C92C2A-5FE9-43A0-A9EC-D05B42C996D4";

        public sealed class Entry
        {
            public string Language;   // zh-CN
            public string Tip;        // 0804:{CLSID}{PROFILE} 或 0409:00000409
            public bool Ours => Tip.IndexOf(OurClsid, StringComparison.OrdinalIgnoreCase) >= 0;
            public string Name => Describe(this);
        }

        // 认得出的输入法（先按 profile GUID，再按 CLSID）；认不出的显示成「某语言 的输入法」
        static readonly Dictionary<string, string> KnownTips = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "FA550B04-5AD7-411F-A5AC-CA038EC515D7", "微软拼音" },
            { "82590C13-F4DD-44F4-BA1D-8667246FDF8E", "微软五笔" },
            { "81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E", "微软输入法" },
            { "A3F4CDED-B1E9-41EE-9CA6-7B4D0DE6CB0A", "小狼毫" },
        };

        static string Describe(Entry e)
        {
            string lang;
            try { lang = new CultureInfo(e.Language).DisplayName; } catch { lang = e.Language; }
            if (e.Ours) return "一维输入法";
            var ids = System.Text.RegularExpressions.Regex.Matches(e.Tip, @"\{([0-9A-Fa-f\-]{36})\}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).Reverse().ToList();
            if (ids.Count == 0) return lang + " 键盘";
            foreach (var id in ids) if (KnownTips.TryGetValue(id, out var n)) return n + "（" + lang + "）";
            return lang + " 的输入法";
        }

        /// <summary>Every keyboard in the user's language list. Empty when PowerShell is unavailable.</summary>
        public static List<Entry> List()
        {
            var list = new List<Entry>();
            var output = PowerShell("Get-WinUserLanguageList | ForEach-Object { $t = $_.LanguageTag; $_.InputMethodTips | ForEach-Object { 'TIP ' + $t + ' ' + $_ } }");
            foreach (var line in (output ?? "").Split('\n'))
            {
                var p = line.Trim().Split(' ');
                if (p.Length == 3 && p[0] == "TIP") list.Add(new Entry { Language = p[1], Tip = p[2] });
            }
            return list;
        }

        /// <summary>The keyboards that would be removed: everything but ours, except the Windows display language itself (it must keep a keyboard).</summary>
        public static List<Entry> Others(List<Entry> all)
        {
            var ourLang = all.FirstOrDefault(e => e.Ours)?.Language;
            return all.Where(e => !e.Ours && (e.Language == ourLang || !IsDisplayLanguage(e.Language, all))).ToList();
        }

        /// <summary>Keyboards Windows keeps because they belong to the display language (when it is not our language).</summary>
        public static List<Entry> Kept(List<Entry> all)
        {
            var ourLang = all.FirstOrDefault(e => e.Ours)?.Language;
            return all.Where(e => !e.Ours && e.Language != ourLang && IsDisplayLanguage(e.Language, all)).ToList();
        }

        /// <summary>
        /// LANGID of the Windows display language, as the 4 hex digits that start an input method tip ("0804:…").
        /// Language tags differ between .NET and the language list (zh-CN vs zh-Hans-CN), the LANGID does not.
        /// </summary>
        static string UiLangId => (CultureInfo.CurrentUICulture.LCID & 0xFFFF).ToString("X4");

        static bool IsDisplayLanguage(string lang, List<Entry> all) =>
            all.Any(x => x.Language == lang && x.Tip.StartsWith(UiLangId + ":", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Keeps only 一维输入法 in its language, removes other languages except the display language,
        /// and puts 一维输入法's language first. Returns null on success, else a message.
        /// </summary>
        public static string KeepOnlyOurs()
        {
            var script = new StringBuilder();
            script.AppendLine("$ErrorActionPreference = 'Stop'");
            script.AppendLine("$clsid = '" + OurClsid + "'");
            script.AppendLine("$uid = '" + UiLangId + ":*'");
            script.AppendLine("$l = Get-WinUserLanguageList");
            script.AppendLine("$ours = $null");
            script.AppendLine("foreach ($x in $l) { foreach ($t in $x.InputMethodTips) { if ($t -match $clsid) { $ours = $x } } }");
            script.AppendLine("if (-not $ours) { 'RESULT NOTFOUND'; exit }");
            script.AppendLine("foreach ($t in @($ours.InputMethodTips)) { if ($t -notmatch $clsid) { [void]$ours.InputMethodTips.Remove($t) } }");
            script.AppendLine("for ($i = $l.Count - 1; $i -ge 0; $i--) { $x = $l[$i]; if ($x.LanguageTag -ne $ours.LanguageTag -and -not ($x.InputMethodTips | Where-Object { $_ -like $uid })) { $l.RemoveAt($i) } }");
            script.AppendLine("if ($l[0].LanguageTag -ne $ours.LanguageTag) { [void]$l.Remove($ours); $l.Insert(0, $ours) }");
            script.AppendLine("Set-WinUserLanguageList $l -Force");
            script.AppendLine("'RESULT OK'");
            var output = PowerShell(script.ToString()) ?? "";
            if (output.Contains("RESULT OK")) return null;
            if (output.Contains("RESULT NOTFOUND")) return "在系统的语言列表里没有找到一维输入法。请先在 Windows 设置 → 时间和语言 → 语言 里添加它。";
            Log.Write("keyboards: " + output);
            return "修改没有成功，可以在 Windows 设置 → 时间和语言 → 语言和区域 里手动删除其他键盘。";
        }

        public static void OpenSystemSettings() => Dialogs.Open("ms-settings:regionlanguage");

        /// <summary>
        /// Once, a little after start: if Windows will show a second (keyboard switcher) icon because other keyboards are
        /// installed, say why and offer the cleanup. Never during the first-run wizard.
        /// </summary>
        public static void HintOnceLater()
        {
            var s = Settings.Current;
            if (s.KeyboardHintShown || !s.FirstRunDone) return;
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
                    var all = List();
                    if (all.Count == 0 || !all.Any(e => e.Ours)) return; // unknown, or not added yet
                    var others = Others(all);
                    Settings.Current.KeyboardHintShown = true;
                    Settings.Current.Save();
                    if (others.Count == 0) return;
                    Tray.Balloon("任务栏上还有一个「一」图标？",
                        "那是 Windows 的键盘切换按钮：因为还装了" + string.Join("、", others.Select(e => e.Name).Distinct().Take(2)) + "。点这里，可以一键只保留一维输入法。",
                        () => Program.ShowSettings("常规"));
                }
                catch (Exception e) { Log.Write("keyboard hint: " + e.Message); }
            });
        }

        static string PowerShell(string script)
        {
            try
            {
                var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded)
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                };
                using (var p = Process.Start(psi))
                {
                    var err = p.StandardError.ReadToEndAsync();
                    var output = p.StandardOutput.ReadToEnd();
                    if (!p.WaitForExit(30000)) { try { p.Kill(); } catch { } }
                    if (err.Result.Trim().Length > 0) Log.Write("powershell: " + err.Result.Trim());
                    return output;
                }
            }
            catch (Exception e) { Log.Write("powershell: " + e.Message); return null; }
        }
    }
}
