using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace Yiwei
{
    /// <summary>
    /// 按应用自动切中英：每个应用获得焦点时进入英文（ascii_mode），以及可选的 vim_mode
    /// （Esc / Ctrl+[ / Ctrl+C 回到英文）。写入 weasel.custom.yaml 的 app_options。
    ///
    /// librime 的补丁路径只用「/」分段，点号不需要转义，所以键写成 "app_options/code.exe"。
    /// Weasel 把应用名转成小写后再查表（CHANGELOG：app_options 中應用名大小寫不敏感），因此这里一律写小写。
    ///
    /// 英文开关沿用 Settings.AppAscii（没有记录时用内置推荐的默认值）；vim_mode 存在 yiwei/appmodes.json，
    /// 不往 Settings 里加字段。与「快捷键 → 不响应 Alt 手势的应用」（AltBlocklist）是两张互不相干的表。
    /// </summary>
    public static class AppModes
    {
        public sealed class App
        {
            public string Exe, Name, Group;
            public bool Vim;   // vim_mode 的默认值
            public bool Ascii = true;
        }

        // 游戏默认保持中文（游戏内聊天常打中文），用户可在应用规则页手动勾选英文
        static App A(string exe, string name, string group, bool vim = false) => new App { Exe = exe, Name = name, Group = group, Vim = vim, Ascii = group != "游戏" };

        /// <summary>内置推荐：默认英文。终端和编辑器同时默认开启 vim_mode。</summary>
        public static readonly App[] Catalog =
        {
            // 终端
            A("windowsterminal.exe", "Windows 终端", "终端", true),
            A("openconsole.exe", "Windows 终端（控制台宿主）", "终端", true),
            A("cmd.exe", "命令提示符", "终端", true),
            A("conhost.exe", "控制台窗口", "终端", true),
            A("powershell.exe", "Windows PowerShell", "终端", true),
            A("pwsh.exe", "PowerShell 7", "终端", true),
            A("powershell_ise.exe", "PowerShell ISE", "终端"),
            A("wsl.exe", "WSL", "终端", true),
            A("wslhost.exe", "WSL", "终端", true),
            A("bash.exe", "Bash", "终端", true),
            A("mintty.exe", "Git Bash / MSYS2", "终端", true),
            A("alacritty.exe", "Alacritty", "终端", true),
            A("wezterm-gui.exe", "WezTerm", "终端", true),
            A("tabby.exe", "Tabby", "终端", true),
            A("hyper.exe", "Hyper", "终端", true),
            A("mobaxterm.exe", "MobaXterm", "终端", true),
            A("putty.exe", "PuTTY", "终端", true),
            A("xshell.exe", "Xshell", "终端", true),
            A("termius.exe", "Termius", "终端", true),
            A("windterm.exe", "WindTerm", "终端", true),
            // 编辑器 / IDE
            A("code.exe", "Visual Studio Code", "编辑器", true),
            A("code - insiders.exe", "VS Code Insiders", "编辑器", true),
            A("vscodium.exe", "VSCodium", "编辑器", true),
            A("cursor.exe", "Cursor", "编辑器", true),
            A("windsurf.exe", "Windsurf", "编辑器", true),
            A("trae.exe", "Trae", "编辑器", true),
            A("zed.exe", "Zed", "编辑器", true),
            A("devenv.exe", "Visual Studio", "编辑器", true),
            A("idea64.exe", "IntelliJ IDEA", "编辑器", true),
            A("pycharm64.exe", "PyCharm", "编辑器", true),
            A("webstorm64.exe", "WebStorm", "编辑器", true),
            A("goland64.exe", "GoLand", "编辑器", true),
            A("clion64.exe", "CLion", "编辑器", true),
            A("rider64.exe", "Rider", "编辑器", true),
            A("phpstorm64.exe", "PhpStorm", "编辑器", true),
            A("rubymine64.exe", "RubyMine", "编辑器", true),
            A("datagrip64.exe", "DataGrip", "编辑器", true),
            A("rustrover64.exe", "RustRover", "编辑器", true),
            A("fleet.exe", "Fleet", "编辑器", true),
            A("studio64.exe", "Android Studio", "编辑器", true),
            A("sublime_text.exe", "Sublime Text", "编辑器", true),
            A("gvim.exe", "gVim", "编辑器", true),
            A("nvim-qt.exe", "Neovim Qt", "编辑器", true),
            A("neovide.exe", "Neovide", "编辑器", true),
            A("emacs.exe", "Emacs", "编辑器"),
            A("runemacs.exe", "Emacs", "编辑器"),
            // 工具
            A("everything.exe", "Everything", "工具"),
            A("powertoys.powerlauncher.exe", "PowerToys Run", "工具"),
            A("listary.exe", "Listary", "工具"),
            // 游戏与平台
            A("steam.exe", "Steam", "游戏"),
            A("epicgameslauncher.exe", "Epic Games", "游戏"),
            A("battle.net.exe", "Battle.net", "游戏"),
            A("riotclientservices.exe", "Riot Client", "游戏"),
            A("leagueclient.exe", "英雄联盟客户端", "游戏"),
            A("league of legends.exe", "英雄联盟", "游戏"),
            A("valorant.exe", "无畏契约", "游戏"),
            A("valorant-win64-shipping.exe", "无畏契约", "游戏"),
            A("cs2.exe", "Counter-Strike 2", "游戏"),
            A("dota2.exe", "Dota 2", "游戏"),
            A("pubg.exe", "PUBG", "游戏"),
            A("tslgame.exe", "PUBG", "游戏"),
            A("r5apex.exe", "Apex 英雄", "游戏"),
            A("overwatch.exe", "守望先锋", "游戏"),
            A("genshinimpact.exe", "原神", "游戏"),
            A("yuanshen.exe", "原神", "游戏"),
            A("starrail.exe", "崩坏：星穹铁道", "游戏"),
            A("zenlesszonezero.exe", "绝区零", "游戏"),
            A("minecraft.exe", "Minecraft", "游戏"),
            A("eldenring.exe", "艾尔登法环", "游戏"),
            A("gta5.exe", "GTA V", "游戏"),
            A("rdr2.exe", "荒野大镖客 2", "游戏"),
            A("cyberpunk2077.exe", "赛博朋克 2077", "游戏"),
            A("b1-win64-shipping.exe", "黑神话：悟空", "游戏"),
            A("naraka.exe", "永劫无间", "游戏"),
        };

        static readonly Dictionary<string, App> ByExe = Catalog.GroupBy(a => a.Exe).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        public static App Find(string exe) => exe != null && ByExe.TryGetValue(exe, out var a) ? a : null;

        public static string Norm(string exe)
        {
            var n = (exe ?? "").Trim().Trim('"').ToLowerInvariant();
            if (n.Length == 0) return "";
            n = Path.GetFileName(n);
            if (!n.EndsWith(".exe")) n += ".exe";
            return n;
        }

        // ---------- 存储（vim_mode） ----------

        public sealed class Store
        {
            public Dictionary<string, bool> Vim { get; set; } = new Dictionary<string, bool>();
        }

        static string StoreFile => Path.Combine(Paths.YiweiDir, "appmodes.json");
        static Store _store;
        static readonly object Gate = new object();

        public static Store Current
        {
            get
            {
                lock (Gate)
                {
                    if (_store == null)
                    {
                        try { _store = Json.Load<Store>(StoreFile); } catch { _store = new Store(); }
                        if (_store.Vim == null) _store.Vim = new Dictionary<string, bool>();
                    }
                    return _store;
                }
            }
        }

        public static void SaveStore() { lock (Gate) Json.Save(StoreFile, Current); }

        // ---------- 有效值 ----------

        public static bool IsAscii(Settings s, string exe)
        {
            exe = Norm(exe);
            if (s.AppAscii != null && s.AppAscii.TryGetValue(exe, out var v)) return v;
            var app = Find(exe);
            return app != null && app.Ascii;
        }

        public static void SetAscii(Settings s, string exe, bool on)
        {
            exe = Norm(exe); if (exe.Length == 0) return;
            if (s.AppAscii == null) s.AppAscii = new Dictionary<string, bool>();
            bool shippedOn = (Find(exe)?.Ascii ?? false) || exe == "conhost.exe" || exe == "cmd.exe"; // weasel.yaml 也默认 cmd/conhost 英文
            if (on) s.AppAscii[exe] = true;
            else if (shippedOn) s.AppAscii[exe] = false;   // 显式覆盖默认
            else s.AppAscii.Remove(exe);
        }

        public static bool IsVim(string exe)
        {
            exe = Norm(exe);
            if (Current.Vim.TryGetValue(exe, out var v)) return v;
            return Find(exe)?.Vim ?? false;
        }

        public static void SetVim(string exe, bool on)
        {
            exe = Norm(exe); if (exe.Length == 0) return;
            bool def = Find(exe)?.Vim ?? false;
            lock (Gate) { if (on == def) Current.Vim.Remove(exe); else Current.Vim[exe] = on; }
            SaveStore();
        }

        /// <summary>用户亲手添加过的（不在内置清单里的）应用。</summary>
        public static IEnumerable<string> CustomApps(Settings s) =>
            (s.AppAscii ?? new Dictionary<string, bool>()).Keys.Concat(Current.Vim.Keys)
                .Select(Norm).Where(x => x.Length > 0 && Find(x) == null).Distinct();

        // ---------- 写入 weasel.custom.yaml ----------

        /// <summary>由 WeaselConfig.Write 调用：输出每个应用的 app_options 补丁。</summary>
        public static void WritePatches(Settings s, Action<string, string> patch)
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var a in Catalog) names.Add(a.Exe);
            if (s.AppAscii != null) foreach (var k in s.AppAscii.Keys) names.Add(Norm(k));
            foreach (var k in Current.Vim.Keys) names.Add(Norm(k));
            foreach (var exe in names)
            {
                if (exe.Length == 0 || exe.IndexOfAny(new[] { '/', '"', '\\', '\n', '\r' }) >= 0) continue;
                bool ascii = IsAscii(s, exe), vim = IsVim(exe);
                bool explicitAscii = s.AppAscii != null && s.AppAscii.ContainsKey(exe);
                if (!ascii && !vim && !explicitAscii) continue;
                var parts = new List<string>();
                if (ascii || explicitAscii) parts.Add("ascii_mode: " + (ascii ? "true" : "false"));
                if (vim) parts.Add("vim_mode: true");
                patch("app_options/" + exe, "{" + string.Join(", ", parts) + "}");
            }
        }

        /// <summary>旧版生成的 weasel.custom.yaml 没有内置清单：启动时补写一次。</summary>
        public static void UpgradeIfNeeded()
        {
            try
            {
                var f = Paths.WeaselCustom;
                if (!File.Exists(f)) return;
                var y = File.ReadAllText(f, Encoding.UTF8);
                if (y.Contains("由「一维输入法设置」生成") && !y.Contains("vim_mode")) Rime.ApplyWhenIdle();
            }
            catch (Exception e) { Log.Write("appmodes upgrade: " + e.Message); }
        }

        // ---------- 扫描已安装 / 正在运行的程序 ----------

        /// <summary>exe（小写）→ 来源说明（「正在运行」「已安装」「系统自带」）。耗时，放到后台线程调用。</summary>
        public static Dictionary<string, string> Scan(bool includeAllRunning)
        {
            var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            void Mark(string exe, string how)
            {
                exe = Norm(exe);
                if (exe.Length == 0) return;
                if (!includeAllRunning && Find(exe) == null) return;
                if (!found.ContainsKey(exe) || how == "正在运行") found[exe] = how;
            }

            // 系统自带
            foreach (var e in new[] { "cmd.exe", "conhost.exe", "powershell.exe" }) found[e] = "系统自带";
            var sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (File.Exists(Path.Combine(sys, "wsl.exe"))) found["wsl.exe"] = "系统自带";
            var winApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps");
            if (File.Exists(Path.Combine(winApps, "wt.exe"))) { found["windowsterminal.exe"] = "已安装"; found["openconsole.exe"] = "已安装"; }
            if (File.Exists(Path.Combine(winApps, "pwsh.exe"))) found["pwsh.exe"] = "已安装";

            // 开始菜单快捷方式
            try
            {
                var dirs = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                }.Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d));
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = shellType != null ? Activator.CreateInstance(shellType) : null;
                try
                {
                    foreach (var d in dirs)
                        foreach (var lnk in SafeFiles(d, "*.lnk"))
                        {
                            try
                            {
                                string target = shell != null ? (string)shell.CreateShortcut(lnk).TargetPath : "";
                                if (!string.IsNullOrEmpty(target) && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) Mark(target, "已安装");
                            }
                            catch { }
                        }
                }
                finally { if (shell != null) try { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); } catch { } }
            }
            catch (Exception e) { Log.Write("scan start menu: " + e.Message); }

            // App Paths 注册表（安装程序常在这里登记主程序）
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
                try
                {
                    using (var k = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths"))
                        if (k != null) foreach (var n in k.GetSubKeyNames()) Mark(n, "已安装");
                }
                catch { }

            // Uninstall 注册表的 DisplayIcon（常指向主程序 exe）
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                    try
                    {
                        using (var b = RegistryKey.OpenBaseKey(hive, view))
                        using (var k = b.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                        {
                            if (k == null) continue;
                            foreach (var n in k.GetSubKeyNames())
                                try
                                {
                                    using (var sub = k.OpenSubKey(n))
                                    {
                                        var icon = (sub?.GetValue("DisplayIcon") as string ?? "").Split(',')[0].Trim('"', ' ');
                                        if (icon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) Mark(icon, "已安装");
                                    }
                                }
                                catch { }
                        }
                    }
                    catch { }

            // 正在运行
            foreach (var pr in Process.GetProcesses())
            {
                try
                {
                    var n = pr.ProcessName + ".exe";
                    if (Find(n) != null) Mark(n, "正在运行");
                    else if (includeAllRunning && pr.MainWindowHandle != IntPtr.Zero) Mark(n, "正在运行");
                }
                catch { }
                finally { pr.Dispose(); }
            }
            return found;
        }

        static IEnumerable<string> SafeFiles(string dir, string pattern)
        {
            var stack = new Stack<string>(); stack.Push(dir);
            int guard = 0;
            while (stack.Count > 0 && guard++ < 2000)
            {
                var d = stack.Pop();
                string[] files = new string[0], subs = new string[0];
                try { files = Directory.GetFiles(d, pattern); } catch { }
                try { subs = Directory.GetDirectories(d); } catch { }
                foreach (var f in files) yield return f;
                foreach (var s in subs) stack.Push(s);
            }
        }
    }
}
