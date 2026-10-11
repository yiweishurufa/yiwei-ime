using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Yiwei
{
    /// <summary>
    /// W33 Markdown 快捷符号：长按 Alt + M 在光标处弹出菜单。选中了文字就包起来（加粗、斜体、代码、链接…，
    /// 标题/列表/引用按行加前缀）；没选中就插入一对符号并把光标放在中间。
    /// </summary>
    public static class MarkdownTools
    {
        sealed class Item { public string Name, Before, After, Line, Key; }

        static readonly Item[] Items =
        {
            new Item { Name = "加粗 **文字**", Before = "**", After = "**", Key = "B" },
            new Item { Name = "斜体 *文字*", Before = "*", After = "*", Key = "I" },
            new Item { Name = "删除线 ~~文字~~", Before = "~~", After = "~~", Key = "S" },
            new Item { Name = "行内代码 `代码`", Before = "`", After = "`", Key = "C" },
            new Item { Name = "代码块 ```", Before = "```\n", After = "\n```", Key = "K" },
            new Item { Name = "链接 [文字](网址)", Before = "[", After = "]()", Key = "L" },
            new Item { Name = "图片 ![说明](网址)", Before = "![", After = "]()", Key = "P" },
            new Item { Name = "一级标题 #", Line = "# ", Key = "1" },
            new Item { Name = "二级标题 ##", Line = "## ", Key = "2" },
            new Item { Name = "三级标题 ###", Line = "### ", Key = "3" },
            new Item { Name = "无序列表 -", Line = "- ", Key = "U" },
            new Item { Name = "有序列表 1.", Line = "1. ", Key = "O" },
            new Item { Name = "待办 - [ ]", Line = "- [ ] ", Key = "T" },
            new Item { Name = "引用 >", Line = "> ", Key = "Q" },
            new Item { Name = "分隔线 ---", Before = "\n---\n", After = "", Key = "H" },
            new Item { Name = "表格（3 列）", Before = "| 列1 | 列2 | 列3 |\n| --- | --- | --- |\n| ", After = " |  |  |", Key = "A" },
        };

        public static void ShowMenu()
        {
            var m = new ContextMenuStrip { ShowImageMargin = false, Font = new Font("Microsoft YaHei UI", 9.5f) };
            foreach (var it in Items)
            {
                var item = it;
                if (it.Name.StartsWith("一级标题") || it.Name.StartsWith("无序列表") || it.Name.StartsWith("分隔线")) m.Items.Add(new ToolStripSeparator());
                m.Items.Add(new ToolStripMenuItem("&" + it.Key + "  " + it.Name, null, (s, e) => Program.Ui.BeginInvoke(new Action(() => { TextOut.Pump(60); Apply(item); }))));
            }
            var p = Native.CaretPosition();
            m.Show(new Point(p.X, p.Y + 22));
        }

        static void Apply(Item it)
        {
            string sel = "";
            try { sel = TextOut.CopySelection(); } catch { }
            if (it.Line != null)
            {
                if (sel.Length == 0) { TextOut.Type(it.Line); return; }
                var lines = sel.Replace("\r\n", "\n").Split('\n');
                int n = 0;
                var outp = lines.Select(l => l.Trim().Length == 0 ? l : (it.Line == "1. " ? (++n) + ". " : it.Line) + l);
                TextOut.Replace(string.Join(Environment.NewLine, outp));
                return;
            }
            if (sel.Length > 0) { TextOut.Replace(it.Before.Replace("\n", Environment.NewLine) + sel + it.After.Replace("\n", Environment.NewLine)); return; }
            TextOut.Type(it.Before + it.After);
            // put the caret back between the two halves (line breaks count as one step in most editors)
            for (int i = 0; i < it.After.Length; i++) TextOut.Left();
        }
    }
}
