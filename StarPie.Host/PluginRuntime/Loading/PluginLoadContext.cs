using System.Reflection;
using System.Runtime.Loader;

namespace StarPie.PluginRuntime.Loading
{
    /// <summary>
    /// 单个插件的 collectible ALC：共享契约与框架程序集回退默认 ALC，
    /// 其余程序集经 <see cref="AssemblyDependencyResolver"/> 从包内私有解析。
    /// </summary>
    /// <remarks>
    /// 回退默认 ALC 保证 SDK 与框架类型身份跨 ALC 唯一；私有解析保证插件私有依赖不与宿主或
    /// 其它插件共享类型身份（首期禁止插件间依赖）。解析不到的程序集交回默认 ALC 兜底。
    /// </remarks>
    public sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        /// <summary>为指定插件建一个可回收装载上下文；入口程序集路径决定私有依赖的分辨起点。</summary>
        public PluginLoadContext(string pluginId, string entryAssemblyPath)
            : base(BuildContextName(pluginId), isCollectible: true)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entryAssemblyPath);

            PluginId = pluginId;
            _resolver = new AssemblyDependencyResolver(entryAssemblyPath);
        }

        /// <summary>本上下文服务的插件 id。</summary>
        public string PluginId { get; }

        /// <inheritdoc/>
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (PluginSharedAssemblyPolicy.MustResolveFromDefaultAlc(assemblyName.Name))
            {
                return null;
            }

            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
        }

        /// <inheritdoc/>
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return libraryPath is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(libraryPath);
        }

        /// <summary>上下文名：同一插件重复装载时多个上下文同名，定位靠 id 而非名称唯一性。</summary>
        private static string BuildContextName(string pluginId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
            return $"StarPie.Plugin.{pluginId}";
        }
    }
}
