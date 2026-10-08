using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Yiwei
{
    /// <summary>「一维输入法设置」</summary>
    sealed class SettingsForm : Form
    {
        readonly Settings S = Settings.Current;
        readonly TabControl _tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 6) };
        static readonly Font UiFont = new Font("Microsoft YaHei UI", 9.5f);
        static readonly Font TitleFont = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold);

        public SettingsForm(string page = null)
        {
            Text = "一维输入法设置";
            Font = UiFont;
            Icon = Program.AppIcon;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(860, 620);
            MinimumSize = new Size(760, 560);
            Controls.Add(_tabs);
            AddPage("概览", BuildOverview());
            AddPage("外观", BuildAppearance());
            AddPage("应用", BuildApps());
            AddPage("常用语", BuildSnippets());
            AddPage("AI 助手", BuildAi());
            AddPage("词库", BuildDicts());
            AddPage("统计", BuildStats());
            AddPage("导入", BuildImport());
            AddPage("关于", BuildAbout());
            if (page != null)
                foreach (TabPage p in _tabs.TabPages) if (p.Text == page) _tabs.SelectedTab = p;
        }

        void AddPage(string title, Control content)
        {
            var p = new TabPage(title) { Padding = new Padding(18), BackColor = SystemColors.Window };
            content.Dock = DockStyle.Fill;
            p.Controls.Add(content);
            _tabs.TabPages.Add(p);
        }

        // ---------- small layout helpers ----------

        static FlowLayoutPanel Stack() => new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        static Label Title(string t) => new Label { Text = t, Font = TitleFont, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
        static Label Note(string t, int width = 760) => new Label { Text = t, AutoSize = true, MaximumSize = new Size(width, 0), ForeColor = SystemColors.GrayText, Margin = new Padding(0, 2, 0, 10) };
        static Label Lbl(string t) => new Label { Text = t, AutoSize = true, Margin = new Padding(0, 8, 8, 0) };
        static Button Btn(string t, EventHandler onClick) { var b = new Button { Text = t, AutoSize = true, Padding = new Padding(8, 2, 8, 2), Margin = new Padding(0, 4, 8, 4) }; b.Click += onClick; return b; }
        static FlowLayoutPanel Row(params Control[] cs) { var f = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) }; f.Controls.AddRange(cs); return f; }

        static void Open(string path, string args = null)
        {
            try { Process.Start(new ProcessStartInfo(path, args ?? "") { UseShellExecute = true }); }
            catch (Exception e) { MessageBox.Show(e.Message, "一维输入法"); }
        }

        void ApplyConfig(string done = "已保存，正在重新部署…")
        {
            S.Save();
            try { WeaselConfig.Write(S); Deploy.Run(); _status.Text = done; }
            catch (Exception e) { MessageBox.Show("保存失败：" + e.Message, "一维输入法"); }
        }

        readonly Label _status = new Label { AutoSize = true, ForeColor = Color.SeaGreen, Margin = new Padding(0, 10, 0, 0) };

        // ---------- 概览 ----------

        Control BuildOverview()
        {
            var st = Stack();
            st.Controls.Add(Title("一维输入法"));
            st.Controls.Add(Note("中文常新，自在表达。基于 RIME 与雾凇拼音，组字、候选与个人词频都在本机处理，不含遥测。"));
            st.Controls.Add(new Label { Text = "常用操作", Font = new Font(UiFont, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 8, 0, 4) });
            st.Controls.Add(Row(
                Btn("重新部署", (s, e) => { Deploy.Run(); _status.Text = "正在重新部署…"; }),
                Btn("输入方案选单", (s, e) => Open(Paths.Deployer)),
                Btn("用户词典管理", (s, e) => Open(Paths.Deployer, "/dict")),
                Btn("打开用户文件夹", (s, e) => Open(Paths.UserDir))));
            st.Controls.Add(new Label { Text = "快捷键", Font = new Font(UiFont, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 14, 0, 4) });
            st.Controls.Add(Note(
                "长按 Alt 再按 1–9　打开常用语面板（手机号、邮箱、地址、符号…），字母键直接上屏\n" +
                "选中文字，长按 Alt 再按空格　AI 翻译 / 润色 / 粤语，数字键替换选中的文字\n" +
                "Shift　切换中英文　　Ctrl+`　方案选单（简繁、全半角、双拼…）\n" +
                "输入 rq / sj / xq　日期 · 时间 · 星期　　输入 V 开头　计算器与符号"));
            st.Controls.Add(_status);
            return st;
        }

        // ---------- 外观 ----------

        ComboBox _light, _dark;
        PreviewBox _preview;

        Control BuildAppearance()
        {
            var st = Stack();
            st.Controls.Add(Title("配色、字号、圆角，边改边看"));
            var schemes = WeaselConfig.BuiltinSchemes();
            _light = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, DisplayMember = "Value", ValueMember = "Key" };
            _dark = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, DisplayMember = "Value", ValueMember = "Key" };
            _light.DataSource = schemes.ToList(); _dark.DataSource = schemes.ToList();
            _light.SelectedValue = S.ColorScheme; _dark.SelectedValue = S.ColorSchemeDark;
            st.Controls.Add(Row(Lbl("浅色模式"), _light, Lbl("　深色模式"), _dark));

            var horiz = new RadioButton { Text = "横排", Checked = S.Horizontal && !S.VerticalText, AutoSize = true };
            var stacked = new RadioButton { Text = "竖排列表", Checked = !S.Horizontal && !S.VerticalText, AutoSize = true };
            var vtext = new RadioButton { Text = "直书（竖排文字）", Checked = S.VerticalText, AutoSize = true };
            st.Controls.Add(Row(Lbl("候选排列"), horiz, stacked, vtext));

            var size = new NumericUpDown { Minimum = 10, Maximum = 40, Value = Clamp(S.FontPoint, 10, 40), Width = 60 };
            var fonts = new ComboBox { Width = 220, Text = S.FontFace };
            using (var ifc = new InstalledFontCollection()) fonts.Items.AddRange(ifc.Families.Select(f => (object)f.Name).ToArray());
            st.Controls.Add(Row(Lbl("字号"), size, Lbl("　字体"), fonts));

            var radius = new TrackBar { Minimum = 0, Maximum = 30, Value = Clamp(S.CornerRadius, 0, 30), Width = 200, TickFrequency = 5 };
            var hradius = new TrackBar { Minimum = 0, Maximum = 30, Value = Clamp(S.HilitedCornerRadius, 0, 30), Width = 200, TickFrequency = 5 };
            st.Controls.Add(Row(Lbl("候选窗圆角"), radius, Lbl("高亮圆角"), hradius));

            var inline = new CheckBox { Text = "在输入框里显示拼音（内嵌编码）", Checked = S.InlinePreedit, AutoSize = true };
            st.Controls.Add(inline);

            _preview = new PreviewBox { Width = 760, Height = 120, Margin = new Padding(0, 10, 0, 6) };
            st.Controls.Add(_preview);

            var custom = Btn("我的配色…", (s, e) => EditCustomTheme());
            var save = Btn("应用", (s, e) =>
            {
                S.ColorScheme = _light.SelectedValue as string ?? S.ColorScheme;
                S.ColorSchemeDark = _dark.SelectedValue as string ?? S.ColorSchemeDark;
                S.Horizontal = horiz.Checked; S.VerticalText = vtext.Checked;
                S.FontPoint = (int)size.Value; S.FontFace = string.IsNullOrWhiteSpace(fonts.Text) ? S.FontFace : fonts.Text;
                S.CornerRadius = radius.Value; S.HilitedCornerRadius = hradius.Value; S.InlinePreedit = inline.Checked;
                ApplyConfig();
            });
            st.Controls.Add(Row(save, custom, Btn("主题博物馆（导入链接）", (s, e) => _tabs.SelectedIndex = 7)));
            st.Controls.Add(Note("主题链接（yiwei-ime://theme?…）点开即可导入，格式与 AIME 主题包相同。"));

            void Refresh()
            {
                _preview.Scheme = _light.SelectedValue as string; _preview.Horizontal = horiz.Checked || vtext.Checked;
                _preview.FontPoint = (int)size.Value; _preview.Radius = radius.Value; _preview.HRadius = hradius.Value;
                _preview.FontName = fonts.Text; _preview.Invalidate();
            }
            _light.SelectedIndexChanged += (s, e) => Refresh();
            horiz.CheckedChanged += (s, e) => Refresh(); stacked.CheckedChanged += (s, e) => Refresh(); vtext.CheckedChanged += (s, e) => Refresh();
            size.ValueChanged += (s, e) => Refresh(); radius.ValueChanged += (s, e) => Refresh(); hradius.ValueChanged += (s, e) => Refresh();
            fonts.TextChanged += (s, e) => Refresh();
            Refresh();
            return st;
        }

        static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

        void EditCustomTheme()
        {
            var baseColors = WeaselConfig.SchemeColors(_light.SelectedValue as string ?? "yiwei_light");
            uint Get(string k, uint d) => baseColors.TryGetValue(k, out var v) ? v : d;
            var pairs = new[] { ("back_color", "背景"), ("candidate_text_color", "候选文字"), ("hilited_candidate_back_color", "高亮背景"), ("hilited_candidate_text_color", "高亮文字"), ("comment_text_color", "注释") };
            var theme = S.CustomTheme != null ? new Dictionary<string, string>(S.CustomTheme) : new Dictionary<string, string>
            {
                ["name"] = "我的配色", ["author"] = Environment.UserName,
                ["back_color"] = Hex(Get("back_color", 0xFFFFFFFF)), ["border_color"] = Hex(Get("border_color", 0x14000000)),
                ["text_color"] = Hex(Get("text_color", 0xFF555555)), ["hilited_text_color"] = Hex(Get("hilited_text_color", 0xFF222222)),
                ["hilited_back_color"] = Hex(Get("hilited_back_color", 0x14000000)), ["candidate_text_color"] = Hex(Get("candidate_text_color", 0xFF222222)),
                ["comment_text_color"] = Hex(Get("comment_text_color", 0xFF777777)), ["label_color"] = Hex(Get("label_color", 0xFF888888)),
                ["hilited_candidate_back_color"] = Hex(Get("hilited_candidate_back_color", 0xFFB64032)), ["hilited_candidate_text_color"] = Hex(Get("hilited_candidate_text_color", 0xFFFFFFFF)),
                ["hilited_comment_text_color"] = Hex(Get("hilited_comment_text_color", 0xE6FFFFFF)), ["hilited_label_color"] = Hex(Get("hilited_label_color", 0xE6FFFFFF)),
            };
            foreach (var (key, label) in pairs)
            {
                WeaselConfig.TryColor(theme[key], out var cur);
                using (var dlg = new ColorDialog { Color = Color.FromArgb(unchecked((int)(cur | 0xFF000000))), FullOpen = true })
                {
                    MessageBox.Show("选择「" + label + "」的颜色", "我的配色");
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    uint alpha = key == "back_color" ? 0xF5000000u : 0xFF000000u;
                    theme[key] = Hex(alpha | (uint)(dlg.Color.ToArgb() & 0xFFFFFF));
                }
            }
            S.CustomTheme = theme;
            S.ColorScheme = "yiwei_custom";
            ApplyConfig("我的配色已启用，正在重新部署…");
            var list = WeaselConfig.BuiltinSchemes();
            _light.DataSource = list.ToList(); _dark.DataSource = list.ToList();
            _light.SelectedValue = S.ColorScheme; _dark.SelectedValue = S.ColorSchemeDark;
        }

        static string Hex(uint v) => "0x" + v.ToString("X8");

        sealed class PreviewBox : Control
        {
            public string Scheme, FontName = "Microsoft YaHei UI";
            public bool Horizontal = true;
            public int FontPoint = 14, Radius = 10, HRadius = 6;
            public PreviewBox() { DoubleBuffered = true; }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.FromArgb(236, 236, 234));
                var c = WeaselConfig.SchemeColors(Scheme ?? "yiwei_light");
                Color C(string k, Color d) => c.TryGetValue(k, out var v) ? Color.FromArgb(unchecked((int)(v | 0xFF000000))) : d;
                Font f; try { f = new Font(string.IsNullOrWhiteSpace(FontName) ? "Microsoft YaHei UI" : FontName, FontPoint * 0.75f); } catch { f = new Font("Microsoft YaHei UI", FontPoint * 0.75f); }
                var small = new Font(f.FontFamily, Math.Max(7, f.Size * 0.75f));
                var cands = new[] { "中文", "钟文", "中闻", "忠文", "仲文" };
                var sizes = cands.Select(s => g.MeasureString(s, f)).ToArray();
                float lh = sizes[0].Height + 8;
                float w = Horizontal ? sizes.Sum(s => s.Width + 30) + 20 : sizes.Max(s => s.Width) + 60;
                float h = Horizontal ? lh * 2 + 12 : lh * (cands.Length + 1) + 12;
                h = Math.Min(h, Height - 10);
                var box = new RectangleF(12, 6, w, h);
                using (var p = Round(box, Radius)) { using (var b = new SolidBrush(C("back_color", Color.White))) g.FillPath(b, p); using (var pen = new Pen(C("border_color", Color.LightGray))) g.DrawPath(pen, p); }
                using (var b = new SolidBrush(C("text_color", Color.Gray))) g.DrawString("zhong'wen", small, b, box.X + 10, box.Y + 6);
                float x = box.X + 8, y = box.Y + lh;
                for (int i = 0; i < cands.Length && y + lh <= box.Bottom + 2; i++)
                {
                    var r = new RectangleF(x, y, Horizontal ? sizes[i].Width + 26 : box.Width - 16, lh - 4);
                    bool hi = i == 0;
                    if (hi) using (var p = Round(r, HRadius)) using (var b = new SolidBrush(C("hilited_candidate_back_color", Color.Firebrick))) g.FillPath(b, p);
                    using (var lb = new SolidBrush(hi ? C("hilited_label_color", Color.White) : C("label_color", Color.Gray)))
                        g.DrawString((i + 1).ToString(), small, lb, r.X + 4, r.Y + (r.Height - small.Height) / 2);
                    using (var tb = new SolidBrush(hi ? C("hilited_candidate_text_color", Color.White) : C("candidate_text_color", Color.Black)))
                        g.DrawString(cands[i], f, tb, r.X + 18, r.Y + (r.Height - f.Height) / 2);
                    if (Horizontal) x += r.Width + 4; else y += lh;
                }
                f.Dispose(); small.Dispose();
            }
            static GraphicsPath Round(RectangleF r, float radius)
            {
                var p = new GraphicsPath(); float d = Math.Max(1, radius * 2);
                if (radius <= 0) { p.AddRectangle(r); return p; }
                p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
            }
        }

        // ---------- 应用 ----------

        static readonly string[] Recommended =
        {
            "windowsterminal.exe", "cmd.exe", "powershell.exe", "pwsh.exe", "code.exe", "cursor.exe", "windsurf.exe",
            "idea64.exe", "pycharm64.exe", "webstorm64.exe", "devenv.exe", "everything.exe", "powertoys.powerlauncher.exe",
        };

        Control BuildApps()
        {
            var st = Stack();
            st.Controls.Add(Title("终端里打英文，微信里打中文"));
            st.Controls.Add(Note("勾选的应用切换过去时自动进入英文状态。其他应用各自记住自己的中英文状态。"));
            var global = new CheckBox { Text = "所有应用共用同一个中英文状态（不分应用）", Checked = S.GlobalAscii, AutoSize = true };
            st.Controls.Add(global);
            var list = new ListView { View = View.Details, CheckBoxes = true, Width = 760, Height = 330, FullRowSelect = true };
            list.Columns.Add("应用", 300); list.Columns.Add("默认英文", 120); list.Columns.Add("来源", 200);
            void Fill(bool scan)
            {
                list.Items.Clear();
                var names = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in Recommended) names[r] = "推荐";
                foreach (var kv in S.AppAscii) names[kv.Key] = "已设置";
                if (scan)
                    foreach (var p in Process.GetProcesses())
                    {
                        try { if (p.MainWindowHandle != IntPtr.Zero && !names.ContainsKey(p.ProcessName + ".exe")) names[(p.ProcessName + ".exe").ToLowerInvariant()] = "正在运行"; }
                        catch { }
                        finally { p.Dispose(); }
                    }
                foreach (var kv in names)
                {
                    bool on = S.AppAscii.TryGetValue(kv.Key.ToLowerInvariant(), out var v) ? v : (!S.AppsScanned && kv.Value == "推荐");
                    var it = new ListViewItem(new[] { kv.Key, on ? "英文" : "", kv.Value }) { Checked = on };
                    list.Items.Add(it);
                }
            }
            list.ItemChecked += (s, e) => e.Item.SubItems[1].Text = e.Item.Checked ? "英文" : "";
            Fill(true);
            st.Controls.Add(list);
            var add = new TextBox { Width = 200 };
            st.Controls.Add(Row(Lbl("添加应用（如 wechat.exe）"), add, Btn("添加", (s, e) =>
            {
                var n = add.Text.Trim().ToLowerInvariant(); if (n.Length == 0) return; if (!n.EndsWith(".exe")) n += ".exe";
                list.Items.Add(new ListViewItem(new[] { n, "英文", "手动添加" }) { Checked = true }); add.Clear();
            }), Btn("扫描正在运行的应用", (s, e) => Fill(true))));
            st.Controls.Add(Row(Btn("应用", (s, e) =>
            {
                S.GlobalAscii = global.Checked;
                S.AppAscii.Clear();
                foreach (ListViewItem it in list.Items)
                {
                    var name = it.Text.ToLowerInvariant();
                    if (it.Checked) S.AppAscii[name] = true;
                    else if (Recommended.Contains(name) || name == "conhost.exe") S.AppAscii[name] = false; // override shipped defaults
                }
                S.AppsScanned = true;
                ApplyConfig();
            })));
            return st;
        }

        // ---------- 常用语 ----------

        Control BuildSnippets()
        {
            var book = SnippetBook.Current;
            var root = new TableLayoutPanel { ColumnCount = 2, RowCount = 3 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var head = Stack(); head.AutoSize = true; head.AutoScroll = false;
            head.Controls.Add(Title("号码和邮箱，按一个键"));
            head.Controls.Add(Note("长按 Alt 再按分类序号打开面板，按 A、S、D… 直接上屏。填了「编码」的条目，打拼音时也会出现在候选里。"));
            root.Controls.Add(head, 0, 0); root.SetColumnSpan(head, 2);

            var cats = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = true, AllowUserToDeleteRows = true, RowHeadersWidth = 30,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = SystemColors.Window, BorderStyle = BorderStyle.FixedSingle,
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "键", ReadOnly = true, FillWeight = 8 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "内容", FillWeight = 70 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "编码（可选）", FillWeight = 22 });
            int current = -1;

            void Store()
            {
                if (current < 0 || current >= book.Categories.Count) return;
                grid.EndEdit();
                var items = new List<Snippet>();
                foreach (DataGridViewRow r in grid.Rows)
                {
                    if (r.IsNewRow) continue;
                    var text = r.Cells[1].Value as string;
                    if (string.IsNullOrEmpty(text)) continue;
                    items.Add(new Snippet { Text = text, Code = (r.Cells[2].Value as string ?? "").Trim() });
                }
                book.Categories[current].Items = items;
            }
            void Load(int i)
            {
                Store();
                current = i; grid.Rows.Clear();
                if (i < 0 || i >= book.Categories.Count) return;
                var items = book.Categories[i].Items;
                for (int k = 0; k < items.Count; k++)
                    grid.Rows.Add(k < SnippetBook.ItemKeys.Length ? SnippetBook.ItemKeys[k].ToString() : "", items[k].Text, items[k].Code);
            }
            void FillCats(int select)
            {
                cats.Items.Clear();
                for (int i = 0; i < book.Categories.Count; i++) cats.Items.Add((i + 1) + "  " + book.Categories[i].Name);
                if (book.Categories.Count > 0) cats.SelectedIndex = Math.Min(Math.Max(0, select), book.Categories.Count - 1);
            }
            cats.SelectedIndexChanged += (s, e) => { if (cats.SelectedIndex != current) Load(cats.SelectedIndex); };
            FillCats(0);
            root.Controls.Add(cats, 0, 1);
            root.Controls.Add(grid, 1, 1);

            string Ask(string title, string value)
            {
                using (var f = new Form { Text = title, ClientSize = new Size(320, 90), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, Font = UiFont })
                {
                    var tb = new TextBox { Text = value, Left = 12, Top = 14, Width = 296 };
                    var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Left = 228, Top = 50, Width = 80 };
                    f.Controls.Add(tb); f.Controls.Add(ok); f.AcceptButton = ok;
                    return f.ShowDialog(this) == DialogResult.OK ? tb.Text.Trim() : null;
                }
            }
            var buttons = Row(
                Btn("添加分类", (s, e) => { var n = Ask("新分类", ""); if (string.IsNullOrEmpty(n)) return; Store(); book.Categories.Add(new SnippetCategory { Name = n }); current = -1; FillCats(book.Categories.Count - 1); }),
                Btn("重命名", (s, e) => { if (current < 0) return; var n = Ask("重命名分类", book.Categories[current].Name); if (string.IsNullOrEmpty(n)) return; book.Categories[current].Name = n; var c = current; Store(); current = -1; FillCats(c); }),
                Btn("删除分类", (s, e) => { if (current < 0 || MessageBox.Show("删除这个分类和其中的内容？", "常用语", MessageBoxButtons.OKCancel) != DialogResult.OK) return; book.Categories.RemoveAt(current); current = -1; FillCats(0); }),
                Btn("上移", (s, e) => { if (current <= 0) return; Store(); var c = current; var t = book.Categories[c]; book.Categories.RemoveAt(c); book.Categories.Insert(c - 1, t); current = -1; FillCats(c - 1); }),
                Btn("保存", (s, e) => { Store(); book.Save(); _status.Text = "常用语已保存"; MessageBox.Show("常用语已保存。", "一维输入法"); }));
            root.Controls.Add(buttons, 0, 2); root.SetColumnSpan(buttons, 2);
            FormClosing += (s, e) => { Store(); };
            return root;
        }

        // ---------- AI ----------

        static readonly (string Name, string Url, string Model)[] Presets =
        {
            ("OpenAI", "https://api.openai.com/v1", "gpt-4o-mini"),
            ("DeepSeek", "https://api.deepseek.com/v1", "deepseek-chat"),
            ("通义千问（阿里云百炼）", "https://dashscope.aliyuncs.com/compatible-mode/v1", "qwen-plus"),
            ("Kimi（月之暗面）", "https://api.moonshot.cn/v1", "moonshot-v1-8k"),
            ("智谱 GLM", "https://open.bigmodel.cn/api/paas/v4", "glm-4-flash"),
            ("本机 Ollama（免费、离线）", "http://localhost:11434/v1", "qwen2.5:7b"),
            ("本机 LM Studio", "http://localhost:1234/v1", "local-model"),
        };

        Control BuildAi()
        {
            var st = Stack();
            st.Controls.Add(Title("翻译和润色，在光标处完成"));
            st.Controls.Add(Note("选中一段话，长按 Alt 再按空格。只处理选中的这一段，只发给你在这里配置的接口（可能计费）；用本机 Ollama 则完全不出电脑。"));
            var preset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
            preset.Items.Add("选择服务商快速填写…"); foreach (var p in Presets) preset.Items.Add(p.Name); preset.SelectedIndex = 0;
            var url = new TextBox { Text = S.AiBaseUrl, Width = 420 };
            var model = new TextBox { Text = S.AiModel, Width = 220 };
            var key = new TextBox { Text = S.AiKey, Width = 420, UseSystemPasswordChar = true };
            preset.SelectedIndexChanged += (s, e) => { if (preset.SelectedIndex > 0) { var p = Presets[preset.SelectedIndex - 1]; url.Text = p.Url; model.Text = p.Model; } };
            st.Controls.Add(Row(Lbl("服务商"), preset));
            st.Controls.Add(Row(Lbl("接口地址"), url));
            st.Controls.Add(Row(Lbl("模型　　"), model));
            st.Controls.Add(Row(Lbl("API 密钥"), key));
            st.Controls.Add(Note("密钥用 Windows 数据保护加密后只存在这台电脑上。"));

            var grid = new DataGridView { Width = 760, Height = 170, AllowUserToAddRows = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersWidth = 30, BackgroundColor = SystemColors.Window };
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "动作", FillWeight = 18 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "给 AI 的要求", FillWeight = 82 });
            foreach (var a in S.AiActions) grid.Rows.Add(a.Name, a.Prompt);
            st.Controls.Add(new Label { Text = "动作（面板里按数字选择，最多 9 个）", AutoSize = true, Margin = new Padding(0, 6, 0, 4) });
            st.Controls.Add(grid);

            var hold = new NumericUpDown { Minimum = 150, Maximum = 1000, Increment = 50, Value = Clamp(S.HoldMs, 150, 1000), Width = 70 };
            var snip = new CheckBox { Text = "长按 Alt + 数字 打开常用语", Checked = S.SnippetsHotkey, AutoSize = true };
            var aik = new CheckBox { Text = "长按 Alt + 空格 打开 AI", Checked = S.AiHotkey, AutoSize = true };
            st.Controls.Add(Row(snip, aik, Lbl("　长按判定"), hold, Lbl("毫秒")));

            void Store()
            {
                S.AiBaseUrl = url.Text.Trim(); S.AiModel = model.Text.Trim(); S.AiKey = key.Text.Trim();
                S.HoldMs = (int)hold.Value; S.SnippetsHotkey = snip.Checked; S.AiHotkey = aik.Checked;
                grid.EndEdit();
                S.AiActions = grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow && !string.IsNullOrWhiteSpace(r.Cells[0].Value as string))
                    .Select(r => new AiAction { Name = (r.Cells[0].Value as string).Trim(), Prompt = (r.Cells[1].Value as string ?? "").Trim() }).ToList();
                S.Save();
            }
            st.Controls.Add(Row(
                Btn("保存", (s, e) => { Store(); _status.Text = "AI 设置已保存"; MessageBox.Show("已保存。", "一维输入法"); }),
                Btn("测试连接", async (s, e) =>
                {
                    Store();
                    try { var r = await Ai.Run("把用户的话翻译成英文，只输出译文。", "你好，世界"); MessageBox.Show("连接成功：" + r, "一维输入法"); }
                    catch (Exception ex) { MessageBox.Show("连接失败：" + ex.GetBaseException().Message, "一维输入法"); }
                }),
                Btn("恢复默认动作", (s, e) => { grid.Rows.Clear(); foreach (var a in Settings.DefaultActions()) grid.Rows.Add(a.Name, a.Prompt); })));
            return st;
        }

        // ---------- 词库 ----------

        Control BuildDicts()
        {
            var st = Stack();
            st.Controls.Add(Title("词库每天自动更新，校验后才会装"));
            st.Controls.Add(Note("默认方案是雾凇拼音（全拼，另含小鹤、自然码、微软、搜狗等双拼）。每天检查一次新词库，SHA-256 校验通过后才安装并重新部署。另附 2026 年科技与互联网补充词库。"));
            var auto = new CheckBox { Text = "每天自动更新雾凇拼音词库", Checked = S.DictAutoUpdate, AutoSize = true };
            auto.CheckedChanged += (s, e) => { S.DictAutoUpdate = auto.Checked; S.Save(); };
            st.Controls.Add(auto);
            var info = new Label { AutoSize = true, Margin = new Padding(0, 8, 0, 8), Text = string.IsNullOrEmpty(S.DictTag) ? "当前：随安装包附带的版本" : "当前：" + S.DictTag.Split('@')[0] + "（" + S.DictCheckedAt + " 检查）" };
            st.Controls.Add(info);
            Button now = null;
            now = Btn("立即检查更新", async (s, e) =>
            {
                now.Enabled = false; info.Text = "正在检查…";
                try { var r = await DictUpdater.Check(false); info.Text = r.Message; }
                catch (Exception ex) { info.Text = "更新失败：" + ex.GetBaseException().Message; }
                now.Enabled = true;
            });
            st.Controls.Add(Row(now, Btn("输入方案选单（全拼 / 双拼）", (s, e) => Open(Paths.Deployer))));
            var trad = new CheckBox { Text = "默认输出繁体（简繁转换在本机完成，不需要 AI）", Checked = S.Traditional, AutoSize = true, Margin = new Padding(0, 14, 0, 0) };
            trad.CheckedChanged += (s, e) => { S.Traditional = trad.Checked; ApplyConfig(); };
            st.Controls.Add(trad);
            return st;
        }

        // ---------- 统计 ----------

        Control BuildStats()
        {
            var st = Stack();
            st.Controls.Add(Title("这一年打了多少字"));
            st.Controls.Add(Note("字数、时段、用得最多的应用和词。只存在这台电脑上，可以随时清空。默认关闭，开启后开始记录。"));
            var on = new CheckBox { Text = "记录输入统计", Checked = S.StatsEnabled, AutoSize = true };
            on.CheckedChanged += (s, e) => S.StatsEnabled = on.Checked;
            st.Controls.Add(on);
            var nums = new Label { AutoSize = true, Font = new Font(UiFont.FontFamily, 11f), Margin = new Padding(0, 10, 0, 6) };
            var chart = new BarChart { Width = 760, Height = 150 };
            var hours = new BarChart { Width = 760, Height = 100 };
            var apps = new ListBox { Width = 370, Height = 150 };
            var words = new ListBox { Width = 370, Height = 150 };
            void Refresh()
            {
                var r = Stats.Build();
                nums.Text = $"今天 {r.Today:N0} 字　本周 {r.ThisWeek:N0} 字　今年 {r.ThisYear:N0} 字　累计 {r.Characters:N0} 字";
                var days = Enumerable.Range(0, 30).Select(i => DateTime.Today.AddDays(i - 29)).ToList();
                chart.Values = days.Select(d => r.ByDay.TryGetValue(d, out var v) ? v : 0).ToArray();
                chart.Labels = days.Select(d => d.Day == 1 || d == days[0] || d == days.Last() ? d.ToString("M/d") : "").ToArray();
                chart.Caption = "最近 30 天"; chart.Invalidate();
                hours.Values = r.ByHour; hours.Labels = Enumerable.Range(0, 24).Select(h => h % 3 == 0 ? h + "时" : "").ToArray(); hours.Caption = "按时段"; hours.Invalidate();
                apps.Items.Clear(); foreach (var a in r.TopApps) apps.Items.Add($"{a.Key}　{a.Value:N0} 字");
                if (apps.Items.Count == 0) apps.Items.Add("（还没有数据）");
                words.Items.Clear(); foreach (var w in r.TopWords) words.Items.Add($"{w.Key}　{w.Value} 次");
                if (words.Items.Count == 0) words.Items.Add("（还没有数据）");
            }
            st.Controls.Add(nums); st.Controls.Add(chart); st.Controls.Add(hours);
            st.Controls.Add(Row(new Label { Text = "用得最多的应用", AutoSize = true, Width = 370 }, new Label { Text = "　　　　　　　　　　　　　　　常用词", AutoSize = true }));
            st.Controls.Add(Row(apps, words));
            st.Controls.Add(Row(Btn("刷新", (s, e) => Refresh()), Btn("清空统计", (s, e) =>
            {
                if (MessageBox.Show("清空所有输入统计？", "一维输入法", MessageBoxButtons.OKCancel) == DialogResult.OK) { Stats.Clear(); Refresh(); }
            })));
            VisibleChanged += (s, e) => { if (Visible) Refresh(); };
            _tabs.SelectedIndexChanged += (s, e) => { if (_tabs.SelectedTab?.Text == "统计") Refresh(); };
            return st;
        }

        sealed class BarChart : Control
        {
            public long[] Values = new long[0]; public string[] Labels = new string[0]; public string Caption = "";
            public BarChart() { DoubleBuffered = true; }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics; g.Clear(SystemColors.Window); g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var f = new Font("Microsoft YaHei UI", 8.5f))
                {
                    TextRenderer.DrawText(g, Caption, f, new Point(0, 0), SystemColors.GrayText);
                    if (Values.Length == 0) return;
                    long max = Math.Max(1, Values.Max());
                    float top = 18, bottom = Height - 18, slot = (float)Width / Values.Length;
                    using (var b = new SolidBrush(Color.FromArgb(196, 80, 63)))
                        for (int i = 0; i < Values.Length; i++)
                        {
                            float h = (bottom - top) * Values[i] / max;
                            if (Values[i] > 0 && h < 2) h = 2;
                            g.FillRectangle(b, i * slot + slot * 0.18f, bottom - h, slot * 0.64f, h);
                            if (i < Labels.Length && Labels[i].Length > 0) TextRenderer.DrawText(g, Labels[i], f, new Point((int)(i * slot), (int)bottom + 2), SystemColors.GrayText);
                        }
                }
            }
        }

        // ---------- 导入 ----------

        Control BuildImport()
        {
            var st = Stack();
            st.Controls.Add(Title("从小狼毫搬家"));
            st.Controls.Add(Note("读取小狼毫的用户文件夹（" + Paths.LegacyWeaselUserDir + "），复制自定义方案、补丁、词库和同步快照到一维输入法。小狼毫的界面补丁另存为 weasel.custom.yaml.imported 供参考。"));
            st.Controls.Add(Btn("从小狼毫导入", (s, e) =>
            {
                try { var n = WeaselImport.Run(); MessageBox.Show($"已导入 {n} 个文件，正在重新部署。", "一维输入法"); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "一维输入法"); }
            }));
            st.Controls.Add(Title("导入主题链接"));
            st.Controls.Add(Note("粘贴 yiwei-ime://theme?… 链接（也兼容 AIME 主题包内容，把 aime-ime:// 换成 yiwei-ime:// 即可）。"));
            var link = new TextBox { Width = 620 };
            st.Controls.Add(Row(link, Btn("导入", (s, e) =>
            {
                var t = link.Text.Trim(); if (t.StartsWith("aime-ime://")) t = "yiwei-ime://" + t.Substring(11);
                ThemeLinks.Handle(t);
            })));
            return st;
        }

        // ---------- 关于 ----------

        Control BuildAbout()
        {
            var st = Stack();
            st.Controls.Add(Title("一维输入法 " + Application.ProductVersion));
            st.Controls.Add(Note("中文常新，自在表达。开源，注重隐私，基于 RIME。"));
            st.Controls.Add(Note(
                "开源与致谢\n" +
                "· 小狼毫 Weasel（GPL-3.0）— Windows 输入法前端，一维输入法在其基础上修改\n" +
                "· librime（BSD-3-Clause）— RIME 输入法引擎\n" +
                "· 雾凇拼音 rime-ice（GPL-3.0）— 默认方案与词库\n" +
                "· OpenCC（Apache-2.0）— 简繁转换\n" +
                "· AIME 艾么输入法（MIT, © 2026 ZOOL LLC）— 产品设计、配色与科技词库的来源\n" +
                "一维输入法整体以 GPL-3.0 发布。"));
            st.Controls.Add(Row(Btn("项目主页", (s, e) => Open("https://github.com/yiweishurufa/yiwei-ime")),
                Btn("问题反馈", (s, e) => Open("https://github.com/yiweishurufa/yiwei-ime/issues"))));
            return st;
        }
    }
}
