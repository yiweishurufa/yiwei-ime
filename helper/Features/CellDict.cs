using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Yiwei
{
    /// <summary>读取搜狗细胞词库（.scel）：纯本机解析，输出「词 拼音」行，供「我的词条」导入。</summary>
    static class CellDict
    {
        public static List<string> Parse(byte[] d)
        {
            var res = new List<string>();
            if (d.Length < 0x2700 || d[0] != 0x40 || d[1] != 0x15) throw new InvalidDataException("不是搜狗细胞词库（.scel）");
            var py = new Dictionary<int, string>();
            int p = 0x1540 + 4;
            while (p + 4 <= 0x2628)
            {
                int idx = BitConverter.ToUInt16(d, p), len = BitConverter.ToUInt16(d, p + 2);
                p += 4;
                if (len <= 0 || p + len > d.Length) break;
                py[idx] = Encoding.Unicode.GetString(d, p, len);
                p += len;
            }
            p = 0x2628;
            while (p + 4 <= d.Length)
            {
                int same = BitConverter.ToUInt16(d, p), pyLen = BitConverter.ToUInt16(d, p + 2);
                p += 4;
                if (pyLen <= 0 || p + pyLen > d.Length) break;
                var parts = new List<string>();
                bool ok = true;
                for (int i = 0; i + 1 < pyLen; i += 2)
                {
                    if (py.TryGetValue(BitConverter.ToUInt16(d, p + i), out var s)) parts.Add(s); else ok = false;
                }
                p += pyLen;
                for (int k = 0; k < same; k++)
                {
                    if (p + 2 > d.Length) return res;
                    int wl = BitConverter.ToUInt16(d, p); p += 2;
                    if (p + wl > d.Length) return res;
                    var w = Encoding.Unicode.GetString(d, p, wl); p += wl;
                    if (p + 2 > d.Length) return res;
                    int el = BitConverter.ToUInt16(d, p); p += 2 + el;
                    if (ok && w.Length >= 2 && w.Length == parts.Count) res.Add(w + " " + string.Join(" ", parts));
                }
            }
            return res;
        }
    }
}
