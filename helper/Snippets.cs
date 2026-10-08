using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Yiwei
{
    public class Snippet
    {
        public string Text { get; set; } = "";
        /// <summary>Optional RIME code: typing it offers the snippet as a candidate (custom phrase).</summary>
        public string Code { get; set; } = "";
    }

    public class SnippetCategory
    {
        public string Name { get; set; } = "";
        public List<Snippet> Items { get; set; } = new List<Snippet>();
    }

    public class SnippetBook
    {
        public List<SnippetCategory> Categories { get; set; } = new List<SnippetCategory>();

        public static readonly string ItemKeys = "ASDFGHJKLQWERTYUIOPZXCVBNM";

        static SnippetBook _current;
        public static SnippetBook Current
        {
            get
            {
                if (_current != null) return _current;
                _current = File.Exists(Paths.SnippetsFile) ? Json.Load<SnippetBook>(Paths.SnippetsFile) : Defaults();
                if (_current.Categories.Count == 0) _current = Defaults();
                return _current;
            }
        }

        public void Save()
        {
            Json.Save(Paths.SnippetsFile, this);
            ExportCustomPhrases();
        }

        public static SnippetBook Defaults() => new SnippetBook
        {
            Categories =
            {
                new SnippetCategory { Name = "手机号" },
                new SnippetCategory { Name = "邮箱" },
                new SnippetCategory { Name = "地址" },
                new SnippetCategory
                {
                    Name = "符号",
                    Items = "→ ← ↑ ↓ ✓ ✗ ※ · — … 「」 『』 《》 【】 ° ℃ ± × ÷ ≈ ≠ ≤ ≥ ∞ ¥ € £ $ © ® ™ ★ ☆ ♥ ①"
                        .Split(' ').Select(t => new Snippet { Text = t }).ToList()
                },
            }
        };

        const string Begin = "# >>> 一维输入法常用语（由设置生成，请在「一维输入法设置 → 常用语」里修改）";
        const string End = "# <<< 一维输入法常用语";

        /// <summary>Snippets with a code become RIME custom phrases (user copy of custom_phrase.txt).</summary>
        public void ExportCustomPhrases()
        {
            try
            {
                var user = Path.Combine(Paths.UserDir, "custom_phrase.txt");
                string baseText;
                if (File.Exists(user)) baseText = File.ReadAllText(user, Encoding.UTF8);
                else
                {
                    var shared = Path.Combine(Paths.SharedDataDir, "custom_phrase.txt");
                    baseText = File.Exists(shared) ? File.ReadAllText(shared, Encoding.UTF8) : "";
                }
                int b = baseText.IndexOf(Begin, StringComparison.Ordinal), e = baseText.IndexOf(End, StringComparison.Ordinal);
                if (b >= 0 && e > b) baseText = baseText.Remove(b, e + End.Length - b);
                baseText = baseText.TrimEnd('\r', '\n') + "\n";

                var sb = new StringBuilder();
                foreach (var c in Categories)
                    foreach (var s in c.Items.Where(i => !string.IsNullOrWhiteSpace(i.Code) && !string.IsNullOrEmpty(i.Text)))
                        sb.Append(s.Text.Replace("\t", " ").Replace("\r", "").Replace("\n", " ")).Append('\t')
                          .Append(s.Code.Trim().ToLowerInvariant()).Append("\t100\n");
                if (sb.Length == 0 && b < 0) return;
                File.WriteAllText(user, baseText + Begin + "\n" + sb + End + "\n", new UTF8Encoding(false));
                Deploy.Run();
            }
            catch (Exception ex) { Log.Write("custom phrase: " + ex.Message); }
        }
    }
}
