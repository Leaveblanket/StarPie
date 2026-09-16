using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using StarPie.Configuration;

namespace StarPie.ShellIntegration
{
    /// <summary>
    /// 提权实例接管非提权实例的握手信道：**命名内核对象**（同用户跨完整性级别可开），
    /// 不用窗口消息——窗口消息只保留既有的置前语义，接管不经它。
    /// </summary>
    /// <remarks>
    /// 两个对象，都由首实例发布并持有到进程结束，第二个实例只按名字探测与置位：
    /// <list type="bullet">
    /// <item><b>标记事件</b>（<see cref="OwnerMarkerName"/>）：名字编码发布者的形态与权限态，一个对象
    /// 同时担三件事——"同形态首实例在此"（存在即真）、"它是否提权"（<c>_High</c> / <c>_Normal</c>）、
    /// "请让位"（提权新实例对它置位，首实例等到即走既有退出编排）。一个对象够用，是因为让位只有一个
    /// 方向：只有提权新实例会置位，也只有非提权首实例会等它。</item>
    /// <item><b>"提权未生效"事件</b>（<see cref="ElevationFailedEventName"/>）：接管没成时由即将退出的新实例
    /// 置位，首实例据此给用户一句明确的话。事件不带载荷，故"让位"与"没生效"必须是两个对象——
    /// 它们是两个方向不同、后果也不同的信号。</item>
    /// </list>
    /// 对象归首实例所有，是因为置位方向总是"更高的完整性级别写更低的对象"（强制标签只拦低写高），
    /// 反过来（非提权实例去建对象再让提权实例置位）会被拒。dev 实例的名字带独立后缀（与计划任务名、
    /// 配置目录同口径）：不同形态共用单实例互斥体，却不共用握手对象，故接管对它们不可能成立。
    /// **就绪判据只有"单实例互斥体已可取得"这一个来源**（见 <see cref="WaitForSingleInstanceRelease"/>），
    /// 不取自任何触发命令的退出码——实测该退出码只表示"任务被受理"，动作为空时同样返回成功。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static class InstanceHandover
    {
        /// <summary>等首实例让位的时限：数秒量级；超时即走失败语义（新实例退出，不留残进程）。</summary>
        public static readonly TimeSpan YieldTimeout = TimeSpan.FromSeconds(5);

        /// <summary>首实例标记事件名（纯字符串构造，供单测锁定形状）。</summary>
        public static string BuildOwnerMarkerName(bool devInstance, bool elevated)
            => $@"Global\StarPie_InstanceOwner{(elevated ? "_High" : "_Normal")}{(devInstance ? "_Dev" : string.Empty)}";

        /// <summary>"提权未生效"事件名（纯字符串构造，供单测锁定形状）。</summary>
        public static string BuildElevationFailedEventName(bool devInstance)
            => $@"Global\StarPie_InstanceHandover_Failed{(devInstance ? "_Dev" : string.Empty)}";

        /// <summary>本实例应发布的标记事件名（编码本实例的形态与权限态）。</summary>
        public static string OwnerMarkerName
            => BuildOwnerMarkerName(AppDataPaths.IsDevInstance, ProcessElevation.IsRunningAsAdministrator());

        /// <summary>本实例形态下的"提权未生效"事件名。</summary>
        public static string ElevationFailedEventName
            => BuildElevationFailedEventName(AppDataPaths.IsDevInstance);

        /// <summary>
        /// 发布"同形态首实例在此"的标记并返回句柄——**调用方必须持有到进程结束**：句柄一关对象即销毁，
        /// 标记随之消失（下一个实例会当它没在跑）。发布失败返回 null，下游退回置前退出那条路。
        /// </summary>
        public static EventWaitHandle? PublishOwnerMarker()
        {
            try
            {
                return new EventWaitHandle(false, EventResetMode.ManualReset, OwnerMarkerName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Handover] 首实例标记发布失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 读既有实例的形态。<paramref name="SameInstanceKind"/> 为假表示互斥体被**另一形态**的实例
        /// （dev / 正式）持有——两者的握手对象不共用，接管对它们不可能成立，调用方按置前退出处理。
        /// </summary>
        public static (bool SameInstanceKind, bool Elevated) ProbeOwner()
        {
            bool elevated = MarkerExists(BuildOwnerMarkerName(AppDataPaths.IsDevInstance, elevated: true));
            bool normal = MarkerExists(BuildOwnerMarkerName(AppDataPaths.IsDevInstance, elevated: false));
            return (normal || elevated, elevated);
        }

        /// <summary>
        /// 置位让位请求。目标恒为**非提权**首实例的标记名——能走到这一步的前提就是既有实例非提权
        /// （判定严格单向，见 <see cref="SingleInstanceGate.Resolve"/>）。标记不存在、或对象由更高
        /// 完整性级别持有而置位被拒，都返回 false：调用方按失败处理，不空等。
        /// </summary>
        public static bool RequestYield()
            => Set(BuildOwnerMarkerName(AppDataPaths.IsDevInstance, elevated: false));

        /// <summary>打开非提权首实例的标记事件（让位请求的接收端用）；尚未发布时返回 null。</summary>
        public static EventWaitHandle? OpenYieldRequestEvent()
        {
            try
            {
                return EventWaitHandle.OpenExisting(BuildOwnerMarkerName(AppDataPaths.IsDevInstance, elevated: false));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Handover] 打开让位请求事件失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 发布"提权未生效"事件并返回句柄——**调用方必须持有到进程结束**：事件对象归首实例所有，
        /// 置位方（提权新实例）打开的是同一个对象；句柄一关对象即销毁，提示就无处可投。
        /// 发布失败返回 null（下游降级为"不告知"，接管本身照旧）。
        /// </summary>
        public static EventWaitHandle? PublishElevationFailedEvent()
        {
            try
            {
                return new EventWaitHandle(false, EventResetMode.AutoReset, ElevationFailedEventName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Handover] 提权未生效事件发布失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>打开"提权未生效"事件（接收端用）；尚未发布时返回 null（该提示降级缺席）。</summary>
        public static EventWaitHandle? OpenElevationFailedEvent()
        {
            try
            {
                return EventWaitHandle.OpenExisting(ElevationFailedEventName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Handover] 打开提权未生效事件失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 置位"提权未生效"：本次提权尝试没成，请首实例替本实例把话说明白。置位方是**提权新实例**
        /// ——它即将退出，无处呈现；首实例是唯一还活着、且能呈现告知的一方。
        /// </summary>
        public static bool NotifyElevationNotApplied()
            => Set(BuildElevationFailedEventName(AppDataPaths.IsDevInstance));

        /// <summary>
        /// 等首实例释放单实例互斥体——**接管就绪判据的唯一来源**。首实例若未释放就消亡（崩溃），
        /// 锁随进程消失，本次启动接手（<see cref="AbandonedMutexException"/> 按取得处理）。
        /// </summary>
        public static bool WaitForSingleInstanceRelease(Mutex mutex, TimeSpan timeout)
        {
            try
            {
                return mutex.WaitOne(timeout);
            }
            catch (AbandonedMutexException)
            {
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Handover] 等首实例释放互斥体失败: {ex.Message}");
                return false;
            }
        }

        private static bool Set(string eventName)
        {
            try
            {
                using var marker = EventWaitHandle.OpenExisting(eventName);
                return marker.Set();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Handover] 置位 {eventName} 失败: {ex.Message}");
                return false;
            }
        }

        private static bool MarkerExists(string eventName)
        {
            try
            {
                using EventWaitHandle marker = EventWaitHandle.OpenExisting(eventName);
                return true;
            }
            catch
            {
                // 不存在，或由更高完整性级别的实例持有（强制标签拒绝写访问）——两者都按"读不到"处理。
                return false;
            }
        }
    }
}
