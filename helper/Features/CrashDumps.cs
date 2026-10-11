using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;

namespace Yiwei
{
    /// <summary>
    /// W45 崩溃 minidump 自动收集：
    /// · 一维助手自己未处理的异常 → MiniDumpWriteDump 写到 %LocalAppData%\YiweiIME\CrashDumps；
    /// · 输入法服务 / 部署工具 / 助手崩溃 → Windows 错误报告 LocalDumps（安装包写 HKLM，同一文件夹，每个程序最多 5 个）；
    /// · 「导出诊断信息」把最近 3 个 dump 一起打包（只含崩溃时的内存片段与调用栈，不含词库文件）。
    /// </summary>
    public static class CrashDumps
    {
        public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YiweiIME", "CrashDumps");

        [DllImport("dbghelp.dll", SetLastError = true)]
        static extern bool MiniDumpWriteDump(IntPtr process, uint pid, SafeHandle file, uint type, IntPtr exceptionParam, IntPtr userStream, IntPtr callback);

        public static void Install()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Write("YiweiHelper");
        }

        public static string Write(string name)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var path = Path.Combine(Dir, name + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".dmp");
                using (var p = Process.GetCurrentProcess())
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                    MiniDumpWriteDump(p.Handle, (uint)p.Id, fs.SafeFileHandle, 0x00000004 /* MiniDumpWithHandleData */, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                foreach (var old in Directory.GetFiles(Dir, name + "-*.dmp").OrderByDescending(f => f).Skip(5)) try { File.Delete(old); } catch { }
                return path;
            }
            catch (Exception e) { Log.Write("minidump: " + e.Message); return null; }
        }

        public static string[] Recent(int n = 3) =>
            Directory.Exists(Dir) ? Directory.GetFiles(Dir, "*.dmp").OrderByDescending(File.GetLastWriteTime).Take(n).ToArray() : new string[0];

        /// <summary>Adds the newest dumps to a diagnostics zip (called by Diagnostics.Export).</summary>
        public static void AddTo(ZipArchive zip)
        {
            foreach (var f in Recent())
                try { zip.CreateEntryFromFile(f, "崩溃转储/" + Path.GetFileName(f), CompressionLevel.Optimal); } catch { }
        }
    }
}
