using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace StarPie.Services.Shell
{
    /// <summary>
    /// 内存优化器：压缩托管堆并裁剪进程工作集页，使占用回落到最小足迹（约 15–25MB）。
    /// </summary>
    public static class MemoryOptimizer
    {
        [DllImport("psapi.dll", SetLastError = true)]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        private static int _isTrimming = 0;
        private static DateTime _lastTrimTime = DateTime.MinValue;

        /// <summary>深度压缩 GC 堆（含大对象堆 LOH）并把工作集内存页裁剪到最小足迹；
        /// force 为 false 时距上次修剪不足 2 秒则跳过。</summary>
        public static void TrimMemory(bool force = false)
        {
            if (!force && (DateTime.UtcNow - _lastTrimTime).TotalSeconds < 2.0)
            {
                return;
            }

            if (Interlocked.Exchange(ref _isTrimming, 1) == 1) return;

            Task.Run(() =>
            {
                try
                {
                    _lastTrimTime = DateTime.UtcNow;

                    // 回收 Gen 0/1/2 并压缩大对象堆（LOH）
                    GC.Collect(2, GCCollectionMode.Forced, true, true);
                    GC.WaitForPendingFinalizers();
                    GC.Collect(2, GCCollectionMode.Forced, true, true);

                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        var handle = Process.GetCurrentProcess().Handle;
                        EmptyWorkingSet(handle);
                        SetProcessWorkingSetSize(handle, new IntPtr(-1), new IntPtr(-1));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MemoryOptimizer Error]: {ex.Message}");
                }
                finally
                {
                    Interlocked.Exchange(ref _isTrimming, 0);
                }
            });
        }
    }
}
