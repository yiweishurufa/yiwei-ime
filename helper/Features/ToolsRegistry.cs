using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Yiwei
{
    /// <summary>Every tool action of the 工具 group, in menu order. Starters run once at helper start.</summary>
    static class ToolsRegistry
    {
        public static void RegisterAll(List<Action> starters)
        {
            ToolsHub.Register(new ToolAction { Id = "text-menu", Name = "文字工具菜单", Group = "菜单", AltKey = Keys.T, InMenu = false, Run = TextToolsMenu.Show,
                Help = "在光标处弹出文字工具菜单（翻译、拼音、繁简、大小写、Markdown…）" });
            // W29 截图翻译 / 翻译选中文字
            ToolsHub.Register(new ToolAction { Id = "translate-shot", Name = "截图翻译", Group = "翻译", AltKey = Keys.S, Run = ScreenTranslate.Start,
                Help = "框选屏幕上的文字，离线识别后翻译（中文→英文，其它→中文）" });
            ToolsHub.Register(new ToolAction { Id = "translate-sel", Name = "翻译选中文字", Group = "翻译", Run = ScreenTranslate.Selection,
                Help = "翻译选中的文字，可以直接替换原文" });
            // W30 加注拼音
            ToolsHub.Register(new ToolAction { Id = "pinyin", Name = "加注拼音", Group = "文字", Help = "给选中的汉字加上拼音（多音字按词读；样式在 设置 → 工具 里选）",
                Run = () => ToolsHub.TransformSelection("加注拼音", t => PinyinAnnotate.Annotate(t, ToolSettings.Current.PinyinStyle)) });
            ToolsHub.Register(new ToolAction { Id = "pinyin-only", Name = "转成拼音", Group = "文字", Help = "把选中的汉字换成拼音",
                Run = () => ToolsHub.TransformSelection("转成拼音", t => PinyinAnnotate.Annotate(t, "only")) });
            // W31 繁简互转
            ToolsHub.Register(new ToolAction { Id = "to-simp", Name = "转为简体", Group = "繁简", Help = "选中文字繁体→简体（Windows 系统转换，离线）",
                Run = () => ToolsHub.TransformSelection("转为简体", TextConvert.ToSimplified) });
            ToolsHub.Register(new ToolAction { Id = "to-trad", Name = "转为繁体", Group = "繁简", Help = "选中文字简体→繁体（Windows 系统转换，离线）",
                Run = () => ToolsHub.TransformSelection("转为繁体", TextConvert.ToTraditional) });
            // W32 大小写、全半角、中英标点
            void T(string id, string name, string help, Func<string, string> f) =>
                ToolsHub.Register(new ToolAction { Id = id, Name = name, Group = "大小写", Help = help, Run = () => ToolsHub.TransformSelection(name, f) });
            T("upper", "全部大写", "abc → ABC", TextConvert.Upper);
            T("lower", "全部小写", "ABC → abc", TextConvert.Lower);
            T("title", "单词首字母大写", "hello world → Hello World", TextConvert.Title);
            T("toggle-case", "大小写互换", "全大写变全小写，否则变全大写", TextConvert.ToggleCase);
            T("fullwidth", "转为全角", "ABC 123 → ＡＢＣ　１２３", TextConvert.FullWidth);
            T("halfwidth", "转为半角", "ＡＢＣ　１２３ → ABC 123", TextConvert.HalfWidth);
            T("punct-ascii", "中文标点→英文标点", "，。！？ → ,.!?", TextConvert.PunctToAscii);
            T("punct-zh", "英文标点→中文标点", ",.!?\"\" → ，。！？“”", TextConvert.PunctToChinese);
            // W33 Markdown 快捷符号
            ToolsHub.Register(new ToolAction { Id = "markdown", Name = "Markdown 符号…", Group = "Markdown", AltKey = Keys.M, Run = MarkdownTools.ShowMenu,
                Help = "加粗、斜体、代码、链接、标题、列表、待办、引用、表格：选中文字就包起来，没选中就插入" });
            // W34 与 Win+V 联动
            ToolsHub.Register(new ToolAction { Id = "clip-system", Name = "系统剪贴板历史 (Win+V)", Group = "剪贴板", Run = WinClipboardLink.OpenSystemPanel,
                Help = "打开 Windows 自带的剪贴板历史；没开启时可以一键开启。一维自己的剪贴板历史是长按 Alt+V" });
            starters.Add(WinClipboardLink.Start);
            // W35 邮箱后缀、路径补全
            ToolsHub.Register(new ToolAction { Id = "complete", Name = "补全邮箱/路径", Group = "补全", AltKey = Keys.OemQuestion, Run = Completion.Run,
                Help = "光标前是「name@」时列出邮箱后缀，是「C:\\…」「%APPDATA%\\」「~\\」时列出文件和文件夹" });
            // W36 WebDAV 同步
            ToolsHub.Register(new ToolAction { Id = "webdav-sync", Name = "WebDAV 同步词库和常用语", Group = "同步", InMenu = false, Help = "坚果云等 WebDAV 网盘，设置 → 工具 里填账号",
                Run = async () => { try { Tray.Balloon("WebDAV 同步", await WebDavSync.SyncAsync()); } catch (Exception e) { Tray.Balloon("WebDAV 同步失败", e.GetBaseException().Message); } } });
            starters.Add(WebDavSync.Start);
            // W37 自动备份
            ToolsHub.Register(new ToolAction { Id = "backup-now", Name = "立即备份词库", Group = "同步", InMenu = false, Help = "导出一份 .yiwei-backup 到自动备份文件夹",
                Run = () => Task.Run(() => { try { var f = AutoBackup.RunNow(); Tray.Balloon("备份完成", f); } catch (Exception e) { Tray.Balloon("备份失败", e.Message); } }) });
            starters.Add(AutoBackup.Start);
        }
    }

    /// <summary>长按 Alt + T：在光标处弹出文字工具菜单（不抢焦点，点一下对选中文字生效）。</summary>
    static class TextToolsMenu
    {
        static ContextMenuStrip _menu;

        public static void Show()
        {
            _menu?.Close();
            var m = new ContextMenuStrip { ShowImageMargin = false, Font = new Font("Microsoft YaHei UI", 9.5f) };
            string group = null;
            foreach (var a in ToolsHub.Actions.Where(x => x.InMenu))
            {
                if (group != null && a.Group != group) m.Items.Add(new ToolStripSeparator());
                group = a.Group;
                var id = a.Id;
                var hk = ToolSettings.Current.Hotkeys.TryGetValue(id, out var h) && !string.IsNullOrEmpty(h) ? h : a.AltKey != Keys.None ? "长按 Alt+" + KeyName(a.AltKey) : "";
                var item = new ToolStripMenuItem(a.Name, null, (s, e) => Program.Ui.BeginInvoke(new Action(() => { TextOut.Pump(60); ToolsHub.Run(id); }))) { ShortcutKeyDisplayString = hk, ToolTipText = a.Help };
                m.Items.Add(item);
            }
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("工具设置…", null, (s, e) => Program.ShowSettings("工具"));
            _menu = m;
            var p = Native.CaretPosition();
            m.Show(new Point(p.X, p.Y + 22));
        }

        public static string KeyName(Keys k) => k == Keys.OemQuestion ? "/" : k >= Keys.D0 && k <= Keys.D9 ? ((char)('0' + (k - Keys.D0))).ToString() : k.ToString();
    }
}
