using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StarPie.Manifest;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Diagnostics;
using StarPie.PluginRuntime.Lifecycle;
using StarPie.PluginRuntime.Loading;
using StarPie.PluginRuntime.Manifest;
using StarPie.PluginRuntime.State;
using StarPie.PluginRuntime.Unloading;

namespace StarPie.PluginRuntime.Hosting
{
    /// <summary>
    /// 宿主侧插件运行时：启动扫描之后装载「启用且准入通过」的 headless 插件，并提供停用与再启用入口。
    /// </summary>
    /// <remarks>
    /// 本类只做编排与宿主状态落盘，装载/卸载本身归两条管线；插件对象的持有权在装载成功后经
    /// <see cref="PluginUnloadRequest.FromLoaded"/> 交给本对象，停用时再交接给卸载管线。
    /// 拒绝与隔离都不是错误路径：拒绝的包不装载（准入结论已由扫描层落状态），隔离的包经本入口不重试，
    /// 换来的 <see cref="PluginLoadResult"/> 仍是隔离结论——重启与显式重试属管理面，不在此处自动发生。
    /// 隔离是持久状态：装载/卸载落入隔离即写进宿主状态，下次启动的扫描不会覆盖它，本类因此不再装载它；
    /// 隔离插件留下的 ALC 在重启前不可回收（隔离态按设计不自动重试），停用入口会先尝试回收资源。
    /// </remarks>
    public sealed class PluginRuntimeHost
    {
        private const string ManifestFileName = "plugin.json";

        private readonly PluginStartupScanner _startupScanner;
        private readonly PluginStateStore _stateStore;
        private readonly PluginLoadPipeline _loadPipeline;
        private readonly PluginUnloadPipeline _unloadPipeline;
        private readonly Dictionary<string, PluginUnloadRequest> _handovers =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>构造插件运行时：扫描器、状态存储与两条管线均为显式依赖。</summary>
        public PluginRuntimeHost(
            PluginStartupScanner startupScanner,
            PluginStateStore stateStore,
            PluginLoadPipeline loadPipeline,
            PluginUnloadPipeline unloadPipeline)
        {
            ArgumentNullException.ThrowIfNull(startupScanner);
            ArgumentNullException.ThrowIfNull(stateStore);
            ArgumentNullException.ThrowIfNull(loadPipeline);
            ArgumentNullException.ThrowIfNull(unloadPipeline);

            _startupScanner = startupScanner;
            _stateStore = stateStore;
            _loadPipeline = loadPipeline;
            _unloadPipeline = unloadPipeline;
        }

        /// <summary>最近一次启动扫描结果（<see cref="StartAsync"/> 之后的快照）。</summary>
        public PluginStartupReport? Report { get; private set; }

        /// <summary>当前活动态插件 id（按装载顺序）；被隔离或停用的插件不在列。</summary>
        public IReadOnlyList<string> ActivePluginIds => _handovers
            .Where(pair => pair.Value.Lifecycle.Current == PluginLifecycleState.Active)
            .Select(pair => pair.Key)
            .ToList();

