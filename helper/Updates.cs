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
    // 词库自动更新已移到 Features/DictUpdate.cs（DictUpdater）。

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
