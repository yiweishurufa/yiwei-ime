using System;
using System.IO;
using System.Text;

namespace Yiwei
{
    /// <summary>
    /// 输入核心组的开关：都是用户目录 yiwei/ 下的小文件，由 overlay/data/lua 里的 Lua 读取（yiwei_flags.lua，3 秒内生效，不用部署），
    /// 安卓（同文）上同名文件同样有效。需要改方案 YAML 的开关在 <see cref="PatchLines"/> 里追加到 *.custom.yaml。
    /// </summary>
    public static class CoreInput
    {
        static string F(string name) => Path.Combine(Paths.YiweiDir, name);

        /// <summary>文件存在 = 开（name 形如 xxx.enabled）或 关（xxx.disabled）。</summary>
        public static bool Has(string name) => File.Exists(F(name));

        public static void Set(string name, bool exists)
        {
            try
            {
                if (exists) File.WriteAllText(F(name), "1");
                else if (File.Exists(F(name))) File.Delete(F(name));
            }
            catch (Exception e) { Log.Write("core flag " + name + ": " + e.Message); }
        }

        public static string Read(string name, string fallback)
        {
            try { return File.Exists(F(name)) ? File.ReadAllText(F(name), Encoding.UTF8).Trim() : fallback; }
            catch { return fallback; }
        }

        public static void Write(string name, string value)
        {
            try { File.WriteAllText(F(name), value ?? "", new UTF8Encoding(false)); }
            catch (Exception e) { Log.Write("core value " + name + ": " + e.Message); }
        }

        // ---------- X1 按习惯调整候选（yiwei_learn.lua） ----------
        public static bool Learn { get => !Has("learn.disabled"); set => Set("learn.disabled", !value); }
        /// <summary>学习强度 1 低 / 2 中 / 3 高（W9）。</summary>
        public static string LearnLevel { get => Read("learn.level", "2"); set => Write("learn.level", value); }
        /// <summary>清除学习：all，或最近 N 秒。下一次打字时由 Lua 执行，同时从 RIME 用户词库删除这些词。</summary>
        public static void ClearLearned(string how) => Write("learn.clear", how);

        /// <summary>追加到每个雾凇方案的 *.custom.yaml（patch: 下的行）。</summary>
        public static string PatchLines(string schema, Settings s)
        {
            var y = new StringBuilder();
            return y.ToString();
        }
    }
}
