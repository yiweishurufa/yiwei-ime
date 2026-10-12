using System;
using System.Diagnostics;
using System.Linq;

namespace Yiwei
{
    /// <summary>W28 搜索前缀：yiwei_search.lua 发来 "search:词"，用默认浏览器打开所选搜索引擎。</summary>
    static class WebSearch
    {
        public static readonly (string Id, string Name, string Url)[] Engines =
        {
            ("baidu", "百度", "https://www.baidu.com/s?wd={0}"),
            ("bing", "必应", "https://cn.bing.com/search?q={0}"),
            ("google", "Google", "https://www.google.com/search?q={0}"),
            ("sogou", "搜狗", "https://www.sogou.com/web?query={0}"),
            ("zhihu", "知乎", "https://www.zhihu.com/search?type=content&q={0}"),
            ("bilibili", "哔哩哔哩", "https://search.bilibili.com/all?keyword={0}"),
            ("wiki", "维基百科", "https://zh.wikipedia.org/w/index.php?search={0}"),
        };

        public static string UrlFor(string query, string engine)
        {
            var e = Engines.FirstOrDefault(x => x.Id == engine);
            if (e.Url == null) e = Engines[0];
            return string.Format(e.Url, Uri.EscapeDataString(query.Trim()));
        }

        public static void Open(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length > 500) return;
            var url = UrlFor(query, ToolSettings.Current.SearchEngine);
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { Log.Write("search: " + ex.Message); Tray.Balloon("搜索", "打不开浏览器：" + ex.Message); }
        }
    }
}
