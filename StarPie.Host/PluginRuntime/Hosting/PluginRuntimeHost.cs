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
        private readonly PluginUninstallOptions _uninstallOptions;
        private readonly Dictionary<string, PluginUnloadRequest> _handovers =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PluginUnloadResult> _lastUnloadResults =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _loadedVersions =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>构造插件运行时：扫描器、状态存储与两条管线均为显式依赖。</summary>
        /// <param name="startupScanner">启动扫描器（发现/清单校验/准入与宿主状态刷新）。</param>
        /// <param name="stateStore">宿主状态存储（启用意图、挂起版本与隔离状态的唯一权威）。</param>
        /// <param name="loadPipeline">装载管线。</param>
        /// <param name="unloadPipeline">安全点卸载管线。</param>
        /// <param name="uninstallOptions">
        /// 彻底移除的三条宿主侧接缝（配置段删除、插件数据根、落盘冲刷）；缺省即都不动作。
        /// </param>
        public PluginRuntimeHost(
            PluginStartupScanner startupScanner,
            PluginStateStore stateStore,
            PluginLoadPipeline loadPipeline,
            PluginUnloadPipeline unloadPipeline,
            PluginUninstallOptions? uninstallOptions = null)
        {
            ArgumentNullException.ThrowIfNull(startupScanner);
            ArgumentNullException.ThrowIfNull(stateStore);
            ArgumentNullException.ThrowIfNull(loadPipeline);
            ArgumentNullException.ThrowIfNull(unloadPipeline);

            _startupScanner = startupScanner;
            _stateStore = stateStore;
            _loadPipeline = loadPipeline;
            _unloadPipeline = unloadPipeline;
            _uninstallOptions = uninstallOptions ?? PluginUninstallOptions.None;
        }

        /// <summary>最近一次启动扫描结果（<see cref="StartAsync"/> 之后的快照）。</summary>
        public PluginStartupReport? Report { get; private set; }

        /// <summary>当前活动态插件 id（按装载顺序）；被隔离或停用的插件不在列。</summary>
        public IReadOnlyList<string> ActivePluginIds => _handovers
            .Where(pair => pair.Value.Lifecycle.Current == PluginLifecycleState.Active)
            .Select(pair => pair.Key)
            .ToList();

        /// <summary>开发者模式是否已开启（管理面常显准入模式的依据）。</summary>
        public bool IsDeveloperModeEnabled => _stateStore.Current.DeveloperModeEnabled;

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

            PluginUnloadResult? result = await UnloadIfLoadedAsync(pluginId, cancellationToken).ConfigureAwait(false);
            SetEnabled(pluginId, enabled: false);
            return result;
        }

        /// <summary>
        /// 重载插件：走一次完整安全点卸载，再按当前包立即重新装载（不做无约束即时重载）。
        /// </summary>
        /// <param name="pluginId">插件 id。</param>
        /// <param name="cancellationToken">卸载与装载的取消令牌。</param>
        /// <returns>重新装载的结果；卸载未收口时结论是隔离，不把新实例叠在旧残留上。</returns>
        /// <exception cref="InvalidOperationException">插件不在最近一次启动扫描结果里。</exception>
        public async Task<PluginLoadResult> ReloadAsync(string pluginId, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

            PluginStartupReportEntry entry = RequireScannedEntry(pluginId);
            if (QuarantinedIfIsolated(pluginId) is { } quarantined)
            {
                // 隔离是持久状态，重载不是重试：要恢复必须先显式重试。
                return quarantined;
            }

            PluginUnloadResult? unload = await UnloadIfLoadedAsync(pluginId, cancellationToken)
                .ConfigureAwait(false);
            if (unload is { Status: PluginUnloadStatus.Quarantined })
            {
                return QuarantinedLoadResult(pluginId, unload.FailureReason);
            }

            SetEnabled(pluginId, enabled: true);
            return await LoadEntryAsync(entry, cancellationToken).ConfigureAwait(false)
                ?? Reject(pluginId, "包不可用：清单缺失或目录已不在");
        }

        /// <summary>
        /// 应用新版本：无界面插件就地卸载旧版本并按新包装载；界面插件的程序集留在进程内不可回收，
        /// 因此只隔离旧版本并登记挂起版本，等下次启动装载。
        /// </summary>
        /// <param name="pluginId">插件 id。</param>
        /// <param name="cancellationToken">卸载与装载的取消令牌。</param>
        /// <returns>
        /// 无界面插件返回新版本的装载结果；界面插件返回待重启结果；卸载未收口或已隔离时结论是隔离。
        /// </returns>
        /// <exception cref="InvalidOperationException">插件不在最近一次启动扫描结果里。</exception>
        public async Task<PluginLoadResult> UpdateAsync(string pluginId, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

            _ = RequireScannedEntry(pluginId);
            if (QuarantinedIfIsolated(pluginId) is { } quarantined)
            {
                return quarantined;
            }

            PluginUnloadResult? unload = await UnloadIfLoadedAsync(pluginId, cancellationToken)
                .ConfigureAwait(false);
            if (unload is { Status: PluginUnloadStatus.Quarantined })
            {
                return QuarantinedLoadResult(pluginId, unload.FailureReason);
            }

            // 重扫一次以读到磁盘上的新清单与准入结果：更新语义的前提是"按新包判定"。
            Report = _startupScanner.Scan();
            PluginStartupReportEntry entry = RequireScannedEntry(pluginId);

            if (entry.HasUi)
            {
                // 界面插件的旧程序集留在进程内，就地装载新版本会把两代实例叠在同一个进程里。
                PluginStateEntry state = _stateStore.Current.GetOrCreate(pluginId);
                state.PendingVersion = entry.Version;
                _stateStore.Save();
                _lastUnloadResults.Remove(pluginId);
                return PendingRestartResult(pluginId, entry.Version ?? "（未知）");
            }

            SetEnabled(pluginId, enabled: true);
            return await LoadEntryAsync(entry, cancellationToken).ConfigureAwait(false)
                ?? Reject(pluginId, "包不可用：清单缺失或目录已不在");
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
            if (_handovers.TryGetValue(pluginId, out PluginUnloadRequest? active)
                && active.Lifecycle.Current == PluginLifecycleState.Active)
            {
                // 已装载即不重复装载：覆盖持有权会丢掉落单实例的 StopAsync 与 ALC 回收。
                throw new InvalidOperationException(
                    $"插件已处于活动态：{pluginId}；重载请先停用再启用");
            }

            if (QuarantinedIfIsolated(pluginId) is { } quarantined)
            {
                return quarantined;
            }

            // 挂起版本不得在同一个进程里就地生效：界面插件的旧一代程序集仍在进程内，
            // 就地装载会把两代实例叠在一起；本次只落启用意图，等下次启动装载。
            if (_stateStore.Current.GetOrCreate(pluginId).PendingVersion is { } pending)
            {
                SetEnabled(pluginId, enabled: true);
                return PendingRestartResult(pluginId, pending);
            }

            SetEnabled(pluginId, enabled: true);
            return await LoadEntryAsync(entry, cancellationToken).ConfigureAwait(false)
                ?? Reject(pluginId, "包不可用：清单缺失或目录已不在");
        }

        /// <summary>
        /// 显式重试隔离插件：先把上一次没走完的安全点回收续完，再按启用意图重新装载。
        /// </summary>
        /// <param name="pluginId">插件 id。</param>
        /// <param name="cancellationToken">回收与装载的取消令牌。</param>
        /// <returns>重新装载的结果；回收未完成或装载再次失败时结论仍是隔离。</returns>
        /// <exception cref="InvalidOperationException">
        /// 插件不在最近一次启动扫描结果里，或已处于活动态（活动插件无需重试）。
        /// </exception>
        public async Task<PluginLoadResult> RetryAsync(string pluginId, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

            PluginStartupReportEntry entry = RequireScannedEntry(pluginId);
            if (_handovers.TryGetValue(pluginId, out PluginUnloadRequest? request))
            {
                if (request.Lifecycle.Current == PluginLifecycleState.Active)
                {
                    throw new InvalidOperationException(
                        $"插件处于活动态，无需重试：{pluginId}；重载请先停用再启用");
                }

                // 隔离插件的上一次卸载可能停在危险区之前（例如在途调用未归零）：重试先把安全点
                // 续完。回收没成功就不重新装载——旧实例还活着时重装会把泄漏叠成两份。
                PluginUnloadResult reclaim = await _unloadPipeline
                    .UnloadAsync(request, cancellationToken)
                    .ConfigureAwait(false);
                if (!reclaim.Reclaimed)
                {
                    PersistQuarantine(pluginId, reclaim.FailureReason, reclaim.Residuals);
                    return QuarantinedLoadResult(pluginId, reclaim.FailureReason);
                }

                _handovers.Remove(pluginId);
            }

            SetEnabled(pluginId, enabled: true);
            PluginLoadResult? result = await LoadEntryAsync(entry, cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                // 包不可用不算恢复：保持隔离，装载结果如实报"拒绝"。
                const string Unavailable = "包不可用：清单缺失或目录已不在";
                PersistQuarantine(pluginId, Unavailable);
                return Reject(pluginId, Unavailable);
            }

            if (result.Status == PluginLoadStatus.Active)
            {
                // 显式重试成功：隔离状态随本次装载清除并落盘；失败结果保留新写入的原因。
                ClearQuarantine(pluginId);
            }

            return result;
        }

        /// <summary>
        /// 彻底移除插件：停用后删包目录、删 <c>plugins.&lt;id&gt;</c> 配置段、删插件数据目录，
        /// 再删宿主状态条目并立即冲刷配置落盘。
        /// </summary>
        /// <param name="pluginId">插件 id。</param>
        /// <param name="cancellationToken">停用与回收的取消令牌。</param>
        /// <returns>四类产物逐项的清理结论；任一未清掉即失败并给出原因，不谎报成功。</returns>
        public async Task<PluginUninstallResult> UninstallAsync(string pluginId, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

            var failures = new List<string>();

            // 先停用：包目录正在被装载上下文占用时删不掉，安全点卸载必须先走完。
            PluginStartupReportEntry? entry = Report?.Plugins.FirstOrDefault(
                item => string.Equals(item.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
            try
            {
                await DisableAsync(pluginId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add($"停用失败：{DescribeException(exception)}");
            }

            foreach (string packagePath in entry?.PackagePaths ?? new List<string>())
            {
                TryDeleteDirectory(packagePath, "包目录", failures);
            }

            try
            {
                _uninstallOptions.RemoveConfigSection?.Invoke(pluginId);
            }
            catch (Exception exception)
            {
                failures.Add($"配置段删除失败：{DescribeException(exception)}");
            }

            if (_uninstallOptions.PluginDataRoot is { } dataRoot)
            {
                TryDeleteDirectory(Path.Combine(dataRoot, pluginId), "插件数据", failures);
            }

            _loadedVersions.Remove(pluginId);
            _lastUnloadResults.Remove(pluginId);
            if (_stateStore.Current.Plugins.Remove(pluginId))
            {
                _stateStore.Save();
            }

            // 配置段删除只动了内存态：立即冲刷，避免挂起的防抖落盘把旧段又写回磁盘。
            try
            {
                _uninstallOptions.FlushPendingSaves?.Invoke();
            }
            catch (Exception exception)
            {
                failures.Add($"配置落盘失败：{DescribeException(exception)}");
            }

            // 从启动报告摘除，管理面随后取不到该条目（包已不在磁盘上，重扫也不会再出现）。
            if (entry is not null)
            {
                Report?.Plugins.Remove(entry);
            }

            return failures.Count == 0
                ? PluginUninstallResult.Success(pluginId)
                : new PluginUninstallResult(pluginId, false, failures);
        }

        /// <summary>取单个插件的诊断报告：状态、准入、隔离原因与可定位的残留清单。</summary>
        /// <param name="pluginId">插件 id。</param>
        /// <exception cref="InvalidOperationException">插件不在最近一次启动扫描结果里。</exception>
        public PluginDiagnosticsReport GetDiagnostics(string pluginId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

            PluginStartupReportEntry entry = RequireScannedEntry(pluginId);
            PluginStateEntry state = _stateStore.Current.GetOrCreate(pluginId);
            bool active = _handovers.TryGetValue(pluginId, out PluginUnloadRequest? request)
                && request.Lifecycle.Current == PluginLifecycleState.Active;
            string? loadedVersion = _loadedVersions.TryGetValue(pluginId, out string? loaded)
                ? loaded
                : null;
            return new PluginDiagnosticsReport
            {
                PluginId = entry.PluginId,
                Name = entry.Name,
                Version = entry.Version,
                Status = ResolveStatus(entry, state, active),
                Admission = entry.Admission,
                AdmissionReason = entry.AdmissionReason,
                PackagePath = entry.PackagePaths.FirstOrDefault(),
                LoadedVersion = string.IsNullOrEmpty(loadedVersion) ? null : loadedVersion,
                PendingRestartVersion = state.PendingVersion,
                Enabled = state.Enabled,
                QuarantineReason = state.Quarantine?.Reason,
                QuarantinedAt = state.Quarantine?.Since,
                Residuals = state.Quarantine?.Residuals
                    ?? (_lastUnloadResults.TryGetValue(pluginId, out PluginUnloadResult? last)
                        ? last.Residuals
                        : Array.Empty<PluginResidual>()),
                ReclaimNote = state.Quarantine is null
                    && _lastUnloadResults.TryGetValue(pluginId, out PluginUnloadResult? lastUnload)
                        ? lastUnload.ReclaimNote
                        : null,
            };
        }

        /// <summary>全部已扫描插件的诊断报告（插件管理面的列表数据源）。</summary>
        public IReadOnlyList<PluginDiagnosticsReport> DescribePlugins()
            => Report is null
                ? Array.Empty<PluginDiagnosticsReport>()
                : Report.Plugins
                    .Select(item => GetDiagnostics(item.PluginId))
                    .ToList();

        /// <summary>
        /// 状态判定优先级：隔离 → 活动 → 待重启 → 准入拒绝 → 停用 → 启用未活动。
        /// 待重启排在活动与拒绝之后：挂起版本时插件已停用但用户意图仍是启用，与"已停用"不是一回事。
        /// </summary>
        private static PluginRuntimeStatus ResolveStatus(
            PluginStartupReportEntry entry,
            PluginStateEntry state,
            bool active)
            => state.Quarantine is not null ? PluginRuntimeStatus.Quarantined
            : active ? PluginRuntimeStatus.Active
            : state.PendingVersion is not null && state.Enabled ? PluginRuntimeStatus.PendingRestart
            : entry.Admission == PluginAdmission.Rejected ? PluginRuntimeStatus.Rejected
            : !state.Enabled ? PluginRuntimeStatus.Disabled
            : PluginRuntimeStatus.Inactive;

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
                // 重新装载后上一次卸载的残留不再代表当前状态（旧 ALC 的释放留给重启）。
                _lastUnloadResults.Remove(entry.PluginId);
                _loadedVersions[entry.PluginId] = entry.Version ?? string.Empty;
                ClearPendingVersion(entry.PluginId, entry.Version);
            }
            else if (result.Status == PluginLoadStatus.Quarantined)
            {
                // 隔离是持久状态：写进宿主状态后，下次启动扫描保留它，本类不再自动重试装载。
                PersistQuarantine(entry.PluginId, result.FailureReason);
                if (result.LoadContext is not null)
                {
                    // 失败装载留下的 ALC 不能无人回收：接进交接账本，用户显式重试或停用时
                    // 先经同一个安全点把上一次的失败现场收干净。
                    _handovers[entry.PluginId] = PluginUnloadRequest.FromQuarantined(result);
                }
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

        /// <summary>
        /// 已装载时跑一次安全点卸载并把结果收口进账本；未装载时只返回 null（无资源可收）。
        /// </summary>
        /// <remarks>
        /// 回收成功或降级判定下请求已耗尽即从交接账本摘除；隔离结论保留交接对象，
        /// 让下一次安全点（重试/停用/重载）能续做回收，不谎报已收干净。
        /// </remarks>
        private async Task<PluginUnloadResult?> UnloadIfLoadedAsync(
            string pluginId,
            CancellationToken cancellationToken)
        {
            if (!_handovers.TryGetValue(pluginId, out PluginUnloadRequest? request))
            {
                return null;
            }

            PluginUnloadResult result = await _unloadPipeline
                .UnloadAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (result.Status == PluginUnloadStatus.Unloaded || result.Reclaimed)
            {
                _handovers.Remove(pluginId);
                _loadedVersions.Remove(pluginId);
            }

            if (result.Status == PluginUnloadStatus.Quarantined)
            {
                PersistQuarantine(pluginId, result.FailureReason, result.Residuals);
                _lastUnloadResults.Remove(pluginId);
            }
            else
            {
                // 降级判定下 ALC 与程序集可能仍被宿主框架缓存：残留清单留给诊断报告，
                // 重启后随宿主框架缓存一起消失。
                _lastUnloadResults[pluginId] = result;
            }

            return result;
        }

        /// <summary>已隔离时给出不装载的隔离结论；未隔离时返回 null。</summary>
        private PluginLoadResult? QuarantinedIfIsolated(string pluginId)
        {
            PluginStateEntry state = _stateStore.Current.GetOrCreate(pluginId);
            if (state.Quarantine is not { } quarantine)
            {
                return null;
            }

            return QuarantinedLoadResult(
                pluginId,
                $"插件已隔离（{quarantine.Reason}）：需显式重试或停用，不自动重试装载");
        }

        /// <summary>
        /// 落隔离状态（宿主状态是唯一权威；扫描只刷新准入与路径，不覆盖它）。
        /// 既有隔离原因不被后续回收续做或重试覆盖——首次进入隔离的原因才是用户要定位的现场；
        /// 残留清单按最新一次回收结果补齐。
        /// </summary>
        private void PersistQuarantine(
            string pluginId,
            string? reason,
            IReadOnlyList<PluginResidual>? residuals = null)
        {
            PluginStateEntry state = _stateStore.Current.GetOrCreate(pluginId);
            if (state.Quarantine is { } existing)
            {
                if (residuals is { Count: > 0 })
                {
                    state.Quarantine = existing with { Residuals = residuals };
                    _stateStore.Save();
                }

                return;
            }

            state.Quarantine = new PluginQuarantineState(
                string.IsNullOrWhiteSpace(reason) ? "装载或卸载失败" : reason,
                DateTimeOffset.Now)
            {
                Residuals = residuals ?? Array.Empty<PluginResidual>(),
            };
            _stateStore.Save();
        }

        /// <summary>删除一个目录并记失败原因；目录本就不存在算成功（无残留可清）。</summary>
        private static void TryDeleteDirectory(string path, string label, List<string> failures)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (Exception exception)
            {
                failures.Add($"{label}删除失败（{path}）：{DescribeException(exception)}");
            }
        }

        /// <summary>异常的可读单行形态（类型全名 + message）；异常实例不进入结果。</summary>
        private static string DescribeException(Exception exception)
            => $"{exception.GetType().FullName}：{exception.Message}";

        /// <summary>清除隔离状态并落盘（显式重试成功后调用）。</summary>
        private void ClearQuarantine(string pluginId)
        {
            _stateStore.Current.GetOrCreate(pluginId).Quarantine = null;
            _stateStore.Save();
        }

        /// <summary>
        /// 挂起版本已被装载时清掉挂起标记（仅当挂起版本与本次装载的版本一致）。
        /// </summary>
        private void ClearPendingVersion(string pluginId, string? loadedVersion)
        {
            PluginStateEntry state = _stateStore.Current.GetOrCreate(pluginId);
            if (state.PendingVersion is null
                || !string.Equals(state.PendingVersion, loadedVersion, StringComparison.Ordinal))
            {
                return;
            }

            state.PendingVersion = null;
            _stateStore.Save();
        }

        /// <summary>不装载、不创建 ALC 的隔离结论（重试被回收门槛拦下时返回）。</summary>
        private static PluginLoadResult PendingRestartResult(string pluginId, string pendingVersion)
        {
            return new PluginLoadResult(
                pluginId,
                PluginLoadStatus.PendingRestart,
                $"新版本 {pendingVersion} 已就位：界面插件重启后装载",
                null,
                null,
                new PluginLifecycleStateMachine(),
                null);
        }

        /// <summary>不装载、不创建 ALC 的隔离结论（重试被回收门槛拦下时返回）。</summary>
        private static PluginLoadResult QuarantinedLoadResult(string pluginId, string? reason)
        {
            string text = string.IsNullOrWhiteSpace(reason) ? "插件已隔离" : reason;
            var lifecycle = new PluginLifecycleStateMachine();
            lifecycle.Quarantine(text);
            return new PluginLoadResult(
                pluginId,
                PluginLoadStatus.Quarantined,
                text,
                null,
                null,
                lifecycle,
                null);
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
