using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Yiwei
{
    /// <summary>
    /// 32 轻量版词库：「标准」不挂雾凇的腾讯词向量大词库（cn_dicts/tencent，约 50 万词、17 MB），部署快很多、内存更少；
    /// 「完整」照雾凇默认。做法：在用户文件夹放一份 rime_ice.dict.yaml（用户文件夹优先于安装目录，词库更新也是这样放的），
    /// 只把 import_tables 里 tencent 那一行注释掉，其它不动；词库名不变，用户词库和各种反查都不受影响。
    /// </summary>
    static class DictLite
    {
        const string Off = "  # - cn_dicts/tencent  # 一维输入法：标准词库不含腾讯词向量（设置 → 词库 可改回完整）";
        static readonly Regex OnLine = new Regex(@"^  - cn_dicts/tencent\b.*$", RegexOptions.Multiline);
        static readonly Regex OffLine = new Regex(@"^  # - cn_dicts/tencent  # 一维输入法：标准词库不含.*$", RegexOptions.Multiline);

        static string UserFile => Path.Combine(Paths.UserDir, "rime_ice.dict.yaml");
        static string SharedFile => Path.Combine(Paths.SharedDataDir, "rime_ice.dict.yaml");

        /// <summary>和 scripts/rime_test 里的 lite 用例同一规则。</summary>
        public static string Transform(string text, bool lite) =>
            lite ? OnLine.Replace(text, Off) : OffLine.Replace(text, "  - cn_dicts/tencent  # 腾讯词向量（大词库，部署时间较长）");

        public static bool IsLiteNow()
        {
            try { return File.Exists(UserFile) && OffLine.IsMatch(File.ReadAllText(UserFile, Encoding.UTF8)); }
            catch { return false; }
        }

        /// <summary>按设置改写用户文件夹里的 rime_ice.dict.yaml；有变化返回 true（调用方负责重新部署）。</summary>
        public static bool Apply(bool lite)
        {
            try
            {
                bool hasUser = File.Exists(UserFile);
                if (!hasUser && !lite) return false; // 完整版 = 直接用安装目录的
                var src = hasUser ? UserFile : SharedFile;
                if (!File.Exists(src)) return false;
                var text = File.ReadAllText(src, Encoding.UTF8);
                var next = Transform(text, lite);
                if (next == text && hasUser) return false;
                Directory.CreateDirectory(Paths.UserDir);
                File.WriteAllText(UserFile, next, new UTF8Encoding(false));
                return true;
            }
            catch (Exception e) { Log.Write("dict lite: " + e.Message); return false; }
        }
    }
}
