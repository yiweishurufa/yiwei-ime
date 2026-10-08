using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Yiwei
{
    /// <summary>
    /// 一维输入法程序自动更新。
    /// 读取仓库里的 update/appcast.xml（CI 每次发布自动生成），版本比当前助手版本新时：
    /// 托盘气泡提示 → 用户点「立即更新」→ 下载安装包、校验大小 → 退出助手并静默运行安装包（/S）。
    /// 安装包装完会自己重新启动服务和助手，用户设置、词库、常用语都在用户目录，不受影响。
    /// WinSparkle（YiweiServer 内置）的自动检查已关闭，避免弹两次。
    /// </summary>
    public static class AppUpdate
    {
        static readonly string[] Feeds =
        {
            "https://raw.githubusercontent.com/yiweishurufa/yiwei-ime/main/update/appcast.xml",
            "https://cdn.jsdelivr.net/gh/yiweishurufa/yiwei-ime@main/update/appcast.xml", // 国内镜像
        };

        public sealed class Info { public Version Version; public string Url; public long Length; }

        static System.Windows.Forms.Timer _timer;
        static bool _busy;
        static string _notified;
        static Info _pending;

        /// <summary>托盘气泡被点击时调用。</summary>
        public static void OnBalloonClicked()
        {
            var info = _pending; _pending = null;
            if (info != null) _ = InstallAsync(info, false);
        }

        public static Version Current
        {
            get { var v = typeof(AppUpdate).Assembly.GetName().Version; return new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision)); }
        }

        public static void StartDaily()
        {
            _timer = new System.Windows.Forms.Timer { Interval = 6 * 60 * 60 * 1000 };
            _timer.Tick += (s, e) => _ = CheckAsync(false);
            _timer.Start();
            Task.Delay(3 * 60 * 1000).ContinueWith(_ => { try { Program.Ui.BeginInvoke(new Action(() => { _ = CheckAsync(false); })); } catch { } });
        }

        /// <summary>Fetch the appcast; null when unreachable.</summary>
        public static Info Fetch()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            foreach (var feed in Feeds)
            {
                try
                {
                    using (var wc = new WebClient { Encoding = System.Text.Encoding.UTF8 })
                    {
                        wc.Headers[HttpRequestHeader.UserAgent] = "YiweiIME/" + Current;
                        var xml = wc.DownloadString(feed + "?t=" + DateTime.UtcNow.Ticks);
                        var m = Regex.Match(xml, "<enclosure[^>]*url=\"([^\"]+)\"[^>]*sparkle:version=\"([0-9.]+)\"[^>]*length=\"([0-9]+)\"");
                        if (!m.Success) continue;
                        if (!Version.TryParse(m.Groups[2].Value, out var v)) continue;
                        return new Info { Url = m.Groups[1].Value, Version = v, Length = long.Parse(m.Groups[3].Value) };
                    }
                }
                catch (Exception e) { Log.Write("app update feed " + feed + ": " + e.Message); }
            }
            return null;
        }

        /// <summary>manual = 用户点了「检查更新」：结果都要告诉用户；自动检查只在有新版本时提示一次。</summary>
        public static async Task CheckAsync(bool manual)
        {
            if (_busy) return;
            if (!manual && !Settings.Current.AppAutoUpdate) return;
            _busy = true;
            try
            {
                var info = await Task.Run(() => Fetch());
                if (info == null) { if (manual) Tray.Balloon("检查更新失败", "暂时连不上更新服务器，稍后再试。"); return; }
                if (info.Version <= Current) { if (manual) Tray.Balloon("已是最新版本", "一维输入法 " + Current); return; }
                if (!manual && _notified == info.Version.ToString()) return;
                _notified = info.Version.ToString();
                if (manual) { await InstallAsync(info, true); return; }
                // 自动检查：不弹窗打断，只在托盘冒一个气泡，点一下再更新
                _pending = info;
                Tray.Balloon("一维输入法有新版本 " + info.Version, "点这里更新（约半分钟，设置和词库都会保留）。也可以在托盘菜单里点「检查更新」。");
            }
            finally { _busy = false; }
        }

        static async Task InstallAsync(Info info, bool askFirst)
        {
            if (askFirst && !Dialogs.Confirm("发现新版本 " + info.Version + "（当前 " + Current + "）。现在更新吗？\n设置、词库和常用语都会保留。", "检查更新")) return;
            if (!info.Url.StartsWith("https://github.com/yiweishurufa/", StringComparison.OrdinalIgnoreCase)) { Log.Write("app update: unexpected url " + info.Url); return; }
            var file = Path.Combine(Path.GetTempPath(), "yiwei-ime-" + info.Version + "-installer.exe");
            Tray.Balloon("正在下载更新", "一维输入法 " + info.Version);
            try
            {
                await Task.Run(() =>
                {
                    if (File.Exists(file) && new FileInfo(file).Length == info.Length) return;
                    using (var wc = new WebClient())
                    {
                        wc.Headers[HttpRequestHeader.UserAgent] = "YiweiIME/" + Current;
                        wc.DownloadFile(info.Url, file + ".part");
                    }
                    if (new FileInfo(file + ".part").Length != info.Length) throw new IOException("下载的文件大小不对");
                    if (File.Exists(file)) File.Delete(file);
                    File.Move(file + ".part", file);
                });
            }
            catch (Exception e)
            {
                Log.Write("app update download: " + e.Message);
                Tray.Balloon("更新下载失败", e.GetBaseException().Message);
                return;
            }
            try
            {
                // 安装包需要管理员权限（会弹一次 UAC），/S 静默安装，装完会自己重启服务和助手。
                Process.Start(new ProcessStartInfo(file, "/S") { UseShellExecute = true, Verb = "runas" });
                Program.Quit();
            }
            catch (Exception e)
            {
                Log.Write("app update run: " + e.Message);
                Tray.Balloon("没有开始更新", "需要允许管理员权限才能安装更新。");
            }
        }
    }
}
