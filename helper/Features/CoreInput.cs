using System;
using System.IO;
using System.Linq;
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

        // ---------- W10 Emoji 候选（雾凇 emoji 开关的默认值；F4 菜单里也能临时切换） ----------
        public static bool Emoji { get => !Has("emoji.disabled"); set => Set("emoji.disabled", !value); }

        // ---------- W1 模糊音地区预设 ----------
        public sealed class FuzzyPreset { public string Name, Desc; public string[] Ids; }
        public static readonly FuzzyPreset[] FuzzyPresets =
        {
            new FuzzyPreset { Name = "南方通用", Desc = "平翘舌 + 前后鼻音", Ids = new[] { "zh_z", "ch_c", "sh_s", "an_ang", "en_eng", "in_ing" } },
            new FuzzyPreset { Name = "西南（川渝云贵）", Desc = "n/l、f/h、前后鼻音", Ids = new[] { "n_l", "f_h", "en_eng", "in_ing" } },
            new FuzzyPreset { Name = "两湖", Desc = "n/l、f/h、r/l、前后鼻音", Ids = new[] { "n_l", "f_h", "r_l", "an_ang", "en_eng", "in_ing" } },
            new FuzzyPreset { Name = "闽粤", Desc = "平翘舌、n/l、f/h、前后鼻音、ian/uan", Ids = new[] { "zh_z", "ch_c", "sh_s", "n_l", "f_h", "an_ang", "en_eng", "in_ing", "ian_iang", "uan_uang" } },
            new FuzzyPreset { Name = "江浙沪", Desc = "平翘舌、前后鼻音、n/l", Ids = new[] { "zh_z", "ch_c", "sh_s", "n_l", "en_eng", "in_ing" } },
            new FuzzyPreset { Name = "全部清除", Desc = "不用模糊音", Ids = new string[0] },
        };

        /// <summary>追加到每个雾凇方案的 *.custom.yaml（patch: 下的行）。</summary>
        public static string PatchLines(string schema, Settings s)
        {
            var y = new StringBuilder();
            var f = RimeFeatures.Of(s);
            bool full = schema == "rime_ice";

            // W10：雾凇 switches 顺序 ascii_mode, ascii_punct, traditionalization, emoji, …
            if (!Emoji) y.Append("  \"switches/@3/reset\": 0\n");

            // W1 模糊音也给双拼：双拼方案的 algebra 先把全拼转成键位，所以规则要插在最前面（librime 的 @before N）
            if (!full)
            {
                int i = 0;
                foreach (var r in RimeFeatures.FuzzyPairs.Where(p => f.Fuzzy.Contains(p.Id)).SelectMany(p => p.Rules))
                    y.Append("  \"speller/algebra/@before ").Append(i++).Append("\": ").Append(r).Append('\n');
            }
            return y.ToString();
        }
    }
}
