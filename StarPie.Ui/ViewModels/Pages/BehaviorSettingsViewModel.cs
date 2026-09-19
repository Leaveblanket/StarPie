using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Input;
using StarPie.Sdk.Services;

namespace StarPie.Ui.ViewModels.Pages
{
    /// <summary>
    /// 触发与场景页面 ViewModel：触发阈值、场景隔离（全屏禁用、修饰键旁路）、外圈逃逸
    /// 取消与进程排除黑名单的状态与编排。
    /// </summary>
    /// <remarks>
    /// 直接持有运行态 <see cref="AppConfig"/> 引用，属性变更即时写回运行态配置并生效
    /// （live-apply）；落盘经 <see cref="IMessenger"/> 上报组合根编排的订阅者
    /// （立即请求/防抖请求两类消息）。导入配置会替换运行态配置实例，届时经
    /// <see cref="Reload"/> 重挂，绑定控件随属性通知自动刷新。
    /// </remarks>
    public partial class BehaviorSettingsViewModel : ObservableObject, IDisposable
    {
        private AppConfig _config;
        private readonly IDialogService _dialogs;
        private readonly IMessenger _messenger;

        /// <summary>轮盘触发阈值（像素）。变更即时写回运行态配置。</summary>
        [ObservableProperty]
        private double _dragThreshold;

        /// <summary>
        /// 轮盘触发键（配置键名，词表见 <see cref="TriggerButtonNames"/>）。变更即时写回运行态配置；
        /// 捕获栈每事件实时读配置，改键无需任何推送即生效（ADR-0056）。
        /// </summary>
        [ObservableProperty]
        private string _triggerButton = TriggerButtonNames.Default;

        /// <summary>全屏游戏/独占应用自动禁用轮盘手势。</summary>
        [ObservableProperty]
        private bool _disableOnFullScreen;

        /// <summary>按住 Ctrl 键时旁路轮盘手势。</summary>
        [ObservableProperty]
        private bool _disableOnCtrl;

        /// <summary>按住 Shift 键时旁路轮盘手势。</summary>
        [ObservableProperty]
        private bool _disableOnShift;

        /// <summary>按住 Alt 键时旁路轮盘手势。</summary>
        [ObservableProperty]
        private bool _disableOnAlt;

        /// <summary>启用向外顺势甩出取消轮盘手势。</summary>
        [ObservableProperty]
        private bool _enableOuterEscapeCancel;

        /// <summary>外甩取消距离灵敏度（滑条原始值；写回配置时取整）。</summary>
        [ObservableProperty]
        private double _outerEscapeDistance;

        /// <summary>黑名单输入框文本。</summary>
        [ObservableProperty]
        private string _newBlacklistProcess = "";

        /// <summary>黑名单列表当前选中项。</summary>
        [ObservableProperty]
        private string? _selectedBlacklistProcess;

        /// <summary>黑名单展示列表（与运行态配置的 BlacklistedProcesses 同步维护）。</summary>
        public ObservableCollection<string> BlacklistProcesses { get; } = new();

        // Reload 批量重挂期间为 true：属性变更通知照发，但 Config 回写与落盘事件被抑制
        //（值来自配置本身，无需回写；绑定控件随属性通知自动刷新）。
        private bool _isReloading;

        public BehaviorSettingsViewModel(AppConfig config, IDialogService dialogs, IMessenger messenger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));

            // 导入成功广播 → 以消息携带的新配置自行重挂；绑定控件随属性通知自动刷新，
            // View 无需同步消息。
            messenger.Register<ConfigImportedMessage>(this, (_, msg) =>
            {
                Reload(msg.ImportedConfig);
            });

