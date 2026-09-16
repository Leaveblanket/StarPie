using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace StarPie.ShellIntegration
{
    /// <summary>
    /// 内存整理：纯托管 GC 收敛（两轮全量压缩 + finalizer 排空），GC 预算内自我约束。
    /// 堆预算由 runtimeconfig.template.json 的 System.GC.HeapHardLimit（256 MiB）设定，
    /// 逼近上限时 GC 自动提升回收激进度；本类不再触碰工作集裁剪。
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class MemoryOptimizer
    {
        private static int _isTrimming = 0;
        private static DateTime _lastTrimTime = DateTime.MinValue;

        /// <summary>两轮全量压缩 GC（含大对象堆 LOH）+ finalizer 排空；
        /// force 为 false 时距上次整理不足 2 秒则跳过。</summary>
        public static void CollectGarbage(bool force = false)
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

                    // 两轮：终结器执行可能复活对象并产生新垃圾，排空后再收一次
                    GC.Collect(2, GCCollectionMode.Forced, true, true);
                    GC.WaitForPendingFinalizers();
                    GC.Collect(2, GCCollectionMode.Forced, true, true);
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
