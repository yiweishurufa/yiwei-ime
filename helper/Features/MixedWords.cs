using System;
using System.IO;
using System.Text;

namespace Yiwei
{
    /// <summary>
    /// backlog 7 中英混输词条：词条随安装包放在共享目录的 yiwei_mixed.txt（table_translator@yiwei_mixed + yiwei_mixed_filter.lua）。
    /// 关掉 = 在用户文件夹放一份只有说明、没有词条的 yiwei_mixed.txt（用户文件夹优先），打开 = 删掉这份。
    /// 和 scripts/rime_test 里 mixed_words 用例同一规则。
    /// </summary>
    static class MixedWords
    {
        public const string OffMarker = "# 一维输入法：中英混输词条已关闭（设置 → 词库 可重新打开；删掉本文件也会恢复）";
        static string UserFile => Path.Combine(Paths.UserDir, "yiwei_mixed.txt");

        /// <summary>按设置放置/删除用户文件夹里的空词表；有变化返回 true（调用方负责重新部署）。</summary>
        public static bool Apply(bool on)
        {
            try
            {
                bool exists = File.Exists(UserFile);
                if (on)
                {
                    if (!exists) return false;
                    var text = File.ReadAllText(UserFile, Encoding.UTF8);
                    if (!text.StartsWith(OffMarker)) return false; // 不是我们放的，不动
                    File.Delete(UserFile);
                    return true;
                }
                if (exists) return false;
                Directory.CreateDirectory(Paths.UserDir);
                File.WriteAllText(UserFile, OffMarker + "\n", new UTF8Encoding(false));
                return true;
            }
            catch (Exception e) { Log.Write("mixed words: " + e.Message); return false; }
        }
    }
}
