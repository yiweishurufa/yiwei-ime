using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ui = Wpf.Ui.Controls;
using WF = System.Windows.Forms;

namespace Yiwei
{
    /// <summary>「工具」页：文字工具、翻译、补全、同步备份、快捷键自定义（工具组的设置都在 tools.json）。</summary>
    sealed partial class SettingsWindow
    {
        ToolSettings T => ToolSettings.Current;

        void SaveTools() { try { T.Save(); } catch (Exception e) { _toast.Show("保存失败：" + e.Message); } }

        static KeyValuePair<string, string> KS(string k, string v) => new KeyValuePair<string, string>(k, v);

        StackPanel PageTools()
        {
            var p = new StackPanel();
            p.Children.Add(K.PageTitle("工具"));
            p.Children.Add(K.Card("长按 Alt 工具手势", "长按 Alt + T 文字工具菜单（对选中文字：翻译、拼音、繁简、大小写、Markdown…）；长按 Alt + S 截图翻译；长按 Alt + M Markdown 符号",
                K.Toggle(T.AltGestures, v => { T.AltGestures = v; SaveTools(); }), Ui.SymbolRegular.Toolbox24));
            foreach (var build in ToolSections()) { try { foreach (var e in build()) p.Children.Add(e); } catch (Exception ex) { Log.Write("tools page: " + ex); } }
            return p;
        }

        /// <summary>Sections of the 工具 page, in order.</summary>
        IEnumerable<Func<IEnumerable<UIElement>>> ToolSections()
        {
            yield return OverviewSection;
            yield return TranslateSection;
            yield return SkinColorSection;
            yield return TextSection;
            yield return SearchSection;
            yield return ClipLinkSection;
            yield return CompletionSection;
            yield return WebDavSection;
            yield return BackupSection;
            yield return PerformanceSection;
            yield return HotkeysSection;
        }

        IEnumerable<UIElement> TranslateSection()
        {
            yield return K.Section("翻译");
            yield return K.Card("翻译引擎", "「自动」：设置了 AI 就用 AI，否则用免费的 MyMemory 在线接口（无需账号，每天约 5000 字；文字会发送到该服务）",
                K.Combo(new[] { KS("auto", "自动"), KS("ai", "AI（设置 → AI）"), KS("free", "免费接口 MyMemory") }, T.TranslateEngine ?? "auto", v => { T.TranslateEngine = v; SaveTools(); }), Ui.SymbolRegular.Translate24);
            yield return K.Card("翻译成", "截图翻译和翻译选中文字的目标语言",
                K.Combo(Translator.Targets, T.TranslateTarget ?? "auto", v => { T.TranslateTarget = v; SaveTools(); }, 260));
            yield return K.Card("截图翻译", "长按 Alt + S，或下面给它设一个快捷键：框选屏幕文字 → 离线识别 → 翻译，可上屏或复制",
                K.Btn("现在试试", () => { Hide(); Dispatcher.BeginInvoke(new Action(() => { TextOut.Pump(250); ScreenTranslate.Start(); Show(); })); }), Ui.SymbolRegular.ScreenshotRecord24);
        }

        IEnumerable<UIElement> TextSection()
        {
            yield return K.Section("选中文字");
            yield return K.Card("加注拼音样式", "长按 Alt + T →「加注拼音」：多音字按雾凇词库里的词来读；词库不带声调，所以拼音不带声调",
                K.Combo(PinyinAnnotate.Styles, T.PinyinStyle ?? "after", v => { T.PinyinStyle = v; SaveTools(); }, 260), Ui.SymbolRegular.TextFont24);
        }

        IEnumerable<UIElement> SearchSection()
        {
            yield return K.Section("搜索");
            yield return K.Card("sS 搜索前缀", "全拼里打 sS + 拼音（如 sSbeijing），选中的词不上屏，直接用浏览器搜索",
                K.Combo(WebSearch.Engines.Select(e => KS(e.Id, e.Name)), T.SearchEngine ?? "baidu", v => { T.SearchEngine = v; SaveTools(); }, 200), Ui.SymbolRegular.Search24);
        }

