using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Yiwei
{
    /// <summary>
    /// 一维分类词库（overlay/data/yiwei_dicts/yiwei.*.dict.yaml，scripts/data_gen/lexicon/build.py 生成）：
    /// 安装包里的 rime_ice.dict.yaml 在 cn_dicts/base 前面 import 这些词典（同字同音上面的权重生效：只含新词和有意校准的词）（scripts/customize_data.py 的 mount_domain_dicts，同一规则）。
    /// 词库更新、标准词库会在用户文件夹另写一份 rime_ice.dict.yaml，这里负责把 import 补回去（<see cref="Mount"/>）。
    /// 关掉某个领域 = 在用户文件夹 yiwei_dicts\ 放一份同名、没有词条的词典（用户文件夹优先），打开 = 删掉这份。
    /// 词典主名仍是 rime_ice，用户词库不受影响。
    /// </summary>
    static class DomainDicts
    {
        public sealed class Domain
        {
            public string Id, Name, Desc;
            public Domain(string id, string name, string desc) { Id = id; Name = name; Desc = desc; }
        }

        /// <summary>和 customize_data.py 的 DOMAIN_DICTS 顺序一致。</summary>
        public static readonly Domain[] All =
        {
            new Domain("spoken", "口语短句", "吃点饭、喝点水、到哪了"),
            new Domain("geo", "中国地名", "省市县、山川河流、名胜"),
            new Domain("world", "世界地名", "国家、首都、主要城市"),
            new Domain("poetry", "诗词名句", "唐诗、宋词、千家诗、诗经"),
            new Domain("idiom", "成语", "成语与固定词组"),
            new Domain("history", "历史人文", "朝代帝王、历史人物、节日节气"),
            new Domain("science", "自然科学", "化学元素、数理化与天文"),
            new Domain("medical", "医学", "疾病、药品、检查"),
            new Domain("law", "法律", "法律法规与术语"),
            new Domain("finance", "财经", "金融、证券、经济"),
            new Domain("it", "计算机", "计算机与互联网"),
            new Domain("food", "饮食", "菜名、食材、小吃"),
            new Domain("nature", "动植物", "动物、植物、花卉"),
            new Domain("brand", "品牌机构", "品牌、企业、高校、机构"),
        };

        const string Begin = "  # >>> 一维分类词库（设置 → 词库 →「分类词库」可关）";
        const string End = "  # <<< 一维分类词库";
        const string OffMarker = "# 一维输入法：分类词库已关闭（设置 → 词库 →「分类词库」可重新打开；删掉本文件也会恢复）";
        static readonly Regex Block = new Regex(@"^  # >>> 一维分类词库.*?^  # <<< 一维分类词库[^\n]*\n", RegexOptions.Multiline | RegexOptions.Singleline);
        static readonly Regex BaseLine = new Regex(@"^(  - cn_dicts/base\b[^\n]*\n)", RegexOptions.Multiline);

        static string UserDict => Path.Combine(Paths.UserDir, "rime_ice.dict.yaml");
        static string StubPath(string id) => Path.Combine(Paths.UserDir, "yiwei_dicts", "yiwei." + id + ".dict.yaml");

        static string ImportBlock()
        {
            var sb = new StringBuilder();
            sb.Append(Begin).Append('\n');
            foreach (var d in All) sb.Append("  - yiwei_dicts/yiwei.").Append(d.Id).Append('\n');
            sb.Append(End).Append('\n');
            return sb.ToString();
        }

        /// <summary>rime_ice.dict.yaml 文本：去掉旧的一维块，在 cn_dicts/base 行前插入最新的。找不到 base 行就原样返回。</summary>
        public static string Mount(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var nl = text.Contains("\r\n");
            var t = Block.Replace(text.Replace("\r\n", "\n"), "");
            if (!BaseLine.IsMatch(t)) return text;
            t = BaseLine.Replace(t, m => ImportBlock() + m.Groups[1].Value, 1);
            return nl ? t.Replace("\n", "\r\n") : t;
        }

        /// <summary>用户文件夹里有 rime_ice.dict.yaml（词库更新 / 标准词库写的）时补上分类词库的 import；有变化返回 true。</summary>
        public static bool EnsureUserImports()
        {
            try
            {
                if (!File.Exists(UserDict)) return false;
                var text = File.ReadAllText(UserDict, Encoding.UTF8);
                var next = Mount(text);
                if (next == text) return false;
                File.WriteAllText(UserDict, next, new UTF8Encoding(false));
                Log.Write("domain dicts: imports restored in user rime_ice.dict.yaml");
                return true;
            }
            catch (Exception e) { Log.Write("domain dicts: " + e.Message); return false; }
        }

        public static HashSet<string> OffSet(string csv) =>
            new HashSet<string>((csv ?? "").Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));

        public static bool IsOn(string id) => Settings.Current.DomainDictsOn && !OffSet(Settings.Current.DomainDictsOff).Contains(id);

        public static void SetDomain(string id, bool on)
        {
            var off = OffSet(Settings.Current.DomainDictsOff);
            if (on) off.Remove(id); else off.Add(id);
            Settings.Current.DomainDictsOff = string.Join(",", All.Select(d => d.Id).Where(off.Contains));
        }

        /// <summary>按设置放置/删除空词典并补 import；有变化返回 true（调用方负责重新部署）。</summary>
        public static bool Apply()
        {
            bool changed = EnsureUserImports();
            foreach (var d in All)
            {
                try
                {
                    var path = StubPath(d.Id);
                    bool exists = File.Exists(path);
                    if (IsOn(d.Id))
                    {
                        if (!exists) continue;
                        if (!File.ReadAllText(path, Encoding.UTF8).StartsWith(OffMarker)) continue; // 不是我们放的，不动
                        File.Delete(path);
                        changed = true;
                    }
                    else
                    {
                        if (exists) continue;
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        File.WriteAllText(path, OffMarker + "\n---\nname: yiwei." + d.Id + "\nversion: \"off\"\nsort: by_weight\n...\n", new UTF8Encoding(false));
                        changed = true;
                    }
                }
                catch (Exception e) { Log.Write("domain dicts " + d.Id + ": " + e.Message); }
            }
            return changed;
        }
    }
}
