using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Yiwei
{
    /// <summary>
    /// 按住说话的悬浮胶囊（光标旁，点击穿透）：录音时显示音量条与时长，松开后显示「识别中」，然后上屏。
    /// 状态机：Idle → Recording →（松开）Recognizing → Idle；Esc 或按了别的键 → 取消。
    /// </summary>
    sealed class VoiceInput : FloatingWindow
    {
        public const int MaxSeconds = 60;
        static readonly Color Blue = Color.FromRgb(0x2B, 0x5B, 0xD7);

        readonly Border _pill;
        readonly TextBlock _label = new TextBlock { Foreground = Brushes.White, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 2, 0) };
        readonly Rectangle[] _bars = new Rectangle[5];
        readonly Ellipse _dot = new Ellipse { Width = 8, Height = 8, Fill = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
        readonly DispatcherTimer _tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
        readonly DispatcherTimer _hide = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };
        Recorder _rec;
        CancellationTokenSource _cts;
        IntPtr _target;

        public bool Recording => _rec != null;
        public bool Busy => _rec != null || _cts != null;

        public VoiceInput() : base(clickThrough: true)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(_dot);
            var meter = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            for (int i = 0; i < _bars.Length; i++)
            {
                _bars[i] = new Rectangle { Width = 3, Height = 4, RadiusX = 1.5, RadiusY = 1.5, Fill = Brushes.White, Margin = new Thickness(1.5, 0, 1.5, 0), VerticalAlignment = VerticalAlignment.Center };
                meter.Children.Add(_bars[i]);
            }
            row.Children.Add(meter);
            row.Children.Add(_label);
            _pill = new Border
            {
                Background = new SolidColorBrush(Blue), CornerRadius = new CornerRadius(16), Height = 32, Padding = new Thickness(14, 0, 14, 0),
                Child = row, Margin = new Thickness(8),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 1, Opacity = 0.28 },
            };
            Content = _pill;
            _tick.Tick += (s, e) => Animate();
            _hide.Tick += (s, e) => { _hide.Stop(); if (!Busy) Hide(); };
        }

        /// <summary>Starts recording (the voice key has been held long enough).</summary>
        public void Begin()
        {
            if (Busy) return;
            _hide.Stop();
            if (!VoiceModel.Installed)
            {
                Say("语音模型未下载：设置 → 语音", true);
                return;
            }
            _target = Native.GetForegroundWindow();
            try
            {
                _rec = new Recorder();
                _rec.Start();
            }
            catch (Exception e)
            {
                _rec = null;
                Log.Write("voice: " + e.Message);
                Say(e.Message.Length > 26 ? "麦克风不可用，详见设置 → 语音" : e.Message, true);
                Tray.Balloon("语音输入", e.Message);
                return;
            }
            _dot.Visibility = Visibility.Visible;
            foreach (var b in _bars) b.Visibility = Visibility.Visible;
            _label.Text = "正在听… 0:00";
            _pill.Background = new SolidColorBrush(Blue);
            ShowAtCaret();
            _tick.Start();
        }

        /// <summary>The voice key was released: recognise and type the result into the window that had focus.</summary>
        public void End()
        {
            if (_rec == null) return;
            _tick.Stop();
            var rec = _rec; _rec = null;
            var samples = rec.Stop();
            double peak = rec.Peak;
            if (samples.Length < Recorder.Rate / 3) { Hide(); return; } // a tap, not speech
            if (peak < 0.02) { Say("没有听到声音", true); return; }
            foreach (var b in _bars) b.Visibility = Visibility.Collapsed;
            _label.Text = "识别中…";
            var s = Settings.Current;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            var target = _target;
            Task.Run(() => VoiceModel.Recognize(samples, s.VoiceLanguage, ct)).ContinueWith(t =>
            {
                _cts?.Dispose(); _cts = null;
                if (ct.IsCancellationRequested) { Hide(); return; }
                if (t.IsFaulted)
                {
                    var msg = t.Exception.GetBaseException().Message;
                    Log.Write("voice recognize: " + msg);
                    Say("识别失败：" + msg, true);
                    return;
                }
                var text = VoiceModel.Tidy(t.Result, s.VoiceRemoveFillers, s.VoicePanguSpacing);
                if (text.Length == 0) { Say("没有识别到文字", true); return; }
                Hide();
                if (Native.GetForegroundWindow() != target)
                {
                    // Focus moved while recognising: do not type into the wrong window.
                    try { System.Windows.Forms.Clipboard.SetText(text); } catch { }
                    Tray.Balloon("语音输入", "窗口已切换，识别结果已复制到剪贴板：" + text);
                    return;
                }
                TextOut.Type(text);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>Esc, another key, or the hold was interrupted: drop the recording.</summary>
        public void Cancel()
        {
            _tick.Stop();
            if (_rec != null) { try { _rec.Dispose(); } catch { } _rec = null; }
            if (_cts != null) { try { _cts.Cancel(); } catch { } }
            Hide();
        }

        void Animate()
        {
            if (_rec == null) return;
            var sec = _rec.Seconds;
            if (sec >= MaxSeconds) { End(); return; }
            _label.Text = "正在听… " + ((int)sec / 60) + ":" + ((int)sec % 60).ToString("00") + "　Esc 取消";
            double level = _rec.Level;
            var rnd = new Random();
            for (int i = 0; i < _bars.Length; i++)
            {
                double shape = 1 - Math.Abs(i - 2) * 0.22;
                _bars[i].Height = 4 + 14 * Math.Min(1, level * shape * (0.75 + rnd.NextDouble() * 0.5));
            }
            _dot.Opacity = (Environment.TickCount / 500) % 2 == 0 ? 1 : 0.45;
        }

        void Say(string text, bool warn)
        {
            _tick.Stop();
            _dot.Visibility = Visibility.Collapsed;
            foreach (var b in _bars) b.Visibility = Visibility.Collapsed;
            _label.Text = text;
            _pill.Background = new SolidColorBrush(warn ? Color.FromRgb(0x3A, 0x3D, 0x44) : Blue);
            ShowAtCaret();
            _hide.Stop(); _hide.Start();
        }
    }
}
