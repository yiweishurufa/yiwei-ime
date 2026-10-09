using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Yiwei
{
    /// <summary>Live preview of the candidate window: pinyin "yi'wei", candidates 一维 依偎 意味 以为.</summary>
    sealed class CandidatePreview : FrameworkElement
    {
        public string Scheme = "yiwei_qingbi";
        public bool Horizontal = true;
        public string FontName = "Microsoft YaHei UI";
        public int FontPoint = 12, Radius = 8, HRadius = 6;
        static readonly string[] Cands = { "一维", "依偎", "意味", "以为" };

        public CandidatePreview() { Height = 150; SnapsToDevicePixels = true; }

        public void Refresh() { InvalidateVisual(); }

        static Color C(Dictionary<string, uint> d, string k, Color fallback) =>
            d.TryGetValue(k, out var v) ? Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v) : fallback;

        static Geometry Round(Rect r, double radius) => radius <= 0 ? (Geometry)new RectangleGeometry(r) : new RectangleGeometry(r, radius, radius);

        protected override void OnRender(DrawingContext dc)
        {
            double dip = 1; try { dip = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { }
            var colors = WeaselConfig.SchemeColors(Scheme ?? "yiwei_qingbi");
            var back = C(colors, "back_color", Colors.White);
            var border = C(colors, "border_color", Color.FromRgb(0xE1, 0xE4, 0xE8));
            var text = C(colors, "text_color", Color.FromRgb(0x5B, 0x62, 0x70));
            var cand = C(colors, "candidate_text_color", Color.FromRgb(0x1F, 0x23, 0x28));
            var label = C(colors, "label_color", Color.FromRgb(0x88, 0x88, 0x88));
            var hiBack = C(colors, "hilited_candidate_back_color", UiTheme.Accent);
            var hiText = C(colors, "hilited_candidate_text_color", Colors.White);
            var hiLabel = C(colors, "hilited_label_color", Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF));

            Typeface face;
            try { face = new Typeface(new FontFamily(string.IsNullOrWhiteSpace(FontName) ? "Microsoft YaHei UI" : FontName), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal); }
            catch { face = new Typeface("Microsoft YaHei UI"); }
            double size = Math.Max(10, FontPoint) * 96.0 / 72.0, small = size * 0.78;
            FormattedText F(string s, double sz, Color c) => new FormattedText(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, face, sz, new SolidColorBrush(c), dip);

            var pre = F("yi'wei", small, text);
            var items = Cands.Select((s, i) => (label: F((i + 1) + ".", small, i == 0 ? hiLabel : label), word: F(s, size, i == 0 ? hiText : cand))).ToList();
            double pad = 8, gap = 6, itemPadX = 8, rowH = Math.Max(items[0].word.Height, items[0].label.Height) + 8;
            double w, h;
            var widths = items.Select(x => x.label.Width + 4 + x.word.Width + itemPadX * 2).ToList();
            if (Horizontal) { w = Math.Max(pre.Width, widths.Sum() + gap * (widths.Count - 1)) + pad * 2; h = pad * 2 + pre.Height + 6 + rowH; }
            else { w = Math.Max(pre.Width, widths.Max()) + pad * 2 + 24; h = pad * 2 + pre.Height + 6 + rowH * items.Count; }
            Height = Math.Max(150, h + 32);

            // page-like backdrop so dark schemes are visible too
            var area = new Rect(0, 0, ActualWidth, Height);
            dc.DrawRoundedRectangle(new SolidColorBrush(UiTheme.Dark ? Color.FromRgb(0x1B, 0x1C, 0x1F) : Color.FromRgb(0xEE, 0xF0, 0xF2)), null, area, 8, 8);
            var box = new Rect(24, 16, w, h);
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0)), null, Round(new Rect(box.X + 1, box.Y + 3, box.Width, box.Height), Radius));
            dc.DrawGeometry(new SolidColorBrush(back), new Pen(new SolidColorBrush(border), 1), Round(box, Radius));
            dc.DrawText(pre, new Point(box.X + pad + 2, box.Y + pad));
            double x = box.X + pad, y = box.Y + pad + pre.Height + 6;
            for (int i = 0; i < items.Count; i++)
            {
                var r = new Rect(x, y, Horizontal ? widths[i] : box.Width - pad * 2, rowH);
                if (i == 0) dc.DrawGeometry(new SolidColorBrush(hiBack), null, Round(r, HRadius));
                dc.DrawText(items[i].label, new Point(r.X + itemPadX, r.Y + (rowH - items[i].label.Height) / 2));
                dc.DrawText(items[i].word, new Point(r.X + itemPadX + items[i].label.Width + 4, r.Y + (rowH - items[i].word.Height) / 2));
                if (Horizontal) x += widths[i] + gap; else y += rowH;
            }
        }
    }

    /// <summary>A small QWERTY diagram of a double-pinyin layout.</summary>
    sealed class KeyboardDiagram : FrameworkElement
    {
        readonly Dictionary<char, string> _map;
        static readonly string[] Rows = { "QWERTYUIOP", "ASDFGHJKL;", "ZXCVBNM" };

        public KeyboardDiagram(string schema)
        {
            _map = Layout(schema);
            Width = 320; Height = 104;
        }

        public static Dictionary<char, string> Layout(string schema)
        {
            string spec;
            switch (schema)
            {
                case "double_pinyin_flypy":
                    spec = "Q iu|W ei|E e|R uan|T ve|Y un|U sh u|I ch i|O uo|P ie|A a|S ong|D ai|F en|G eng|H ang|J an|K ing|L iang|Z ou|X ia|C ao|V zh ui|B in|N iao|M ian"; break;
                case "double_pinyin":
                    spec = "Q iu|W ua|E e|R uan|T ve|Y ing|U sh u|I ch i|O uo|P un|A a|S ong|D uang|F en|G eng|H ang|J an|K ao|L ai|Z ei|X ie|C iao|V zh ui|B ou|N in|M ian"; break;
                case "double_pinyin_mspy":
                    spec = "Q iu|W ua|E e|R uan|T ve|Y uai|U sh u|I ch i|O uo|P un|A a|S ong|D uang|F en|G eng|H ang|J an|K ao|L ai|; ing|Z ei|X ie|C iao|V zh ui|B ou|N in|M ian"; break;
                default: spec = ""; break;
            }
            var d = new Dictionary<char, string>();
            foreach (var part in spec.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = part.Split(' ');
                d[p[0][0]] = string.Join("\n", p.Skip(1));
            }
            return d;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double dip = 1; try { dip = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { }
            var face = new Typeface("Microsoft YaHei UI");
            bool dark = UiTheme.Dark;
            var keyBrush = new SolidColorBrush(dark ? Color.FromRgb(0x3A, 0x3D, 0x44) : Color.FromRgb(0xF2, 0xF4, 0xF6));
            var text = new SolidColorBrush(dark ? Color.FromRgb(0xEC, 0xEE, 0xF1) : Color.FromRgb(0x1F, 0x23, 0x28));
            var accent = new SolidColorBrush(UiTheme.Accent);
            double k = 28, g = 2;
            for (int r = 0; r < Rows.Length; r++)
            {
                double x0 = r * 8;
                for (int i = 0; i < Rows[r].Length; i++)
                {
                    var ch = Rows[r][i];
                    var rect = new Rect(x0 + i * (k + g), r * (k + 6), k, k + 4);
                    dc.DrawRoundedRectangle(keyBrush, null, rect, 4, 4);
                    dc.DrawText(new FormattedText(ch.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, 9, text, dip), new Point(rect.X + 3, rect.Y + 1));
                    if (_map.TryGetValue(ch, out var v))
                    {
                        var ft = new FormattedText(v, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, 8, accent, dip) { TextAlignment = TextAlignment.Right };
                        dc.DrawText(ft, new Point(rect.Right - 2, rect.Bottom - ft.Height - 1));
                    }
                }
            }
        }
    }

    /// <summary>Line chart for the last days.</summary>
    sealed class LineChart : FrameworkElement
    {
        public long[] Values = new long[0];
        public string[] Labels = new string[0];
        public LineChart() { Height = 170; }

        protected override void OnRender(DrawingContext dc)
        {
            double dip = 1; try { dip = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { }
            var face = new Typeface("Microsoft YaHei UI");
            var dim = new SolidColorBrush(UiTheme.Dark ? Color.FromRgb(0x9A, 0xA0, 0xA8) : Color.FromRgb(0x6B, 0x72, 0x80));
            var grid = new Pen(new SolidColorBrush(UiTheme.Dark ? Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x20, 0, 0, 0)), 1);
            var acc = UiTheme.Accent;
            double left = 44, right = ActualWidth - 12, top = 10, bottom = Height - 26;
            if (Values.Length < 2 || right <= left) return;
            long max = Math.Max(10, Values.Max());
            // nice max
            double mag = Math.Pow(10, Math.Floor(Math.Log10(max)));
            double nice = Math.Ceiling(max / mag) * mag;
            for (int i = 0; i <= 3; i++)
            {
                double y = bottom - (bottom - top) * i / 3.0;
                dc.DrawLine(grid, new Point(left, y), new Point(right, y));
                var ft = new FormattedText(((long)(nice * i / 3)).ToString("N0"), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, 10, dim, dip) { TextAlignment = TextAlignment.Right };
                dc.DrawText(ft, new Point(left - 6, y - ft.Height / 2));
            }
            var pts = Values.Select((v, i) => new Point(left + (right - left) * i / (Values.Length - 1), bottom - (bottom - top) * v / nice)).ToList();
            var area = new StreamGeometry();
            using (var c = area.Open())
            {
                c.BeginFigure(new Point(pts[0].X, bottom), true, true);
                foreach (var p in pts) c.LineTo(p, true, true);
                c.LineTo(new Point(pts.Last().X, bottom), true, true);
            }
            var fill = new LinearGradientBrush(Color.FromArgb(0x55, acc.R, acc.G, acc.B), Color.FromArgb(0x05, acc.R, acc.G, acc.B), 90);
            dc.DrawGeometry(fill, null, area);
            var line = new StreamGeometry();
            using (var c = line.Open())
            {
                c.BeginFigure(pts[0], false, false);
                foreach (var p in pts.Skip(1)) c.LineTo(p, true, true);
            }
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(acc), 2.2) { LineJoin = PenLineJoin.Round }, line);
            var dotFill = new SolidColorBrush(UiTheme.Dark ? Color.FromRgb(0x20, 0x22, 0x26) : Colors.White);
            for (int i = 0; i < pts.Count; i++)
            {
                dc.DrawEllipse(dotFill, new Pen(new SolidColorBrush(acc), 2), pts[i], 3.5, 3.5);
                if (i < Labels.Length)
                {
                    var ft = new FormattedText(Labels[i], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, 10, dim, dip) { TextAlignment = TextAlignment.Center };
                    dc.DrawText(ft, new Point(pts[i].X, bottom + 6));
                    var val = new FormattedText(Values[i].ToString("N0"), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, 10, dim, dip) { TextAlignment = TextAlignment.Center };
                    if (Values[i] > 0) dc.DrawText(val, new Point(pts[i].X, pts[i].Y - val.Height - 4));
                }
            }
        }
    }

    /// <summary>A year of days as a GitHub-style heat map (weeks as columns, Monday on top).</summary>
    sealed class Heatmap : FrameworkElement
    {
        public SortedDictionary<DateTime, long> Days = new SortedDictionary<DateTime, long>();
        public Heatmap() { Height = 130; }

        protected override void OnRender(DrawingContext dc)
        {
            double dip = 1; try { dip = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { }
            var face = new Typeface("Microsoft YaHei UI");
            var dim = new SolidColorBrush(UiTheme.Dark ? Color.FromRgb(0x9A, 0xA0, 0xA8) : Color.FromRgb(0x6B, 0x72, 0x80));
            var acc = UiTheme.Accent;
            var empty = UiTheme.Dark ? Color.FromRgb(0x33, 0x36, 0x3C) : Color.FromRgb(0xEB, 0xED, 0xF0);
            var today = DateTime.Today;
            int offset = ((int)today.DayOfWeek + 6) % 7;            // 0 = Monday
            var start = today.AddDays(-offset - 52 * 7);              // Monday 52 weeks ago
            long max = Math.Max(1, Days.Where(kv => kv.Key >= start).Select(kv => kv.Value).DefaultIfEmpty(0).Max());
            double left = 26, top = 18;
            double cell = Math.Max(6, Math.Min(13, (ActualWidth - left - 4) / 53.0 - 2)), g = 2;
            string[] rows = { "一", "", "三", "", "五", "", "日" };
            for (int r = 0; r < 7; r++)
                if (rows[r].Length > 0)
                    dc.DrawText(new FormattedText(rows[r], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, 9, dim, dip), new Point(4, top + r * (cell + g)));
            int lastMonth = -1;
            for (var d = start; d <= today; d = d.AddDays(1))
            {
                int week = (int)((d - start).TotalDays / 7), dow = ((int)d.DayOfWeek + 6) % 7;
                double x = left + week * (cell + g), y = top + dow * (cell + g);
                Days.TryGetValue(d, out var v);
                Color c = empty;
                if (v > 0)
                {
                    double t = 0.25 + 0.75 * Math.Sqrt((double)v / max);
                    c = Color.FromArgb((byte)(t * 255), acc.R, acc.G, acc.B);
                }
                dc.DrawRoundedRectangle(new SolidColorBrush(c), null, new Rect(x, y, cell, cell), 2, 2);
                if (dow == 0 && d.Month != lastMonth)
                {
                    lastMonth = d.Month;
                    dc.DrawText(new FormattedText(d.Month + "月", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, 9, dim, dip), new Point(x, 2));
                }
            }
            Height = top + 7 * (cell + g) + 6;
        }
    }

    /// <summary>Horizontal bars with labels (top words, top apps).</summary>
    sealed class BarList : FrameworkElement
    {
        public List<KeyValuePair<string, long>> Items = new List<KeyValuePair<string, long>>();
        public string Unit = "";
        public BarList() { }

        protected override Size MeasureOverride(Size available) => new Size(0, Math.Max(24, Items.Count * 24));

        protected override void OnRender(DrawingContext dc)
        {
            double dip = 1; try { dip = VisualTreeHelper.GetDpi(this).PixelsPerDip; } catch { }
            var face = new Typeface("Microsoft YaHei UI");
            var text = new SolidColorBrush(UiTheme.Dark ? Color.FromRgb(0xEC, 0xEE, 0xF1) : Color.FromRgb(0x1F, 0x23, 0x28));
            var dim = new SolidColorBrush(UiTheme.Dark ? Color.FromRgb(0x9A, 0xA0, 0xA8) : Color.FromRgb(0x6B, 0x72, 0x80));
            var acc = UiTheme.Accent;
            var bar = new SolidColorBrush(Color.FromArgb(0x55, acc.R, acc.G, acc.B));
            if (Items.Count == 0)
            {
                dc.DrawText(new FormattedText("还没有数据", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, 12, dim, dip), new Point(0, 2));
                return;
            }
            long max = Math.Max(1, Items.Max(i => i.Value));
            double labelW = 110, w = Math.Max(10, ActualWidth - labelW - 70);
            for (int i = 0; i < Items.Count; i++)
            {
                double y = i * 24;
                var name = new FormattedText(Items[i].Key, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, 12, text, dip) { MaxTextWidth = labelW - 8, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
                dc.DrawText(name, new Point(0, y + 3));
                double bw = Math.Max(2, w * Items[i].Value / max);
                dc.DrawRoundedRectangle(bar, null, new Rect(labelW, y + 5, bw, 14), 3, 3);
                dc.DrawText(new FormattedText(Items[i].Value.ToString("N0") + Unit, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, 11, dim, dip), new Point(labelW + bw + 6, y + 4));
            }
        }
    }
}