        IEnumerable<UIElement> ClipLinkSection()
        {
            yield return K.Section("剪贴板");
            yield return K.Card("Windows 剪贴板历史（Win+V）", "系统自带的剪贴板历史；一维自己的在长按 Alt+V（本机加密保存、排除密码管理器）",
                K.Toggle(WinClipboardLink.SystemHistoryEnabled, v => WinClipboardLink.SystemHistoryEnabled = v), Ui.SymbolRegular.ClipboardPaste24);
            var status = K.Text("", 12, true);
            var row = K.Row(K.Btn("现在合并", async () =>
            {
                status.Text = "正在读取…";
                try { var n = await WinClipboardLink.ImportAsync(); status.Text = n == 0 ? "没有新内容" : "并入了 " + n + " 条"; }
                catch (Exception ex) { status.Text = ex.Message; }
            }), status);
            status.VerticalAlignment = VerticalAlignment.Center;
            yield return K.Card("把 Win+V 的内容并入 Alt+V 面板", "开启后每 10 分钟尝试一次（Windows 只让前台窗口读剪贴板历史，后台可能读不到；点「现在合并」最可靠）",
                K.Toggle(T.MergeWinClipboard, v => { T.MergeWinClipboard = v; SaveTools(); }));
            yield return K.CardBorder(row);
        }

