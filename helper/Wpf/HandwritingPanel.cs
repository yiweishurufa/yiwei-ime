using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Yiwei
{
    /// <summary>
    /// 鼠标手写面板：在框里写字，停笔 0.5 秒自动识别，点候选或按 1–9 上屏到当前窗口（面板不抢焦点）。
    /// 写不出来的字提示用 uU 拆字（雾凇「拆字反查」）。触发：长按 Alt + H、托盘「手写输入」、命令 /handwrite。
    /// </summary>
    sealed class HandwritingPanel : FloatingWindow
    {
        static readonly Color Blue = Color.FromRgb(0x2B, 0x5B, 0xD7);
        readonly Canvas _ink = new Canvas { Width = 360, Height = 220, Background = Brushes.Transparent, Cursor = Cursors.Pen, ClipToBounds = true };
        readonly WrapPanel _cands = new WrapPanel { Margin = new Thickness(0, 8, 0, 0), MinHeight = 34 };
        readonly TextBlock _hint = new TextBlock { FontSize = 11, Margin = new Thickness(2, 6, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 360 };
        readonly List<List<Point>> _strokes = new List<List<Point>>();
        readonly DispatcherTimer _idle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        List<Point> _cur;
        Polyline _line;
        List<string> _results = new List<string>();
        PanelPalette _p;
        int _gen;

        public bool IsOpen => IsVisible;

        public HandwritingPanel() : base(clickThrough: false)
        {
            _idle.Tick += async (s, e) =>
            {
                _idle.Stop();
                int gen = ++_gen;
                try
                {
                    var r = await Handwriting.Recognize(_strokes.Select(x => (IList<Point>)x).ToList());
                    if (gen != _gen) return;
                    _results = r; ShowCandidates();
                }
                catch (Exception ex) { _hint.Text = ex.Message; }
            };
            _ink.MouseLeftButtonDown += (s, e) =>
            {
                _ink.CaptureMouse();
                _idle.Stop();
                _cur = new List<Point> { e.GetPosition(_ink) };
                _line = new Polyline { Stroke = new SolidColorBrush(_p?.Text is SolidColorBrush b ? b.Color : Colors.Black), StrokeThickness = 4, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
                _line.Points.Add(_cur[0]);
                _ink.Children.Add(_line);
            };
            _ink.MouseMove += (s, e) =>
            {
                if (_cur == null || e.LeftButton != MouseButtonState.Pressed) return;
                var pt = e.GetPosition(_ink);
                var last = _cur[_cur.Count - 1];
                if (Math.Abs(pt.X - last.X) + Math.Abs(pt.Y - last.Y) < 1.5) return;
                _cur.Add(pt); _line.Points.Add(pt);
            };
            _ink.MouseLeftButtonUp += (s, e) =>
            {
                _ink.ReleaseMouseCapture();
                if (_cur == null) return;
                if (_cur.Count == 1) _cur.Add(new Point(_cur[0].X + 1, _cur[0].Y + 1)); // a dot
                _strokes.Add(_cur); _cur = null;
                _idle.Stop(); _idle.Start();
            };
            _ink.MouseRightButtonUp += (s, e) => Undo();
        }

        public void Open()
        {
            _p = PanelPalette.Now();
            if (!Handwriting.Ready(out var msg) && msg.Length > 0) { Tray.Balloon("手写输入", msg); return; }
            Build();
            _hint.Text = msg.Length > 0 ? msg : "写完停一下自动识别 · 1–9 / 点击上屏 · 右键退一笔 · Esc 关闭 · 不会写的字：打 uU 加部件拼音拆字";
            ShowAtCaret();
        }

        public void Close2() { Clear(); Hide(); }

        void Build()
        {
            var root = new StackPanel();
            var pad = new Border
            {
                Background = _p.Card, BorderBrush = _p.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                Child = new Grid { Children = { Guides(), _ink } },
            };
            root.Children.Add(pad);
            root.Children.Add(_cands);
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            bar.Children.Add(PanelParts.Chip(_p, "清空", false)); ((Border)bar.Children[0]).MouseLeftButtonUp += (s, e) => Clear();
            bar.Children.Add(PanelParts.Chip(_p, "退一笔", false)); ((Border)bar.Children[1]).MouseLeftButtonUp += (s, e) => Undo();
            bar.Children.Add(PanelParts.Chip(_p, "关闭", false)); ((Border)bar.Children[2]).MouseLeftButtonUp += (s, e) => Close2();
            root.Children.Add(bar);
            _hint.Foreground = _p.Dim;
            root.Children.Add(_hint);
            Content = PanelParts.Shell(_p, root);
            ShowCandidates();
        }

        /// <summary>米字格 guide lines.</summary>
        UIElement Guides()
        {
            var c = new Canvas { Width = _ink.Width, Height = _ink.Height, IsHitTestVisible = false };
            var brush = _p.Border;
            void L(double x1, double y1, double x2, double y2) => c.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 4 } });
            L(_ink.Width / 2, 0, _ink.Width / 2, _ink.Height);
            L(0, _ink.Height / 2, _ink.Width, _ink.Height / 2);
            return c;
        }

        void ShowCandidates()
        {
            _cands.Children.Clear();
            if (_results.Count == 0)
            {
                _cands.Children.Add(new TextBlock { Text = _strokes.Count == 0 ? "在上面写字" : "识别中…", Foreground = _p.Dim, FontSize = 13, Margin = new Thickness(4, 6, 0, 0) });
                return;
            }
            for (int i = 0; i < _results.Count; i++)
            {
                int idx = i;
                var t = new TextBlock { FontSize = 18 };
                t.Inlines.Add(new System.Windows.Documents.Run((i + 1) + " ") { FontSize = 11, Foreground = i == 0 ? _p.AccentText : _p.Accent });
                t.Inlines.Add(new System.Windows.Documents.Run(_results[i]) { Foreground = i == 0 ? _p.AccentText : _p.Text });
                var b = new Border { Child = t, Padding = new Thickness(10, 2, 10, 4), Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(8), Background = i == 0 ? _p.Accent : Brushes.Transparent, Cursor = Cursors.Hand };
                b.MouseLeftButtonUp += (s, e) => Commit(idx);
                _cands.Children.Add(b);
            }
        }

        void Commit(int i)
        {
            if (i < 0 || i >= _results.Count) return;
            var text = _results[i];
            Clear();
            Program.Ui.BeginInvoke(new Action(() => TextOut.Type(text))); // the panel never takes focus, so this lands in the app
        }

        void Undo()
        {
            if (_strokes.Count == 0) return;
            _strokes.RemoveAt(_strokes.Count - 1);
            if (_ink.Children.Count > 0) _ink.Children.RemoveAt(_ink.Children.Count - 1);
            _results.Clear();
            if (_strokes.Count > 0) { _idle.Stop(); _idle.Start(); }
            ShowCandidates();
        }

        void Clear()
        {
            _gen++; _idle.Stop();
            _strokes.Clear(); _ink.Children.Clear(); _results.Clear();
            if (_p != null) ShowCandidates();
        }

        /// <summary>Keys routed by the hook while the panel is open.</summary>
        public bool HandleKey(System.Windows.Forms.Keys k)
        {
            if (k == System.Windows.Forms.Keys.Escape) { Close2(); return true; }
            if (k >= System.Windows.Forms.Keys.D1 && k <= System.Windows.Forms.Keys.D9) { Commit(k - System.Windows.Forms.Keys.D1); return true; }
            if (k == System.Windows.Forms.Keys.Space || k == System.Windows.Forms.Keys.Enter) { if (_results.Count > 0) Commit(0); return true; }
            if (k == System.Windows.Forms.Keys.Back) { Undo(); return true; }
            return true; // the panel owns the keyboard while open (like the snippet panel); Esc closes it
        }
    }
}
