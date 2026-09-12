using System.Reflection;
using StarPie.Abstractions;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Lifecycle;
using StarPie.PluginRuntime.Manifest;

namespace StarPie.PluginRuntime.Loading
{
    /// <summary>
    /// 插件装载管线：准入/校验 → 建 collectible ALC → 载入入口程序集并实例化入口类型 → 启动。
    /// </summary>
    /// <remarks>
    /// 每次装载尝试都新建 ALC 与状态机；拒绝路径不创建 ALC、不加载任何程序集，
    /// 重校验、装载与启动的失败都进入隔离并在结果中给出可读原因，不中断宿主启动流程。
    /// 装载完成后的 ALC 由卸载管线负责回收。
    /// </remarks>
    public sealed class PluginLoadPipeline
    {
        /// <summary>执行一次装载尝试。</summary>
        /// <param name="request">装载请求（清单、包目录与准入结果）。</param>
        /// <param name="cancellationToken">
        /// 传入插件启动方法的取消令牌；取消按中止处理（进入隔离，不保留半启动实例）。
        /// </param>
        /// <returns>装载结果三态之一，带生命周期机的转移记录。</returns>
        public async Task<PluginLoadResult> LoadAsync(
            PluginLoadRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var lifecycle = new PluginLifecycleStateMachine();
            string pluginId = ResolvePluginId(request);

            // 拒绝路径：准入拒绝不创建 ALC、不加载任何程序集。
            if (request.Admission.Status == PluginAdmission.Rejected)
            {
                return Reject(pluginId, lifecycle, request.Admission.Reason);
            }

            PluginLoadContext? loadContext = null;
            try
            {
                // 装载前重校验：发现到装载之间包内容可能变化，校验不通过同样走拒绝路径。
                IReadOnlyList<string> violations =
                    PluginManifestValidator.Validate(request.Manifest, request.PackageDirectory);
                if (violations.Count > 0)
                {
                    return Reject(pluginId, lifecycle, string.Join("；", violations));
                }

                lifecycle.Transition(PluginLifecycleState.Validated);
                lifecycle.Transition(PluginLifecycleState.Loading);
                string entryAssemblyPath = Path.Combine(
                    request.PackageDirectory,
                    request.Manifest.EntryAssembly!);
                loadContext = new PluginLoadContext(pluginId, entryAssemblyPath);
                Assembly entryAssembly = loadContext.LoadFromAssemblyPath(entryAssemblyPath);
                IPlugin plugin = CreateEntryInstance(entryAssembly, request.Manifest.EntryType!);

                lifecycle.Transition(PluginLifecycleState.Starting);
                await plugin
                    .StartAsync(new PluginHostContext(pluginId), cancellationToken)
                    .ConfigureAwait(false);

                lifecycle.Transition(PluginLifecycleState.Active);
                return new PluginLoadResult(
                    pluginId,
                    PluginLoadStatus.Active,
                    null,
                    plugin,
                    loadContext,
                    lifecycle);
            }
            catch (Exception ex)
            {
                // 重校验、装载与启动的任意异常都收在隔离态：宿主不因单个插件失败而中断启动。
                string reason = ex is OperationCanceledException
                    ? $"装载/启动被取消：{ex.Message}"
                    : $"装载/启动失败：{ex.GetType().Name}：{ex.Message}";
                lifecycle.Quarantine(reason);
                return new PluginLoadResult(
                    pluginId,
                    PluginLoadStatus.Quarantined,
                    reason,
                    null,
                    loadContext,
                    lifecycle);
            }
        }

        /// <summary>
        /// 从入口程序集解析入口类型并实例化：类型只在本次调用内存在，不写入任何宿主结构。
        /// </summary>
        private static IPlugin CreateEntryInstance(Assembly entryAssembly, string entryTypeName)
        {
            Type? entryType = entryAssembly.GetType(entryTypeName, throwOnError: false, ignoreCase: false);
            if (entryType is null)
            {
                throw new InvalidOperationException($"入口类型未找到：{entryTypeName}");
            }

            if (!typeof(IPlugin).IsAssignableFrom(entryType))
            {
                throw new InvalidOperationException($"入口类型未实现 IPlugin：{entryTypeName}");
            }

            return Activator.CreateInstance(entryType) as IPlugin
                ?? throw new InvalidOperationException($"入口类型无法实例化（需要公开无参构造函数）：{entryTypeName}");
        }

        private static string ResolvePluginId(PluginLoadRequest request)
            => string.IsNullOrWhiteSpace(request.Manifest.Id)
                ? Path.GetFileName(request.PackageDirectory)
                : request.Manifest.Id!;

        private static PluginLoadResult Reject(
            string pluginId,
            PluginLifecycleStateMachine lifecycle,
            string reason)
            => new(pluginId, PluginLoadStatus.Rejected, reason, null, null, lifecycle);
    }
}
