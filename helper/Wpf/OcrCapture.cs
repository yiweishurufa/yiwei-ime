using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Yiwei
{
    /// <summary>
    /// 截图识字：冻结整个屏幕，拖出一个框，用 Windows 离线 OCR 识别，结果可以直接上屏到原来的窗口或复制。
    /// 触发：长按 Alt + O、托盘「截图识字」、命令 /ocr。Esc 或右键取消。
    /// </summary>
    sealed class OcrCapture : Form
    {
        static readonly Color Blue = Color.FromArgb(0x2B, 0x5B, 0xD7);
        static OcrCapture _open;

        readonly Bitmap _shot;
        readonly Rectangle _virtual;
        readonly IntPtr _target;
        Point _start;
        Rectangle _sel;
        bool _dragging;

        public static void Start()
        {
            if (_open != null) { _open.Activate(); return; }
            try
            {
                if (!Ocr.Supported) { Tray.Balloon("截图识字", "这台电脑没有可用的 OCR 识别器：请在 Windows 设置 → 时间和语言 → 语言 → 中文 → 语言选项 里安装「光学字符识别」"); return; }
                _open = new OcrCapture();
                _open.FormClosed += (s, e) => { _open.Dispose2(); _open = null; };
                _open.Show();
                _open.Activate();
            }
            catch (Exception e) { Log.Write("ocr: " + e); Tray.Balloon("截图识字", e.Message); _open = null; }
        }

        OcrCapture()
        {
            _target = Native.GetForegroundWindow();
            _virtual = SystemInformation.VirtualScreen;
            _shot = new Bitmap(_virtual.Width, _virtual.Height);
            using (var g = Graphics.FromImage(_shot)) g.CopyFromScreen(_virtual.Location, Point.Empty, _virtual.Size);
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None;
            Bounds = _virtual; TopMost = true; ShowInTaskbar = false; KeyPreview = true; Cursor = Cursors.Cross;
            DoubleBuffered = true; Text = "截图识字";
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right) { Close(); return; }
                _dragging = true; _start = e.Location; _sel = new Rectangle(e.Location, Size.Empty); Invalidate();
            };
            MouseMove += (s, e) =>
            {
                if (!_dragging) return;
                _sel = Rectangle.FromLTRB(Math.Min(_start.X, e.X), Math.Min(_start.Y, e.Y), Math.Max(_start.X, e.X), Math.Max(_start.Y, e.Y));
                Invalidate();
            };
            MouseUp += async (s, e) =>
            {
                if (!_dragging || e.Button != MouseButtons.Left) return;
                _dragging = false;
                if (_sel.Width < 6 || _sel.Height < 6) { _sel = Rectangle.Empty; Invalidate(); return; }
                await Recognize();
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImageUnscaled(_shot, 0, 0);
            using (var dim = new SolidBrush(Color.FromArgb(110, 0, 0, 0)))
            using (var region = new Region(ClientRectangle))
            {
                if (!_sel.IsEmpty) region.Exclude(_sel);
                g.FillRegion(dim, region);
            }
            if (!_sel.IsEmpty)
                using (var pen = new Pen(Blue, 2)) g.DrawRectangle(pen, _sel);
            var tip = _sel.IsEmpty ? "拖动框选要识别的文字 · Esc 取消" : _sel.Width + " × " + _sel.Height;
            using (var f = new Font("Microsoft YaHei UI", 10f))
            {
                var size = g.MeasureString(tip, f);
                var at = _sel.IsEmpty ? new PointF(Cursor.Position.X - _virtual.X + 16, Cursor.Position.Y - _virtual.Y + 16) : new PointF(_sel.X, Math.Max(0, _sel.Y - size.Height - 8));
                var box = new RectangleF(at.X, at.Y, size.Width + 16, size.Height + 6);
                using (var bg = new SolidBrush(Blue)) using (var path = Pill(box)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.FillPath(bg, path); }
                g.DrawString(tip, f, Brushes.White, at.X + 8, at.Y + 3);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (_sel.IsEmpty) Invalidate(); }

        static GraphicsPath Pill(RectangleF r)
        {
            var p = new GraphicsPath(); float d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 90, 180); p.AddArc(r.Right - d, r.Y, d, d, 270, 180); p.CloseFigure();
            return p;
        }

        async Task Recognize()
        {
            string text = null, error = null;
            Cursor = Cursors.WaitCursor;
            try
            {
                using (var crop = _shot.Clone(_sel, _shot.PixelFormat)) text = await Ocr.Recognize(crop);
            }
            catch (Exception e) { error = e.GetBaseException().Message; Log.Write("ocr: " + e); }
            var target = _target;
            var where = PointToScreen(new Point(_sel.Left, _sel.Bottom + 8));
            Close();
            OcrResult.Show(text, error, target, where);
        }

        void Dispose2() { try { _shot.Dispose(); } catch { } }
    }

    /// <summary>识别结果：可编辑文本 + 上屏 / 复制。</summary>
    sealed class OcrResult : Form
    {
        public static void Show(string text, string error, IntPtr target, Point where)
        {
            var f = new OcrResult(text, error, target);
            var wa = Screen.FromPoint(where).WorkingArea;
            f.Location = new Point(Math.Max(wa.Left, Math.Min(where.X, wa.Right - f.Width)), Math.Max(wa.Top, Math.Min(where.Y, wa.Bottom - f.Height)));
            f.Show();
            f.Activate();
        }

        OcrResult(string text, string error, IntPtr target)
        {
            Text = "截图识字 · 一维输入法"; Icon = Program.AppIcon; StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.SizableToolWindow; TopMost = true; ShowInTaskbar = false; KeyPreview = true;
            AutoScaleMode = AutoScaleMode.Dpi; Font = new Font("Microsoft YaHei UI", 9.5f);
            ClientSize = new Size(440, 240); BackColor = Color.White;
            var box = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("Microsoft YaHei UI", 11f), Text = error == null ? text ?? "" : "", WordWrap = true };
            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 6, 8, 6), BackColor = Color.FromArgb(0xF4, 0xF6, 0xFB) };
            var type = Btn("上屏", true); var copy = Btn("复制", false); var close = Btn("关闭", false);
            bar.Controls.Add(type); bar.Controls.Add(copy); bar.Controls.Add(close);
            var info = new Label { Dock = DockStyle.Top, Height = 28, Padding = new Padding(10, 6, 10, 0), ForeColor = Color.FromArgb(0x5B, 0x64, 0x78),
                Text = error != null ? "识别失败：" + error : string.IsNullOrWhiteSpace(text) ? "没有识别到文字，换个区域再试" : "识别结果可以先改一改，再上屏或复制（Ctrl+Enter 上屏）" };
            var pad = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 4) };
            pad.Controls.Add(box);
            Controls.Add(pad); Controls.Add(info); Controls.Add(bar);
            type.Enabled = copy.Enabled = error == null && !string.IsNullOrWhiteSpace(text);
            type.Click += (s, e) =>
            {
                var t = box.Text; Close();
                if (target != IntPtr.Zero && Native.IsWindow(target)) Native.SetForegroundWindow(target);
                TextOut.Pump(150);
                TextOut.Type(t);
            };
            copy.Click += (s, e) => { try { Clipboard.SetText(box.Text); } catch { } Close(); };
            close.Click += (s, e) => Close();
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) Close();
                if (e.KeyCode == Keys.Enter && e.Control && type.Enabled) { e.SuppressKeyPress = true; type.PerformClick(); }
            };
            Shown += (s, e) => { box.SelectionStart = box.TextLength; box.Focus(); };
        }

        static Button Btn(string text, bool primary)
        {
            var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(76, 32), FlatStyle = FlatStyle.Flat, Margin = new Padding(6, 0, 0, 0), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderColor = Color.FromArgb(0x2B, 0x5B, 0xD7);
            b.BackColor = primary ? Color.FromArgb(0x2B, 0x5B, 0xD7) : Color.White;
            b.ForeColor = primary ? Color.White : Color.FromArgb(0x2B, 0x5B, 0xD7);
            return b;
        }
    }
}
