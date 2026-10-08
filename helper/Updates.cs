using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Yiwei
{
    /// <summary>Daily 雾凇拼音 dictionary update: download, verify SHA-256, then install into the user folder and redeploy.</summary>
    public static class DictUpdater
    {
        const string Api = "https://api.github.com/repos/iDvel/rime-ice/releases/latest";
        static Timer _timer;

        public static void StartDaily()
        {
            _timer = new Timer { Interval = 60 * 60 * 1000 };
            _timer.Tick += (s, e) => MaybeCheck();
            _timer.Start();
            Task.Delay(90 * 1000).ContinueWith(_ => Program.Ui.BeginInvoke(new Action(MaybeCheck)));
        }

        static void MaybeCheck()
        {
            var s = Settings.Current;
            if (!s.DictAutoUpdate) return;
            if (DateTime.TryParse(s.DictCheckedAt, out var last) && (DateTime.Now - last).TotalHours < 23) return;
            Check(false).ContinueWith(t => { if (t.IsFaulted) Log.Write("dict update: " + t.Exception.GetBaseException().Message); });
        }

        public class Result { public bool Updated; public string Tag; public string Message; }

        public static Task<Result> Check(bool force) => Task.Run(() =>
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var s = Settings.Current;
            var wc = new WebClient { Encoding = Encoding.UTF8 };
            wc.Headers[HttpRequestHeader.UserAgent] = "YiweiIME/0.1";
            var rel = Json.Parse(wc.DownloadString(Api)) as Dictionary<string, object>;
            var assets = (rel["assets"] as object[]).Cast<Dictionary<string, object>>();
            var asset = assets.FirstOrDefault(a => (a["name"] as string) == "full.zip") ?? throw new Exception("找不到 full.zip");
            var updated = (asset.TryGetValue("updated_at", out var u) ? u as string : "") ?? "";
            var tag = (rel["tag_name"] as string) + "@" + updated;
            s.DictCheckedAt = DateTime.Now.ToString("o");
            if (!force && tag == s.DictTag) { s.Save(); return new Result { Tag = tag, Message = "词库已是最新" }; }

            var digest = asset.TryGetValue("digest", out var dg) ? (dg as string ?? "") : "";
            if (!digest.StartsWith("sha256:")) throw new Exception("发布没有提供 SHA-256 校验值，已跳过本次更新");
            var tmp = Path.Combine(Path.GetTempPath(), "yiwei-rime-ice-" + Guid.NewGuid().ToString("N") + ".zip");
            wc.DownloadFile(asset["browser_download_url"] as string, tmp);
            try
            {
                string hash;
                using (var sha = SHA256.Create()) using (var f = File.OpenRead(tmp))
                    hash = BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
                if (hash != digest.Substring(7).ToLowerInvariant()) throw new Exception("校验失败（SHA-256 不一致），未安装");

                var user = Paths.UserDir;
                using (var zip = ZipFile.OpenRead(tmp))
                {
                    foreach (var e in zip.Entries)
                    {
                        var n = e.FullName.Replace('\\', '/');
                        bool dict = n.StartsWith("cn_dicts/") || n.StartsWith("en_dicts/") || n == "rime_ice.dict.yaml" || n == "melt_eng.dict.yaml";
                        if (!dict || n.EndsWith("/") || n.Contains("..")) continue;
                        var dest = Path.Combine(user, n.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        e.ExtractToFile(dest, true);
                    }
                }
                s.DictTag = tag; s.Save();
                Deploy.Run(true);
                return new Result { Updated = true, Tag = tag, Message = "词库已更新到 " + rel["tag_name"] };
            }
            finally { try { File.Delete(tmp); } catch { } }
        });
    }

    /// <summary>yiwei-ime://theme?v=2&amp;d=&lt;base64url JSON&gt; links (same package format as AIME themes).</summary>
    public static class ThemeLinks
    {
        static readonly string[] ColorKeys =
        {
            "back_color", "border_color", "preedit_back_color", "text_color", "hilited_text_color", "hilited_back_color",
            "candidate_text_color", "comment_text_color", "label_color", "hilited_candidate_back_color",
            "hilited_candidate_text_color", "hilited_comment_text_color", "hilited_candidate_label_color",
        };

        public static void Handle(string url)
        {
            try
            {
                var uri = new Uri(url);
                if (!uri.Scheme.Equals("yiwei-ime", StringComparison.OrdinalIgnoreCase) || uri.Host != "theme")
                    throw new Exception("不认识的链接");
                var q = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var d = q["d"] ?? throw new Exception("链接缺少主题数据");
                var b64 = d.Replace('-', '+').Replace('_', '/');
                b64 += new string('=', (4 - b64.Length % 4) % 4);
                var bytes = Convert.FromBase64String(b64);
                if (bytes.Length > 4096) throw new Exception("主题数据过大");
                var pkg = Json.Parse(Encoding.UTF8.GetString(bytes)) as Dictionary<string, object> ?? throw new Exception("主题数据无效");
                var format = pkg.TryGetValue("format", out var f) ? f as string : "";
                if (format != "yiwei-theme" && format != "aime-theme") throw new Exception("不是主题包");
                var id = pkg["id"] as string ?? "";
                if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z][a-z0-9_]{1,31}$") || id == "yiwei_custom") throw new Exception("主题 id 不合法");
                var name = Clean(pkg.TryGetValue("name", out var n) ? n as string : id);
                var author = Clean(pkg.TryGetValue("author", out var a) ? a as string : "");
                var colors = pkg["colors"] as Dictionary<string, object> ?? throw new Exception("缺少颜色");
                var theme = new Dictionary<string, string> { ["name"] = name, ["author"] = author };
                foreach (var k in ColorKeys)
                {
                    if (!colors.TryGetValue(k, out var v)) { if (k == "preedit_back_color") continue; throw new Exception("缺少颜色 " + k); }
                    if (!WeaselConfig.TryColor(v as string, out var c)) throw new Exception("颜色格式不对：" + k);
                    theme[k] = "0x" + c.ToString("X8");
                }
                var exists = Settings.Current.ImportedThemes.ContainsKey(id) ? $"\n\n将替换已导入的「{id}」。" : "";
                var r = MessageBox.Show($"导入主题「{name}」{(author.Length > 0 ? "（作者：" + author + "）" : "")}？{exists}\n\n是：导入并启用　否：仅导入　取消：不导入",
                    "一维输入法 · 导入主题", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) return;
                var s = Settings.Current;
                s.ImportedThemes[id] = theme;
                if (r == DialogResult.Yes)
                {
                    bool darkBack = WeaselConfig.TryColor(theme["back_color"], out var back) && Luma(back) < 0.45;
                    if (darkBack) s.ColorSchemeDark = id; else s.ColorScheme = id;
                }
                s.Save();
                WeaselConfig.Write(s);
                Deploy.Run();
            }
            catch (Exception e)
            {
                MessageBox.Show("无法导入主题：" + e.Message, "一维输入法", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        static string Clean(string s)
        {
            s = new string((s ?? "").Where(ch => !char.IsControl(ch)).ToArray()).Trim();
            return s.Length > 40 ? s.Substring(0, 40) : s;
        }

        static double Luma(uint argb) => (0.2126 * ((argb >> 16) & 0xFF) + 0.7152 * ((argb >> 8) & 0xFF) + 0.0722 * (argb & 0xFF)) / 255.0;
    }

    /// <summary>Imports an existing 小狼毫 (Weasel) user folder: custom patches, schemas, user dictionaries.</summary>
    public static class WeaselImport
    {
        public static int Run()
        {
            var src = Paths.LegacyWeaselUserDir;
            var dst = Paths.UserDir;
            if (!Directory.Exists(src)) throw new Exception("没有找到小狼毫的用户文件夹：" + src);
            if (Path.GetFullPath(src).TrimEnd('\\').Equals(Path.GetFullPath(dst).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return 0;
            int n = 0;
            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                var rel = file.Substring(src.Length).TrimStart('\\', '/');
                var top = rel.Split('\\', '/')[0];
                if (top.Equals("build", StringComparison.OrdinalIgnoreCase) || top.Equals("yiwei", StringComparison.OrdinalIgnoreCase)) continue;
                if (rel.EndsWith(".userdb", StringComparison.OrdinalIgnoreCase) || rel.Contains(".userdb\\")) continue; // live LevelDB; use sync snapshots
                if (rel.Equals("installation.yaml", StringComparison.OrdinalIgnoreCase) || rel.Equals("user.yaml", StringComparison.OrdinalIgnoreCase)) continue;
                if (rel.Equals("weasel.custom.yaml", StringComparison.OrdinalIgnoreCase))
                {
                    // keep it beside ours instead of overwriting the generated file
                    var keep = Path.Combine(dst, "weasel.custom.yaml.imported");
                    File.Copy(file, keep, true); n++; continue;
                }
                var dest = Path.Combine(dst, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(file, dest, true); n++;
            }
            Deploy.Run();
            return n;
        }
    }
}
