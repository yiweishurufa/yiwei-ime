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
            public long[] ByHour = new long[24];
            public SortedDictionary<DateTime, long> ByDay = new SortedDictionary<DateTime, long>();
            public List<KeyValuePair<string, long>> TopApps = new List<KeyValuePair<string, long>>();
            public List<KeyValuePair<string, long>> TopWords = new List<KeyValuePair<string, long>>();
        }

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
                    if (n >= 2 && n <= 8 && text.Any(c => c >= 0x4E00 && c <= 0x9FFF))
                    { words.TryGetValue(text, out var w); words[text] = w + 1; }
                }
            r.TopApps = appCount.OrderByDescending(x => x.Value).Take(8).ToList();
            r.TopWords = words.OrderByDescending(x => x.Value).Take(20).ToList();
            return r;
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
            foreach (var f in Directory.GetFiles(Paths.YiweiDir, "stats-*.tsv").Concat(Directory.GetFiles(Paths.YiweiDir, "apps-*.tsv")))
                try { File.Delete(f); } catch { }
        }
    }
}
