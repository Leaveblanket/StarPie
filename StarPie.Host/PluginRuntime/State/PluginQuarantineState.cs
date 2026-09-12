using System.Collections.Generic;
using StarPie.PluginRuntime.Diagnostics;

namespace StarPie.PluginRuntime.State
{
    /// <summary>
    /// 插件的隔离状态：进入隔离即写宿主状态，本次进程不再调用该插件，重启也不自动重试。
    /// </summary>
    /// <param name="Reason">首次进入隔离的原因；后续回收续做与重试失败不改写它。</param>
    /// <param name="Since">首次进入隔离的时间。</param>
    public sealed record PluginQuarantineState(string Reason, DateTimeOffset Since)
    {
        /// <summary>可定位的残留清单（未回收资源）；无残留时为空。</summary>
        public IReadOnlyList<PluginResidual> Residuals { get; init; } = Array.Empty<PluginResidual>();
    }
}
