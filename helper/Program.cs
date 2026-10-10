using System;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Yiwei
{
    /// <summary>
    /// 一维助手: tray icon, Alt-hold panels, settings, first-run wizard and the command pipe.
    /// Command line: (none) or /settings [页面] · /wizard · /background · /deploy · /quit · toast:中 · toast:A · yiwei-ime://theme?…
    /// A second launch forwards its command to the running instance over the named pipe.
    /// </summary>
    static class Program
    {
        const string MutexName = @"Local\YiweiHelper.Instance";
        public const string PipeName = "YiweiHelper.Commands";

        public static Control Ui;            // marshals work onto the UI thread
        public static Icon AppIcon;
        static SnippetPanel _snippets;
        static AiPanel _ai;
        static ImeToast _toast;
        static VoiceInput _voice;
        static HandwritingPanel _hand;
        static DeployProgress _deployProgress;
        static KeyHook _hook;
        static SettingsWindow _settings;
        static WizardWindow _wizard;
        static System.Windows.Application _app;

        [STAThread]
        static void Main(string[] args)
        {
            var command = string.Join(" ", args).Trim();
            using (var mutex = new Mutex(true, MutexName, out bool first))
            {
                if (!first) { Send(command.Length == 0 ? "/settings" : command); return; }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write("fatal: " + e.ExceptionObject);

                _app = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
                _app.DispatcherUnhandledException += (s, e) => { Log.Write("ui: " + e.Exception); e.Handled = true; };
                // An invisible main window, so WPF-UI's theme manager never restyles the floating panels.
                _app.MainWindow = new System.Windows.Window { ShowInTaskbar = false, WindowStyle = System.Windows.WindowStyle.None, Width = 0, Height = 0 };

                using (var st = typeof(Program).Assembly.GetManifestResourceStream("yiwei.ico"))
                    AppIcon = st != null ? new Icon(st) : SystemIcons.Application;

                Ui = new Control(); Ui.CreateControl();
                SystemTheme.StartWatching();
                UiTheme.Init(_app);

                _snippets = new SnippetPanel();
                _ai = new AiPanel();
                _toast = new ImeToast();
                _voice = new VoiceInput();
                _hand = new HandwritingPanel();
                _deployProgress = new DeployProgress();
                _hook = new KeyHook
                {
                    PanelOpen = () => _snippets.IsOpen || _ai.WantsKeys || _hand.IsOpen,
                    PanelKey = (k, alt) => _snippets.IsOpen ? _snippets.HandleKey(k, alt) : _hand.IsOpen ? _hand.HandleKey(k) : _ai.HandleKey(k, alt),
                    ClosePanel = () => { _snippets.Close2(); _ai.Close2(); _hand.Close2(); },
                    OnSnippets = c => { _ai.Close2(); _snippets.Open(c); },
                    OnAi = () => { _snippets.Close2(); _ai.Open(); },
                    OnVoiceStart = () => { _snippets.Close2(); _ai.Close2(); _voice.Begin(); },
                    OnVoiceEnd = () => _voice.End(),
                    OnVoiceCancel = () => _voice.Cancel(),
                };
                _hook.Install();
                Tray.Setup();
                SystemTheme.Changed += dark =>
                {
                    // 小狼毫自己按 color_scheme / color_scheme_dark 切换候选框配色，不用重新部署。
                    // 只有旧版本写的文件（把深色配色写进了 color_scheme）才需要重写一次。
                    if (!WeaselConfig.UpToDateForDarkMode(Settings.Current)) Rime.ApplySoon(1500);
                };
                Stats.StartAppTracking();
                ClipHistory.Start();
                DictUpdater.StartDaily();
                AppUpdate.StartDaily();
                Sync.StartScheduler();
                AppModes.UpgradeIfNeeded();
                FirstRun();
                StartPipeServer();
                ImeModeWatcher.Start(chinese => { Tray.SetMode(chinese); _toast.Flash(chinese); });

                if (command.Length > 0 && command != "/background") Ui.BeginInvoke(new Action(() => Execute(command)));
                else if (!Settings.Current.FirstRunDone) Ui.BeginInvoke(new Action(() => Execute("/wizard")));

                _app.Run();
                _hook.Dispose();
                Tray.Hide();
            }
        }

        public static void Quit()
        {
            try { _hook?.Dispose(); } catch { }
            Tray.Hide();
            _app?.Shutdown();
        }

        static void FirstRun()
        {
            try
            {
                var s = Settings.Current;
                if (!File.Exists(Paths.SettingsFile))
                {
                    s.Save();
                    WeaselConfig.Write(s);
                    if (!File.Exists(Paths.DefaultCustom)) Rime.WriteDefaultCustom(s);
                    SnippetBook.Current.Save();
                }
                else if (File.Exists(Paths.WeaselCustom))
                {
                    // Upgrade from 0.1: add the brand colour schemes to the generated file.
                    var y = File.ReadAllText(Paths.WeaselCustom, Encoding.UTF8);
                    if (y.Contains("由「一维输入法设置」生成") && (!y.Contains("yiwei_qingbi") || !WeaselConfig.UpToDateForDarkMode(Settings.Current))) Rime.ApplyWhenIdle();
                }
            }
            catch (Exception e) { Log.Write("first run: " + e.Message); }
        }

        public static void Execute(string command)
        {
            command = (command ?? "").Trim();
            try
            {
                if (command.StartsWith("yiwei-ime://", StringComparison.OrdinalIgnoreCase) || command.StartsWith("\"yiwei-ime://", StringComparison.OrdinalIgnoreCase))
                {
                    ThemeLinks.Handle(command.Trim('"'));
                    return;
                }
                if (command == "caret:left")
                {
                    // yiwei_autopair.lua just committed a pair such as （）; put the caret between the halves.
                    TextOut.Pump(40);
                    TextOut.Left();
                    return;
                }
                if (command.StartsWith("regret:"))
                {
                    // yiwei_regret.lua: "regret:<字数>:<拼音>" → delete what was just committed and retype the pinyin.
                    var parts = command.Split(new[] { ':' }, 3);
                    if (parts.Length == 3 && int.TryParse(parts[1], out var n) && n > 0 && n <= 200) TextOut.Regret(n, parts[2]);
                    return;
                }
                if (command.StartsWith("toast:", StringComparison.OrdinalIgnoreCase))
                {
                    var m = command.Substring(6).Trim();
                    bool zh = m == "中" || m.Equals("zh", StringComparison.OrdinalIgnoreCase) || m.Equals("cn", StringComparison.OrdinalIgnoreCase);
                    Tray.SetMode(zh);
                    _toast.Flash(zh);
                    return;
                }
                switch (command)
                {
                    case "/quit": Quit(); return;
                    case "/background": return;
                    case "/deploy": Rime.ApplySoon(100); return;
                    case "/diagnostics": Diagnostics.ExportAndShow(); return;
                    case "/update": _ = AppUpdate.CheckAsync(true); return;
                    case "/wizard": ShowWizard(); return;
                    case "/share": ShowLanShare(); return;
                    case "/ocr": OcrCapture.Start(); return;
                    case "/handwrite": ShowHandwriting(); return;
                }
                if (command.StartsWith("/settings") || command.Length == 0)
                {
                    var page = command.Length > 9 ? command.Substring(9).Trim() : null;
                    ShowSettings(page);
                }
            }
            catch (Exception e) { Log.Write("execute " + command + ": " + e); }
        }

        public static void ShowSettings(string page = null)
        {
            if (_settings == null)
            {
                _settings = new SettingsWindow();
                _settings.Closed += (s, e) => _settings = null;
                _settings.Show();
            }
            if (!string.IsNullOrEmpty(page)) _settings.GoTo(page);
            if (_settings.WindowState == System.Windows.WindowState.Minimized) _settings.WindowState = System.Windows.WindowState.Normal;
            _settings.Activate();
        }

        public static void ShowHandwriting()
        {
            _snippets.Close2(); _ai.Close2();
            _hand.Open();
        }

        static LanShareWindow _lan;

        public static void ShowLanShare()
        {
            if (_lan == null)
            {
                _lan = new LanShareWindow();
                _lan.Closed += (s, e) => _lan = null;
                _lan.Show();
            }
            if (_lan.WindowState == System.Windows.WindowState.Minimized) _lan.WindowState = System.Windows.WindowState.Normal;
            _lan.Activate();
        }

        public static void ShowWizard()
        {
            if (_wizard == null)
            {
                _wizard = new WizardWindow();
                _wizard.Closed += (s, e) => _wizard = null;
                _wizard.Show();
            }
            if (_wizard.WindowState == System.Windows.WindowState.Minimized) _wizard.WindowState = System.Windows.WindowState.Normal;
            _wizard.Activate();
        }

        // ---------- single instance: later launches forward their command ----------

        static void Send(string command)
        {
            try
            {
                using (var c = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    c.Connect(3000);
                    var b = Encoding.UTF8.GetBytes(command);
                    c.Write(b, 0, b.Length);
                }
            }
            catch (Exception e) { Log.Write("send: " + e.Message); }
        }

        static void StartPipeServer()
        {
            var t = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using (var s = new NamedPipeServerStream(PipeName, PipeDirection.In, 1))
                        {
                            s.WaitForConnection();
                            using (var r = new StreamReader(s, Encoding.UTF8))
                            {
                                var cmd = r.ReadToEnd().Trim();
                                if (cmd.Length > 4096) cmd = cmd.Substring(0, 4096);
                                Ui.BeginInvoke(new Action(() => Execute(cmd)));
                            }
                        }
                    }
                    catch (Exception e) { Log.Write("pipe: " + e.Message); Thread.Sleep(1000); }
                }
            }) { IsBackground = true };
            t.Start();
        }
    }

    /// <summary>Tray icon: 切换方案 ▸ · 换主题 ▸ · 重新部署 · 设置 · 首次引导 · 退出.</summary>
    static class Tray
    {
        static NotifyIcon _icon;

        public static void Setup()
        {
            var menu = new ContextMenuStrip();
            var schemas = new ToolStripMenuItem("切换方案");
            var themes = new ToolStripMenuItem("换主题");
            schemas.DropDownItems.Add("…"); themes.DropDownItems.Add("…");
            menu.Items.Add(schemas);
            menu.Items.Add(themes);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("重新部署", null, (s, e) => Rime.ApplySoon(50));
            menu.Items.Add("同步用户数据", null, (s, e) => System.Threading.Tasks.Task.Run(() => Deploy.Run("/sync", false)));
            menu.Items.Add("手写输入", null, (s, e) => Program.ShowHandwriting());
            menu.Items.Add("截图识字", null, (s, e) => OcrCapture.Start());
            menu.Items.Add("局域网互传", null, (s, e) => Program.ShowLanShare());
            menu.Items.Add("用户文件夹", null, (s, e) => { try { System.Diagnostics.Process.Start("explorer.exe", "\"" + Paths.UserDir + "\""); } catch { } });
            menu.Items.Add("设置", null, (s, e) => Program.ShowSettings());
            menu.Items.Add("检查更新", null, (s, e) => { _ = AppUpdate.CheckAsync(true); });
            menu.Items.Add("导出诊断信息", null, (s, e) => Diagnostics.ExportAndShow());
            menu.Items.Add("首次引导", null, (s, e) => Program.ShowWizard());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) => Program.Quit());
            menu.Opening += (s, e) =>
            {
                var st = Settings.Current;
                schemas.DropDownItems.Clear();
                foreach (var sc in Rime.Schemas)
                {
                    var id = sc.Id;
                    schemas.DropDownItems.Add(new ToolStripMenuItem(sc.Name, null, (a, b) => Rime.UseSchema(id)) { Checked = st.Schema == id });
                }
                themes.DropDownItems.Clear();
                foreach (var b in Brand.Palette)
                {
                    var id = Brand.SchemeId(b.Id, false);
                    themes.DropDownItems.Add(new ToolStripMenuItem("一维 · " + b.Name, Swatch(b.Rgb), (x, y) => UseTheme(id)) { Checked = st.ColorScheme == id });
                }
                themes.DropDownItems.Add(new ToolStripSeparator());
                foreach (var kv in WeaselConfig.BuiltinSchemes().Where(kv => Brand.FromScheme(kv.Key) == null))
                {
                    var id = kv.Key;
                    themes.DropDownItems.Add(new ToolStripMenuItem(kv.Value, null, (x, y) => UseTheme(id)) { Checked = st.ColorScheme == id });
                }
            };
            _icon = new NotifyIcon { Icon = ModeIcon(true), Text = "一维输入法 · 中文", ContextMenuStrip = menu, Visible = true };
            _icon.DoubleClick += (s, e) => Program.ShowSettings();
            _icon.BalloonTipClicked += (s, e) => AppUpdate.OnBalloonClicked();
        }

        static bool? _zh;
        static IntPtr _hicon;

        /// <summary>
        /// The one tray icon for the whole IME (like 搜狗 / 微信输入法): a brand-colour tile
        /// showing 中 or 英. Weasel's own tray icon is turned off (style/display_tray_icon: false).
        /// </summary>
        public static void SetMode(bool chinese)
        {
            if (_icon == null || _zh == chinese) return;
            try
            {
                _icon.Icon = ModeIcon(chinese);
                _icon.Text = chinese ? "一维输入法 · 中文" : "一维输入法 · 英文";
            }
            catch (Exception e) { Log.Write("tray icon: " + e.Message); }
        }

        /// <summary>Repaint after a theme change so the tile follows the accent colour.</summary>
        public static void Repaint() { var z = _zh ?? true; _zh = null; SetMode(z); }

        static Icon ModeIcon(bool chinese)
        {
            _zh = chinese;
            int size = Math.Max(16, System.Windows.Forms.SystemInformation.SmallIconSize.Width * 2);
            // 托盘状态（与安卓版标志同一枚蓝）：中 = 蓝底白字；英 = 白底蓝框蓝字。
            var blue = Color.FromArgb(0x2B, 0x5B, 0xD7);
            using (var bmp = new Bitmap(size, size))
            {
                using (var g = Graphics.FromImage(bmp))
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    float stroke = chinese ? 0 : Math.Max(1.5f, size * 0.08f);
                    float inset = stroke / 2, w = size - 1 - stroke, r = size * 0.44f;
                    path.AddArc(inset, inset, r, r, 180, 90); path.AddArc(inset + w - r, inset, r, r, 270, 90);
                    path.AddArc(inset + w - r, inset + w - r, r, r, 0, 90); path.AddArc(inset, inset + w - r, r, r, 90, 90);
                    path.CloseFigure();
                    using (var fill = new SolidBrush(chinese ? blue : Color.White)) g.FillPath(fill, path);
                    if (!chinese) using (var pen = new Pen(blue, stroke)) g.DrawPath(pen, path);
                    using (var font = new Font("Microsoft YaHei UI", size * 0.58f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    using (var ink = new SolidBrush(chinese ? Color.White : blue))
                        g.DrawString(chinese ? "中" : "英", font, ink, new RectangleF(0, -size * 0.03f, size, size), sf);
                }
                var h = bmp.GetHicon();
                var icon = (Icon)Icon.FromHandle(h).Clone();
                if (_hicon != IntPtr.Zero) DestroyIcon(_hicon);
                _hicon = h;
                return icon;
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr h);

        static void UseTheme(string id)
        {
            var s = Settings.Current;
            s.ColorScheme = id;
            var b = Brand.FromScheme(id);
            if (b != null) s.Accent = b.Id;
            Rime.ApplySoon(100);
            Repaint();
        }

        static Bitmap Swatch(uint rgb)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            using (var br = new SolidBrush(Color.FromArgb((int)(0xFF000000 | rgb))))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.FillEllipse(br, 2, 2, 12, 12);
            }
            return bmp;
        }

        public static void Hide() { if (_icon != null) _icon.Visible = false; }

        public static void Balloon(string title, string text)
        {
            try { _icon?.ShowBalloonTip(4000, title, text, ToolTipIcon.Info); } catch { }
        }
    }
}
