using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Yiwei
{
    /// <summary>
    /// 34 性能体检：内存（输入法服务、助手）、每次按键引擎耗时（yiwei_perf*.lua 写的 yiwei/perf.tsv）、
    /// 词库与用户词库大小、本机配置，给出建议（标准词库、低配机模式、清理等）。只在本机计算，不上传。
    /// 设置 → 工具 →「性能体检」，或命令 /perfcheck（写 yiwei/perfcheck.txt，CI 冒烟用）。
    /// </summary>
    static class PerfCheck
    {
        public sealed class Report
        {
            public long ServerMb = -1, HelperMb;
            public int KeySamples; public double KeyAvgMs = -1, KeyMaxMs; public int KeySlow;
            public long BuildMb, UserDbMb;
            public bool Lite, Grammar;
            public double RamGb; public int Cores;
            public List<string> Advice = new List<string>();

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine("一维输入法 性能体检 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                sb.AppendLine("输入法服务内存：" + (ServerMb >= 0 ? ServerMb + " MB" : "服务没有运行"));
                sb.AppendLine("一维助手内存：" + HelperMb + " MB");
                sb.AppendLine(KeyAvgMs >= 0
                    ? string.Format("每次按键出候选：平均 {0:0.0} ms，最慢 {1:0} ms，超过 50 ms 的 {2} 次（最近 {3} 次按键）", KeyAvgMs, KeyMaxMs, KeySlow, KeySamples)
                    : "每次按键出候选：还没有数据（用全拼或双拼打几个字后再体检）");
                sb.AppendLine("词库：" + (Lite ? "标准（不含腾讯词向量）" : "完整") + (Grammar ? " + 语法模型" : "") + "，编译后 " + BuildMb + " MB；用户词库 " + UserDbMb + " MB");
                sb.AppendLine(string.Format("本机：{0:0.#} GB 内存、{1} 核", RamGb, Cores));
                sb.AppendLine();
                sb.AppendLine(Advice.Count == 0 ? "建议：一切正常 👍" : "建议：");
                foreach (var a in Advice) sb.AppendLine("· " + a);
                return sb.ToString();
            }
        }

        static long DirMb(string dir)
        {
            try { return Directory.Exists(dir) ? new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / 1048576 : 0; }
            catch { return 0; }
        }

        static long ProcMb(string name)
        {
            long mb = -1;
            foreach (var p in Process.GetProcessesByName(name))
                using (p) { try { mb = Math.Max(mb, 0) + p.WorkingSet64 / 1048576; } catch { } }
            return mb;
        }

        /// <summary>yiwei/perf.tsv：次数、平均、最慢、慢次数、累计、时间戳。</summary>
        public static bool ReadKeyTiming(string path, out int n, out double avg, out double max, out int slow)
        {
            n = 0; avg = max = 0; slow = 0;
            try
            {
                if (!File.Exists(path)) return false;
                var p = File.ReadAllText(path).Trim().Split('\t');
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                return p.Length >= 4 && int.TryParse(p[0], out n) && double.TryParse(p[1], System.Globalization.NumberStyles.Float, inv, out avg)
                       && double.TryParse(p[2], System.Globalization.NumberStyles.Float, inv, out max) && int.TryParse(p[3], out slow);
            }
            catch { return false; }
        }

        public static Report Run()
        {
            var s = Settings.Current;
            var r = new Report
            {
                ServerMb = ProcMb("YiweiServer"), HelperMb = LowSpec.WorkingSetMb,
                BuildMb = DirMb(Path.Combine(Paths.UserDir, "build")), UserDbMb = DirMb(Path.Combine(Paths.UserDir, "rime_ice.userdb")),
                Lite = DictLite.IsLiteNow(), Grammar = Yiwei.Grammar.Installed, RamGb = LowSpec.TotalRamGb, Cores = Environment.ProcessorCount,
            };
            if (ReadKeyTiming(Path.Combine(Paths.YiweiDir, "perf.tsv"), out var n, out var avg, out var max, out var slow))
            { r.KeySamples = n; r.KeyAvgMs = avg; r.KeyMaxMs = max; r.KeySlow = slow; }
            bool lowEnd = LowSpec.LooksLowEnd;
            if ((r.KeyAvgMs > 25 || lowEnd || r.ServerMb > 400) && !r.Lite)
                r.Advice.Add("换成「标准」词库（设置 → 词库 → 词库大小）：不含腾讯词向量大词库，部署和打字更快、服务内存明显减少");
            if (r.KeyAvgMs > 40 && r.Grammar)
                r.Advice.Add("按键偏慢：可以先删除语法模型（设置 → 词库）试试，长句准确度会略降");
            if (r.KeySlow > Math.Max(5, r.KeySamples / 10))
                r.Advice.Add("有不少按键超过 50 ms：关掉不用的扩展功能（设置 → 输入 里的人名候选、Emoji 等）或重启一下输入法服务");
            if ((lowEnd || r.HelperMb > 150) && !ToolSettings.Current.LowSpec)
                r.Advice.Add("打开「低配机模式」（设置 → 工具 → 性能）：关掉 AI 手势、语音等后台功能并定时回收内存");
            if (r.UserDbMb > 80)
                r.Advice.Add("用户词库已经 " + r.UserDbMb + " MB：可在 设置 → 输入 清除很久以前学到的词，或导出备份后重建");
            if (r.ServerMb < 0)
                r.Advice.Add("输入法服务没有运行：切换一下输入法，或运行安装目录里的 YiweiServer.exe");
            return r;
        }

        /// <summary>/perfcheck：写 yiwei/perfcheck.txt（CI 冒烟检查它）。</summary>
        public static string RunToFile()
        {
            var path = Path.Combine(Paths.YiweiDir, "perfcheck.txt");
            File.WriteAllText(path, Run().ToString(), new UTF8Encoding(false));
            return path;
        }
    }
}
