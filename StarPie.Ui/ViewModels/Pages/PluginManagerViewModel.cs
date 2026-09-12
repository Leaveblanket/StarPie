using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using StarPie.Kernel.Localization;
using StarPie.PluginHosting;
using StarPie.PluginHosting.Extensions;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Diagnostics;
using StarPie.PluginRuntime.Hosting;
using StarPie.Services.Dialogs;
using StarPie.Services.Navigation;

namespace StarPie.ViewModels.Pages
{
    /// <summary>
    /// 插件管理页 ViewModel：列出宿主扫描到的插件与状态，提供启用/停用、重试与诊断入口。
    /// </summary>
    /// <remarks>
    /// 数据源是宿主报告快照（<see cref="PluginRuntimeHost.DescribePlugins"/>）：页面每次被导航到时刷新，
    /// 启停/重试完成后立即刷新。页面不直接读写宿主状态文件——状态权威始终在 Host，页面只呈现与发命令。
    /// </remarks>
    public partial class PluginManagerViewModel : ObservableObject, IDisposable
    {
        private readonly PluginRuntimeHost _runtime;
        private readonly PluginUiCoordinator? _pluginUi;
        private readonly NavigationStore _navigation;
        private readonly ILocalizationService _localization;
        private readonly IDialogService? _dialogs;

        /// <summary>插件条目（按宿主报告的稳定序）。</summary>
        public ObservableCollection<PluginManagerItemViewModel> Plugins { get; } = new();

        /// <summary>插件贡献的设置区块（无插件时为空——扩展点降级不出现空壳）。</summary>
        public ObservableCollection<PluginSettingsSectionViewModel> PluginSettingsSections { get; } = new();

        /// <summary>是否存在插件设置区块：XAML 用它在区块为空时整块隐藏设置区。</summary>
        [ObservableProperty]
        private bool _hasPluginSettingsSections;

        /// <summary>诊断面板当前展示的插件；为 null 时显示占位文案。</summary>
        [ObservableProperty]
        private PluginManagerItemViewModel? _selectedPlugin;

        /// <summary>诊断面板文本（状态、准入、隔离原因与可定位的残留清单）。</summary>
        [ObservableProperty]
        private string _diagnosticsText = string.Empty;

        /// <summary>页面常显的当前准入模式文案。</summary>
        [ObservableProperty]
        private string _admissionModeText = string.Empty;

        /// <summary>开发者模式是否已开启（准入模式的依据）。</summary>
        [ObservableProperty]
        private bool _isDeveloperModeEnabled;

        /// <summary>构造页面 VM：宿主运行时、导航状态与本地化服务均为显式依赖。</summary>
        /// <param name="runtime">宿主侧插件运行时。</param>
        /// <param name="navigation">导航状态（页面被导航到时重读宿主报告）。</param>
        /// <param name="localization">本地化服务。</param>
        /// <param name="pluginUi">插件 UI 托管门面；为 null 时不呈现插件设置区块。</param>
        /// <param name="dialogs">对话框服务（彻底移除前的确认）；为 null 时按取消处理，不误删。</param>
        public PluginManagerViewModel(
            PluginRuntimeHost runtime,
            NavigationStore navigation,
            ILocalizationService localization,
            PluginUiCoordinator? pluginUi = null,
            IDialogService? dialogs = null)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _pluginUi = pluginUi;
            _dialogs = dialogs;

            _navigation.PropertyChanged += OnNavigationChanged;
            Refresh();
        }

        /// <summary>重新读取宿主报告并重建列表；刷新后保持原来选中的插件。</summary>
        public void Refresh()
        {
            string? selectedId = SelectedPlugin?.PluginId;
            Plugins.Clear();
            foreach (PluginDiagnosticsReport report in _runtime.DescribePlugins())
            {
                Plugins.Add(new PluginManagerItemViewModel(report, this, _localization));
            }

            SelectedPlugin = Plugins.FirstOrDefault(item => item.PluginId == selectedId)
                ?? Plugins.FirstOrDefault();
            RefreshAdmissionMode();
            UpdateDiagnostics();
            RefreshPluginSettingsSections();
        }

        /// <summary>刷新页面常显的准入模式文案；语言切换后随本页刷新重取。</summary>
        private void RefreshAdmissionMode()
        {
            IsDeveloperModeEnabled = _runtime.IsDeveloperModeEnabled;
            AdmissionModeText = _localization.GetString(
                IsDeveloperModeEnabled ? "PluginManagerAdmissionModeDev" : "PluginManagerAdmissionModeDefault");
        }

        /// <summary>重建插件设置区块：标题按当前语言取词，区块 VM 由插件描述符工厂创建。</summary>
        private void RefreshPluginSettingsSections()
        {
            PluginSettingsSections.Clear();
            if (_pluginUi is null)
            {
                return;
            }

            foreach (PluginSettingsSection section in _pluginUi.SettingsSections)
            {
                PluginSettingsSections.Add(PluginSettingsSectionViewModel.From(
                    section,
                    _localization.GetString(section.Descriptor.TitleKey)));
            }

            HasPluginSettingsSections = PluginSettingsSections.Count > 0;
        }

        /// <summary>选中条目并刷新诊断面板。</summary>
        /// <param name="item">要查看的插件条目。</param>
        public void ShowDiagnostics(PluginManagerItemViewModel item)
        {
            ArgumentNullException.ThrowIfNull(item);
            SelectedPlugin = item;
            UpdateDiagnostics();
        }

