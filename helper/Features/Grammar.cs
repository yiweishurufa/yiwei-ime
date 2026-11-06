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
    /// Optional 万象 (amzxyz/rime-wanxiang-data, formerly RIME-LMDG; CC BY 4.0) grammar model for librime-octagram:
    /// better long sentences. The model (~400 MB) is downloaded on request into the user folder, verified against the
    /// GitHub SHA-256 digest, then each pinyin schema gets a patch block between markers. Removing deletes both.
    /// The LTS asset is overwritten in place upstream, so "is there an update" compares the remote digest / size / date
    /// with what we recorded at install time (<see cref="CheckUpdate"/>), not with a size baked into this file.
    /// Patch values follow the model author's README (https://github.com/amzxyz/rime-wanxiang-data#readme, 2026-11).
    /// </summary>
    public static class Grammar
    {
        public const string Language = "wanxiang-lts-zh-hans";
        public const string FileName = Language + ".gram";
        /// <summary>The repository was renamed from amzxyz/RIME-LMDG; mirrors do not always follow GitHub's redirect, so use the new name.</summary>
        const string ReleaseApi = "https://api.github.com/repos/amzxyz/rime-wanxiang-data/releases/tags/LTS";
        public const string Homepage = "https://github.com/amzxyz/rime-wanxiang-data";
        public const string LicenseUrl = "https://creativecommons.org/licenses/by/4.0/deed.zh-hans";
        /// <summary>Approximate size for the UI before the first lookup only (LTS asset re-uploaded 2026-10-26: 418,078,764 bytes).</summary>
        public const long KnownSize = 418078764;

        const string Begin = "  # >>> 一维输入法：语法模型（在「设置 → 词库」删除模型会自动移除这一段）";
        const string End = "  # <<< 一维输入法：语法模型";
        const string CreatedHeader = "# 由「一维输入法设置」创建，用于挂载语法模型。";

        public static string ModelPath => Path.Combine(Paths.UserDir, FileName);
        static string PartPath => ModelPath + ".part";
        static string InfoFile => Path.Combine(Paths.YiweiDir, "grammar.json");
        static string PendingPath => ModelPath + ".pending";
        static string PendingInfoFile => Path.Combine(Paths.YiweiDir, "grammar.pending.json");

        /// <summary>Puts a verified file in place of the model. False when the old model is locked (mapped by the IME service).</summary>
        static bool Swap(string src)
        {
            var old = ModelPath + ".old";
            try
            {
                if (File.Exists(old)) File.Delete(old);
                if (File.Exists(ModelPath)) File.Move(ModelPath, old); // 先改名：比直接删更容易在被映射时成功
                File.Move(src, ModelPath);
                try { File.Delete(old); } catch { /* 还被占用就留着，下次再删 */ }
                return true;
            }
            catch (IOException e) { Log.Write("grammar swap: " + e.Message); }
            catch (UnauthorizedAccessException e) { Log.Write("grammar swap: " + e.Message); }
            if (!File.Exists(ModelPath) && File.Exists(old)) { try { File.Move(old, ModelPath); } catch { } }
            return false;
        }

        /// <summary>0.1.0.113 起参数对齐万象 README（collocation_min_length 2、rear_penalty -18）：旧补丁重写一次并重新部署。</summary>
        static void UpgradeParams()
        {
            if (!Installed) return;
            var path = Path.Combine(Paths.UserDir, "rime_ice.custom.yaml");
            if (!File.Exists(path)) return;
            var text = File.ReadAllText(path, Encoding.UTF8);
            if (!text.Contains(Begin) || !text.Contains("    rear_penalty: -20")) return;
            WritePatches();
            Rime.ApplySoon(5000); // 设置生成的补丁由 WeaselConfig 按 PatchBlock() 重写
            Log.Write("grammar: patch params upgraded");
        }

        /// <summary>At helper start: install a model update that was downloaded while the old one was in use.</summary>
        public static void ApplyPending()
        {
            try
            {
                var stale = ModelPath + ".old";
                if (File.Exists(stale)) { try { File.Delete(stale); } catch { } }
                if (!File.Exists(PendingPath)) { UpgradeParams(); return; }
                var info = Json.Load<Info>(PendingInfoFile);
                if (string.IsNullOrEmpty(info.Sha256) || new FileInfo(PendingPath).Length != info.Size) { File.Delete(PendingPath); return; }
                if (!Swap(PendingPath)) return;
                Json.Save(InfoFile, info);
                try { File.Delete(PendingInfoFile); } catch { }
                WritePatches();
                Rime.ApplySoon(3000);
                Log.Write("grammar: staged update applied");
            }
            catch (Exception e) { Log.Write("grammar pending: " + e.Message); }
        }

        public class Info
        {
            public string Sha256 { get; set; } = "";
            public long Size { get; set; }
            public string InstalledAt { get; set; } = "";
            public string Url { get; set; } = "";
            /// <summary>GitHub asset updated_at of the installed file (the LTS asset is replaced in place).</summary>
            public string UpdatedAt { get; set; } = "";
            /// <summary>Last remote check: what GitHub offers now (for the "有更新" hint without a network call each time).</summary>
            public string RemoteSha256 { get; set; } = "";
            public long RemoteSize { get; set; }
            public string RemoteUpdatedAt { get; set; } = "";
            public string CheckedAt { get; set; } = "";
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
            y.AppendLine("    collocation_min_length: 2");
            y.AppendLine("    collocation_penalty: -14");
            y.AppendLine("    non_collocation_penalty: -6");
            y.AppendLine("    weak_collocation_penalty: -100");
            y.AppendLine("    rear_penalty: -18");
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

        /// <summary>The installed model differs from the one GitHub offers now (as of the last <see cref="CheckUpdate"/>).</summary>
        public static bool UpdateAvailable
        {
            get
            {
                if (!Installed) return false;
                try { return Differs(Load()); } catch { return false; }
            }
        }

        /// <summary>Compare by SHA-256 when both sides have one, else by size, else by GitHub's updated_at.</summary>
        static bool Differs(Info i)
        {
            if (string.IsNullOrEmpty(i.CheckedAt)) return false;
            if (!string.IsNullOrEmpty(i.Sha256) && !string.IsNullOrEmpty(i.RemoteSha256))
                return !string.Equals(i.Sha256, i.RemoteSha256, StringComparison.OrdinalIgnoreCase);
            if (i.RemoteSize > 0)
            {
                long local = i.Size > 0 ? i.Size : (File.Exists(ModelPath) ? new FileInfo(ModelPath).Length : 0);
                if (local != i.RemoteSize) return true;
            }
            return !string.IsNullOrEmpty(i.UpdatedAt) && !string.IsNullOrEmpty(i.RemoteUpdatedAt) && i.UpdatedAt != i.RemoteUpdatedAt;
        }

        /// <summary>
        /// Asks GitHub what the LTS asset is now and records it. Returns true when it differs from the installed model.
        /// Only runs when the user opens the dictionary page or presses the button (no background polling).
        /// </summary>
        public static async Task<bool> CheckUpdate(CancellationToken ct = default(CancellationToken))
        {
            var asset = await LookUp(ct).ConfigureAwait(false);
            var i = File.Exists(InfoFile) ? Load() : new Info();
            i.RemoteSha256 = asset.Sha256 ?? ""; i.RemoteSize = asset.Size; i.RemoteUpdatedAt = asset.UpdatedAt ?? ""; i.CheckedAt = DateTime.Now.ToString("o");
            if (Installed) Json.Save(InfoFile, i);
            return Installed && Differs(i);
        }

        /// <summary>Remote size from the last check (for the download button), else the approximate size.</summary>
        public static long ExpectedSize { get { try { var i = Load(); return i.RemoteSize > 0 ? i.RemoteSize : KnownSize; } catch { return KnownSize; } } }

        /// <summary>Downloads (resuming a partial file), verifies, installs the patches and redeploys.</summary>
        public static Task Install(IProgress<double> progress, IProgress<string> status, CancellationToken ct) => Task.Run(async () =>
        {
            if (Interlocked.Exchange(ref _busy, 1) == 1) throw new Exception("正在下载中");
            try
            {
                status?.Report("正在查询语法模型…");
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
                var info = new Info
                {
                    Sha256 = asset.Sha256, Size = asset.Size, InstalledAt = DateTime.Now.ToString("o"), Url = asset.Url, UpdatedAt = asset.UpdatedAt ?? "",
                    RemoteSha256 = asset.Sha256, RemoteSize = asset.Size, RemoteUpdatedAt = asset.UpdatedAt ?? "", CheckedAt = DateTime.Now.ToString("o"),
                };
                if (!Swap(PartPath))
                {
                    // 旧模型正被输入法服务占用（更新时）：留到下次启动助手再换（ApplyPending），先不改补丁
                    File.Move(PartPath, PendingPath);
                    Json.Save(PendingInfoFile, info);
                    Log.Write("grammar: model in use, update staged");
                    status?.Report("新版模型已下载，正在使用的旧模型被占用，重启电脑后自动换上");
                    return;
                }
                Json.Save(InfoFile, info);
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
                foreach (var p in new[] { ModelPath, PartPath, InfoFile, PendingPath, PendingInfoFile }) if (File.Exists(p)) File.Delete(p);
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
