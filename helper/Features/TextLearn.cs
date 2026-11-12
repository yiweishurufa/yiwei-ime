using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Yiwei
{
    /// <summary>文章学词：从一段文本里找反复出现的 2–4 字词组（纯本机统计，无分词库），供用户确认后加入「我的词条」。</summary>
    static class TextLearn
    {
        static readonly Regex Han = new Regex(@"[\u4e00-\u9fff]+");
        const string Stop = "的了是在和有我他她它这那就也都不而与及或但把被让对于";

        public static List<string> Extract(string text, int minCount = 3, int max = 200)
        {
            var cnt = new Dictionary<string, int>();
            foreach (Match m in Han.Matches(text))
            {
                var s = m.Value;
                for (int i = 0; i < s.Length; i++)
                    for (int n = 2; n <= 4 && i + n <= s.Length; n++)
                    {
                        var w = s.Substring(i, n);
                        if (Stop.IndexOf(w[0]) >= 0 || Stop.IndexOf(w[n - 1]) >= 0) continue;
                        cnt[w] = cnt.TryGetValue(w, out var c) ? c + 1 : 1;
                    }
            }
            var cand = cnt.Where(kv => kv.Value >= minCount).ToDictionary(kv => kv.Key, kv => kv.Value);
            // 去掉被更长词组"包住"且次数几乎相同的短词，以及次数几乎相同的长词的残片
            var keep = cand.Where(kv => !cand.Any(o => o.Key.Length > kv.Key.Length && o.Key.Contains(kv.Key) && o.Value * 10 >= kv.Value * 8))
                           .OrderByDescending(kv => kv.Value).ThenByDescending(kv => kv.Key.Length)
                           .Take(max).Select(kv => kv.Key).ToList();
            return keep;
        }
    }
}