        /// <summary>按当前状态启用或停用条目；操作完成后刷新列表。</summary>
        /// <param name="item">目标条目。</param>
        internal async Task ToggleAsync(PluginManagerItemViewModel item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (item.Report.Status is PluginRuntimeStatus.Active or PluginRuntimeStatus.Quarantined)
            {
                await _runtime.DisableAsync(item.PluginId, CancellationToken.None).ConfigureAwait(true);
            }
            else if (item.Report.Status == PluginRuntimeStatus.Disabled)
            {
                await _runtime.EnableAsync(item.PluginId, CancellationToken.None).ConfigureAwait(true);
            }

            Refresh();
        }

        /// <summary>显式重试隔离插件；失败时宿主保持隔离，列表仍如实刷新。</summary>
        /// <param name="item">目标条目。</param>
        internal async Task RetryAsync(PluginManagerItemViewModel item)
        {
            ArgumentNullException.ThrowIfNull(item);
            await _runtime.RetryAsync(item.PluginId, CancellationToken.None).ConfigureAwait(true);
            Refresh();
        }

        /// <summary>重载插件：安全点卸载后按当前包重新装载，完成后刷新列表。</summary>
        /// <param name="item">目标条目。</param>
        internal async Task ReloadAsync(PluginManagerItemViewModel item)
        {
            ArgumentNullException.ThrowIfNull(item);
            await _runtime.ReloadAsync(item.PluginId, CancellationToken.None).ConfigureAwait(true);
            Refresh();
        }

        /// <summary>
        /// 应用新版本：无界面插件当场生效，界面插件转入"下次启动生效"，完成后刷新列表。
        /// </summary>
        /// <param name="item">目标条目。</param>
        internal async Task UpdateAsync(PluginManagerItemViewModel item)
        {
            ArgumentNullException.ThrowIfNull(item);
            await _runtime.UpdateAsync(item.PluginId, CancellationToken.None).ConfigureAwait(true);
            Refresh();
        }

        /// <summary>
        /// 彻底移除插件：先经用户确认（对话框服务缺席时按取消处理，不误删），
        /// 再清包/配置段/数据/宿主状态；未清干净时保持列表并给出失败原因。
        /// </summary>
        /// <param name="item">目标条目。</param>
        internal async Task UninstallAsync(PluginManagerItemViewModel item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (_dialogs is null
                || !_dialogs.Confirm(
                    _localization.GetString("PluginManagerUninstallConfirmTitle"),
                    string.Format(
                        _localization.GetString("PluginManagerUninstallConfirmMessage"),
                        item.DisplayName)))
            {
                return;
            }

            PluginUninstallResult? result = await _runtime
                .UninstallAsync(item.PluginId, CancellationToken.None)
                .ConfigureAwait(true);
            Refresh();
            if (result is { Succeeded: false })
            {
                _dialogs.ShowInfo(
                    _localization.GetString("PluginManagerUninstallFailedTitle"),
                    result.FailureReason ?? string.Empty);
            }
        }

        /// <summary>退订导航状态（容器单例，随组合根释放）。</summary>
        public void Dispose()
        {
            _navigation.PropertyChanged -= OnNavigationChanged;
        }

        /// <summary>导航到本页时重读宿主报告：状态可能已被宿主内部流程改变（例如熔断隔离）。</summary>
        private void OnNavigationChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NavigationStore.CurrentViewModel)
                && ReferenceEquals(_navigation.CurrentViewModel, this))
            {
                Refresh();
            }
        }

        private void UpdateDiagnostics()
            => DiagnosticsText = SelectedPlugin is null
                ? _localization.GetString("PluginManagerNoPluginSelected")
                : ComposeDiagnostics(SelectedPlugin.Report, _localization);

        /// <summary>诊断文本：状态、准入、路径、隔离原因、回收说明与可定位残留清单。</summary>
        private static string ComposeDiagnostics(
            PluginDiagnosticsReport report,
            ILocalizationService localization)
        {
            var lines = new List<string>
            {
                $"{localization.GetString("PluginManagerPluginIdLabel")}：{report.PluginId}",
                $"{localization.GetString("PluginManagerVersionLabel")}：{(string.IsNullOrWhiteSpace(report.Version) ? "-" : report.Version)}",
                $"{localization.GetString("PluginManagerStatusLabel")}：{localization.GetString(PluginManagerItemViewModel.ToStatusKey(report.Status))}",
                $"{localization.GetString("PluginManagerAdmissionLabel")}：{localization.GetString(PluginManagerItemViewModel.ToAdmissionKey(report.Admission))}",
                $"{localization.GetString("PluginManagerPackagePathLabel")}：{report.PackagePath ?? "-"}",
            };

            if (!string.IsNullOrWhiteSpace(report.LoadedVersion))
            {
                lines.Add(
                    $"{localization.GetString("PluginManagerLoadedVersionLabel")}：{report.LoadedVersion}");
            }

            if (!string.IsNullOrWhiteSpace(report.PendingRestartVersion))
            {
                lines.Add(string.Format(
                    localization.GetString("PluginManagerPendingRestart"),
                    report.PendingRestartVersion));
            }

            if (!string.IsNullOrWhiteSpace(report.QuarantineReason))
            {
                lines.Add(
                    $"{localization.GetString("PluginManagerQuarantineLabel")}：{report.QuarantineReason}");
            }

            if (!string.IsNullOrWhiteSpace(report.ReclaimNote))
            {
                lines.Add(report.ReclaimNote);
            }

            if (report.Residuals.Count == 0)
            {
                lines.Add(localization.GetString("PluginManagerNoResiduals"));
            }
            else
            {
                lines.Add($"{localization.GetString("PluginManagerResiduals")}（{report.Residuals.Count}）：");
                foreach (PluginResidual residual in report.Residuals)
                {
                    lines.Add("  • " + residual.Detail);
                }
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
