using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Input;
using StarPie.Services;

namespace StarPie.ViewModels.Pages
{
    /// <summary>
    /// 设置窗口通用分区 ViewModel：界面语言切换、开机自启、提权重启与配置导入/导出的状态与编排。
    /// </summary>
    /// <remarks>
    /// 语言切换写入运行态配置并调用 <see cref="ILocalizationService.SetLanguage"/>；界面文本
    /// 刷新由窗口订阅本地化服务的语言切换广播完成。注册表读写（开机自启）与配置导入/导出
    /// 均经组合根接线的注入委托编排进本 VM，保证可测。落盘请求经 <see cref="IMessenger"/>
    /// 上报组合根编排的订阅者；配置导入成功发布 <see cref="ConfigImportedMessage"/>，各页面 VM
    /// 订阅后自行重挂（本 VM 亦订阅重挂语言码，并经 <see cref="PageConfigReloadedMessage"/>
    /// 通知页面 View 同步控件）。
    /// 托盘气泡与提权重启都是壳层动作（[ADR-0039](../adr/0039-resident-shell-and-transient-settings-console.md)
    /// 决策 7）：本 VM 只经 <see cref="AppHostDelegates.ElevateAndRestart"/> 转发触发提权，
    /// 不自行启动进程、不碰托盘。
    /// </remarks>
    public partial class GeneralSettingsViewModel : ObservableObject
    {
        private AppConfig _config;
        private readonly IDialogService _dialogs;
        private readonly Action _elevateAndRestart;
        private readonly Func<bool> _isAutoStartEnabled;
        private readonly Func<bool, bool, bool> _applyAutoStart;
        private readonly Func<bool> _isAdminAutoStartEnabled;
        private readonly Func<string, bool> _exportConfig;
        private readonly Func<string, bool> _importConfig;
        private readonly Func<AppConfig> _currentConfig;
        private readonly IMessenger _messenger;
        private readonly ILocalizationService _localization;
        private readonly Func<bool> _isAdministratorProbe;

        /// <summary>开机自启开关状态（读自注册表——经注入委托，组合根接线 AutostartRegistry）。</summary>
        [ObservableProperty]
        private bool _autoStartEnabled;

        /// <summary>
        /// 以管理员身份开机自启（任务计划程序 `/rl highest`，ADR-0041）。状态读自计划任务的实况，
        /// 不读配置——用户在任务计划程序里手工删掉任务时，开关必须如实反映"没在跑"。
        /// </summary>
        [ObservableProperty]
        private bool _adminAutoStartEnabled;

        /// <summary>当前界面语言码（"Auto"/"zh-CN"/"zh-TW"/"en"/"ja"），窗口据此初始化语言下拉。</summary>
        [ObservableProperty]
        private string _languageCode = "Auto";

        [ObservableProperty]
        private bool _isAdministrator;

        public bool ShowUacWarning => !IsAdministrator;

        partial void OnIsAdministratorChanged(bool value) => OnPropertyChanged(nameof(ShowUacWarning));

        /// <summary>落位组合开关时的重入守卫（见 <see cref="SyncProperty"/>）。</summary>
        private bool _applyingAutoStart;

        /// <summary>构造通用分区 VM：注入运行态配置、对话框与系统能力委托，订阅配置导入广播。</summary>
        public GeneralSettingsViewModel(
            AppConfig config,
            IDialogService dialogs,
            Action elevateAndRestart,
            Func<bool> isAutoStartEnabled,
            Func<bool, bool, bool> applyAutoStart,
            Func<string, bool> exportConfig,
            Func<string, bool> importConfig,
            Func<AppConfig> currentConfig,
            IMessenger messenger,
            ILocalizationService localization,
            Func<bool>? isAdministrator = null,
            Func<bool>? isAdminAutoStartEnabled = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _elevateAndRestart = elevateAndRestart ?? throw new ArgumentNullException(nameof(elevateAndRestart));
            _isAutoStartEnabled = isAutoStartEnabled ?? throw new ArgumentNullException(nameof(isAutoStartEnabled));
            _applyAutoStart = applyAutoStart ?? throw new ArgumentNullException(nameof(applyAutoStart));
            _isAdminAutoStartEnabled = isAdminAutoStartEnabled ?? (static () => false);
            _exportConfig = exportConfig ?? throw new ArgumentNullException(nameof(exportConfig));
            _importConfig = importConfig ?? throw new ArgumentNullException(nameof(importConfig));
            _currentConfig = currentConfig ?? throw new ArgumentNullException(nameof(currentConfig));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _isAdministratorProbe = isAdministrator ?? (() => false);

            // 导入成功广播 → 以新配置重挂语言码，并通知页面 View 同步控件。
            messenger.Register<ConfigImportedMessage>(this, (_, msg) =>
            {
                Reload(msg.ImportedConfig);
                _messenger.Send(new PageConfigReloadedMessage(typeof(GeneralSettingsViewModel)));
            });

            _autoStartEnabled = _isAutoStartEnabled();
            OnPropertyChanged(nameof(AutoStartEnabled));
            // 直接写后备字段：初始化不触发落位（否则每次开页都会跑一遍注册表/任务计划程序）。
            _adminAutoStartEnabled = _isAdminAutoStartEnabled();
            OnPropertyChanged(nameof(AdminAutoStartEnabled));
            LanguageCode = _config.Language ?? "Auto";
            IsAdministrator = _isAdministratorProbe();
        }

        partial void OnAutoStartEnabledChanged(bool value)
        {
            if (_config == null || _applyingAutoStart) return;

            // 自启关掉即提权形态一并关掉（提权自启以自启为前提）。
            if (!value && AdminAutoStartEnabled)
            {
                SyncProperty(() => AdminAutoStartEnabled = false);
            }

            ApplyAutoStartShape();
        }