        IEnumerable<UIElement> CompletionSection()
        {
            yield return K.Section("补全");
            var box = new TextBox { Width = 380, Text = string.Join(" ", T.EmailSuffixes.Count > 0 ? T.EmailSuffixes : Completion.DefaultSuffixes.ToList()), ToolTip = "空格分隔；清空恢复默认" };
            box.LostFocus += (s, e) =>
            {
                var list = (box.Text ?? "").Split(new[] { ' ', ',', '，', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().TrimStart('@')).Where(x => x.Contains(".")).Distinct().ToList();
                T.EmailSuffixes = list.SequenceEqual(Completion.DefaultSuffixes) ? new List<string>() : list; SaveTools();
            };
            yield return K.Card("邮箱后缀", "打到「名字@」后长按 Alt + / 选后缀；用得多的自动排前面", box, Ui.SymbolRegular.Mail24);
            yield return K.Card("文件路径补全", "打到「C:\\Users\\」「%APPDATA%\\」「~\\」后长按 Alt + / 列出里面的文件夹和文件（隐藏文件不列）", null, Ui.SymbolRegular.Folder24);
        }

        IEnumerable<UIElement> WebDavSection()
        {
            yield return K.Section("WebDAV / 坚果云同步");
            var url = new TextBox { Width = 380, Text = T.DavUrl ?? "" };
            var user = new TextBox { Width = 380, Text = T.DavUser ?? "" };
            var pass = new PasswordBox { Width = 380 };
            if (!string.IsNullOrEmpty(T.DavPass)) pass.Password = "\u2022\u2022\u2022\u2022\u2022\u2022";
            var status = K.Text(T.DavLast ?? "", 12, true); status.TextWrapping = TextWrapping.Wrap;
            void Store()
            {
                T.DavUrl = (url.Text ?? "").Trim(); T.DavUser = (user.Text ?? "").Trim();
                if (pass.Password != "\u2022\u2022\u2022\u2022\u2022\u2022") T.DavPass = WebDavSync.Protect(pass.Password);
                SaveTools();
            }
            var form = new StackPanel();
            form.Children.Add(K.Text("WebDAV 地址", 12, true)); form.Children.Add(url);
            form.Children.Add(K.Text("账号", 12, true)); form.Children.Add(user);
            form.Children.Add(K.Text("密码（坚果云：账户信息 → 安全选项 → 添加应用，用生成的应用密码）", 12, true)); form.Children.Add(pass);
            var buttons = K.Row(
                K.Btn("用坚果云", () => { url.Text = WebDavSync.JianguoyunUrl; }),
                K.Btn("测试连接", async () => { Store(); status.Text = "正在连接…"; try { status.Text = await WebDavSync.TestAsync(); } catch (Exception ex) { status.Text = ex.GetBaseException().Message; } }),
                K.Btn("立即同步", async () => { Store(); status.Text = "正在同步…（会先整理本机词库，可能要几十秒）"; try { status.Text = await WebDavSync.SyncAsync(); } catch (Exception ex) { status.Text = "同步失败：" + ex.GetBaseException().Message; } }, Ui.ControlAppearance.Primary));
            buttons.Margin = new Thickness(0, 10, 0, 6);
            form.Children.Add(buttons);
            form.Children.Add(status);
            yield return K.Block("WebDAV 账号", "用户词库和常用语存到你自己的网盘 yiwei-ime 文件夹，多台电脑互相合并（和安卓同一种快照格式）", form, Ui.SymbolRegular.CloudSync24);
            yield return K.Card("自动同步", "开机 2 分钟后和之后每 6 小时同步一次", K.Toggle(T.DavAuto, v => { T.DavAuto = v; SaveTools(); }));
        }

        IEnumerable<UIElement> BackupSection()
        {
            yield return K.Section("自动备份");
            var status = K.Text("", 12, true);
            void Refresh() => status.Text = "备份到：" + AutoBackup.TargetDir + (string.IsNullOrEmpty(T.BackupLast) ? "　还没有备份过" : "　上次：" + T.BackupLast);
            Refresh();
            yield return K.Card("每天自动备份用户词库", "空闲时导出一份 .yiwei-backup（词库 + 常用语 + 设置），可在 词库 → 备份与恢复 里还原",
                K.Toggle(T.AutoBackup, v => { T.AutoBackup = v; SaveTools(); }), Ui.SymbolRegular.History24);
            var where = new List<KeyValuePair<string, string>> { KS("local", "文档\\一维输入法备份") };
            if (AutoBackup.OneDriveDir != null) where.Add(KS("onedrive", "OneDrive（随网盘上云）"));
            where.Add(KS("custom", "自定义文件夹…"));
            yield return K.Card("备份位置", AutoBackup.OneDriveDir == null ? "没有检测到 OneDrive" : "检测到 OneDrive：" + AutoBackup.OneDriveDir,
                K.Combo(where, T.BackupWhere ?? "local", v =>
                {
                    if (v == "custom")
                    {
                        using (var d = new WF.FolderBrowserDialog { Description = "选择自动备份文件夹" })
                            if (d.ShowDialog() == WF.DialogResult.OK) T.BackupDir = d.SelectedPath; else return;
                    }
                    T.BackupWhere = v; SaveTools(); Refresh();
                }, 260));
            yield return K.Card("保留份数", "更早的自动备份会被删掉（手动导出的不动）",
                K.Combo(new[] { 3, 5, 10, 30 }.Select(n => new KeyValuePair<int, string>(n, n + " 份")), T.BackupKeep, v => { T.BackupKeep = v; SaveTools(); }, 120));
            yield return K.CardBorder(K.Row(
                K.Btn("立即备份", async () => { status.Text = "正在备份…"; try { var f = await System.Threading.Tasks.Task.Run(() => AutoBackup.RunNow()); Refresh(); status.Text += "\n" + f; } catch (Exception ex) { status.Text = "备份失败：" + ex.Message; } }),
                K.Btn("打开文件夹", () => { System.IO.Directory.CreateDirectory(AutoBackup.TargetDir); System.Diagnostics.Process.Start("explorer.exe", "\"" + AutoBackup.TargetDir + "\""); }),
                status));
        }

        IEnumerable<UIElement> SkinColorSection()
        {
            yield return K.Section("取色皮肤");
            var status = K.Text(string.IsNullOrEmpty(T.ThemeRgb) ? "" : "当前取色 #" + T.ThemeRgb, 12, true);
            yield return K.Card("按壁纸生成皮肤", "取桌面壁纸里最鲜明的主色做候选高亮色，生成「我的配色」（读不到壁纸时用 Windows 主题色）",
                K.Row(K.Btn("现在取色", () => { try { status.Text = ThemeFromColor.FromWallpaper(); } catch (Exception ex) { status.Text = ex.Message; } }), status), Ui.SymbolRegular.Image24);
            yield return K.Card("屏幕取色生成皮肤", "在屏幕上任意位置点一下（带放大镜），用那个颜色生成「我的配色」",
                K.Btn("开始取色", () => { WindowState = WindowState.Minimized; Dispatcher.BeginInvoke(new Action(() => { TextOut.Pump(300); ScreenColorPicker.Start(rgb => { ThemeFromColor.Apply(rgb, "屏幕取色"); status.Text = "已用 #" + ThemeFromColor.Readable(rgb).ToString("X6") + " 生成「我的配色」"; WindowState = WindowState.Normal; Activate(); }); })); }), Ui.SymbolRegular.Eyedropper24);
            yield return K.Card("跟随壁纸自动换色", "换壁纸后自动重新取色（会切到「我的配色」）",
                K.Toggle(T.FollowWallpaper, v => { T.FollowWallpaper = v; SaveTools(); if (v) { try { status.Text = ThemeFromColor.FromWallpaper(); } catch (Exception ex) { status.Text = ex.Message; } } }));
        }

        IEnumerable<UIElement> OverviewSection()
        {
            yield return K.Section("快捷键总览");
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int row = 0;
            foreach (var r in ShortcutOverview.Build())
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var cells = new[] { K.Text(r.Group, 12, true), K.Text(r.Keys, 13, false, FontWeights.SemiBold), K.Text(r.What + (r.On ? "" : "（已关闭）"), 13, !r.On) };
                for (int c = 0; c < 3; c++) { cells[c].Margin = new Thickness(0, 3, 8, 3); Grid.SetRow(cells[c], row); Grid.SetColumn(cells[c], c); grid.Children.Add(cells[c]); }
                row++;
            }
            var body = new StackPanel();
            body.Children.Add(grid);
            var copy = K.Btn("复制为文字", () => { Clipboard.SetText(ShortcutOverview.AsText()); _toast.Show("已复制"); });
            copy.Margin = new Thickness(0, 10, 0, 0); copy.HorizontalAlignment = HorizontalAlignment.Left;
            body.Children.Add(copy);
            yield return K.Block("所有快捷键", "按现在的设置列出；长按 Alt + K 随时打开这里", body, Ui.SymbolRegular.KeyboardShift24);
        }

