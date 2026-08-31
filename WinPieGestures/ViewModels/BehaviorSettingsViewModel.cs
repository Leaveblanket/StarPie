using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WinPieGestures.ViewModels
{
    /// <summary>
    /// 设置窗口·手势行为分区 ViewModel (T13, ADR-0001)：承接迁移前 SettingsWindow code-behind 的
    /// 触发阈值、场景隔离（全屏禁用、修饰键旁路）、外圈逃逸取消与进程排除黑名单的状态与编排。
    /// 直接持有运行态 <see cref="AppConfig"/> 引用（live-apply：属性变更即时写回运行态配置
    /// 并生效，与 <see cref="WheelViewModel.Config"/> / <see cref="ProfileListViewModel"/> 先例同理）；
    /// 落盘经 <see cref="SaveRequested"/> / <see cref="SaveDebounceRequested"/> 交窗口统一处理
    /// （窗口走 SyncUiToConfigAndSave / ScheduleAutoSave，防抖节奏留在 View 基础设施）。
    /// 导入配置会替换运行态配置实例（JsonConfigService.Import），届时经 <see cref="Reload"/> 重挂。
    /// </summary>
    public partial class BehaviorSettingsViewModel : ObservableObject
    {
        private AppConfig _config;
        private readonly IDialogService _dialogs;

        /// <summary>手势触发阈值（像素）。变更即时写回运行态配置（对应迁移前 ThresholdSlider_ValueChanged）。</summary>
        [ObservableProperty]
        private double _dragThreshold;

        /// <summary>全屏游戏/独占应用自动禁用手势。</summary>
        [ObservableProperty]
        private bool _disableOnFullScreen;

        /// <summary>按住 Ctrl 键时旁路手势。</summary>
        [ObservableProperty]
        private bool _disableOnCtrl;

        /// <summary>按住 Shift 键时旁路手势。</summary>
        [ObservableProperty]
        private bool _disableOnShift;

        /// <summary>按住 Alt 键时旁路手势。</summary>
        [ObservableProperty]
        private bool _disableOnAlt;

        /// <summary>启用向外顺势甩出取消手势。</summary>
        [ObservableProperty]
        private bool _enableOuterEscapeCancel;

        /// <summary>外甩取消距离灵敏度（滑条原始值；写回配置时取整，对应迁移前 Math.Round）。</summary>
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
        //（值来自配置本身，无需回写；窗口在 _isUpdatingUi 保护内同步控件）。
        private bool _isReloading;

        /// <summary>配置需要立即落盘（对应迁移前 SyncUiToConfigAndSave(true)）。</summary>
        public event Action? SaveRequested;

        /// <summary>配置需要防抖落盘（触发阈值滑条专用，对应迁移前 ScheduleAutoSave 的 400ms 防抖）。</summary>
        public event Action? SaveDebounceRequested;

        /// <summary>黑名单新增条目后触发，窗口据此滚动到该项（View 层滚动效果）。</summary>
        public event Action<string>? BlacklistEntryAdded;

        public BehaviorSettingsViewModel(AppConfig config, IDialogService dialogs)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            Reload(config);
        }

        /// <summary>
        /// 以运行态配置重挂状态（构造与导入配置后调用）。经属性赋值刷新通知，重挂期间
        /// 抑制 Config 回写与落盘事件（值来自配置本身）；控件同步由窗口在 _isUpdatingUi 保护内完成。
        /// </summary>
        public void Reload(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _isReloading = true;
            try
            {
                DragThreshold = config.DragThreshold;
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

        // --- live-apply 写回（对应迁移前各事件处理器对运行态配置的直接写入） ---

        partial void OnDragThresholdChanged(double value)
        {
            if (_isReloading || _config == null) return;
            _config.DragThreshold = value;
            SaveDebounceRequested?.Invoke();
        }

        partial void OnDisableOnFullScreenChanged(bool value)
        {
            if (_isReloading || _config == null) return;
            _config.DisableOnFullScreen = value;
            SaveRequested?.Invoke();
        }

        partial void OnDisableOnCtrlChanged(bool value)
        {
            if (_isReloading || _config == null) return;
            _config.DisableOnCtrl = value;
            SaveRequested?.Invoke();
        }

        partial void OnDisableOnShiftChanged(bool value)
        {
            if (_isReloading || _config == null) return;
            _config.DisableOnShift = value;
            SaveRequested?.Invoke();
        }

        partial void OnDisableOnAltChanged(bool value)
        {
            if (_isReloading || _config == null) return;
            _config.DisableOnAlt = value;
            SaveRequested?.Invoke();
        }

        partial void OnEnableOuterEscapeCancelChanged(bool value)
        {
            if (_isReloading || _config == null) return;
            _config.EnableOuterEscapeCancel = value;
            SaveRequested?.Invoke();
        }

        partial void OnOuterEscapeDistanceChanged(double value)
        {
            if (_isReloading || _config == null) return;
            // 与迁移前一致：写回配置前取整（滑条仍显示原始值）
            _config.OuterEscapeDistance = Math.Round(value);
            SaveRequested?.Invoke();
        }

        // --- 进程排除黑名单编排（迁移前 Browse/Add/Delete 三个处理器与 AddBlacklistProcess） ---

        /// <summary>把输入框中的进程加入黑名单（迁移前 AddBlacklistButton_Click）；
        /// 输入为空时转入程序选择——与迁移前"空输入直接打开选择器"一致。</summary>
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

        /// <summary>弹出程序选择器并把所选程序加入黑名单（迁移前 BrowseBlacklistButton_Click）。</summary>
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
        /// 按迁移前规则归一化并加入黑名单：首尾去空白、小写、缺省补 .exe、去重。
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
                // 与迁移前一致：重复项仅选中并滚动，无其他副作用
                SelectedBlacklistProcess = proc;
                BlacklistEntryAdded?.Invoke(proc);
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
            BlacklistEntryAdded?.Invoke(proc);
            NewBlacklistProcess = "";
            SaveRequested?.Invoke();
        }

        /// <summary>移除选中的黑名单进程；未选中时兜底移除最后一项（与迁移前一致）。</summary>
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
                SaveRequested?.Invoke();
            }
        }
    }
}
