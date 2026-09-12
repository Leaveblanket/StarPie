using System;
using StarPie.PluginRuntime.Lifecycle;

namespace StarPie.PluginRuntime.Registry
{
    /// <summary>能力调用被守卫拒绝：插件不处于活动态（尚未启动完成、已开始卸载或已隔离）。</summary>
    public sealed class CapabilityUnavailableException : InvalidOperationException
    {
        /// <summary>构造"插件不可用"异常。</summary>
        /// <param name="pluginId">插件 id。</param>
        /// <param name="capabilityId">能力 id。</param>
        /// <param name="state">拒绝时的插件生命周期状态。</param>
        public CapabilityUnavailableException(
            string pluginId,
            string capabilityId,
            PluginLifecycleState state)
            : base($"插件 {pluginId} 当前状态为 {state}，拒绝能力调用：{capabilityId}")
        {
            PluginId = pluginId;
            CapabilityId = capabilityId;
            State = state;
        }

        /// <summary>插件 id。</summary>
        public string PluginId { get; }

        /// <summary>能力 id。</summary>
        public string CapabilityId { get; }

        /// <summary>拒绝时的插件生命周期状态。</summary>
        public PluginLifecycleState State { get; }
    }

    /// <summary>能力调用被守卫拒绝：该插件已因连续失败熔断（并已隔离）。</summary>
    public sealed class CapabilityCircuitOpenException : InvalidOperationException
    {
        /// <summary>构造"熔断已断开"异常。</summary>
        /// <param name="pluginId">插件 id。</param>
        /// <param name="capabilityId">能力 id。</param>
        /// <param name="failureCount">触发熔断的连续失败次数。</param>
        public CapabilityCircuitOpenException(string pluginId, string capabilityId, int failureCount)
            : base($"插件 {pluginId} 已因连续 {failureCount} 次失败熔断，拒绝能力调用：{capabilityId}")
        {
            PluginId = pluginId;
            CapabilityId = capabilityId;
            FailureCount = failureCount;
        }

        /// <summary>插件 id。</summary>
        public string PluginId { get; }

        /// <summary>能力 id。</summary>
        public string CapabilityId { get; }

        /// <summary>触发熔断的连续失败次数。</summary>
        public int FailureCount { get; }
    }

    /// <summary>能力调用超时（仅异步路径可强制中断；超时计一次失败）。</summary>
    public sealed class CapabilityCallTimeoutException : TimeoutException
    {
        /// <summary>构造"调用超时"异常。</summary>
        /// <param name="pluginId">插件 id。</param>
        /// <param name="capabilityId">能力 id。</param>
        /// <param name="timeout">生效的超时。</param>
        public CapabilityCallTimeoutException(string pluginId, string capabilityId, TimeSpan timeout)
            : base($"能力调用超时（{timeout.TotalMilliseconds:0} ms）：{pluginId} / {capabilityId}")
        {
            PluginId = pluginId;
            CapabilityId = capabilityId;
            Timeout = timeout;
        }

        /// <summary>插件 id。</summary>
        public string PluginId { get; }

        /// <summary>能力 id。</summary>
        public string CapabilityId { get; }

        /// <summary>生效的超时。</summary>
        public TimeSpan Timeout { get; }
    }
}
