using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Yiwei
{
    /// <summary>
    /// Typing statistics. The RIME Lua processor (yiwei_stats.lua) appends committed text to
    /// yiwei/stats-YYYY-MM.tsv; this class records which app was in front (apps-YYYY-MM.tsv)
    /// and turns both into totals. Everything stays on this PC and can be cleared at any time.
    /// </summary>
    public static class Stats
    {
        static Timer _timer;
        static string _lastApp = "";

        public static void StartAppTracking()
        {
            _timer = new Timer { Interval = 2000 };
            _timer.Tick += (s, e) =>
            {
                if (!Settings.Current.StatsEnabled) { _lastApp = ""; return; }
                var app = Native.ForegroundProcessName();
                if (app.Length == 0 || app == _lastApp) return;
                _lastApp = app;
                try
                {
                    File.AppendAllText(Path.Combine(Paths.YiweiDir, "apps-" + DateTime.Now.ToString("yyyy-MM") + ".tsv"),
                        Unix(DateTime.UtcNow) + "\t" + app + "\n", new UTF8Encoding(false));
                }
                catch { }
            };
            _timer.Start();
        }

        static long Unix(DateTime utc) => (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        static DateTime Local(long unix) => new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unix).ToLocalTime();

        public class Report
        {
            public long Characters, Commits;
            public long Today, ThisWeek, ThisYear;
            public long CjkChars, PhraseSelectionsSaved;
            public long[] ByHour = new long[24];
            public SortedDictionary<DateTime, long> ByDay = new SortedDictionary<DateTime, long>();
            public List<KeyValuePair<string, long>> TopApps = new List<KeyValuePair<string, long>>();
            public List<KeyValuePair<string, long>> TopWords = new List<KeyValuePair<string, long>>();
            /// <summary>首选命中：用拼音选词上屏的次数和其中每段都选第一个候选的次数（本周、上周）。</summary>
            public long PicksWeek, FirstWeek, PicksLastWeek, FirstLastWeek;
            /// <summary>本周选词不顺的词：「删掉的字 → 重打成」和翻页才选到的词（来自 typo-*.tsv，本月和上月文件）。</summary>
            public List<KeyValuePair<string, long>> TopRetyped = new List<KeyValuePair<string, long>>();
            public List<KeyValuePair<string, long>> TopPaged = new List<KeyValuePair<string, long>>();
        }

        /// <summary>命中率百分比；样本太少（不到 20 次）时返回 null，免得一两次选词就显示 0% / 100%。</summary>
        public static int? HitRate(long picks, long first) => picks < 20 ? (int?)null : (int)Math.Round(first * 100.0 / picks);

        public static int CountChars(string s)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsWhiteSpace(s[i])) continue;
                if (char.IsHighSurrogate(s[i])) i++;
                n++;
            }
            return n;
        }

        public static Report Build()
        {
            var r = new Report();
            var dir = Paths.YiweiDir;
            var apps = new List<KeyValuePair<long, string>>();
            foreach (var f in Directory.GetFiles(dir, "apps-*.tsv").OrderBy(x => x))
                foreach (var line in SafeLines(f))
                {
                    var p = line.Split('\t');
                    if (p.Length >= 2 && long.TryParse(p[0], out var t)) apps.Add(new KeyValuePair<long, string>(t, p[1]));
                }
            apps.Sort((a, b) => a.Key.CompareTo(b.Key));
            var appCount = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var words = new Dictionary<string, long>();
            var now = DateTime.Now;
            var weekStart = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
            foreach (var f in Directory.GetFiles(dir, "stats-*.tsv").OrderBy(x => x))
                foreach (var line in SafeLines(f))
                {
                    int tab = line.IndexOf('\t');
                    if (tab <= 0 || !long.TryParse(line.Substring(0, tab), out var t)) continue;
                    var text = line.Substring(tab + 1);
                    int n = CountChars(text);
                    if (n == 0) continue;
                    var when = Local(t);
                    r.Characters += n; r.Commits++;
                    if (when.Date == now.Date) r.Today += n;
                    if (when >= weekStart) r.ThisWeek += n;
                    if (when.Year == now.Year) r.ThisYear += n;
                    r.ByHour[when.Hour] += n;
                    r.ByDay.TryGetValue(when.Date, out var d); r.ByDay[when.Date] = d + n;
                    var app = AppAt(apps, t);
                    if (app != null) { appCount.TryGetValue(app, out var a); appCount[app] = a + n; }
                    int cjk = text.Count(c => c >= 0x3400 && c <= 0x9FFF);
                    r.CjkChars += cjk;
                    if (cjk >= 2) r.PhraseSelectionsSaved += cjk - 1; // one selection for the phrase instead of one per character
                    if (n >= 2 && n <= 8 && text.Any(c => c >= 0x4E00 && c <= 0x9FFF))
                    { words.TryGetValue(text, out var w); words[text] = w + 1; }
                }
            var lastWeekStart = weekStart.AddDays(-7);
            foreach (var f in Directory.GetFiles(dir, "pick-*.tsv"))
                foreach (var line in SafeLines(f))
                {
                    var p = line.Split('\t');
                    if (p.Length != 2 || !DateTime.TryParseExact(p[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)) continue;
                    bool first = p[1] == "1";
                    if (day >= weekStart) { r.PicksWeek++; if (first) r.FirstWeek++; }
                    else if (day >= lastWeekStart) { r.PicksLastWeek++; if (first) r.FirstLastWeek++; }
                }
            // typo-*.tsv 不带日期（只记词），按月分文件：取本月和上月
            var months = new[] { now.ToString("yyyy-MM"), now.AddMonths(-1).ToString("yyyy-MM") };
            var typoFiles = months.Select(m => Path.Combine(dir, "typo-" + m + ".tsv")).Where(File.Exists).ToList();
            var retyped = new Dictionary<string, long>(); var paged = new Dictionary<string, long>();
            foreach (var f in typoFiles)
                foreach (var line in SafeLines(f))
                {
                    var p = line.Split('\t');
                    if (p.Length == 3 && p[0] == "retype") { var k = p[1] + " → " + p[2]; retyped.TryGetValue(k, out var n); retyped[k] = n + 1; }
                    else if (p.Length == 2 && p[0] == "paged") { paged.TryGetValue(p[1], out var n); paged[p[1]] = n + 1; }
                }
            r.TopRetyped = retyped.OrderByDescending(x => x.Value).Take(8).ToList();
            r.TopPaged = paged.OrderByDescending(x => x.Value).Take(8).ToList();
            r.TopApps = appCount.OrderByDescending(x => x.Value).Take(8).ToList();
            r.TopWords = words.OrderByDescending(x => x.Value).Take(20).ToList();
            return r;
        }

        /// <summary>
        /// Estimated keystrokes saved compared with typing every character in full pinyin and picking it on its own:
        /// one selection per extra character of each phrase, plus ~1.3 keys per character with a double-pinyin schema.
        /// </summary>
        public static long KeystrokesSaved(Report r, string schema)
        {
            double perChar = schema != null && schema.StartsWith("double_pinyin") ? 1.3 : 0;
            return r.PhraseSelectionsSaved + (long)(r.CjkChars * perChar);
        }

        static string AppAt(List<KeyValuePair<long, string>> apps, long t)
        {
            int lo = 0, hi = apps.Count - 1, best = -1;
            while (lo <= hi) { int mid = (lo + hi) / 2; if (apps[mid].Key <= t) { best = mid; lo = mid + 1; } else hi = mid - 1; }
            return best >= 0 ? apps[best].Value : null;
        }

        static IEnumerable<string> SafeLines(string path)
        {
            string[] lines;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                    lines = sr.ReadToEnd().Split('\n');
            }
            catch { yield break; }
            foreach (var l in lines) if (l.Length > 0) yield return l.TrimEnd('\r');
        }

        public static void Clear()
        {
            foreach (var f in Directory.GetFiles(Paths.YiweiDir, "stats-*.tsv").Concat(Directory.GetFiles(Paths.YiweiDir, "apps-*.tsv")).Concat(Directory.GetFiles(Paths.YiweiDir, "typo-*.tsv")).Concat(Directory.GetFiles(Paths.YiweiDir, "pick-*.tsv")))
                try { File.Delete(f); } catch { }
        }
    }
}
