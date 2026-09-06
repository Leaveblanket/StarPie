using System;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Input;
using WinPieGestures.Services;

namespace WinPieGestures.ViewModels.Pages
{
    /// <summary>
    /// 设置窗口·通用分区 ViewModel (T13, ADR-0001)：界面语言切换、开机自启、退出/提权重启、
    /// 托盘驻留气泡提示与配置导入/导出的状态与编排。
    /// 语言切换写入运行态配置并调用 <see cref="I18n.SetLanguage"/>；界面文本刷新由窗口订阅
    /// I18n.LanguageChanged 广播完成（ADR-0002：I18n 刻意保持静态 + 切换广播）。
    /// 注册表读写（开机自启，组合根接线 AutostartRegistry）与配置导入/导出（组合根接线
    /// 配置服务）经注入委托编排进本 VM，保证可测。
    /// T19 页面化：落盘请求改经 <see cref="IMessenger"/> 上报组合根编排订阅者；导入成功发
    /// <see cref="ConfigImportedMessage"/> 广播，各页面 VM 订阅后自行重挂（本 VM 亦订阅重挂语言码，
    /// 并经 <see cref="PageConfigReloadedMessage"/> 通知页面 View 同步控件）。
    /// </summary>
    public partial class GeneralSettingsViewModel : ObservableObject
    {
        private AppConfig _config;
        private readonly IDialogService _dialogs;
        private readonly Action<string, string> _showTrayBalloonTip;
        private readonly Action _exitApplication;
        private readonly Func<bool> _isAutoStartEnabled;
        private readonly Action<bool> _setAutoStart;
        private readonly Func<string, bool> _exportConfig;
        private readonly Func<string, bool> _importConfig;
        private readonly Func<AppConfig> _currentConfig;
        private readonly IMessenger _messenger;
        private readonly ILocalizationService _localization;
        private readonly Action<string> _startElevated;
        private readonly Func<bool> _isAdministratorProbe;

        /// <summary>开机自启开关状态（读自注册表——经注入委托，组合根接线 AutostartRegistry）。</summary>
        [ObservableProperty]
        private bool _autoStartEnabled;

        [ObservableProperty]
        private string _languageCode = "Auto";

        [ObservableProperty]
        private bool _isAdministrator;

        public bool ShowUacWarning => !IsAdministrator;

        partial void OnIsAdministratorChanged(bool value) => OnPropertyChanged(nameof(ShowUacWarning));

        /// <summary>配置已随导入重挂（T19：本 VM 订阅导入广播后触发），页面 View 据此同步控件显示。</summary>
        public GeneralSettingsViewModel(
            AppConfig config,
            IDialogService dialogs,
            Action<string, string> showTrayBalloonTip,
            Action exitApplication,
            Func<bool> isAutoStartEnabled,
            Action<bool> setAutoStart,
            Func<string, bool> exportConfig,
            Func<string, bool> importConfig,
            Func<AppConfig> currentConfig,
            IMessenger messenger,
            ILocalizationService localization,
            Action<string>? startElevated = null,
            Func<bool>? isAdministrator = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _showTrayBalloonTip = showTrayBalloonTip ?? throw new ArgumentNullException(nameof(showTrayBalloonTip));
            _exitApplication = exitApplication ?? throw new ArgumentNullException(nameof(exitApplication));
            _isAutoStartEnabled = isAutoStartEnabled ?? throw new ArgumentNullException(nameof(isAutoStartEnabled));
            _setAutoStart = setAutoStart ?? throw new ArgumentNullException(nameof(setAutoStart));
            _exportConfig = exportConfig ?? throw new ArgumentNullException(nameof(exportConfig));
            _importConfig = importConfig ?? throw new ArgumentNullException(nameof(importConfig));
            _currentConfig = currentConfig ?? throw new ArgumentNullException(nameof(currentConfig));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _startElevated = startElevated ?? StartElevatedProcess;
            _isAdministratorProbe = isAdministrator ?? (() => false);

            // T19：导入成功广播 → 以新配置重挂语言码，并通知页面 View 同步控件。
            messenger.Register<ConfigImportedMessage>(this, (_, msg) =>
            {
                Reload(msg.ImportedConfig);
                _messenger.Send(new PageConfigReloadedMessage(typeof(GeneralSettingsViewModel)));
            });

            _autoStartEnabled = _isAutoStartEnabled();
            OnPropertyChanged(nameof(AutoStartEnabled));
            LanguageCode = _config.Language ?? "Auto";
            IsAdministrator = _isAdministratorProbe();
        }

