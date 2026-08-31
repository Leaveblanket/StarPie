using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WinPieGestures.ViewModels
{
    /// <summary>
    /// 设置窗口·根 ViewModel (T14, ADR-0001)：聚合各分区子 ViewModel（外观、配置方案列表、
    /// 手势行为、通用；方向槽位 <see cref="SlotViewModel"/> 集合住配置方案分区），是设置窗口
    /// DataContext 的单一根源——窗口 DataContext 指向本 VM，分区控件经 <c>{Binding Appearance.*}</c>
    /// 等根路径解析，分区内部既有绑定表达式结构不变（spec 决策，issue #1）。
    /// 分区间共享状态经根协调：配置导入会替换运行态配置实例（JsonConfigService.Import），
    /// 成功后由本 VM 统一重挂各分区，并经 <see cref="PartitionsReloaded"/> 通知视图同步控件。
    /// T17 起自动保存编排统一住根：各分区的落盘请求（防抖/立即）经根订阅汇聚——防抖请求
    /// 经注入的 <see cref="ISaveDebouncer"/> 折叠为一次延迟落盘，立即请求取消挂起防抖后即刻落盘；
    /// 退出前的兜底冲刷走 <see cref="FlushPendingSave"/>（迁移前窗口的 ScheduleAutoSave /
    /// SyncUiToConfigAndSave 编排上移至此，视图不再持有保存编排）。
    /// 装配遵循 ADR-0002 手动组合根：子 VM 在此集中 <c>new</c>（编译期检查），宿主副作用
    /// （自启注册表、导入导出）经构造委托注入，保持可测。
    /// </summary>
    public partial class RootSettingsViewModel : ObservableObject
    {
        /// <summary>外观分区子 ViewModel (T10)：皮肤、配色、光晕、几何尺寸、排版。</summary>
        public AppearanceSettingsViewModel Appearance { get; }

        /// <summary>配置方案分区列表侧子 ViewModel (T11/T12)：方案列表/选中态、扇区数、方向槽位集合。</summary>
        public ProfileListViewModel ProfileList { get; }

        /// <summary>手势行为分区子 ViewModel (T13)：触发阈值、场景隔离、外圈逃逸、进程黑名单。</summary>
        public BehaviorSettingsViewModel Behavior { get; }

        /// <summary>通用分区子 ViewModel (T13)：语言、开机自启、提权/退出、托盘提示、导入导出。</summary>
        public GeneralSettingsViewModel General { get; }

        /// <summary>分区间重挂完成（配置导入成功后），视图据此同步各分区控件显示。</summary>
        public event Action? PartitionsReloaded;

        private readonly IConfigService _configService;
        private readonly ISaveDebouncer _saveDebouncer;

        /// <summary>迁移前窗口 ScheduleAutoSave 的防抖间隔：连续设置变更折叠为一次落盘。</summary>
        private static readonly TimeSpan AutoSaveDelay = TimeSpan.FromMilliseconds(400);

        // 运行态配置访问器：导入配置会替换实例，分区重挂时经此取最新引用
        //（组合根以 () => configService.Current 接线）。
        private readonly Func<AppConfig> _currentConfig;

        public RootSettingsViewModel(
            IConfigService configService,
            IDialogService dialogs,
            ISaveDebouncer saveDebouncer,
            Func<AppConfig> currentConfig,
            Action<string, string> showTrayBalloonTip,
            Action exitApplication,
            Func<bool> isAutoStartEnabled,
            Action<bool> setAutoStart,
            Func<string, bool> exportConfig,
            Func<string, bool> importConfig)
        {
            if (configService == null) throw new ArgumentNullException(nameof(configService));
            if (dialogs == null) throw new ArgumentNullException(nameof(dialogs));
            if (saveDebouncer == null) throw new ArgumentNullException(nameof(saveDebouncer));
            if (currentConfig == null) throw new ArgumentNullException(nameof(currentConfig));
            if (showTrayBalloonTip == null) throw new ArgumentNullException(nameof(showTrayBalloonTip));
            if (exitApplication == null) throw new ArgumentNullException(nameof(exitApplication));
            if (isAutoStartEnabled == null) throw new ArgumentNullException(nameof(isAutoStartEnabled));
            if (setAutoStart == null) throw new ArgumentNullException(nameof(setAutoStart));
            if (exportConfig == null) throw new ArgumentNullException(nameof(exportConfig));
            if (importConfig == null) throw new ArgumentNullException(nameof(importConfig));

            _currentConfig = currentConfig;
            _configService = configService;
            _saveDebouncer = saveDebouncer;
            var config = currentConfig() ?? throw new ArgumentException("运行态配置尚未初始化。", nameof(currentConfig));

            Appearance = new AppearanceSettingsViewModel(configService, dialogs);
            ProfileList = new ProfileListViewModel(config.Profiles, dialogs);
            Behavior = new BehaviorSettingsViewModel(config, dialogs);
            General = new GeneralSettingsViewModel(
                config,
                dialogs,
                showTrayBalloonTip,
                exitApplication,
                isAutoStartEnabled: isAutoStartEnabled,
                setAutoStart: setAutoStart,
                exportConfig: exportConfig,
                importConfig: importConfig);

            // 分区间共享状态协调：导入成功 → 根统一重挂各分区，再通知视图同步控件。
            General.ConfigImported += OnConfigImported;

            // T17：自动保存编排收口——各分区落盘请求统一经根调度（防抖折叠 / 立即冲刷）。
            Appearance.AutoSaveRequested += ScheduleAutosave;
            Behavior.SaveDebounceRequested += ScheduleAutosave;
            Appearance.SaveNowRequested += SaveNow;
            Behavior.SaveRequested += SaveNow;
            General.SaveRequested += SaveNow;
            ProfileList.SlotEditCommitted += SaveNow;
        }

        private void OnConfigImported() => ReloadAfterConfigImport(_currentConfig());

        /// <summary>
        /// 运行态配置访问（T16）：纯 View 初值读取与预览绘制经此取当前配置，导入后自动取到
        /// 新实例。迁移前视图层直读静态配置门面，现统一收敛到根 VM。
        /// </summary>
        public AppConfig CurrentConfig => _currentConfig();

        /// <summary>把运行态配置落盘（T16）：窗口 SyncUiToConfigAndSave 的保存动作经此走
        /// 注入的配置服务（迁移前为静态配置门面的保存调用）。</summary>
        public void SaveConfig() => _configService.Save();

        /// <summary>防抖落盘请求（T17）：经注入的防抖器折叠连续变更，延迟到期统一落盘一次。</summary>
        private void ScheduleAutosave() => _saveDebouncer.Schedule(SaveConfig, AutoSaveDelay);

        /// <summary>立即落盘请求（T17）：取消挂起的防抖后即刻落盘，覆盖"改完立刻关窗/导入"窗口。</summary>
        private void SaveNow()
        {
            _saveDebouncer.CancelPending();
            SaveConfig();
        }

        /// <summary>冲刷挂起的自动保存并立即落盘（T17）：应用退出与设置窗口关闭的兜底保存点
        /// （迁移前窗口 SavePendingChanges 的公共入口上移至此）。</summary>
        public void FlushPendingSave() => SaveNow();

        /// <summary>
        /// 以新的运行态配置重挂各分区（分区间共享状态经根协调的统一入口，构造后配置实例被
        /// 替换——即配置导入——时调用）。通用分区一并重挂：此前 T13 迁移仅重挂方案列表与
        /// 行为分区，通用 VM 持有的旧配置实例会把语言切换写入已脱离运行态的实例（随导入丢失）。
        /// </summary>
        public void ReloadAfterConfigImport(AppConfig importedConfig)
        {
            if (importedConfig == null) throw new ArgumentNullException(nameof(importedConfig));

            ProfileList.Reload(importedConfig.Profiles);
            Behavior.Reload(importedConfig);
            General.Reload(importedConfig);
            PartitionsReloaded?.Invoke();
        }
    }
}