        partial void OnAdminAutoStartEnabledChanged(bool value)
        {
            if (_config == null || _applyingAutoStart) return;
            ApplyAutoStartShape();
        }

        /// <summary>
        /// 把两个开关的组合落位成系统状态并如实回读（ADR-0041）：注册表 Run 与提权计划任务同源落位，
        /// 失败（用户取消 UAC、标准用户账号无管理员凭据）不静默——以提示告知，并把开关拨回实况。
        /// </summary>
        private void ApplyAutoStartShape()
        {
            // 提权自启蕴含自启：直接勾提权形态时把自启一并打开（用户不必先手工开自启）。
            if (AdminAutoStartEnabled && !AutoStartEnabled)
            {
                SyncProperty(() => AutoStartEnabled = true);
            }

            bool asAdminRequested = AutoStartEnabled && AdminAutoStartEnabled;
            bool applied = _applyAutoStart(AutoStartEnabled, asAdminRequested);
            if (!applied)
            {
                ShowNotice(
                    _localization.GetString("AdminAutoStartFailedTitle"),
                    _localization.GetString("AdminAutoStartFailed"),
                    NoticeKind.Warning);
            }

            // 一律以任务计划程序的实况回读：开关不得停在"系统里其实没有"的位置。
            bool actual = _isAdminAutoStartEnabled();
            if (actual != AdminAutoStartEnabled)
            {
                SyncProperty(() => AdminAutoStartEnabled = actual);
            }

            _config.AutoStartAsAdmin = AdminAutoStartEnabled;
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
        }

        /// <summary>程序化同步开关时挂上重入守卫：一个开关的变更不应再触发一轮落位。</summary>
        private void SyncProperty(Action setter)
        {
            _applyingAutoStart = true;
            try
            {
                setter();
            }
            finally
            {
                _applyingAutoStart = false;
            }
        }

        private void ShowNotice(string title, string message, NoticeKind kind)
            => _messenger.Send(new GeneralNoticeRequestedMessage(new NoticeRequest(title, message, kind)));

        partial void OnLanguageCodeChanged(string value)
        {
            if (_config == null || string.IsNullOrEmpty(value) || string.Equals(_config.Language, value, StringComparison.Ordinal)) return;
            ApplyLanguage(value);
        }

        /// <summary>以运行态配置重挂状态（导入配置后调用——配置实例已被替换）。
        /// 自启勾选与提权形态不随导入刷新：导入只替换配置，不触碰注册表与计划任务（
        /// <see cref="AppConfig.AutoStartAsAdmin"/> 随导入带来的差异会在用户下次拨开关时被实况覆盖）。</summary>
        public void Reload(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            LanguageCode = _config.Language ?? "Auto";
            IsAdministrator = _isAdministratorProbe();
        }

        /// <summary>
        /// 语言切换编排：写入运行态配置 → 切换本地化语言（实际变化时触发广播，窗口经订阅
        /// 刷新全部文本）→ 请求落盘。
        /// </summary>
        public void ApplyLanguage(string langCode)
        {
            if (string.IsNullOrEmpty(langCode)) return;
            _config.Language = langCode;
            _localization.SetLanguage(langCode);
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
        }

        [RelayCommand]
        private void Elevate() => ElevateAndRestart();

        /// <summary>开机自启切换：等价于写入 <see cref="AutoStartEnabled"/>（落位与落盘由属性变更回调统一处理）。</summary>
        public void SetAutoStart(bool enable) => AutoStartEnabled = enable;

        /// <summary>
        /// 以管理员身份重启：壳层动作（托盘点选与页面按钮是同一实现），本 VM 只转发触发；
        /// 失败或用户取消由壳层提示且不退出，故此处无异常分支。
        /// </summary>
        public void ElevateAndRestart() => _elevateAndRestart();

        /// <summary>导出配置编排：保存对话框 → 导出 → 结果弹窗请求。</summary>
        [RelayCommand]
        private void ExportConfig()
        {
            var picked = _dialogs.ShowSaveFileDialog(
                "JSON 配置文件 (*.json)|*.json",
                $"StarPie_Config_Backup_{DateTime.Now:yyyyMMdd}.json",
                "导出配置文件");

            if (picked == null) return;

            if (_exportConfig(picked.Path))
            {
                _messenger.Send(new GeneralNoticeRequestedMessage(new NoticeRequest("提示", "配置导出成功！", NoticeKind.Info)));
            }
            else
            {
                _messenger.Send(new GeneralNoticeRequestedMessage(new NoticeRequest("错误", "配置导出失败，请检查写入权限。", NoticeKind.Error)));
            }
        }

        /// <summary>导入配置编排：打开对话框 → 导入 → 成功时弹窗请求并广播重挂消息，
        /// 失败弹窗请求。</summary>
        [RelayCommand]
        private void ImportConfig()
        {
            var picked = _dialogs.ShowOpenFileDialog("JSON 配置文件 (*.json)|*.json", "选择要导入的配置文件");
            if (picked == null) return;

            if (_importConfig(picked.Path))
            {
                // 模态弹窗先于 UI 重载——先提示、点确定后各页重挂，顺序不可交换
                _messenger.Send(new GeneralNoticeRequestedMessage(new NoticeRequest("提示", "配置导入成功！正在应用新设置...", NoticeKind.Info)));
                _messenger.Send(new ConfigImportedMessage(_currentConfig()));
            }
            else
            {
                _messenger.Send(new GeneralNoticeRequestedMessage(new NoticeRequest("错误", "导入失败：文件格式不匹配或已损坏。", NoticeKind.Error)));
            }
        }
    }
}
