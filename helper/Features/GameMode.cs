using System;
using System.IO;
using System.Linq;

namespace Yiwei
{
    /// <summary>
    /// 游戏模式：前台是全屏程序（独占全屏，或盖满整个屏幕且不是浏览器 / 播放器 / 演示文稿）时写标记 yiwei/game.now，
    /// yiwei_game.lua 看到标记就切到英文、让 Shift 直接交给游戏（不再切换中英），候选框也就不会冒出来；离开全屏后恢复中文。
    /// </summary>
    public static class GameMode
    {
        static readonly string[] NotGames =
        {
            "explorer", "chrome", "msedge", "firefox", "opera", "brave", "vivaldi", "360se", "360chrome", "qqbrowser", "sogouexplorer", "2345explorer",
            "potplayermini64", "potplayermini", "potplayer", "vlc", "mpc-hc64", "mpc-hc", "mpc-be64", "mpv", "wmplayer", "video.ui", "microsoft.media.player",
            "powerpnt", "wpp", "wps", "et", "winword", "excel", "acrord32", "acrobat", "sumatrapdf", "code", "devenv", "windowsterminal",
            "applicationframehost", "searchhost", "startmenuexperiencehost", "shellexperiencehost", "lockapp", "textinputhost", "yiweihelper",
            "mstsc", "vmconnect", "vmware", "virtualboxvm", "obs64", "bilibili", "douyin", "iqiyi", "qqlive", "youku",
        };

        static System.Windows.Forms.Timer _timer;
        static bool _on;

        public static string FlagFile => Path.Combine(Paths.YiweiDir, "game.now");
        public static bool Active => _on;

        public static void Start()
        {
            try { if (File.Exists(FlagFile)) File.Delete(FlagFile); } catch { }
            _timer = new System.Windows.Forms.Timer { Interval = 1000 };
            _timer.Tick += (s, e) => Check();
            _timer.Start();
        }

        static void Check()
        {
            bool game = false;
            var s = Settings.Current;
            if (s.GameMode)
            {
                var exe = Path.GetFileNameWithoutExtension(Native.ForegroundExe() ?? "").ToLowerInvariant();
                bool listed = s.GameApps.Any(a => string.Equals(Path.GetFileNameWithoutExtension(a), exe, StringComparison.OrdinalIgnoreCase));
                if (exe.Length > 0 && (listed || (!NotGames.Contains(exe) && (Native.ExclusiveFullscreen() || Native.ForegroundCoversMonitor()))))
                    game = true;
            }
            if (game == _on) return;
            _on = game;
            try
            {
                if (game) { Directory.CreateDirectory(Paths.YiweiDir); File.WriteAllText(FlagFile, "1"); Log.Write("game mode on"); }
                else if (File.Exists(FlagFile)) File.Delete(FlagFile);
            }
            catch (Exception e) { Log.Write("game flag: " + e.Message); }
        }
    }
}