        /// <summary>当前界面语言码（"Auto"/"zh-CN"/"zh-TW"/"en"/"ja"），窗口据此初始化语言下拉。</summary>
        partial void OnAutoStartEnabledChanged(bool value)
        {
            if (_config == null) return;
            _setAutoStart(value);
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
        }

        partial void OnLanguageCodeChanged(string value)
        {
            if (_config == null || string.IsNullOrEmpty(value) || string.Equals(_config.Language, value, StringComparison.Ordinal)) return;
            ApplyLanguage(value);
        }

        /// <summary>以运行态配置重挂状态（导入配置后窗口调用——配置实例已被替换）。
        /// 与迁移前一致，自启勾选不随导入刷新（迁移前导入重置亦不触碰注册表开关）。</summary>
        public void Reload(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            LanguageCode = _config.Language ?? "Auto";
            IsAdministrator = _isAdministratorProbe();
        }

        /// <summary>
        /// 语言切换编排（迁移前 LanguageComboBox_SelectionChanged）：写入运行态配置 →
        /// I18n.SetLanguage（语言实际变化时触发广播，窗口经订阅刷新全部文本）→ 请求落盘。
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

        [RelayCommand]
        private void TrimMemory()
        {
            MemoryOptimizer.TrimMemory(true);
            _messenger.Send(new GeneralNoticeRequestedMessage(new NoticeRequest("提示", "物理工作集内存已深度压缩！", NoticeKind.Info)));
        }

        /// <summary>开机自启切换（迁移前 AutoStartCheckBox_Changed）：注册表读写经注入委托，并请求落盘。</summary>
        public void SetAutoStart(bool enable)
        {
            _setAutoStart(enable);
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
        }

        /// <summary>窗口最小化到托盘时的气泡提示（迁移前 Window_Closing 的提示调用）；
        /// 经组合根已有的委托传递，不新建服务。</summary>
        public void NotifyMinimizedToTray()
        {
            _showTrayBalloonTip(
                "WinPieGestures",
                "应用已最小化至系统托盘，将在后台继续运行鼠标笔势监视。");
        }

        /// <summary>
        /// 以管理员身份重启并退出应用（迁移前 SettingsWindow.ElevateAndRestart，托盘菜单同样
        /// 经窗口转发调用）。启动编排经注入委托（默认 Process.Start runas）；失败或已取消
        /// 经 <see cref="GeneralNoticeRequestedMessage"/> 交窗口弹窗，不退出。
        /// </summary>
        public void ElevateAndRestart()
        {
            try
            {
                string exePath = Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WinPieGestures.exe");
                _startElevated(exePath);
                _exitApplication();
            }
            catch (Exception ex)
            {
                _messenger.Send(new GeneralNoticeRequestedMessage(new NoticeRequest("管理员提权", $"提权重启失败或已取消: {ex.Message}", NoticeKind.Warning)));
            }
        }

        /// <summary>默认提权启动实现（迁移前内联的 ProcessStartInfo 编排；失败/取消以异常表达）。</summary>
        private static void StartElevatedProcess(string exePath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(startInfo);
        }

        /// <summary>导出配置编排（迁移前 ExportConfigButton_Click）：保存对话框 → 导出 → 结果弹窗请求。</summary>
        [RelayCommand]
        private void ExportConfig()
        {
            var picked = _dialogs.ShowSaveFileDialog(
                "JSON 配置文件 (*.json)|*.json",
                $"WinPieGestures_Config_Backup_{DateTime.Now:yyyyMMdd}.json",
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

        /// <summary>导入配置编排（迁移前 ImportConfigButton_Click）：打开对话框 → 导入 →
        /// 成功时弹窗请求并通知窗口重载 UI，失败弹窗请求。</summary>
        [RelayCommand]
        private void ImportConfig()
        {
            var picked = _dialogs.ShowOpenFileDialog("JSON 配置文件 (*.json)|*.json", "选择要导入的配置文件");
            if (picked == null) return;

            if (_importConfig(picked.Path))
            {
                // 弹窗（模态）先于 UI 重载——与迁移前"先提示、点确定后重载控件"顺序一致
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
