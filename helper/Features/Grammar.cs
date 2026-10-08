using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Yiwei
{
    /// <summary>
    /// Optional 万象 (amzxyz/RIME-LMDG) grammar model for librime-octagram: better long sentences.
    /// The model (~380 MB) is downloaded on request into the user folder, verified against the GitHub
    /// SHA-256 digest, then each pinyin schema gets a patch block between markers. Removing deletes both.
    /// Patch values follow the model author's README and the rime-ice recipe (others/recipes/grammar.recipe.yaml).
    /// </summary>
    public static class Grammar
    {
        public const string Language = "wanxiang-lts-zh-hans";
        public const string FileName = Language + ".gram";
        const string ReleaseApi = "https://api.github.com/repos/amzxyz/RIME-LMDG/releases/tags/LTS";
        /// <summary>Size of the LTS asset when this was written (2025-12-07 release); the UI shows it before the first lookup.</summary>
        public const long KnownSize = 398309420;

        const string Begin = "  # >>> 一维输入法：语法模型（在「设置 → 词库」删除模型会自动移除这一段）";
        const string End = "  # <<< 一维输入法：语法模型";
        const string CreatedHeader = "# 由「一维输入法设置」创建，用于挂载语法模型。";

        public static string ModelPath => Path.Combine(Paths.UserDir, FileName);
        static string PartPath => ModelPath + ".part";
        static string InfoFile => Path.Combine(Paths.YiweiDir, "grammar.json");

        public class Info
        {
            public string Sha256 { get; set; } = "";
            public long Size { get; set; }
            public string InstalledAt { get; set; } = "";
            public string Url { get; set; } = "";
        }

        public static Info Load() => Json.Load<Info>(InfoFile);

        /// <summary>The model file is present (and, when we recorded it, has the recorded size).</summary>
        public static bool Installed
        {
            get
            {
                try
                {
                    if (!File.Exists(ModelPath)) return false;
                    var i = Load();
                    return i.Size <= 0 || new FileInfo(ModelPath).Length == i.Size;
                }
                catch { return false; }
            }
        }

        public static string SizeText(long bytes) => (bytes / 1024.0 / 1024.0).ToString("0") + " MB";

        /// <summary>The patch lines (under "patch:", two-space indent) or "" when no model is installed.</summary>
        public static string PatchBlock()
        {
            if (!Installed) return "";
            var y = new StringBuilder();
            y.AppendLine(Begin);
            y.AppendLine("  grammar:");
            y.AppendLine("    language: " + Language);
            y.AppendLine("    collocation_max_length: 6");
            y.AppendLine("    collocation_min_length: 3");
            y.AppendLine("    collocation_penalty: -14");
            y.AppendLine("    non_collocation_penalty: -6");
            y.AppendLine("    weak_collocation_penalty: -100");
            y.AppendLine("    rear_penalty: -20");
            // The model author and rime-ice turn contextual suggestions off for this model:
            // candidates then do not depend on the text committed before them.
            y.AppendLine("  \"translator/contextual_suggestions\": false");
            y.AppendLine("  \"translator/max_homophones\": 8");
            y.AppendLine("  \"translator/max_homographs\": 8");
            y.AppendLine(End);
            return y.ToString();
        }

        static int _busy;
        public static bool Busy => _busy == 1;

        /// <summary>Asks GitHub for the current asset (url, size, digest).</summary>
        public static async Task<GitHubNet.Asset> LookUp(CancellationToken ct = default(CancellationToken))
        {
            var rel = await GitHubNet.GetJson(ReleaseApi, ct).ConfigureAwait(false);
            return GitHubNet.Assets(rel).FirstOrDefault(a => a.Name == FileName) ?? throw new Exception("发布里找不到 " + FileName);
        }

        /// <summary>Downloads (resuming a partial file), verifies, installs the patches and redeploys.</summary>
        public static Task Install(IProgress<double> progress, IProgress<string> status, CancellationToken ct) => Task.Run(async () =>
        {
            if (Interlocked.Exchange(ref _busy, 1) == 1) throw new Exception("正在下载中");
            try
            {
                status?.Report("正在查询 GitHub…");
                var asset = await LookUp(ct).ConfigureAwait(false);
                if (string.IsNullOrEmpty(asset.Sha256)) throw new Exception("发布没有提供 SHA-256 校验值，未下载");
                var free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(Paths.UserDir))).AvailableFreeSpace;
                if (free < asset.Size * 2 + (200L << 20)) throw new Exception("磁盘空间不足（需要约 " + SizeText(asset.Size * 2) + "）");
                status?.Report("正在下载语法模型（" + SizeText(asset.Size) + "）…");
                await GitHubNet.Download(asset.Url, PartPath, asset.Size, progress, true, ct).ConfigureAwait(false);
                status?.Report("正在校验…");
                if (GitHubNet.Sha256Of(PartPath) != asset.Sha256)
                {
                    try { File.Delete(PartPath); } catch { }
                    throw new Exception("校验失败（SHA-256 不一致），已删除下载的文件");
                }
                if (File.Exists(ModelPath)) File.Delete(ModelPath);
                File.Move(PartPath, ModelPath);
                Json.Save(InfoFile, new Info { Sha256 = asset.Sha256, Size = asset.Size, InstalledAt = DateTime.Now.ToString("o"), Url = asset.Url });
                status?.Report("正在重新部署…");
                var skipped = WritePatches();
                Rime.ApplySoon(100); // rewrites rime_ice.custom.yaml (with the grammar block) and redeploys
                Log.Write("grammar: installed " + asset.Size + " bytes");
                status?.Report(skipped.Count == 0 ? "语法模型已启用" : "语法模型已下载；" + string.Join("、", skipped) + " 由你手动维护，请自行添加语法补丁");
            }
            finally { Interlocked.Exchange(ref _busy, 0); }
        });

        /// <summary>Deletes the model and every patch block, then redeploys.</summary>
        public static Task Remove() => Task.Run(() =>
        {
            if (Interlocked.Exchange(ref _busy, 1) == 1) throw new Exception("正在下载中");
            try
            {
                foreach (var p in new[] { ModelPath, PartPath, InfoFile }) if (File.Exists(p)) File.Delete(p);
                WritePatches();
                Rime.ApplySoon(100);
                Log.Write("grammar: removed");
            }
            finally { Interlocked.Exchange(ref _busy, 0); }
        });

        /// <summary>Partial download left from an interrupted attempt (bytes), 0 if none.</summary>
        public static long PartialBytes { get { try { return File.Exists(PartPath) ? new FileInfo(PartPath).Length : 0; } catch { return 0; } } }

        // ---------- schema patches ----------

        /// <summary>
        /// Adds (model installed) or removes the grammar block in the pinyin schemas' custom files the user maintains
        /// by hand; files generated by the settings get the block from WeaselConfig.WriteSchemaPatch (PatchBlock()).
        /// Returns the files that the user maintains by hand in a shape we cannot safely edit.
        /// </summary>
        public static List<string> WritePatches()
        {
            var skipped = new List<string>();
            var block = PatchBlock();
            foreach (var schema in Rime.Schemas.Select(s => s.Id))
            {
                var path = Path.Combine(Paths.UserDir, schema + ".custom.yaml");
                try
                {
                    if (!File.Exists(path)) continue; // WeaselConfig creates it (with the block) on the next apply
                    var text = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n");
                    if (text.Contains("# 由「一维输入法设置」生成")) continue; // regenerated by WeaselConfig.WriteSchemaPatch with PatchBlock()
                    var stripped = Strip(text);
                    if (block.Length == 0)
                    {
                        if (stripped.Trim() == (CreatedHeader + "\npatch:")) { File.Delete(path); continue; }
                        if (stripped != text) File.WriteAllText(path, stripped, new UTF8Encoding(false));
                        continue;
                    }
                    if (!PatchIsLastTopLevel(stripped) || HasOwnGrammar(stripped)) { skipped.Add(schema + ".custom.yaml"); continue; }
                    var next = stripped.TrimEnd('\n') + "\n" + block;
                    if (next != text) File.WriteAllText(path, next, new UTF8Encoding(false));
                }
                catch (Exception e) { Log.Write("grammar patch " + schema + ": " + e.Message); skipped.Add(schema + ".custom.yaml"); }
            }
            return skipped;
        }

        static string Strip(string text)
        {
            var lines = text.Split('\n').ToList();
            int b = lines.FindIndex(l => l.TrimEnd() == Begin);
            while (b >= 0)
            {
                int e = lines.FindIndex(b, l => l.TrimEnd() == End);
                if (e < 0) break;
                lines.RemoveRange(b, e - b + 1);
                b = lines.FindIndex(l => l.TrimEnd() == Begin);
            }
            return string.Join("\n", lines);
        }

        /// <summary>The file's last top-level key is "patch:" in block style, so lines appended with two spaces land inside it.</summary>
        static bool PatchIsLastTopLevel(string text)
        {
            var top = text.Split('\n').Where(l => l.Length > 0 && !char.IsWhiteSpace(l[0]) && !l.StartsWith("#") && !l.StartsWith("---") && !l.StartsWith("...")).ToList();
            return top.Count > 0 && top.Last().TrimEnd() == "patch:";
        }

        static bool HasOwnGrammar(string text) =>
            text.Split('\n').Any(l => !l.TrimStart().StartsWith("#") && (l.Contains("grammar") || l.Contains("contextual_suggestions")));
    }
}
