namespace StarPie.PluginRuntime.Loading
{
    /// <summary>
    /// 跨 ALC 共享程序集的判定：只有 SDK 共享契约与框架程序集允许由默认 ALC 解析，
    /// 其余程序集一律经插件自己的 <see cref="PluginLoadContext"/> 私有加载。
    /// </summary>
    /// <remarks>
    /// 宿主内核不引用 WPF 契约面，<c>StarPie.Sdk.Wpf</c> 与 WPF 桌面程序集在此按名字判定；
    /// 框架判定与全仓「平台程序集」判定一致（<c>System*</c> 前缀 + 若干基础程序集 + WPF 桌面程序集名），
    /// 两个 SDK 契约名与 <c>StarPie.Sdk.Wpf</c> 的默认 ALC 装载政策保持同一集合。
    /// </remarks>
    public static class PluginSharedAssemblyPolicy
    {
        /// <summary>必须由默认 ALC 加载的 SDK 共享契约程序集名。</summary>
        private static readonly HashSet<string> SharedContractAssemblyNames = new(StringComparer.Ordinal)
        {
            "StarPie.Sdk",
            "StarPie.Sdk.Wpf",
        };

        /// <summary>不随插件包分发、由运行时提供的框架程序集名（<c>System*</c> 之外的例外项）。</summary>
        private static readonly HashSet<string> FrameworkAssemblyNames = new(StringComparer.Ordinal)
        {
            "netstandard",
            "mscorlib",
            "Microsoft.CSharp",
            "Microsoft.VisualBasic",
            "PresentationFramework",
            "PresentationCore",
            "WindowsBase",
            "ReachFramework",
        };

        /// <summary>该程序集名是否必须回退默认 ALC 解析（true 时插件 ALC 不私有加载）。</summary>
        public static bool MustResolveFromDefaultAlc(string? assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                return false;
            }

            return SharedContractAssemblyNames.Contains(assemblyName)
                || FrameworkAssemblyNames.Contains(assemblyName)
                || assemblyName.StartsWith("System", StringComparison.Ordinal);
        }
    }
}