            Reload(config);
        }

        /// <summary>
        /// 成对退订导入广播（页面 VM 随设置台会话释放；消息总线为弱引用注册，
        /// 此处显式出账使订阅面与生命周期一致，不依赖弱引用语义）。
        /// </summary>
        public void Dispose() => _messenger.UnregisterAll(this);

        /// <summary>
        /// 以运行态配置重挂状态（构造与导入配置后调用）。经属性赋值刷新通知，重挂期间
        /// 抑制 Config 回写与落盘事件（值来自配置本身，无需回写）。
        /// </summary>
        public void Reload(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _isReloading = true;
            try
            {
                DragThreshold = config.DragThreshold;
                TriggerButton = TriggerButtonNames.IsKnown(config.TriggerButton)
                    ? config.TriggerButton
                    : TriggerButtonNames.Default; // 手改配置里的非法值：运行态按右键生效，界面同口径回显
                DisableOnFullScreen = config.DisableOnFullScreen;
                DisableOnCtrl = config.DisableOnCtrl;
                DisableOnShift = config.DisableOnShift;
                DisableOnAlt = config.DisableOnAlt;
                EnableOuterEscapeCancel = config.EnableOuterEscapeCancel;
                OuterEscapeDistance = config.OuterEscapeDistance;
                NewBlacklistProcess = "";
                SelectedBlacklistProcess = null;
            }
            finally
            {
                _isReloading = false;
            }

            BlacklistProcesses.Clear();
            if (config.BlacklistedProcesses != null)
            {
                foreach (var proc in config.BlacklistedProcesses)
                {
                    BlacklistProcesses.Add(proc);
                }
            }
        }

        // --- live-apply 写回：属性变更直接写入运行态配置 ---

        partial void OnDragThresholdChanged(double value)
        {
            if (_isReloading || _config == null) return;
            _config.DragThreshold = value;
            _messenger.Send(DebouncedSaveRequestedMessage.Instance);
        }

        partial void OnTriggerButtonChanged(string value)
        {
            if (_isReloading || _config == null) return;
            // 词表外的取值不落运行态配置（运行态一律回退右键）；正常写入只来自录制卡的
            // 五键选项与恢复默认，这里拦的是将来绑定改坏时的静默漂移。
            if (!TriggerButtonNames.IsKnown(value)) return;
            _config.TriggerButton = value;
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
        }

        /// <summary>录制卡五键选项：把所选键写为轮盘触发键（即时生效并立即落盘）。</summary>
        [RelayCommand]
        private void SelectTriggerButton(string? button)
        {
            if (!TriggerButtonNames.IsKnown(button)) return;
            TriggerButton = button!;
        }

        /// <summary>恢复默认触发键（右键）。</summary>
        [RelayCommand]
        private void ResetTriggerButton() => TriggerButton = TriggerButtonNames.Default;

        partial void OnDisableOnFullScreenChanged(bool value)
        {
            if (_isReloading || _config == null) return;
            _config.DisableOnFullScreen = value;
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
        }

        partial void OnDisableOnCtrlChanged(bool value)
        {
            if (_isReloading || _config == null) return;
            _config.DisableOnCtrl = value;
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
        }

        partial void OnDisableOnShiftChanged(bool value)
        {
            if (_isReloading || _config == null) return;
            _config.DisableOnShift = value;
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
        }

        partial void OnDisableOnAltChanged(bool value)
        {
            if (_isReloading || _config == null) return;
            _config.DisableOnAlt = value;
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
        }

        partial void OnEnableOuterEscapeCancelChanged(bool value)
        {
            if (_isReloading || _config == null) return;
            _config.EnableOuterEscapeCancel = value;
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
        }

        partial void OnOuterEscapeDistanceChanged(double value)
        {
            if (_isReloading || _config == null) return;
            // 写回配置前取整（滑条仍显示原始值）
            _config.OuterEscapeDistance = Math.Round(value);
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
        }

        // --- 进程排除黑名单编排 ---

        /// <summary>把输入框中的进程加入黑名单；输入为空时转入程序选择。</summary>
        [RelayCommand]
        private void AddBlacklistFromInput()
        {
            string proc = (NewBlacklistProcess ?? "").Trim();
            if (string.IsNullOrEmpty(proc))
            {
                BrowseBlacklist();
                return;
            }

            AddBlacklistProcess(proc);
        }

        /// <summary>弹出程序选择器并把所选程序加入黑名单。</summary>
        [RelayCommand]
        private void BrowseBlacklist()
        {
            try
            {
                var picked = _dialogs.ShowProgramPicker();
                if (picked != null)
                {
                    AddBlacklistProcess(Path.GetFileName(picked.Path).ToLower());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BrowseBlacklist Error]: {ex}");
            }
        }

        /// <summary>
        /// 归一化并加入黑名单：首尾去空白、小写、缺省补 .exe、去重。
        /// 重复项仅选中并滚动到该项（不清输入框、不落盘）；新项写入展示列表与运行态配置，
        /// 清空输入框并请求落盘。
        /// </summary>
        public void AddBlacklistProcess(string proc)
        {
            if (string.IsNullOrWhiteSpace(proc)) return;
            proc = proc.Trim().ToLower();
            if (!proc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                proc += ".exe";
            }

            if (BlacklistProcesses.Contains(proc))
            {
                // 重复项仅选中并滚动，无其他副作用
                SelectedBlacklistProcess = proc;
                _messenger.Send(new BlacklistEntryAddedMessage(proc));
                return;
            }

            BlacklistProcesses.Add(proc);
            if (_config.BlacklistedProcesses == null)
            {
                _config.BlacklistedProcesses = new System.Collections.Generic.List<string>();
            }
            if (!_config.BlacklistedProcesses.Contains(proc))
            {
                _config.BlacklistedProcesses.Add(proc);
            }

            SelectedBlacklistProcess = proc;
            _messenger.Send(new BlacklistEntryAddedMessage(proc));
            NewBlacklistProcess = "";
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
        }

        /// <summary>移除选中的黑名单进程；未选中时兜底移除最后一项。</summary>
        [RelayCommand]
        private void DeleteBlacklistProcess()
        {
            string? selected = SelectedBlacklistProcess;
            if (string.IsNullOrEmpty(selected) && BlacklistProcesses.Count > 0)
            {
                selected = BlacklistProcesses[BlacklistProcesses.Count - 1];
            }

            if (!string.IsNullOrEmpty(selected))
            {
                BlacklistProcesses.Remove(selected);
                _config.BlacklistedProcesses?.Remove(selected);
                _messenger.Send(ImmediateSaveRequestedMessage.Instance);
            }
        }
    }
}
