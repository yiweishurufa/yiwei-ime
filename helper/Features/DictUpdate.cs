using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Yiwei
{
    /// <summary>
    /// The only network client the dictionary / grammar / app-update features use. Every request is for a GitHub
    /// url (api.github.com, release downloads, raw repo files) and goes through the domestic mirror chain in
    /// <see cref="Mirrors"/>: the source that worked last time first, then the others in order, GitHub itself last.
    /// </summary>
    public static class GitHubNet
    {
        static HttpClient _http;
        static readonly object Gate = new object();

        public static HttpClient Http
        {
            get
            {
                lock (Gate)
                {
                    if (_http != null) return _http;
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                    var h = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate })
                    { Timeout = Timeout.InfiniteTimeSpan };
                    var ver = typeof(GitHubNet).Assembly.GetName().Version;
                    h.DefaultRequestHeaders.UserAgent.ParseAdd("YiweiIME/" + ver.ToString(3) + " (+https://github.com/yiweishurufa/yiwei-ime)");
                    return _http = h;
                }
            }
        }

        /// <summary>GET a small text file (API json, appcast) through the mirror chain. <paramref name="accept"/> rejects a proxy's error page.</summary>
        public static async Task<string> GetText(string url, Func<string, bool> accept, string acceptHeader = null, CancellationToken ct = default(CancellationToken))
        {
            var errors = new List<string>();
            foreach (var c in Mirrors.Candidates(url))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        cts.CancelAfter(c.Key.HeaderTimeout + TimeSpan.FromSeconds(10));
                        using (var req = new HttpRequestMessage(HttpMethod.Get, c.Value))
                        {
                            if (acceptHeader != null) req.Headers.Accept.ParseAdd(acceptHeader);
                            using (var resp = await Http.SendAsync(req, cts.Token).ConfigureAwait(false))
                            {
                                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                                if (!Mirrors.FinalHostOk(c.Key, resp.RequestMessage?.RequestUri?.Host)) throw new Exception("被重定向到 " + resp.RequestMessage?.RequestUri?.Host);
                                if (!resp.IsSuccessStatusCode)
                                    throw new Exception("HTTP " + (int)resp.StatusCode + (body.Contains("rate limit") ? "（请求过于频繁，稍后再试）" : ""));
                                if (accept != null && !accept(body)) throw new Exception("返回的内容无法识别");
                                Mirrors.Remember(c.Key);
                                return body;
                            }
                        }
                    }
                }
                catch (Exception e) when (!ct.IsCancellationRequested)
                {
                    var msg = e is OperationCanceledException ? "超时" : e.GetBaseException().Message;
                    errors.Add(c.Key.Name + "：" + msg);
                    Log.Write("net " + c.Key.Id + " " + url + ": " + msg);
                }
            }
            throw new Exception("所有下载源都连不上（" + string.Join("；", errors) + "）");
        }

        /// <summary>GET a GitHub API url (through the mirror chain) and parse the JSON object.</summary>
        public static async Task<Dictionary<string, object>> GetJson(string url, CancellationToken ct = default(CancellationToken))
        {
            Dictionary<string, object> parsed = null;
            await GetText(url, body =>
            {
                try { parsed = Json.Parse(body) as Dictionary<string, object>; } catch { parsed = null; }
                return parsed != null;
            }, "application/vnd.github+json", ct).ConfigureAwait(false);
            return parsed;
        }

        public sealed class Asset
        {
            public string Name, Url, Sha256, UpdatedAt;
            public long Size;
        }

        public static List<Asset> Assets(Dictionary<string, object> release)
        {
            var list = new List<Asset>();
            if (!(release.TryGetValue("assets", out var a) && a is object[] arr)) return list;
            foreach (var o in arr.OfType<Dictionary<string, object>>())
            {
                var digest = o.TryGetValue("digest", out var d) ? d as string ?? "" : "";
                list.Add(new Asset
                {
                    Name = o.TryGetValue("name", out var n) ? n as string : "",
                    Url = o.TryGetValue("browser_download_url", out var u) ? u as string : "",
                    Sha256 = digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest.Substring(7).ToLowerInvariant() : "",
                    UpdatedAt = o.TryGetValue("updated_at", out var up) ? up as string ?? "" : "",
                    Size = o.TryGetValue("size", out var s) ? Convert.ToInt64(s) : 0,
                });
            }
            return list;
        }

        /// <summary>
        /// Downloads a GitHub url to <paramref name="dest"/>, trying each mirror in turn. When <paramref name="resume"/> is set
        /// and the file exists, asks for the rest only (Range). Reports progress 0..1. A source whose answer does not have the
        /// expected length (a proxy's error page) is skipped. The caller still checks SHA-256 where one is published.
        /// </summary>
        public static async Task Download(string url, string dest, long expectedSize, IProgress<double> progress, bool resume, CancellationToken ct = default(CancellationToken))
        {
            var errors = new List<string>();
            foreach (var c in Mirrors.Candidates(url))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await DownloadFrom(c.Key, c.Value, dest, expectedSize, progress, resume, ct).ConfigureAwait(false);
                    Mirrors.Remember(c.Key);
                    return;
                }
                catch (Exception e) when (!ct.IsCancellationRequested)
                {
                    var msg = e is OperationCanceledException ? "超时" : e.GetBaseException().Message;
                    errors.Add(c.Key.Name + "：" + msg);
                    Log.Write("download " + c.Key.Id + " " + url + ": " + msg);
                    // a wrong-length leftover would poison the next source's resume
                    try { if (File.Exists(dest) && expectedSize > 0 && new FileInfo(dest).Length > expectedSize) File.Delete(dest); } catch { }
                }
            }
            throw new Exception("下载失败，所有下载源都不可用（" + string.Join("；", errors) + "）");
        }

        static async Task DownloadFrom(Mirrors.Source source, string url, string dest, long expectedSize, IProgress<double> progress, bool resume, CancellationToken ct)
        {
            long have = resume && File.Exists(dest) ? new FileInfo(dest).Length : 0;
            if (expectedSize > 0 && have > expectedSize) { File.Delete(dest); have = 0; }
            if (expectedSize > 0 && have == expectedSize) { progress?.Report(1); return; }
            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (have > 0) req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(have, null);
                HttpResponseMessage resp;
                using (var head = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    head.CancelAfter(source.HeaderTimeout);
                    resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, head.Token).ConfigureAwait(false);
                }
                using (resp)
                {
                    if (!resp.IsSuccessStatusCode) throw new Exception("HTTP " + (int)resp.StatusCode);
                    var finalHost = resp.RequestMessage?.RequestUri?.Host ?? "";
                    if (!Mirrors.FinalHostOk(source, finalHost)) throw new Exception("被重定向到 " + finalHost);
                    bool append = have > 0 && resp.StatusCode == HttpStatusCode.PartialContent;
                    if (!append) have = 0;
                    var len = resp.Content.Headers.ContentLength;
                    if (expectedSize > 0 && len.HasValue && have + len.Value != expectedSize)
                        throw new Exception("文件大小不对（" + (have + len.Value) + " / " + expectedSize + " 字节）");
                    long total = expectedSize > 0 ? expectedSize : have + (len ?? 0);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    try
                    {
                        using (var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var dst = new FileStream(dest, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, true))
                        {
                            var buf = new byte[1 << 16];
                            long done = have, lastReport = 0;
                            int n;
                            // a stalled connection should not hang forever: move on to the next source
                            while (true)
                            {
                                using (var stall = CancellationTokenSource.CreateLinkedTokenSource(ct))
                                {
                                    stall.CancelAfter(TimeSpan.FromSeconds(30));
                                    var read = src.ReadAsync(buf, 0, buf.Length, stall.Token);
                                    if (await Task.WhenAny(read, Task.Delay(Timeout.Infinite, stall.Token).ContinueWith(_ => 0)).ConfigureAwait(false) != read)
                                    {
                                        ct.ThrowIfCancellationRequested();
                                        throw new Exception("30 秒没有收到数据");
                                    }
                                    n = await read.ConfigureAwait(false);
                                }
                                if (n <= 0) break;
                                await dst.WriteAsync(buf, 0, n, ct).ConfigureAwait(false);
                                done += n;
                                if (total > 0 && progress != null && done - lastReport > (1 << 20)) { lastReport = done; progress.Report(Math.Min(1.0, (double)done / total)); }
                            }
                        }
                        if (!len.HasValue && expectedSize > 0 && new FileInfo(dest).Length != expectedSize)
                            throw new Exception("下载不完整（" + new FileInfo(dest).Length + " / " + expectedSize + " 字节）");
                    }
                    catch when (!len.HasValue)
                    {
                        // length was never vouched for: do not let a half page from this source become the next source's resume point
                        try { File.Delete(dest); } catch { }
                        throw;
                    }
                    progress?.Report(1);
                }
            }
            if (expectedSize > 0 && new FileInfo(dest).Length != expectedSize)
                throw new Exception("下载不完整（" + new FileInfo(dest).Length + " / " + expectedSize + " 字节）");
        }

        public static string Sha256Of(string path)
        {
            using (var sha = SHA256.Create())
            using (var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
                return BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>Runs the deployer and waits (dictionary compiles can take a minute or two).</summary>
        public static bool DeployAndWait(int timeoutMs = 10 * 60 * 1000)
        {
            try
            {
                if (!File.Exists(Paths.Deployer)) { Log.Write("deploy: deployer not found " + Paths.Deployer); return false; }
                using (var p = Process.Start(new ProcessStartInfo(Paths.Deployer, "/deploy") { UseShellExecute = false, CreateNoWindow = true }))
                {
                    if (p == null) return false;
                    if (!p.WaitForExit(timeoutMs)) { Log.Write("deploy: timeout"); return false; }
                    return true;
                }
            }
            catch (Exception e) { Log.Write("deploy: " + e.Message); return false; }
        }
    }

    /// <summary>
    /// Daily 雾凇拼音 dictionary update from the iDvel/rime-ice GitHub release ("nightly", full.zip).
    /// Download → SHA-256 (GitHub asset digest) → unzip to a staging folder and check the key files →
    /// back up the user's current dictionaries to yiwei/backup/&lt;date&gt; → replace → redeploy.
    /// Any failure (including a deploy that did not rebuild the dictionary) restores the backup.
    /// Only cn_dicts/, en_dicts/, rime_ice.dict.yaml and melt_eng.dict.yaml are touched; *.custom.yaml and userdb never are.
    /// </summary>
    public static class DictUpdater
    {
        const string Api = "https://api.github.com/repos/iDvel/rime-ice/releases/latest";
        const string AssetName = "full.zip";

        /// <summary>What an update replaces, relative to the user folder.</summary>
        static readonly string[] Items = { "cn_dicts", "en_dicts", "rime_ice.dict.yaml", "melt_eng.dict.yaml" };

        /// <summary>Must be in the package, non-empty, and look like a RIME dictionary.</summary>
        static readonly string[] KeyFiles =
        {
            "rime_ice.dict.yaml", "melt_eng.dict.yaml", "cn_dicts/8105.dict.yaml", "cn_dicts/base.dict.yaml",
            "cn_dicts/ext.dict.yaml", "cn_dicts/tencent.dict.yaml", "cn_dicts/others.dict.yaml", "en_dicts/en.dict.yaml",
        };

        // ---------- record (yiwei/dict-update.json) ----------

        public class BackupInfo
        {
            public string Dir { get; set; } = "";
            public string Tag { get; set; } = "";
            public string Time { get; set; } = "";
            /// <summary>Items that existed in the user folder at backup time (others were absent → shared data was used).</summary>
            public List<string> Present { get; set; } = new List<string>();
        }

        public class Record
        {
            public string Tag { get; set; } = "";          // tag@asset updated_at
            public string Sha256 { get; set; } = "";
            public string InstalledAt { get; set; } = "";
            public string CheckedAt { get; set; } = "";
            public string LastError { get; set; } = "";
            public string LastErrorAt { get; set; } = "";
            public List<BackupInfo> Backups { get; set; } = new List<BackupInfo>();
        }

        static string RecordFile => Path.Combine(Paths.YiweiDir, "dict-update.json");
        static string BackupRoot => Path.Combine(Paths.YiweiDir, "backup");
        static readonly object FileGate = new object();

        public static Record Load()
        {
            lock (FileGate)
            {
                var r = Json.Load<Record>(RecordFile);
                if (r.Backups == null) r.Backups = new List<BackupInfo>();
                r.Backups = r.Backups.Where(b => b != null && Directory.Exists(b.Dir)).ToList();
                return r;
            }
        }

        static void Store(Record r) { lock (FileGate) { try { Json.Save(RecordFile, r); } catch (Exception e) { Log.Write("dict record: " + e.Message); } } }

        /// <summary>Human-readable current version, e.g. "nightly · 10月5日 21:38 发布 · 10月8日 安装".</summary>
        public static string Describe()
        {
            var r = Load();
            var tag = string.IsNullOrEmpty(r.Tag) ? Settings.Current.DictTag : r.Tag;
            if (string.IsNullOrEmpty(tag)) return "当前：随安装包附带的版本";
            var parts = tag.Split('@');
            var s = "当前：雾凇拼音 " + parts[0];
            if (parts.Length > 1 && DateTime.TryParse(parts[1], out var pub)) s += "（" + pub.ToLocalTime().ToString("yyyy-M-d HH:mm") + " 发布）";
            if (DateTime.TryParse(r.InstalledAt, out var inst)) s += "\n上次更新：" + inst.ToString("yyyy-M-d HH:mm");
            var checkedAt = string.IsNullOrEmpty(r.CheckedAt) ? Settings.Current.DictCheckedAt : r.CheckedAt;
            if (DateTime.TryParse(checkedAt, out var chk)) s += "　上次检查：" + chk.ToString("M月d日 HH:mm");
            return s;
        }

        public static bool CanRollback => Load().Backups.Count > 0;

        // ---------- schedule ----------

        static System.Windows.Forms.Timer _timer;
        static int _busy;

        public static void StartDaily()
        {
            _timer = new System.Windows.Forms.Timer { Interval = 60 * 60 * 1000 };
            _timer.Tick += (s, e) => MaybeCheck();
            _timer.Start();
            Task.Delay(90 * 1000).ContinueWith(_ => { try { Program.Ui.BeginInvoke(new Action(MaybeCheck)); } catch { } });
        }

        static void MaybeCheck()
        {
            var s = Settings.Current;
            if (!s.DictAutoUpdate) return;
            var r = Load();
            var last = string.IsNullOrEmpty(r.CheckedAt) ? s.DictCheckedAt : r.CheckedAt;
            if (DateTime.TryParse(last, out var t) && (DateTime.Now - t).TotalHours < 23) return;
            // failures are logged only; the user is not interrupted
            Check(false).ContinueWith(task => { if (task.IsFaulted) Log.Write("dict update: " + task.Exception.GetBaseException().Message); });
        }

        public class Result { public bool Updated; public string Tag; public string Message; }

        /// <summary>Checks GitHub and installs a newer dictionary. Throws on failure (after rolling back).</summary>
        public static Task<Result> Check(bool force, IProgress<string> status = null) => Task.Run(async () =>
        {
            if (Interlocked.Exchange(ref _busy, 1) == 1) return new Result { Message = "正在更新中，请稍候" };
            var rec = Load();
            try
            {
                status?.Report("正在检查词库更新…");
                var rel = await GitHubNet.GetJson(Api).ConfigureAwait(false);
                var asset = GitHubNet.Assets(rel).FirstOrDefault(a => a.Name == AssetName) ?? throw new Exception("发布里找不到 " + AssetName);
                var tag = (rel.TryGetValue("tag_name", out var tn) ? tn as string : "?") + "@" + asset.UpdatedAt;
                var now = DateTime.Now.ToString("o");
                rec.CheckedAt = now;
                Settings.Current.DictCheckedAt = now;
                var current = string.IsNullOrEmpty(rec.Tag) ? Settings.Current.DictTag : rec.Tag;
                if (!force && tag == current)
                {
                    Store(rec); SaveSettings();
                    return new Result { Tag = tag, Message = "词库已是最新" };
                }
                if (string.IsNullOrEmpty(asset.Sha256)) throw new Exception("发布没有提供 SHA-256 校验值，已跳过本次更新");

                var work = Path.Combine(Path.GetTempPath(), "yiwei-dict-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(work);
                try
                {
                    var zipPath = Path.Combine(work, AssetName);
                    var pct = status == null ? null : new Progress<double>(p => status.Report("正在下载词库… " + (int)(p * 100) + "%"));
                    await GitHubNet.Download(asset.Url, zipPath, asset.Size, pct, false).ConfigureAwait(false);
                    status?.Report("正在校验…");
                    if (GitHubNet.Sha256Of(zipPath) != asset.Sha256) throw new Exception("校验失败（SHA-256 不一致），未安装");
                    var staging = Path.Combine(work, "staging");
                    ExtractAndVerify(zipPath, staging);

                    if (!Differs(staging))
                    {
                        rec.Tag = tag; rec.Sha256 = asset.Sha256; Store(rec);
                        Settings.Current.DictTag = tag; SaveSettings();
                        return new Result { Tag = tag, Message = "词库内容没有变化，已是最新" };
                    }

                    status?.Report("正在备份当前词库…");
                    var backup = Backup(string.IsNullOrEmpty(current) ? "安装包附带" : current);
                    rec.Backups.Insert(0, backup);
                    try
                    {
                        status?.Report("正在替换词库…");
                        Replace(staging);
                        status?.Report("正在重新部署（编译词库可能需要一两分钟）…");
                        var started = DateTime.Now.AddSeconds(-2);
                        if (!GitHubNet.DeployAndWait() || !Rebuilt(started)) throw new Exception("新词库部署失败");
                    }
                    catch (Exception e)
                    {
                        Log.Write("dict update: install failed, rolling back: " + e.Message);
                        try { Restore(backup); GitHubNet.DeployAndWait(); } catch (Exception e2) { Log.Write("dict rollback: " + e2.Message); }
                        rec.Backups.Remove(backup);
                        try { Directory.Delete(backup.Dir, true); } catch { }
                        throw new Exception(e.Message + "，已恢复原来的词库");
                    }
                    rec.Tag = tag; rec.Sha256 = asset.Sha256; rec.InstalledAt = DateTime.Now.ToString("o");
                    rec.LastError = ""; rec.LastErrorAt = "";
                    Prune(rec);
                    Store(rec);
                    Settings.Current.DictTag = tag; SaveSettings();
                    Log.Write("dict update: installed " + tag);
                    return new Result { Updated = true, Tag = tag, Message = "词库已更新到 " + tag.Split('@')[0] + "（" + Short(asset.UpdatedAt) + "）" };
                }
                finally { try { Directory.Delete(work, true); } catch { } }
            }
            catch (Exception e)
            {
                rec.LastError = e.GetBaseException().Message; rec.LastErrorAt = DateTime.Now.ToString("o");
                Store(rec); SaveSettings();
                throw;
            }
            finally { Interlocked.Exchange(ref _busy, 0); }
        });

        /// <summary>Puts back the dictionaries from the most recent backup and redeploys.</summary>
        public static Task<string> Rollback() => Task.Run(() =>
        {
            if (Interlocked.Exchange(ref _busy, 1) == 1) return "正在更新中，请稍候";
            try
            {
                var rec = Load();
                var b = rec.Backups.FirstOrDefault() ?? throw new Exception("没有可以回滚的备份");
                Restore(b);
                GitHubNet.DeployAndWait();
                rec.Backups.RemoveAt(0);
                try { Directory.Delete(b.Dir, true); } catch { }
                rec.Tag = b.Tag == "安装包附带" ? "" : b.Tag;
                rec.Sha256 = ""; rec.InstalledAt = DateTime.Now.ToString("o");
                Store(rec);
                // stay on this version until the user checks again or the release changes
                Settings.Current.DictTag = rec.Tag; SaveSettings();
                Log.Write("dict rollback to " + b.Tag);
                return "已回滚到 " + (string.IsNullOrEmpty(rec.Tag) ? "安装包附带的词库" : rec.Tag.Split('@')[0] + "（" + Short(rec.Tag.Split('@').ElementAtOrDefault(1)) + "）");
            }
            finally { Interlocked.Exchange(ref _busy, 0); }
        });

        // ---------- steps ----------

        static void ExtractAndVerify(string zipPath, string staging)
        {
            Directory.CreateDirectory(staging);
            var root = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
            int files = 0;
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                foreach (var e in zip.Entries)
                {
                    var n = e.FullName.Replace('\\', '/');
                    if (n.EndsWith("/")) continue;
                    var top = n.Split('/')[0];
                    if (!Items.Contains(top)) continue;
                    var dest = Path.GetFullPath(Path.Combine(staging, n.Replace('/', Path.DirectorySeparatorChar)));
                    if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new Exception("压缩包里有不安全的路径：" + n);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    // reading every byte makes a damaged deflate stream throw here
                    using (var src = e.Open()) using (var dst = File.Create(dest)) src.CopyTo(dst);
                    if (new FileInfo(dest).Length != e.Length) throw new Exception("压缩包损坏：" + n);
                    files++;
                }
            }
            foreach (var k in KeyFiles)
            {
                var p = Path.Combine(staging, k.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(p) || new FileInfo(p).Length < 64) throw new Exception("新词库缺少关键文件 " + k + "，未安装");
                using (var r = new StreamReader(p))
                {
                    var head = new char[4096];
                    var len = r.Read(head, 0, head.Length);
                    var text = new string(head, 0, len);
                    if (!text.Contains("name:")) throw new Exception("新词库文件格式不对：" + k);
                }
            }
            if (files < KeyFiles.Length) throw new Exception("新词库文件不全");
        }

        /// <summary>True when the staged files differ from what is in use now (user folder, else shared data).</summary>
        static bool Differs(string staging)
        {
            foreach (var f in Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
            {
                var rel = f.Substring(staging.Length).TrimStart('\\', '/');
                var mine = Path.Combine(Paths.UserDir, rel);
                var shared = Path.Combine(Paths.SharedDataDir, rel);
                var cur = File.Exists(mine) ? mine : shared;
                if (!File.Exists(cur)) return true;
                var a = new FileInfo(f); var b = new FileInfo(cur);
                if (a.Length != b.Length || GitHubNet.Sha256Of(f) != GitHubNet.Sha256Of(cur)) return true;
            }
            return false;
        }

        static BackupInfo Backup(string tag)
        {
            var dir = Path.Combine(BackupRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(dir);
            var info = new BackupInfo { Dir = dir, Tag = tag, Time = DateTime.Now.ToString("o") };
            foreach (var item in Items)
            {
                var src = Path.Combine(Paths.UserDir, item);
                if (Directory.Exists(src)) { CopyDir(src, Path.Combine(dir, item)); info.Present.Add(item); }
                else if (File.Exists(src)) { File.Copy(src, Path.Combine(dir, item), true); info.Present.Add(item); }
            }
            Json.Save(Path.Combine(dir, "backup.json"), info);
            return info;
        }

        static void Replace(string staging)
        {
            foreach (var item in Items)
            {
                var src = Path.Combine(staging, item);
                var dst = Path.Combine(Paths.UserDir, item);
                if (Directory.Exists(src))
                {
                    if (Directory.Exists(dst)) Directory.Delete(dst, true);
                    CopyDir(src, dst);
                }
                else if (File.Exists(src)) File.Copy(src, dst, true);
            }
        }

        static void Restore(BackupInfo b)
        {
            foreach (var item in Items)
            {
                var dst = Path.Combine(Paths.UserDir, item);
                if (Directory.Exists(dst)) Directory.Delete(dst, true);
                else if (File.Exists(dst)) File.Delete(dst);
                if (!b.Present.Contains(item)) continue; // was not in the user folder: the shared copy takes over again
                var src = Path.Combine(b.Dir, item);
                if (Directory.Exists(src)) CopyDir(src, dst);
                else if (File.Exists(src)) File.Copy(src, dst, true);
            }
        }

        /// <summary>The deployer rewrote the compiled rime_ice dictionary after <paramref name="since"/>.</summary>
        static bool Rebuilt(DateTime since)
        {
            var build = Path.Combine(Paths.UserDir, "build");
            foreach (var name in new[] { "rime_ice.table.bin", "rime_ice.prism.bin" })
            {
                var p = Path.Combine(build, name);
                if (File.Exists(p) && File.GetLastWriteTime(p) >= since) return true;
            }
            Log.Write("dict update: build/rime_ice.table.bin was not rebuilt");
            return false;
        }

        static void Prune(Record rec)
        {
            // keep the three most recent backups
            foreach (var old in rec.Backups.Skip(3).ToList())
            {
                try { Directory.Delete(old.Dir, true); } catch { }
                rec.Backups.Remove(old);
            }
        }

        static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src)) File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(src)) CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        static void SaveSettings() { try { Settings.Current.Save(); } catch (Exception e) { Log.Write("save: " + e.Message); } }

        static string Short(string iso) => DateTime.TryParse(iso, out var t) ? t.ToLocalTime().ToString("yyyy-M-d") : (iso ?? "");
    }
}