        IEnumerable<UIElement> PerformanceSection()
        {
            yield return K.Section("性能");
            var mem = K.Text("", 12, true);
            void Refresh() => mem.Text = "一维助手现在占用内存约 " + LowSpec.WorkingSetMb + " MB · 本机内存 " + LowSpec.TotalRamGb.ToString("0.#") + " GB、" + Environment.ProcessorCount + " 核";
            Refresh();
            yield return K.Card("低配机模式", "关掉长按 Alt+空格 AI、按住说话、跟随壁纸取色、WebDAV 自动同步、Win+V 合并，并定时回收内存（目标 < 100 MB）；关闭时恢复原来的设置",
                K.Toggle(T.LowSpec, v => { LowSpec.Set(v); Refresh(); }), Ui.SymbolRegular.Gauge24);
            yield return K.Card("开机时延迟启动后台任务", "开机自启时助手先低优先级运行，自动备份、同步、取色等后台任务推迟 90 秒，让开机更快",
                K.Toggle(T.DelayAtBoot, v => { T.DelayAtBoot = v; SaveTools(); }), Ui.SymbolRegular.Timer24);
            yield return K.Card("开机自动启动一维助手", "关掉后开机不启动助手（Windows「启动应用」里同一个开关），从语言栏菜单打开设置、手写等时才启动；在那之前长按 Alt 手势、语音、剪贴板历史不可用。打字本身不受影响",
                K.Toggle(StartupMode.AutoStart, v => StartupMode.AutoStart = v), Ui.SymbolRegular.Power24);
            yield return K.Card("更新通道", "测试版：每次构建都推送，新功能最快；稳定版：经过验证后才推送（稳定版发布前选它暂时收不到更新）",
                K.Combo(new[] { KS("beta", "测试版"), KS("stable", "稳定版") }, T.UpdateChannel ?? "beta", v => { T.UpdateChannel = v; SaveTools(); }, 160), Ui.SymbolRegular.ArrowSync24);
            yield return K.CardBorder(K.Row(K.Btn("现在回收内存", () => { LowSpec.Trim(); Refresh(); }), mem));
        }

