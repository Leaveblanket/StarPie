using System;
using CommunityToolkit.Mvvm.Input;
using StarPie.Kernel.Localization;
using StarPie.PluginRuntime.Admission;
using StarPie.PluginRuntime.Diagnostics;

namespace StarPie.ViewModels.Pages
{
    /// <summary>
    /// 插件管理页的单个条目：把宿主诊断报告的只读快照投影为状态/准入文案与操作命令。
    /// </summary>
    /// <remarks>
    /// 条目自身不含操作逻辑——启停/重试/诊断一律回到页面 VM；列表在刷新时整体重建，条目不可变。
    /// </remarks>
    public sealed class PluginManagerItemViewModel
    {
        private readonly ILocalizationService _localization;

        internal PluginManagerItemViewModel(
            PluginDiagnosticsReport report,
            PluginManagerViewModel owner,
            ILocalizationService localization)
        {
            Report = report ?? throw new ArgumentNullException(nameof(report));
            ArgumentNullException.ThrowIfNull(owner);
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));

            ToggleCommand = new AsyncRelayCommand(() => owner.ToggleAsync(this));
            RetryCommand = new AsyncRelayCommand(() => owner.RetryAsync(this));
            ReloadCommand = new AsyncRelayCommand(() => owner.ReloadAsync(this));
            UpdateCommand = new AsyncRelayCommand(() => owner.UpdateAsync(this));
            UninstallCommand = new AsyncRelayCommand(() => owner.UninstallAsync(this));
            DiagnosticsCommand = new RelayCommand(() => owner.ShowDiagnostics(this));
        }

        /// <summary>宿主诊断报告快照。</summary>
        internal PluginDiagnosticsReport Report { get; }

        /// <summary>插件 id。</summary>
        public string PluginId => Report.PluginId;

        /// <summary>展示名（清单名缺失时回落插件 id）。</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(Report.Name) ? Report.PluginId : Report.Name!;

        /// <summary>版本文案（清单不可用时为空）。</summary>
        public string VersionText => string.IsNullOrWhiteSpace(Report.Version) ? string.Empty : "v" + Report.Version;

        /// <summary>本地化状态文案。</summary>
        public string StatusText => _localization.GetString(ToStatusKey(Report.Status));

        /// <summary>本地化准入文案。</summary>
        public string AdmissionText => _localization.GetString(ToAdmissionKey(Report.Admission));

        /// <summary>启用/停用按钮文案：活动与隔离态是"停用"，已停用态是"启用"。</summary>
        public string ToggleText => _localization.GetString(
            Report.Status is PluginRuntimeStatus.Active
                or PluginRuntimeStatus.Quarantined
                or PluginRuntimeStatus.PendingRestart
                ? "PluginManagerDisable"
                : "PluginManagerEnable");

        /// <summary>
        /// 活动与已停用两态可切换；隔离态额外提供"停用"（显式放弃隔离：落停用意图并续做资源回收），
        /// 重试是另一条独立入口；待重启态只提供"停用"（撤销挂起更新）；拒绝走准入流程。
        /// </summary>
        public bool CanToggle
            => Report.Status is PluginRuntimeStatus.Active
                or PluginRuntimeStatus.Disabled
                or PluginRuntimeStatus.PendingRestart
                or PluginRuntimeStatus.Quarantined;

        /// <summary>重载按钮文案。</summary>
        public string ReloadText => _localization.GetString("PluginManagerReload");

        /// <summary>更新按钮文案。</summary>
        public string UpdateText => _localization.GetString("PluginManagerUpdate");

        /// <summary>
        /// 重载只在有活动实例时可用：停用态没有可卸载的实例，隔离态要先走重试。
        /// </summary>
        public bool CanReload => Report.Status == PluginRuntimeStatus.Active;

        /// <summary>更新同样只在有活动实例时可用：先停用再启用是新装而非更新。</summary>
        public bool CanUpdate => Report.Status == PluginRuntimeStatus.Active;

        /// <summary>拒绝的包没有可移除的宿主痕迹；其余状态都可彻底移除。</summary>
        public bool CanUninstall => Report.Status != PluginRuntimeStatus.Rejected;

        /// <summary>是否有挂起的新版本要等下次启动生效。</summary>
        public bool HasPendingRestart => !string.IsNullOrWhiteSpace(Report.PendingRestartVersion);

        /// <summary>挂起更新的说明文案（含待装载版本与"下次启动生效"）。</summary>
        public string PendingRestartText => HasPendingRestart
            ? string.Format(
                _localization.GetString("PluginManagerPendingRestart"),
                Report.PendingRestartVersion)
            : string.Empty;

        /// <summary>仅隔离态显示重试入口。</summary>
        public bool CanRetry => Report.Status == PluginRuntimeStatus.Quarantined;

        /// <summary>是否有隔离原因要展示。</summary>
        public bool HasQuarantine => !string.IsNullOrWhiteSpace(Report.QuarantineReason);

        /// <summary>首次进入隔离的原因（不随后续回收续做改写）。</summary>
        public string QuarantineText => Report.QuarantineReason ?? string.Empty;

        /// <summary>启用/停用命令。</summary>
        public IAsyncRelayCommand ToggleCommand { get; }

        /// <summary>隔离后的显式重试命令。</summary>
        public IAsyncRelayCommand RetryCommand { get; }

        /// <summary>安全点卸载后按当前包重新装载。</summary>
        public IAsyncRelayCommand ReloadCommand { get; }

        /// <summary>应用新版本（界面插件留待下次启动生效）。</summary>
        public IAsyncRelayCommand UpdateCommand { get; }

        /// <summary>彻底移除（确认后清包/配置段/数据/宿主状态）。</summary>
        public IAsyncRelayCommand UninstallCommand { get; }

        /// <summary>诊断入口：把本条目送进诊断面板。</summary>
        public IRelayCommand DiagnosticsCommand { get; }

        /// <summary>列表锚点 AutomationId。</summary>
        public string NameAutomationId => $"PluginManagerName_{Report.PluginId}";

        /// <summary>状态文本 AutomationId。</summary>
        public string StatusAutomationId => $"PluginManagerStatus_{Report.PluginId}";

        /// <summary>启用/停用按钮 AutomationId。</summary>
        public string ToggleAutomationId => $"PluginManagerToggle_{Report.PluginId}";

        /// <summary>重试按钮 AutomationId。</summary>
        public string RetryAutomationId => $"PluginManagerRetry_{Report.PluginId}";

        /// <summary>重载按钮 AutomationId。</summary>
        public string ReloadAutomationId => $"PluginManagerReload_{Report.PluginId}";

        /// <summary>更新按钮 AutomationId。</summary>
        public string UpdateAutomationId => $"PluginManagerUpdate_{Report.PluginId}";

        /// <summary>彻底移除按钮 AutomationId。</summary>
        public string UninstallAutomationId => $"PluginManagerUninstall_{Report.PluginId}";

        /// <summary>待重启说明文本的 AutomationId。</summary>
        public string PendingRestartAutomationId => $"PluginManagerPendingRestart_{Report.PluginId}";

        /// <summary>诊断按钮 AutomationId。</summary>
        public string DiagnosticsAutomationId => $"PluginManagerDiagnostics_{Report.PluginId}";

        /// <summary>状态到本地化键的映射。</summary>
        internal static string ToStatusKey(PluginRuntimeStatus status) => status switch
        {
            PluginRuntimeStatus.Active => "PluginStatusActive",
            PluginRuntimeStatus.Disabled => "PluginStatusDisabled",
            PluginRuntimeStatus.PendingRestart => "PluginStatusPendingRestart",
            PluginRuntimeStatus.Quarantined => "PluginStatusQuarantined",
            PluginRuntimeStatus.Rejected => "PluginStatusRejected",
            _ => "PluginStatusInactive",
        };

        /// <summary>准入四态到本地化键的映射。</summary>
        internal static string ToAdmissionKey(PluginAdmission admission) => admission switch
        {
            PluginAdmission.BuiltIn => "PluginAdmissionBuiltIn",
            PluginAdmission.Reviewed => "PluginAdmissionReviewed",
            PluginAdmission.DeveloperMode => "PluginAdmissionDeveloperMode",
            _ => "PluginAdmissionRejected",
        };
    }
}
