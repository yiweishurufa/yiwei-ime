using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Yiwei
{
    /// <summary>
    /// W2 双拼键位图浮窗：按键盘排列显示当前双拼方案每个键的声母（sh/ch/zh）和韵母。
    /// 数据是安装包里的 data/yiwei_shuangpin.txt（构建时由 customize_data.py 从各双拼方案的 speller/algebra 推出，
    /// 和实际键位一定一致）。置顶、不抢焦点，可拖动；工具「双拼键位图」/ 长按 Alt+P / 自定义快捷键 打开或关闭。
    /// </summary>
    sealed class ShuangpinMap : FloatingWindow
    {
        static ShuangpinMap _win;
        static readonly string[] Rows = { "qwertyuiop", "asdfghjkl;", "zxcvbnm" };
        static readonly HashSet<string> Initials = new HashSet<string> { "sh", "ch", "zh" };

        /// <summary>方案 id → (名称, 键 → 声母韵母)。</summary>
        public static Dictionary<string, (string Name, Dictionary<string, string[]> Keys)> Load(string path = null)
        {
            var d = new Dictionary<string, (string, Dictionary<string, string[]>)>();
            path = path ?? Path.Combine(Paths.SharedDataDir, "yiwei_shuangpin.txt");
            if (!File.Exists(path)) return d;
            foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
            {
                if (line.StartsWith("#")) continue;
                var p = line.Split('\t');
                if (p.Length < 4) continue;
                if (!d.TryGetValue(p[0], out var e)) d[p[0]] = e = (p[1], new Dictionary<string, string[]>());
                e.Item2[p[2]] = p[3].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            }
            return d;
        }

        public static void Toggle()
        {
            if (_win != null && _win.IsVisible) { _win.Hide(); return; }
            var maps = Load();
            if (maps.Count == 0) { Tray.Balloon("双拼键位图", "没找到键位数据（data\\yiwei_shuangpin.txt），请重新安装一维输入法"); return; }
            if (_win == null) _win = new ShuangpinMap();
            var cur = Settings.Current.Schema;
            _win._schema = maps.ContainsKey(cur) ? cur : maps.ContainsKey("double_pinyin_flypy") ? "double_pinyin_flypy" : maps.Keys.First();
            _win._maps = maps;
            _win.Build();
            var wa = Native.WorkArea(0, 0);
            _win.ShowAt(new Native.RECT { Left = wa.Right - 700, Top = wa.Bottom - 300, Right = wa.Right - 699, Bottom = wa.Bottom - 280 });
        }

        Dictionary<string, (string Name, Dictionary<string, string[]> Keys)> _maps;
        string _schema;

        void Build()
        {
            var p = PanelPalette.Now();
            var root = new StackPanel();
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8), LastChildFill = false };
            var close = new TextBlock { Text = "✕", FontSize = 13, Foreground = p.Dim, Cursor = Cursors.Hand, Padding = new Thickness(6, 0, 2, 0) };
            close.MouseLeftButtonUp += (s, e) => Hide();
            DockPanel.SetDock(close, Dock.Right);
            head.Children.Add(close);
            foreach (var kv in _maps)
            {
                var id = kv.Key;
                var c = PanelParts.Chip(p, kv.Value.Name, id == _schema);
                c.MouseLeftButtonUp += (s, e) => { _schema = id; Build(); };
                head.Children.Add(c);
            }
            root.Children.Add(head);
            var keys = _maps[_schema].Keys;
            for (int r = 0; r < Rows.Length; r++)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(r * 18, 0, 0, 4) };
                foreach (var ch in Rows[r])
                {
                    var k = ch.ToString();
                    keys.TryGetValue(k, out var labels);
                    labels = labels ?? new string[0];
                    var ini = labels.Where(Initials.Contains).ToList();
                    if (ini.Count == 0 && "bpmfdtnlgkhjqxrzcsyw".Contains(ch)) ini.Add(k);
                    var fin = labels.Where(x => !Initials.Contains(x)).ToList();
                    var cell = new StackPanel { Width = 58 };
                    var top = new DockPanel();
                    top.Children.Add(new TextBlock { Text = k == ";" ? ";" : k.ToUpperInvariant(), FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = p.Text });
                    var it = new TextBlock { Text = string.Join(" ", ini), FontSize = 12, Foreground = p.Accent, HorizontalAlignment = HorizontalAlignment.Right };
                    DockPanel.SetDock(it, Dock.Right);
                    top.Children.Insert(0, it);
                    cell.Children.Add(top);
                    cell.Children.Add(new TextBlock { Text = string.Join("\n", fin), FontSize = 11.5, Foreground = p.Dim, LineHeight = 14 });
                    row.Children.Add(new Border
                    {
                        Child = cell, Background = p.Card, BorderBrush = p.Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 0, 4, 0), MinHeight = 62,
                    });
                }
                root.Children.Add(row);
            }
            root.Children.Add(PanelParts.Hint(p, "蓝字是声母，灰字是韵母 · 零声母字：" + ZeroHint(_schema) + " · 拖动移动，长按 Alt+P 关闭"));
            var shell = PanelParts.Shell(p, root);
            shell.MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
            Content = shell;
        }

        static string ZeroHint(string schema) =>
            schema == "double_pinyin_mspy" ? "o + 韵母（a→oa，ang→oh）" :
            schema == "double_pinyin" ? "双写或首字母 + 韵母键（a→aa，ang→ah）" :
            "双写或首字母 + 韵母键（a→aa，ang→ah，ai→ai）";
    }
}
