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
    /// <param name="Manifest">已解析的 plugin.json 清单。</param>
    /// <param name="PackageDirectory">插件包目录（入口程序集与私有依赖所在）。</param>
    /// <param name="Admission">发现期准入结果；拒绝即不装载。</param>
    public sealed record PluginLoadRequest(
        PluginManifest Manifest,
        string PackageDirectory,
        PluginAdmissionDecision Admission);
}
