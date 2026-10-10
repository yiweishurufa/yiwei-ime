using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Yiwei
{
    /// <summary>
    /// 国内下载镜像链。助手所有从 GitHub 来的东西（自动更新清单与安装包、雾凇词库、语法模型、发布信息 API）
    /// 都按这里的顺序依次尝试：上次成功的源 → ghfast.top → gh-proxy.com → jsDelivr（只适用于仓库里的文件）→ GitHub 原站。
    /// 代理只是转发，内容的正确性仍由文件大小 + SHA-256（GitHub 发布摘要 / 更新清单）把关。
    /// </summary>
    public static class Mirrors
    {
        public sealed class Source
        {
            public string Id, Name;
            /// <summary>Maps an original GitHub url to this source's url, or null when the source cannot serve it.</summary>
            public Func<Uri, string> Map;
            /// <summary>Hosts a download through this source may end up on (after redirects).</summary>
            public string[] Hosts;
            /// <summary>Time allowed for the response headers (the first byte of a big file comes much later).</summary>
            public TimeSpan HeaderTimeout;
        }

        static bool IsGitHubFile(Uri u) => u.Host == "github.com" || u.Host == "raw.githubusercontent.com" || u.Host == "gist.githubusercontent.com";

        public static readonly Source[] All =
        {
            new Source
            {
                Id = "ghfast", Name = "ghfast.top 加速",
                Map = u => IsGitHubFile(u) ? "https://ghfast.top/" + u.AbsoluteUri : null, // ghfast 不转发 api.github.com
                Hosts = new[] { "ghfast.top" }, HeaderTimeout = TimeSpan.FromSeconds(12),
            },
            new Source
            {
                Id = "ghproxy", Name = "gh-proxy.com 加速",
                Map = u => IsGitHubFile(u) || u.Host == "api.github.com" ? "https://gh-proxy.com/" + u.AbsoluteUri : null,
                Hosts = new[] { "gh-proxy.com" }, HeaderTimeout = TimeSpan.FromSeconds(12),
            },
            new Source
            {
                Id = "jsdelivr", Name = "jsDelivr CDN",
                Map = JsDelivr,
                Hosts = new[] { "cdn.jsdelivr.net", "fastly.jsdelivr.net", "gcore.jsdelivr.net" }, HeaderTimeout = TimeSpan.FromSeconds(10),
            },
            new Source
            {
                Id = "github", Name = "GitHub 原站",
                Map = u => u.AbsoluteUri,
                Hosts = new[] { "github.com", ".github.com", ".githubusercontent.com" }, HeaderTimeout = TimeSpan.FromSeconds(20),
            },
        };

        /// <summary>raw.githubusercontent.com/o/r/branch/path 与 github.com/o/r/raw/branch/path → cdn.jsdelivr.net/gh/o/r@branch/path。发布附件不支持。</summary>
        static string JsDelivr(Uri u)
        {
            var p = u.AbsolutePath.Trim('/').Split('/');
            if (u.Host == "raw.githubusercontent.com" && p.Length >= 4)
                return "https://cdn.jsdelivr.net/gh/" + p[0] + "/" + p[1] + "@" + p[2] + "/" + string.Join("/", p.Skip(3));
            if (u.Host == "github.com" && p.Length >= 5 && (p[2] == "raw" || p[2] == "blob"))
                return "https://cdn.jsdelivr.net/gh/" + p[0] + "/" + p[1] + "@" + p[3] + "/" + string.Join("/", p.Skip(4));
            return null;
        }

        static string StateFile => Path.Combine(Paths.YiweiDir, "mirror.json");
        sealed class State { public string Last { get; set; } = ""; public string LastAt { get; set; } = ""; }
        static State _state;
        static readonly object Gate = new object();

        /// <summary>Id of the source that worked last time ("" when none yet).</summary>
        public static string Last
        {
            get { lock (Gate) { if (_state == null) _state = Json.Load<State>(StateFile); return _state.Last ?? ""; } }
        }

        public static string NameOf(string id) => All.FirstOrDefault(s => s.Id == id)?.Name ?? id;

        public static void Remember(Source s)
        {
            lock (Gate)
            {
                if (_state == null) _state = Json.Load<State>(StateFile);
                if (_state.Last == s.Id) return;
                _state.Last = s.Id; _state.LastAt = DateTime.Now.ToString("o");
                try { Json.Save(StateFile, _state); } catch (Exception e) { Log.Write("mirror state: " + e.Message); }
                Log.Write("mirror: now using " + s.Id);
            }
        }

        /// <summary>The sources to try for <paramref name="githubUrl"/>, last good one first, each with its mapped url.</summary>
        public static List<KeyValuePair<Source, string>> Candidates(string githubUrl)
        {
            var uri = new Uri(githubUrl);
            if (uri.Scheme != "https" || !IsAllowedHost(uri.Host, All.Last().Hosts))
                throw new Exception("只允许从 GitHub 下载：" + uri.Host);
            var last = Last;
            var list = new List<KeyValuePair<Source, string>>();
            foreach (var s in All.OrderBy(s => s.Id == last ? 0 : 1)) // stable: the rest keep their order
            {
                string url = null;
                try { url = s.Map(uri); } catch { }
                if (url != null) list.Add(new KeyValuePair<Source, string>(s, url));
            }
            return list;
        }

        public static bool IsAllowedHost(string host, string[] hosts)
        {
            host = (host ?? "").ToLowerInvariant();
            return hosts.Any(h => h.StartsWith(".") ? host.EndsWith(h) : host == h);
        }

        /// <summary>Hosts a response may finally come from when it was requested through <paramref name="s"/>.</summary>
        public static bool FinalHostOk(Source s, string host) => IsAllowedHost(host, s.Hosts) || IsAllowedHost(host, All.Last().Hosts);
    }
}
