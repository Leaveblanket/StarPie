using StarPie.Manifest;
using StarPie.PluginRuntime.Admission;

namespace StarPie.PluginRuntime.Loading
{
    /// <summary>
    /// 一次插件装载请求：已解析的清单、包目录与发现期准入结果。
    /// </summary>
    /// <remarks>
    /// 装载管线在装载前重跑清单与包校验（发现到装载之间包内容可能变化），
    /// 调用方无需自行校验；准入为拒绝的请求直接走拒绝路径。
    /// </remarks>
    public sealed record PluginLoadRequest(
        PluginManifest Manifest,
        string PackageDirectory,
        PluginAdmissionDecision Admission);
}
