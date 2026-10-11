using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
