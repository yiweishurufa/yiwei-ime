using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Yiwei
{
    /// <summary>
    /// W23 屏幕取色：冻结屏幕，鼠标处显示放大镜和色值，单击取色 → 生成「我的配色」；Esc / 右键取消。
    /// </summary>
    sealed class ScreenColorPicker : Form
    {
        static ScreenColorPicker _open;
        readonly Bitmap _shot;
        readonly Rectangle _virtual;
        readonly Action<uint> _done;

        public static void Start(Action<uint> done = null)
        {
            if (_open != null) { _open.Activate(); return; }
            _open = new ScreenColorPicker(done ?? (rgb => { ThemeFromColor.Apply(rgb, "屏幕取色"); Tray.Balloon("屏幕取色", "已用 #" + ThemeFromColor.Readable(rgb).ToString("X6") + " 生成「我的配色」"); }));
            _open.FormClosed += (s, e) => { _open._shot.Dispose(); _open = null; };
            _open.Show(); _open.Activate();
        }

        ScreenColorPicker(Action<uint> done)
        {
            _done = done;
            _virtual = SystemInformation.VirtualScreen;
            _shot = new Bitmap(_virtual.Width, _virtual.Height);
            using (var g = Graphics.FromImage(_shot)) g.CopyFromScreen(_virtual.Location, Point.Empty, _virtual.Size);
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None;
            Bounds = _virtual; TopMost = true; ShowInTaskbar = false; KeyPreview = true; Cursor = Cursors.Cross; DoubleBuffered = true; Text = "屏幕取色";
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            MouseMove += (s, e) => Invalidate();
            MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) { Close(); return; }
                var c = _shot.GetPixel(Math.Min(_shot.Width - 1, e.X), Math.Min(_shot.Height - 1, e.Y));
                Close();
                _done((uint)(c.ToArgb() & 0xFFFFFF));
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImageUnscaled(_shot, 0, 0);
            var m = PointToClient(Cursor.Position);
            int x = Math.Max(0, Math.Min(_shot.Width - 1, m.X)), y = Math.Max(0, Math.Min(_shot.Height - 1, m.Y));
            var c = _shot.GetPixel(x, y);
            const int zoom = 10, cells = 13, size = zoom * cells;
            var box = new Rectangle(m.X + 20, m.Y + 20, size, size + 30);
            if (box.Right > ClientSize.Width) box.X = m.X - 20 - size;
            if (box.Bottom > ClientSize.Height) box.Y = m.Y - 20 - box.Height;
            g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
            g.FillRectangle(Brushes.White, box);
            g.DrawImage(_shot, new Rectangle(box.X, box.Y, size, size), new Rectangle(x - cells / 2, y - cells / 2, cells, cells), GraphicsUnit.Pixel);
            g.DrawRectangle(Pens.Black, box.X + cells / 2 * zoom, box.Y + cells / 2 * zoom, zoom, zoom);
            using (var br = new SolidBrush(c)) g.FillRectangle(br, box.X + 4, box.Y + size + 6, 18, 18);
            using (var f = new Font("Microsoft YaHei UI", 9f))
                g.DrawString("#" + (c.ToArgb() & 0xFFFFFF).ToString("X6") + "  单击取色 · Esc 取消", f, Brushes.Black, box.X + 26, box.Y + size + 7);
            g.DrawRectangle(Pens.Gray, box);
        }
    }
}
