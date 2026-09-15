using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace StarPie.Kernel.ShellIntegration
{
    /// <summary>
    /// 当前进程的提权态探测，以及前台窗口是否运行在更高的完整性级别上。
    /// 与 <c>AutostartRegistry</c> 同属无状态系统调用静态工具——高级与系统页经委托注入消费
    /// （可测缝是 ViewModel 的注入委托，不是身份探测本身），壳层直接消费以决定提权入口的可见性
    /// 与高权限窗口的一次性告知。
    /// </summary>
    /// <remarks>
    /// 前台窗口探测刻意只用 <c>PROCESS_QUERY_LIMITED_INFORMATION</c> 这一受限子集：
    /// 它是唯一能跨完整性级别打开进程的查询权限（<c>PROCESS_QUERY_INFORMATION</c> 会被 UIPI 拒绝），
    /// 换取信息的最小面。UIPI 按完整性级别（不是按 token 特权）划界，故比较的是两者的
    /// TokenIntegrityLevel，而非是否管理员。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static class ProcessElevation
    {
        /// <summary>当前进程是否以管理员身份运行；探测失败按未提权处理。</summary>
        public static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(identity)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 前台窗口所属进程的完整性级别是否高于本进程。
        /// 返回 null 表示**未知**（无前台窗口、进程已退出、权限不足等任何探测失败）——
        /// 消费方按"不提示"处理，不得把未知当成"低于"。
        /// </summary>
        public static bool? IsForegroundWindowHigherIntegrity()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return null;

                GetWindowThreadProcessId(hwnd, out uint processId);
                if (processId == 0 || processId == (uint)Environment.ProcessId) return null;

                uint? foreground = TryGetIntegrityRid(processId);
                uint? self = TryGetOwnIntegrityRid();
                if (foreground is not { } higher || self is not { } mine) return null;

                return higher > mine;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>读指定进程令牌的完整性级别 RID；任一步失败返回 null。</summary>
        private static uint? TryGetIntegrityRid(uint processId)
        {
            IntPtr process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (process == IntPtr.Zero) return null;

            try
            {
                return TryGetIntegrityRidFromProcessHandle(process);
            }
            finally
            {
                CloseHandle(process);
            }
        }

        /// <summary>读本进程令牌的完整性级别 RID（伪句柄无需释放）。</summary>
        private static uint? TryGetOwnIntegrityRid()
            => TryGetIntegrityRidFromProcessHandle(GetCurrentProcess());

        private static uint? TryGetIntegrityRidFromProcessHandle(IntPtr process)
        {
            if (!OpenProcessToken(process, TokenQuery, out IntPtr token)) return null;

            try
            {
                // 缓冲区按 SID 最大长度给足（TOKEN_MANDATORY_LABEL + 最长 SID）；
                // 长度不足时 GetTokenInformation 失败返回 false，按未知处理。
                int size = Marshal.SizeOf<TokenMandatoryLabel>() + MaxSidLength;
                IntPtr buffer = Marshal.AllocHGlobal(size);
                try
                {
                    if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, size, out _)) return null;

                    TokenMandatoryLabel label = Marshal.PtrToStructure<TokenMandatoryLabel>(buffer);
                    if (label.Label.Sid == IntPtr.Zero) return null;

                    // 注意：GetSidSubAuthorityCount 返回的是**指向计数的指针**（PUCHAR），
                    // 不是计数本身；按值取会拿到被截断的指针，进而读到越界的子权威。
                    IntPtr countPtr = GetSidSubAuthorityCount(label.Label.Sid);
                    if (countPtr == IntPtr.Zero) return null;
                    byte subAuthorityCount = Marshal.ReadByte(countPtr);
                    if (subAuthorityCount == 0) return null;

                    // 完整性级别 RID 是 SID 的最后一个子权威。
                    IntPtr ridPtr = GetSidSubAuthority(label.Label.Sid, (uint)(subAuthorityCount - 1));
                    return ridPtr == IntPtr.Zero ? null : (uint)Marshal.ReadInt32(ridPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                CloseHandle(token);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SidAndAttributes
        {
            public IntPtr Sid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TokenMandatoryLabel
        {
            public SidAndAttributes Label;
        }

        private const int TokenIntegrityLevel = 25;
        private const uint TokenQuery = 0x0008;
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const int MaxSidLength = 68; // 最长 SID 的字节数（SECURITY_MAX_SID_SIZE）

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass, IntPtr tokenInformation, int tokenInformationLength, out int returnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern IntPtr GetSidSubAuthorityCount(IntPtr pSid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern IntPtr GetSidSubAuthority(IntPtr pSid, uint nSubAuthority);
    }
}
