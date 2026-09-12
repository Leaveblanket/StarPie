using StarPie.Manifest;

namespace StarPie.PluginRuntime.Manifest
{
    /// <summary>
    /// 清单解析结果：<see cref="Errors"/> 非空即解析失败，<see cref="Manifest"/> 为 null。
    /// </summary>
    public sealed record PluginManifestParseResult(PluginManifest? Manifest, IReadOnlyList<string> Errors);
}
