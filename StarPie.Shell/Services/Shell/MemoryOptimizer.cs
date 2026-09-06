using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace WinPieGestures.Services.Shell
{
    /// <summary>
    /// High-efficiency memory optimizer for WinPieGestures.
    /// Compacts heap and trims process working set pages down to minimal footprint (~15-25MB).
    /// </summary>
    public static class MemoryOptimizer
    {
        [DllImport("psapi.dll", SetLastError = true)]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        private static int _isTrimming = 0;
        private static DateTime _lastTrimTime = DateTime.MinValue;

        /// <summary>
        /// Aggressively compacts LOH/GC heap and trims working set memory pages down to minimal footprint.
        /// </summary>
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

                    // Collect Gen 0, 1, 2 and compact Large Object Heap (LOH)
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
