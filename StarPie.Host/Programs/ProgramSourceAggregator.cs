using System.Diagnostics;
using StarPie.PluginRuntime.Registry;
using StarPie.Services.Programs;

namespace StarPie.Programs
{
    /// <summary>
    /// 程序来源聚合：把能力表里的内置来源与各插件来源合成一份候选程序列表，供程序选择器消费。
    /// </summary>
    /// <remarks>
    /// 顺序语义沿用能力表：内置来源在前，其后按插件 priority 与 plugin id 稳定序；同路径候选按
    /// <see cref="ProgramCatalog.MergeSources"/> 去重并升级显示名。单个来源抛异常（调用超时、插件
    /// 隔离等）只跳过该来源——插件缺席、停用或失败都必须留下可用列表，这是本扩展点的降级行为；
    /// 失败本身由能力守卫记录并熔断，不在此处重复记账。
    /// </remarks>
    public sealed class ProgramSourceAggregator : IProgramScanner
    {
        private readonly CapabilityRegistry _registry;

        /// <summary>构造聚合器：能力表须已声明程序来源契约（含内置条目）。</summary>
        public ProgramSourceAggregator(CapabilityRegistry capabilityRegistry)
        {
            ArgumentNullException.ThrowIfNull(capabilityRegistry);
            _registry = capabilityRegistry;
        }

        /// <inheritdoc/>
        public IReadOnlyList<ProgramEntry> ScanInstalledPrograms()
        {
            var candidates = new List<ProgramEntry>();
            foreach (IProgramScanner source in _registry.GetAll<IProgramScanner>())
            {
                try
                {
                    candidates.AddRange(source.ScanInstalledPrograms());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Program source skipped: {ex.Message}");
                }
            }

            List<ProgramEntry> merged = ProgramCatalog.MergeSources(candidates);
            merged.Sort((left, right) =>
                string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase));
            return merged;
        }
    }
}
