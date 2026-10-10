using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Yiwei
{
    /// <summary>
    /// 离线语音输入（最小可用版）：按住右 Ctrl（或右 Alt）说话，松开识别上屏，声音不出电脑。
    /// 引擎是 k2-fsa/sherpa-onnx 的命令行识别程序 + SenseVoice-Small int8 模型（中文 / 英文 / 粤语 / 日 / 韩，自带标点）。
    /// 两样都按需下载到 <用户文件夹>\yiwei\voice，走 Mirrors 镜像链，按固定的大小 + SHA-256 校验（版本钉死，不查 API）。
    /// 解压 .tar.bz2 用 Windows 10 1803 起自带的 tar.exe。
    /// </summary>
    public static class VoiceModel
    {
        public const string EngineVersion = "1.13.8";

        public sealed class Pack
        {
            public string Name, Url, Sha256, Folder;
            public long Size;
            /// <summary>Files kept from the archive (path inside <see cref="Folder"/> → name in the voice folder).</summary>
            public string[][] Keep;
        }

        static readonly Pack EngineX64 = new Pack
        {
            Name = "识别程序（x64）",
            Url = "https://github.com/k2-fsa/sherpa-onnx/releases/download/v1.13.8/sherpa-onnx-v1.13.8-win-x64-shared-MT-Release-no-tts.tar.bz2",
            Size = 23271851, Sha256 = "4b0a94f7b5c606b1b64a19a831c2127559e4b3d34e195465ebc7be73d9ed4783",
            Folder = "sherpa-onnx-v1.13.8-win-x64-shared-MT-Release-no-tts",
            Keep = EngineFiles,
        };

        static readonly Pack EngineArm64 = new Pack
        {
            Name = "识别程序（ARM64）",
            Url = "https://github.com/k2-fsa/sherpa-onnx/releases/download/v1.13.8/sherpa-onnx-v1.13.8-win-arm64-shared-MT-Release-no-tts.tar.bz2",
            Size = 21872501, Sha256 = "29a864324e658bef2a8b83bd3e12adae8b415a5a232d83902030e8c6efa37dbc",
            Folder = "sherpa-onnx-v1.13.8-win-arm64-shared-MT-Release-no-tts",
            Keep = EngineFiles,
        };

        static string[][] EngineFiles => new[]
        {
            new[] { "bin/sherpa-onnx-offline.exe", "sherpa-onnx-offline.exe" },
            new[] { "bin/onnxruntime.dll", "onnxruntime.dll" },
            new[] { "bin/onnxruntime_providers_shared.dll", "onnxruntime_providers_shared.dll" },
        };

        /// <summary>SenseVoice-Small int8 (2024-07-17)：带标点与逆文本规范化（“9点”“50”）。2025-09-09 版实测语种判断不稳，暂不用。</summary>
        static readonly Pack Model = new Pack
        {
            Name = "SenseVoice 模型",
            Url = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17.tar.bz2",
            Size = 163002883, Sha256 = "7d1efa2138a65b0b488df37f8b89e3d91a60676e416f515b952358d83dfd347e",
            Folder = "sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2024-07-17",
            Keep = new[] { new[] { "model.int8.onnx", "model.int8.onnx" }, new[] { "tokens.txt", "tokens.txt" } },
        };

        static Pack Engine => RuntimeInformation.OSArchitecture == Architecture.Arm64 ? EngineArm64 : EngineX64;

        public static long TotalSize => Engine.Size + Model.Size;

        public static string Dir => Path.Combine(Paths.YiweiDir, "voice");
        public static string Exe => Path.Combine(Dir, "sherpa-onnx-offline.exe");
        static string InfoFile => Path.Combine(Dir, "voice.json");

        public class Info { public string Engine { get; set; } = ""; public string Model { get; set; } = ""; public string InstalledAt { get; set; } = ""; }

        /// <summary>All files present and the recorded versions are the ones this build expects.</summary>
        public static bool Installed
        {
            get
            {
                try
                {
                    if (!File.Exists(InfoFile)) return false;
                    var i = Json.Load<Info>(InfoFile);
                    if (i.Engine != Engine.Sha256 || i.Model != Model.Sha256) return false;
                    return Engine.Keep.Concat(Model.Keep).All(k => File.Exists(Path.Combine(Dir, k[1])));
                }
                catch { return false; }
            }
        }

        static int _ready = -1;
        /// <summary>Cached <see cref="Installed"/> for the keyboard hook (re-read after install / remove).</summary>
        public static bool Ready { get { if (_ready < 0) _ready = Installed ? 1 : 0; return _ready == 1; } }

        public static long DiskBytes
        {
            get { try { return Directory.Exists(Dir) ? new DirectoryInfo(Dir).GetFiles().Sum(f => f.Length) : 0; } catch { return 0; } }
        }

        static int _busy;
        public static bool Busy => _busy == 1;

        public static Task Install(IProgress<double> progress, IProgress<string> status, CancellationToken ct) => Task.Run(async () =>
        {
            if (Interlocked.Exchange(ref _busy, 1) == 1) throw new Exception("正在下载中");
            try
            {
                var tar = TarExe();
                if (tar == null) throw new Exception("这台电脑没有系统自带的 tar.exe（Windows 10 1803 起才有），暂时无法安装语音模型");
                Directory.CreateDirectory(Dir);
                var free = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(Dir))).AvailableFreeSpace;
                if (free < TotalSize * 3) throw new Exception("磁盘空间不足（需要约 " + Grammar.SizeText(TotalSize * 3) + "）");
                var packs = new[] { Engine, Model };
                long before = 0;
                foreach (var p in packs)
                {
                    long offset = before;
                    var part = Path.Combine(Dir, Path.GetFileName(new Uri(p.Url).AbsolutePath) + ".part");
                    status?.Report("正在下载" + p.Name + "（" + Grammar.SizeText(p.Size) + "）…");
                    var pr = new Progress<double>(v => progress?.Report((offset + v * p.Size) / TotalSize));
                    await GitHubNet.Download(p.Url, part, p.Size, pr, true, ct).ConfigureAwait(false);
                    status?.Report("正在校验" + p.Name + "…");
                    if (GitHubNet.Sha256Of(part) != p.Sha256)
                    {
                        try { File.Delete(part); } catch { }
                        throw new Exception(p.Name + " 校验失败（SHA-256 不一致），已删除下载的文件");
                    }
                    status?.Report("正在解压" + p.Name + "…");
                    Extract(tar, part, p);
                    try { File.Delete(part); } catch { }
                    before += p.Size;
                }
                Json.Save(InfoFile, new Info { Engine = Engine.Sha256, Model = Model.Sha256, InstalledAt = DateTime.Now.ToString("o") });
                Log.Write("voice: installed engine " + EngineVersion + " + SenseVoice int8");
                _ready = -1;
                status?.Report("语音输入已就绪");
            }
            finally { Interlocked.Exchange(ref _busy, 0); }
        });

        public static Task Remove() => Task.Run(() =>
        {
            if (Interlocked.Exchange(ref _busy, 1) == 1) throw new Exception("正在下载中");
            try { if (Directory.Exists(Dir)) Directory.Delete(Dir, true); _ready = -1; Log.Write("voice: removed"); }
            finally { Interlocked.Exchange(ref _busy, 0); }
        });

        public static long PartialBytes
        {
            get { try { return Directory.Exists(Dir) ? new DirectoryInfo(Dir).GetFiles("*.part").Sum(f => f.Length) : 0; } catch { return 0; } }
        }

        static string TarExe()
        {
            var sys = Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Sysnative")
                : Environment.SystemDirectory;
            var p = Path.Combine(sys, "tar.exe");
            return File.Exists(p) ? p : (File.Exists(Path.Combine(Environment.SystemDirectory, "tar.exe")) ? Path.Combine(Environment.SystemDirectory, "tar.exe") : null);
        }

        /// <summary>Extracts only the files we keep into a scratch folder (ASCII names, relative paths), then moves them into place.</summary>
        static void Extract(string tar, string archive, Pack p)
        {
            var tmp = Path.Combine(Dir, "x");
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            Directory.CreateDirectory(tmp);
            try
            {
                // Relative paths only: the user folder may contain non-ASCII characters that tar's narrow argv would mangle.
                File.Move(archive, Path.Combine(tmp, "a.tar.bz2"));
                var members = string.Join(" ", p.Keep.Select(k => "\"" + p.Folder + "/" + k[0] + "\""));
                var psi = new ProcessStartInfo(tar, "-xjf a.tar.bz2 " + members)
                {
                    WorkingDirectory = tmp, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardError = true, RedirectStandardOutput = true,
                };
                using (var proc = Process.Start(psi))
                {
                    var err = proc.StandardError.ReadToEndAsync();
                    proc.StandardOutput.ReadToEnd();
                    if (!proc.WaitForExit(5 * 60 * 1000)) { try { proc.Kill(); } catch { } throw new Exception("解压超时"); }
                    if (proc.ExitCode != 0) throw new Exception("解压失败：" + err.Result.Trim());
                }
                foreach (var k in p.Keep)
                {
                    var src = Path.Combine(tmp, p.Folder, k[0].Replace('/', '\\'));
                    var dst = Path.Combine(Dir, k[1]);
                    if (!File.Exists(src)) throw new Exception("压缩包里缺少 " + k[0]);
                    if (File.Exists(dst)) File.Delete(dst);
                    File.Move(src, dst);
                }
            }
            finally { try { Directory.Delete(tmp, true); } catch { } }
        }

        // ---------- recognition ----------

        /// <summary>Runs the recognizer on 16 kHz mono samples. Returns the raw text (may be empty).</summary>
        public static string Recognize(short[] samples, string language, CancellationToken ct)
        {
            if (!Installed) throw new Exception("语音模型未安装");
            var wav = Path.Combine(Dir, "rec.wav");
            WriteWav(wav, samples, Recorder.Rate);
            try
            {
                var lang = language == "zh" || language == "en" || language == "yue" || language == "ja" || language == "ko" ? language : "auto";
                int threads = Math.Max(1, Math.Min(4, Environment.ProcessorCount / 2));
                var args = "--tokens=tokens.txt --sense-voice-model=model.int8.onnx --sense-voice-use-itn=1 --sense-voice-language=" + lang
                           + " --num-threads=" + threads + " rec.wav";
                var psi = new ProcessStartInfo(Exe, args)
                {
                    WorkingDirectory = Dir, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                };
                using (var proc = Process.Start(psi))
                using (ct.Register(() => { try { proc.Kill(); } catch { } }))
                {
                    var err = proc.StandardError.ReadToEndAsync();
                    var output = proc.StandardOutput.ReadToEnd();
                    if (!proc.WaitForExit(60 * 1000)) { try { proc.Kill(); } catch { } throw new Exception("识别超时"); }
                    ct.ThrowIfCancellationRequested();
                    if (proc.ExitCode != 0)
                    {
                        var tail = (err.Result ?? "").Trim();
                        if (tail.Length > 300) tail = tail.Substring(tail.Length - 300);
                        Log.Write("voice: exit " + proc.ExitCode + " " + tail);
                        throw new Exception("识别程序出错（" + proc.ExitCode + "）");
                    }
                    return ParseText(output);
                }
            }
            finally { try { File.Delete(wav); } catch { } }
        }

        /// <summary>The recognizer prints one JSON object per file on stdout: {"lang": …, "text": "…", …}.</summary>
        public static string ParseText(string stdout)
        {
            var js = new JavaScriptSerializer();
            foreach (var line in (stdout ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("{")))
            {
                try
                {
                    var d = js.Deserialize<Dictionary<string, object>>(line);
                    if (d != null && d.TryGetValue("text", out var t)) return (t as string ?? "").Trim();
                }
                catch { }
            }
            return "";
        }

        static void WriteWav(string path, short[] samples, int rate)
        {
            using (var f = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(f))
            {
                int bytes = samples.Length * 2;
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes); w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(bytes);
                foreach (var s in samples) w.Write(s);
            }
        }

        // ---------- post-processing ----------

        static readonly Regex Fillers = new Regex(@"(^|[，。！？、,.!?\s])(?:嗯+|呃+|额+|啊+|哦+)(?:[，、,\s]+|(?=[。.！？!?])|$)", RegexOptions.Compiled);
        static readonly Regex CjkThenLatin = new Regex(@"([\u4e00-\u9fff\u3400-\u4dbf])([A-Za-z0-9])", RegexOptions.Compiled);
        static readonly Regex LatinThenCjk = new Regex(@"([A-Za-z0-9%])([\u4e00-\u9fff\u3400-\u4dbf])", RegexOptions.Compiled);

        /// <summary>去口水词（嗯、呃、啊…）与中英之间加空格；规则保守，只动句首或标点后的语气词。</summary>
        public static string Tidy(string text, bool fillers, bool pangu)
        {
            var t = (text ?? "").Trim();
            if (fillers)
            {
                string prev;
                do { prev = t; t = Fillers.Replace(t, "$1"); } while (t != prev);
                t = Regex.Replace(t, @"^[，。、,.\s]+", "");
                t = Regex.Replace(t, @"[，、,]+(?=[。.！？!?]|$)", "");
                t = Regex.Replace(t, @"([。！？!?])[。.]+", "$1");
                // a sentence that was nothing but fillers leaves only punctuation behind
                if (Regex.IsMatch(t, @"^[\p{P}\s]*$")) t = "";
            }
            if (pangu)
            {
                t = CjkThenLatin.Replace(t, "$1 $2");
                t = LatinThenCjk.Replace(t, "$1 $2");
            }
            return t.Trim();
        }
    }

    /// <summary>Records 16 kHz / 16-bit mono from the default microphone with waveIn (polled, no callbacks).</summary>
    public sealed class Recorder : IDisposable
    {
        public const int Rate = 16000;
        const int BufferMs = 100, Buffers = 8;
        const int WAVE_MAPPER = -1, WHDR_DONE = 1, CALLBACK_NULL = 0;

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEFORMATEX { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }

        [StructLayout(LayoutKind.Sequential)]
        struct WAVEHDR { public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded; public IntPtr dwUser; public uint dwFlags, dwLoops; public IntPtr lpNext, reserved; }

        [DllImport("winmm.dll")] static extern int waveInOpen(out IntPtr h, int dev, ref WAVEFORMATEX fmt, IntPtr cb, IntPtr inst, int flags);
        [DllImport("winmm.dll")] static extern int waveInPrepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] static extern int waveInUnprepareHeader(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] static extern int waveInAddBuffer(IntPtr h, IntPtr hdr, int size);
        [DllImport("winmm.dll")] static extern int waveInStart(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveInReset(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveInClose(IntPtr h);
        [DllImport("winmm.dll")] static extern int waveInGetNumDevs();

        IntPtr _h;
        readonly IntPtr[] _hdrs = new IntPtr[Buffers];
        readonly List<short> _samples = new List<short>();
        Thread _poll;
        volatile bool _stop;
        readonly object _gate = new object();
        static readonly int HdrSize = Marshal.SizeOf(typeof(WAVEHDR));

        /// <summary>Current input level 0..1 (RMS of the last buffer, for the meter).</summary>
        public double Level { get; private set; }
        public double Seconds { get { lock (_gate) return _samples.Count / (double)Rate; } }
        public double Peak { get; private set; }

        public void Start()
        {
            if (waveInGetNumDevs() <= 0) throw new Exception("没有找到麦克风");
            var fmt = new WAVEFORMATEX { wFormatTag = 1, nChannels = 1, nSamplesPerSec = Rate, wBitsPerSample = 16, nBlockAlign = 2, nAvgBytesPerSec = Rate * 2 };
            int r = waveInOpen(out _h, WAVE_MAPPER, ref fmt, IntPtr.Zero, IntPtr.Zero, CALLBACK_NULL);
            if (r != 0) throw new Exception("麦克风打不开（" + r + "）：请在 Windows 设置 → 隐私和安全性 → 麦克风 里允许桌面应用访问麦克风");
            int bytes = Rate * 2 * BufferMs / 1000;
            for (int i = 0; i < Buffers; i++)
            {
                var hdr = new WAVEHDR { lpData = Marshal.AllocHGlobal(bytes), dwBufferLength = (uint)bytes };
                _hdrs[i] = Marshal.AllocHGlobal(HdrSize);
                Marshal.StructureToPtr(hdr, _hdrs[i], false);
                waveInPrepareHeader(_h, _hdrs[i], HdrSize);
                waveInAddBuffer(_h, _hdrs[i], HdrSize);
            }
            r = waveInStart(_h);
            if (r != 0) { Dispose(); throw new Exception("麦克风无法开始录音（" + r + "）"); }
            _poll = new Thread(Poll) { IsBackground = true, Name = "voice-rec" };
            _poll.Start();
        }

        void Poll()
        {
            while (!_stop)
            {
                Thread.Sleep(20);
                Drain(true);
            }
        }

        void Drain(bool requeue)
        {
            foreach (var p in _hdrs)
            {
                if (p == IntPtr.Zero) continue;
                var hdr = (WAVEHDR)Marshal.PtrToStructure(p, typeof(WAVEHDR));
                if ((hdr.dwFlags & WHDR_DONE) == 0) continue;
                int n = (int)hdr.dwBytesRecorded / 2;
                if (n > 0)
                {
                    var buf = new short[n];
                    Marshal.Copy(hdr.lpData, buf, 0, n);
                    double sum = 0; int peak = 0;
                    foreach (var s in buf) { sum += (double)s * s; peak = Math.Max(peak, Math.Abs((int)s)); }
                    Level = Math.Min(1, Math.Sqrt(sum / n) / 6000.0);
                    Peak = Math.Max(Peak, peak / 32768.0);
                    lock (_gate) _samples.AddRange(buf);
                }
                if (!requeue) continue;
                hdr.dwFlags &= ~(uint)WHDR_DONE; hdr.dwBytesRecorded = 0;
                Marshal.StructureToPtr(hdr, p, false);
                waveInAddBuffer(_h, p, HdrSize);
            }
        }

        /// <summary>Stops recording and returns everything captured.</summary>
        public short[] Stop()
        {
            if (_h == IntPtr.Zero) return new short[0];
            _stop = true;
            _poll?.Join(500);
            waveInReset(_h); // marks the pending buffers done
            Drain(false);
            lock (_gate) { var a = _samples.ToArray(); Dispose(); return a; }
        }

        public void Dispose()
        {
            _stop = true;
            if (_h != IntPtr.Zero)
            {
                waveInReset(_h);
                foreach (var p in _hdrs) if (p != IntPtr.Zero) waveInUnprepareHeader(_h, p, HdrSize);
                waveInClose(_h);
                _h = IntPtr.Zero;
            }
            for (int i = 0; i < _hdrs.Length; i++)
            {
                if (_hdrs[i] == IntPtr.Zero) continue;
                var hdr = (WAVEHDR)Marshal.PtrToStructure(_hdrs[i], typeof(WAVEHDR));
                if (hdr.lpData != IntPtr.Zero) Marshal.FreeHGlobal(hdr.lpData);
                Marshal.FreeHGlobal(_hdrs[i]);
                _hdrs[i] = IntPtr.Zero;
            }
        }
    }
}
