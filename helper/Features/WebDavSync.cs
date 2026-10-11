using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Yiwei
{
    /// <summary>
    /// W36 WebDAV / 坚果云同步用户词库和常用语。
    /// 和 librime 自己的同步目录（Features/Sync.cs）同一套：本机快照 sync/&lt;installation_id&gt;/*.userdb.txt
    /// 上传到 &lt;WebDAV&gt;/yiwei-ime/&lt;installation_id&gt;/，其它设备的文件夹下载到本地同步目录，再跑一次 Deployer /sync 合并。
    /// 常用语：每台设备上传 snippets.json（通用格式），下载别人的合并进来（同组同内容跳过）。
    /// 密码用 Windows DPAPI（当前用户）加密后存在 tools.json。
    /// </summary>
    public static class WebDavSync
    {
        public const string JianguoyunUrl = "https://dav.jianguoyun.com/dav/";
        const string Folder = "yiwei-ime";

        public static string Protect(string plain) =>
            string.IsNullOrEmpty(plain) ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser));

        static string Unprotect(string b64)
        {
            try { return string.IsNullOrEmpty(b64) ? "" : Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(b64), null, DataProtectionScope.CurrentUser)); }
            catch { return ""; }
        }

        public static bool Configured { get { var t = ToolSettings.Current; return !string.IsNullOrWhiteSpace(t.DavUrl) && !string.IsNullOrWhiteSpace(t.DavUser); } }

        static HttpClient Client()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var t = ToolSettings.Current;
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(t.DavUser + ":" + Unprotect(t.DavPass))));
            return c;
        }

        static string Base => ToolSettings.Current.DavUrl.TrimEnd('/') + "/";
        static string Url(params string[] parts) => Base + string.Join("/", parts.Select(Uri.EscapeDataString));

        static async Task Ensure(HttpClient c, string url)
        {
            var r = await c.SendAsync(new HttpRequestMessage(new HttpMethod("MKCOL"), url));
            // 201 created, 405 already exists; 401 = wrong password
            if (r.StatusCode == HttpStatusCode.Unauthorized) throw new Exception("WebDAV 账号或密码不对（坚果云要用「第三方应用密码」，不是登录密码）");
        }

        static async Task<List<string>> List(HttpClient c, string url)
        {
            var req = new HttpRequestMessage(new HttpMethod("PROPFIND"), url) { Content = new StringContent("<?xml version=\"1.0\"?><d:propfind xmlns:d=\"DAV:\"><d:prop><d:resourcetype/></d:prop></d:propfind>", Encoding.UTF8, "application/xml") };
            req.Headers.Add("Depth", "1");
            var r = await c.SendAsync(req);
            if (r.StatusCode == HttpStatusCode.NotFound) return new List<string>();
            if (r.StatusCode == HttpStatusCode.Unauthorized) throw new Exception("WebDAV 账号或密码不对（坚果云要用「第三方应用密码」）");
            if ((int)r.StatusCode != 207) throw new Exception("WebDAV 列目录失败：" + (int)r.StatusCode);
            var x = XDocument.Parse(await r.Content.ReadAsStringAsync());
            XNamespace d = "DAV:";
            var self = new Uri(url).AbsolutePath.TrimEnd('/');
            return x.Descendants(d + "href").Select(h => Uri.UnescapeDataString(h.Value.TrimEnd('/')))
                .Select(h => h.Contains("://") ? new Uri(h).AbsolutePath : h)
                .Where(h => !string.Equals(h.TrimEnd('/'), Uri.UnescapeDataString(self), StringComparison.Ordinal))
                .Select(h => h.Substring(h.LastIndexOf('/') + 1)).Where(n => n.Length > 0).Distinct().ToList();
        }

        /// <summary>Test the account. Returns a short status line.</summary>
        public static async Task<string> TestAsync()
        {
            using (var c = Client()) { await Ensure(c, Url(Folder)); var l = await List(c, Url(Folder) + "/"); return "连接成功，云端有 " + l.Count + " 台设备的数据"; }
        }

        static int _running;

        /// <summary>Full sync round. Returns a status line.</summary>
        public static async Task<string> SyncAsync()
        {
            if (!Configured) throw new Exception("还没有填写 WebDAV 地址和账号");
            if (System.Threading.Interlocked.Exchange(ref _running, 1) == 1) return "正在同步中";
            try
            {
                // 1. 本机快照（也会合并已下载的其它设备快照）
                await Sync.RunAsync();
                var id = Sync.InstallationId ?? throw new Exception("没有 installation_id，先在 词库 → 同步 里同步一次");
                var local = Sync.EffectiveDir;
                int up = 0, down = 0, snippets = 0;
                using (var c = Client())
                {
                    await Ensure(c, Url(Folder));
                    await Ensure(c, Url(Folder, id));
                    // 2. 上传本机
                    var mine = Path.Combine(local, id);
                    if (Directory.Exists(mine))
                        foreach (var f in Directory.GetFiles(mine, "*.userdb.txt"))
                        {
                            var r = await c.PutAsync(Url(Folder, id, Path.GetFileName(f)), new ByteArrayContent(File.ReadAllBytes(f)));
                            if (!r.IsSuccessStatusCode) throw new Exception("上传失败：" + (int)r.StatusCode);
                            up++;
                        }
                    await c.PutAsync(Url(Folder, id, "snippets.json"), new StringContent(Backup.SharedSnippetsJson(), new UTF8Encoding(false), "application/json"));
                    // 3. 下载其它设备
                    foreach (var dev in await List(c, Url(Folder) + "/"))
                    {
                        if (dev == id || dev.Contains("..")) continue;
                        foreach (var name in await List(c, Url(Folder, dev) + "/"))
                        {
                            if (name.Contains("..") || name.Contains("\\")) continue;
                            var bytes = await c.GetByteArrayAsync(Url(Folder, dev, name));
                            if (name == "snippets.json") { snippets += Backup.MergeSharedSnippets(Encoding.UTF8.GetString(bytes)); continue; }
                            if (!name.EndsWith(".userdb.txt", StringComparison.OrdinalIgnoreCase)) continue;
                            var dir = Path.Combine(local, dev); Directory.CreateDirectory(dir);
                            File.WriteAllBytes(Path.Combine(dir, name), bytes); down++;
                        }
                    }
                }
                // 4. 合并下载来的快照
                if (down > 0) await Sync.RunAsync();
                var msg = DateTime.Now.ToString("MM-dd HH:mm") + " 同步完成：上传 " + up + " 个词库，下载 " + down + " 个" + (snippets > 0 ? "，新增常用语 " + snippets + " 条" : "");
                var t = ToolSettings.Current; t.DavLast = msg; t.Save();
                return msg;
            }
            catch (Exception e)
            {
                var t = ToolSettings.Current; t.DavLast = DateTime.Now.ToString("MM-dd HH:mm") + " 同步失败：" + e.GetBaseException().Message; t.Save();
                throw;
            }
            finally { System.Threading.Interlocked.Exchange(ref _running, 0); }
        }

        static System.Windows.Forms.Timer _timer;

        /// <summary>Automatic sync every 6 hours (and 2 minutes after start) when 自动同步 is on.</summary>
        public static void Start()
        {
            _timer = new System.Windows.Forms.Timer { Interval = 2 * 60 * 1000 };
            _timer.Tick += async (s, e) =>
            {
                _timer.Interval = 6 * 60 * 60 * 1000;
                if (!ToolSettings.Current.DavAuto || !Configured) return;
                try { await SyncAsync(); } catch (Exception ex) { Log.Write("webdav: " + ex.GetBaseException().Message); }
            };
            _timer.Start();
        }
    }
}
