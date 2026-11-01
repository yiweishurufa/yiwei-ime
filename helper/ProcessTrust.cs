using System;
using System.Runtime.InteropServices;

namespace Yiwei
{
    /// <summary>
    /// 只用于日志：说明这个助手进程的完整性级别（低/中/高）和是否在 AppContainer 沙箱里，
    /// 用来排查「语言栏菜单点了没反应」——从沙箱程序启动的助手进程连不上常驻助手。
    /// </summary>
    static class ProcessTrust
    {
        const int TokenIntegrityLevel = 25, TokenIsAppContainer = 29;
        const uint TOKEN_QUERY = 0x0008;

        [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("advapi32.dll", SetLastError = true)] static extern bool GetTokenInformation(IntPtr token, int cls, IntPtr info, int len, out int retLen);
        [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
        [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);

        public static string Describe()
        {
            IntPtr tok = IntPtr.Zero, buf = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, out tok)) return "?";
                GetTokenInformation(tok, TokenIntegrityLevel, IntPtr.Zero, 0, out int len);
                if (len <= 0) return "?";
                buf = Marshal.AllocHGlobal(len);
                if (!GetTokenInformation(tok, TokenIntegrityLevel, buf, len, out len)) return "?";
                var sid = Marshal.ReadIntPtr(buf); // TOKEN_MANDATORY_LABEL.Label.Sid
                int n = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
                int rid = Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(n - 1)));
                string level = rid >= 0x4000 ? "system" : rid >= 0x3000 ? "high" : rid >= 0x2000 ? "medium" : rid >= 0x1000 ? "low" : "untrusted";
                Marshal.FreeHGlobal(buf); buf = Marshal.AllocHGlobal(4);
                bool ac = GetTokenInformation(tok, TokenIsAppContainer, buf, 4, out _) && Marshal.ReadInt32(buf) != 0;
                return level + (ac ? "+appcontainer" : "") + " session=" + System.Diagnostics.Process.GetCurrentProcess().SessionId;
            }
            catch (Exception e) { return "? " + e.GetType().Name; }
            finally
            {
                if (buf != IntPtr.Zero) Marshal.FreeHGlobal(buf);
                if (tok != IntPtr.Zero) CloseHandle(tok);
            }
        }
    }
}
