namespace StarPie.PluginRuntime.Discovery
{
    /// <summary>
    /// 一个候选插件包：已定位包目录与 plugin.json 原文，尚未解析与校验。
    /// </summary>
    /// <remarks>
    /// <see cref="Violations"/> 承载发现期即成立的包内容违规（清单读取失败、包内出现宿主/SDK
    /// 程序集副本等）——非空即拒绝，不进入装载。
    /// </remarks>
    public sealed record PluginPackageCandidate(
        string DirectoryName,
        string DirectoryPath,
        PluginPackageOrigin Origin,
        string? ManifestJson,
        IReadOnlyList<string> Violations);
}