        /// <summary>启动期：刷新启动扫描并按宿主状态装载全部「启用且准入通过」的插件。</summary>
        /// <param name="cancellationToken">装载期的取消令牌。</param>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            Report = _startupScanner.Scan();
            foreach (PluginStartupReportEntry entry in Report.Plugins
                .OrderBy(item => item.PluginId, StringComparer.Ordinal))
            {
                if (ShouldLoad(entry) && !_handovers.ContainsKey(entry.PluginId))
                {
                    await LoadEntryAsync(entry, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 停用插件：安全点卸载并落「停用」意图；插件未装载时只落意图。
        /// </summary>
        /// <param name="pluginId">插件 id。</param>
        /// <param name="cancellationToken">排空与停用的取消令牌。</param>
        /// <returns>发生卸载时的卸载结果；插件未装载时为 null。</returns>
        public async Task<PluginUnloadResult?> DisableAsync(string pluginId, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

            PluginUnloadResult? result = null;
            if (_handovers.TryGetValue(pluginId, out PluginUnloadRequest? request))
            {
                result = await _unloadPipeline.UnloadAsync(request, cancellationToken).ConfigureAwait(false);
                if (result.Status == PluginUnloadStatus.Unloaded)
                {
                    _handovers.Remove(pluginId);
                }

                // 未归零或回收未过：保留交接对象——隔离后仍可在下一次安全点续做回收，不谎报已停用。
                if (result.Status == PluginUnloadStatus.Quarantined)
                {
                    PersistQuarantine(pluginId, result.FailureReason);
                }
            }

            SetEnabled(pluginId, enabled: false);
            return result;
        }

        /// <summary>
        /// 启用插件：落「启用」意图并装载；已隔离的插件不在此自动重试（隔离是持久状态）。
        /// </summary>
        /// <param name="pluginId">插件 id。</param>
        /// <param name="cancellationToken">装载期的取消令牌。</param>
        /// <returns>
        /// 装载结果；被拒绝的包返回拒绝结果，已隔离的插件返回携带既有隔离原因的隔离结果。
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// 插件不在最近一次启动扫描结果里，或已处于活动态（重载必须先停用，再启用）。
        /// </exception>
        public async Task<PluginLoadResult> EnableAsync(string pluginId, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

            PluginStartupReportEntry entry = RequireScannedEntry(pluginId);
            if (_handovers.TryGetValue(pluginId, out PluginUnloadRequest? active))
            {
                // 已装载即不重复装载：覆盖持有权会丢掉落单实例的 StopAsync 与 ALC 回收。
                throw new InvalidOperationException(
                    $"插件已在活动态 {active.Lifecycle.Current}：{pluginId}；重载请先停用再启用");
            }

            PluginStateEntry state = _stateStore.Current.GetOrCreate(pluginId);
            if (state.Quarantine is { } quarantine)
            {
                string reason = $"插件已隔离（{quarantine.Reason}）：需显式重试或停用，不自动重试装载";
                var lifecycle = new PluginLifecycleStateMachine();
                lifecycle.Quarantine(reason);
                return new PluginLoadResult(
                    pluginId,
                    PluginLoadStatus.Quarantined,
                    reason,
                    null,
                    null,
                    lifecycle,
                    null);
            }

            SetEnabled(pluginId, enabled: true);
            return await LoadEntryAsync(entry, cancellationToken).ConfigureAwait(false)
                ?? Reject(pluginId, "包不可用：清单缺失或目录已不在");
        }

        /// <summary>启动期装载判定：启用意图 + 不在隔离 + 准入不是拒绝。</summary>
        private static bool ShouldLoad(PluginStartupReportEntry entry)
            => entry.Enabled
                && entry.Quarantine is null
                && entry.Admission != PluginAdmission.Rejected;

        /// <summary>装载一个已扫描条目；成功即把插件对象所有权交接给本对象。</summary>
        private async Task<PluginLoadResult?> LoadEntryAsync(
            PluginStartupReportEntry entry,
            CancellationToken cancellationToken)
        {
            PluginLoadRequest? request = TryCreateLoadRequest(entry);
            if (request is null)
            {
                return null;
            }

            PluginLoadResult result = await _loadPipeline
                .LoadAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (result.Status == PluginLoadStatus.Active && result.Scope is not null)
            {
                _handovers[entry.PluginId] = PluginUnloadRequest.FromLoaded(result);
            }
            else if (result.Status == PluginLoadStatus.Quarantined)
            {
                // 隔离是持久状态：写进宿主状态后，下次启动扫描保留它，本类不再自动重试装载。
                PersistQuarantine(entry.PluginId, result.FailureReason);
            }

            return result;
        }

        /// <summary>按启动扫描条目重读包内清单构造装载请求；清单/目录不可用（扫描后被移动）时返回 null。</summary>
        private static PluginLoadRequest? TryCreateLoadRequest(PluginStartupReportEntry entry)
        {
            string? packageDirectory = entry.PackagePaths.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(packageDirectory))
            {
                return null;
            }

            try
            {
                string manifestPath = Path.Combine(packageDirectory, ManifestFileName);
                if (!File.Exists(manifestPath))
                {
                    return null;
                }

                PluginManifest? manifest = PluginManifestParser.Parse(File.ReadAllText(manifestPath)).Manifest;
                return manifest is null
                    ? null
                    : new PluginLoadRequest(
                        manifest,
                        packageDirectory,
                        new PluginAdmissionDecision(entry.Admission, entry.AdmissionReason));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to read plugin package '{entry.PluginId}': {ex.Message}");
                return null;
            }
        }

        private PluginStartupReportEntry RequireScannedEntry(string pluginId)
            => Report?.Plugins.FirstOrDefault(
                entry => string.Equals(entry.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"插件不在启动扫描结果里：{pluginId}（先调用 {nameof(StartAsync)}）");

        /// <summary>落启用/停用意图（宿主状态是唯一权威，随写随存）。</summary>
        private void SetEnabled(string pluginId, bool enabled)
        {
            _stateStore.Current.GetOrCreate(pluginId).Enabled = enabled;
            _stateStore.Save();
        }

        /// <summary>落隔离状态（宿主状态是唯一权威；扫描只刷新准入与路径，不覆盖它）。</summary>
        private void PersistQuarantine(string pluginId, string? reason)
        {
            _stateStore.Current.GetOrCreate(pluginId).Quarantine = new PluginQuarantineState(
                string.IsNullOrWhiteSpace(reason) ? "装载或卸载失败" : reason,
                DateTimeOffset.Now);
            _stateStore.Save();
        }

        /// <summary>包不可用时的拒绝结果（未创建 ALC、未启动插件代码）。</summary>
        private static PluginLoadResult Reject(string pluginId, string reason)
            => new(
                pluginId,
                PluginLoadStatus.Rejected,
                reason,
                null,
                null,
                new PluginLifecycleStateMachine(),
                null);
    }
}
