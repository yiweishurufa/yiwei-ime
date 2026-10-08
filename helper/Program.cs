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
    static class Program
    {
        const string MutexName = @"Local\YiweiHelper.Instance";
        const string PipeName = "YiweiHelper.Commands";

        public static Control Ui;            // marshals work onto the UI thread
        public static Icon AppIcon;
        static NotifyIcon _tray;
        static QuickPanel _panel;
        static KeyHook _hook;
        static SettingsForm _settings;

        [STAThread]
        static void Main(string[] args)
        {
            var command = string.Join(" ", args).Trim();
            using (var mutex = new Mutex(true, MutexName, out bool first))
            {
                if (!first) { Send(command.Length == 0 ? "/settings" : command); return; }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => Log.Write("ui: " + e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write("fatal: " + e.ExceptionObject);

                using (var st = typeof(Program).Assembly.GetManifestResourceStream("yiwei.ico"))
                    AppIcon = st != null ? new Icon(st) : SystemIcons.Application;

                Ui = new Control(); Ui.CreateControl();
                _panel = new QuickPanel();
                _hook = new KeyHook
                {
                    PanelOpen = () => _panel.IsOpen,
                    PanelKey = k => _panel.HandleKey(k),
                    OnSnippets = c => _panel.OpenSnippets(c),
                    OnAi = () => _panel.OpenAi(),
                };
                _hook.Install();
                SetupTray();
                Stats.StartAppTracking();
                DictUpdater.StartDaily();
                FirstRun();
                StartPipeServer();

                if (command.Length > 0 && command != "/background") Ui.BeginInvoke(new Action(() => Execute(command)));
                Application.Run();
                _hook.Dispose();
                _tray.Visible = false;
            }
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
                    SnippetBook.Current.Save();
                }
            }
            catch (Exception e) { Log.Write("first run: " + e.Message); }
        }

        static void SetupTray()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("一维输入法设置", null, (s, e) => Execute("/settings"));
            menu.Items.Add("常用语…", null, (s, e) => Execute("/settings 常用语"));
            menu.Items.Add("AI 助手…", null, (s, e) => Execute("/settings AI 助手"));
            menu.Items.Add("输入统计", null, (s, e) => Execute("/settings 统计"));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("重新部署", null, (s, e) => Deploy.Run());
            menu.Items.Add("打开用户文件夹", null, (s, e) => System.Diagnostics.Process.Start(Paths.UserDir));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出一维助手", null, (s, e) => Application.Exit());
            _tray = new NotifyIcon { Icon = AppIcon, Text = "一维输入法 · 长按 Alt+1 常用语，Alt+空格 AI", ContextMenuStrip = menu, Visible = true };
            _tray.DoubleClick += (s, e) => Execute("/settings");
        }

        static void Execute(string command)
        {
            if (command.StartsWith("yiwei-ime://", StringComparison.OrdinalIgnoreCase) || command.StartsWith("\"yiwei-ime://", StringComparison.OrdinalIgnoreCase))
            {
                ThemeLinks.Handle(command.Trim('"'));
                return;
            }
            if (command == "/quit") { Application.Exit(); return; }
            if (command.StartsWith("/settings") || command.Length == 0)
            {
                var page = command.Length > 9 ? command.Substring(9).Trim() : null;
                if (_settings == null || _settings.IsDisposed)
                {
                    Settings.Reload();
                    _settings = new SettingsForm(page);
                    _settings.FormClosed += (s, e) => _settings = null;
                    _settings.Show();
                }
                else if (!string.IsNullOrEmpty(page)) { _settings.Close(); Execute(command); return; }
                if (_settings.WindowState == FormWindowState.Minimized) _settings.WindowState = FormWindowState.Normal;
                _settings.Activate();
            }
        }

        // ---------- single instance: later launches forward their command ----------

        static void Send(string command)
        {
            try
            {
                using (var c = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    c.Connect(2000);
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
}
