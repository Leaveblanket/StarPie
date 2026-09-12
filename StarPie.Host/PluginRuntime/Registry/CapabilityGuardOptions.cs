using System;

namespace StarPie.PluginRuntime.Registry
{
    /// <summary>能力守卫阈值：单次调用超时与连续失败熔断阈值。</summary>
    public sealed record CapabilityGuardOptions
    {
        /// <summary>默认阈值：5 秒超时、连续 3 次失败熔断。</summary>
        public static CapabilityGuardOptions Default { get; } = new();

        /// <summary>异步调用的默认超时（超过即记失败）；同步调用无法强制中断，见守卫说明。</summary>
        public TimeSpan CallTimeout { get; init; } = TimeSpan.FromSeconds(5);

        /// <summary>连续失败达到该次数即熔断并隔离插件。</summary>
        public int FailureThreshold { get; init; } = 3;
    }
}
