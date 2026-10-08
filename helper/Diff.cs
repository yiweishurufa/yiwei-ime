using System;
using System.Collections.Generic;
using System.Text;

namespace Yiwei
{
    /// <summary>Word-level diff for the 润色 result: Chinese characters count as words, Latin runs as words.</summary>
    public static class TextDiff
    {
        public enum Op { Same, Del, Ins }
        public struct Piece { public Op Op; public string Text; public Piece(Op op, string t) { Op = op; Text = t; } }

        public static List<string> Tokenize(string s)
        {
            var list = new List<string>();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (char.IsLetterOrDigit(c) && c < 0x2E80)
                {
                    int j = i; while (j < s.Length && (char.IsLetterOrDigit(s[j]) && s[j] < 0x2E80 || s[j] == '\'' || s[j] == '-')) j++;
                    list.Add(s.Substring(i, j - i)); i = j;
                }
                else if (char.IsWhiteSpace(c))
                {
                    int j = i; while (j < s.Length && char.IsWhiteSpace(s[j])) j++;
                    list.Add(s.Substring(i, j - i)); i = j;
                }
                else if (char.IsHighSurrogate(c) && i + 1 < s.Length) { list.Add(s.Substring(i, 2)); i += 2; }
                else { list.Add(c.ToString()); i++; }
            }
            return list;
        }

        public static List<Piece> Compute(string before, string after)
        {
            var a = Tokenize(before ?? ""); var b = Tokenize(after ?? "");
            var result = new List<Piece>();
            if ((long)a.Count * b.Count > 4_000_000)
            {
                result.Add(new Piece(Op.Del, before)); result.Add(new Piece(Op.Ins, after)); return result;
            }
            // LCS table from the end
            var dp = new int[a.Count + 1, b.Count + 1];
            for (int i = a.Count - 1; i >= 0; i--)
                for (int j = b.Count - 1; j >= 0; j--)
                    dp[i, j] = a[i] == b[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            int x = 0, y = 0;
            while (x < a.Count && y < b.Count)
            {
                if (a[x] == b[y]) { Add(result, Op.Same, a[x]); x++; y++; }
                else if (dp[x + 1, y] >= dp[x, y + 1]) { Add(result, Op.Del, a[x]); x++; }
                else { Add(result, Op.Ins, b[y]); y++; }
            }
            while (x < a.Count) Add(result, Op.Del, a[x++]);
            while (y < b.Count) Add(result, Op.Ins, b[y++]);
            return result;
        }

        static void Add(List<Piece> list, Op op, string t)
        {
            if (list.Count > 0 && list[list.Count - 1].Op == op) { var p = list[list.Count - 1]; p.Text += t; list[list.Count - 1] = p; }
            else list.Add(new Piece(op, t));
        }
    }
}
