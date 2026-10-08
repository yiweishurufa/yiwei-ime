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
                _hook = new KeyHook
                {
                    PanelOpen = () => _snippets.IsOpen || _ai.WantsKeys,
                    PanelKey = (k, alt) => _snippets.IsOpen ? _snippets.HandleKey(k, alt) : _ai.HandleKey(k, alt),
                    ClosePanel = () => { _snippets.Close2(); _ai.Close2(); },
                    OnSnippets = c => { _ai.Close2(); _snippets.Open(c); },
                    OnAi = () => { _snippets.Close2(); _ai.Open(); },
                };
                _hook.Install();
                Tray.Setup();
                SystemTheme.Changed += dark =>
                {
                    // Pick the light / dark candidate scheme for the new mode.
                    if (Settings.Current.FollowSystemDark) Rime.ApplySoon(1500);
                };
                Stats.StartAppTracking();
                DictUpdater.StartDaily();
                FirstRun();
                StartPipeServer();
                ImeModeWatcher.Start(chinese => _toast.Flash(chinese));

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
                    if (y.Contains("由「一维输入法设置」生成") && !y.Contains("yiwei_qingbi")) Rime.ApplySoon(3000);
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
                if (command.StartsWith("toast:", StringComparison.OrdinalIgnoreCase))
                {
                    var m = command.Substring(6).Trim();
                    bool zh = m == "中" || m.Equals("zh", StringComparison.OrdinalIgnoreCase) || m.Equals("cn", StringComparison.OrdinalIgnoreCase);
                    _toast.Flash(zh);
                    return;
                }
                switch (command)
                {
                    case "/quit": Quit(); return;
                    case "/background": return;
                    case "/deploy": Rime.ApplySoon(100); return;
                    case "/wizard": ShowWizard(); return;
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
            menu.Items.Add("设置", null, (s, e) => Program.ShowSettings());
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
            _icon = new NotifyIcon { Icon = Program.AppIcon, Text = "一维输入法 · 长按 Alt+1 常用语，Alt+空格 AI", ContextMenuStrip = menu, Visible = true };
            _icon.DoubleClick += (s, e) => Program.ShowSettings();
        }

        static void UseTheme(string id)
        {
            var s = Settings.Current;
            s.ColorScheme = id;
            Rime.ApplySoon(100);
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
    }
}