        /// <summary>W47：每个工具动作都可以绑一个全局快捷键（RegisterHotKey，被别的程序占用时会提示）。</summary>
        IEnumerable<UIElement> HotkeysSection()
        {
            ToolsHub.EnsureRegistered();
            yield return K.Section("自定义快捷键");
            var list = new StackPanel();
            var status = K.Text("点输入框后直接按组合键（至少带 Ctrl / Alt / Shift / Win 之一）；Backspace 或 Delete 清除。", 12, true);
            void Fill()
            {
                list.Children.Clear();
                foreach (var a in ToolsHub.Actions)
                {
                    var id = a.Id;
                    var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var label = new StackPanel();
                    label.Children.Add(K.Text(a.Name + (a.AltKey != WF.Keys.None ? "　（长按 Alt + " + TextToolsMenu.KeyName(a.AltKey) + "）" : ""), 13));
                    if (!string.IsNullOrEmpty(a.Help)) label.Children.Add(K.Text(a.Help, 11, true));
                    g.Children.Add(label);
                    var box = new TextBox { Width = 170, IsReadOnly = true, VerticalContentAlignment = VerticalAlignment.Center, Text = T.Hotkeys.TryGetValue(id, out var cur) ? cur : "" };
                    box.PreviewKeyDown += (s, e) =>
                    {
                        e.Handled = true;
                        var key = e.Key == Key.System ? e.SystemKey : e.Key;
                        if (key == Key.Back || key == Key.Delete) { T.Hotkeys.Remove(id); box.Text = ""; SaveTools(); Apply(); return; }
                        if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin) return;
                        var m = Keyboard.Modifiers;
                        bool win = Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin);
                        if (m == ModifierKeys.None && !win) { status.Text = "要带 Ctrl、Alt、Shift 或 Win"; return; }
                        var wk = (WF.Keys)KeyInterop.VirtualKeyFromKey(key);
                        var text = ToolsHub.Format(wk, m.HasFlag(ModifierKeys.Control), m.HasFlag(ModifierKeys.Alt), m.HasFlag(ModifierKeys.Shift), win);
                        var clash = T.Hotkeys.FirstOrDefault(kv => kv.Key != id && string.Equals(kv.Value, text, StringComparison.OrdinalIgnoreCase));
                        if (clash.Key != null) { status.Text = text + " 已经给了「" + (ToolsHub.Find(clash.Key)?.Name ?? clash.Key) + "」"; return; }
                        T.Hotkeys[id] = text; box.Text = text; SaveTools(); Apply();
                    };
                    Grid.SetColumn(box, 1); g.Children.Add(box);
                    list.Children.Add(g);
                }
            }
            void Apply()
            {
                var failed = ToolsHub.ReapplyHotkeys();
                status.Text = failed.Count == 0 ? "已生效。" : "这些快捷键被别的程序占用了，换一个：" + string.Join("、", failed.Select(f => ToolsHub.Find(f)?.Name ?? f));
            }
            Fill();
            var body = new StackPanel();
            body.Children.Add(list);
            status.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(status);
            yield return K.Block("工具快捷键", "给下面每个功能单独设一个全局快捷键，按下就对选中的文字或屏幕执行", body, Ui.SymbolRegular.Keyboard24);
        }
    }
}
