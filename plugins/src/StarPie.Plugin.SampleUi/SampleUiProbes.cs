using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace StarPie.Plugin.SampleUi
{
    /// <summary>
    /// 卸载矩阵探针表：本插件创建的每个插件侧对象（VM/窗口/回调/动画/委托）在创建处登记
    /// <see cref="WeakReference"/>，测试据此断言"卸载后探针对象全部可回收"。
    /// </summary>
    /// <remarks>
    /// 探针只存弱引用，不构成任何强根：登记行为本身不得影响对象回收判定。
    /// 键名是本插件与卸载矩阵测试的稳定契约，只增不改。
    /// </remarks>
    public static class SampleUiProbes
    {
        private static readonly object Sync = new();
        private static readonly Dictionary<string, WeakReference> Probes = new(StringComparer.Ordinal);

        /// <summary>登记一个探针目标（弱引用；同键覆盖）。登记在 UI 线程的创建路径发生，读侧任意线程。</summary>
        public static void Track(string name, object target)
        {
            lock (Sync)
            {
                Probes[name] = new WeakReference(target);
            }
        }

        /// <summary>取当前全部探针快照（测试经反射读取；快照是弱引用，不构成强根）。</summary>
        public static IReadOnlyDictionary<string, WeakReference> Snapshot()
        {
            lock (Sync)
            {
                return new ReadOnlyDictionary<string, WeakReference>(
                    new Dictionary<string, WeakReference>(Probes));
            }
        }
    }
}
