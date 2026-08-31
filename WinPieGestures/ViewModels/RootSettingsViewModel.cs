using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WinPieGestures
{
    /// <summary>
    /// 设置窗口·根 ViewModel (T14, ADR-0001)：聚合各分区子 ViewModel（外观、配置方案列表、
    /// 手势行为、通用；方向槽位 <see cref="SlotViewModel"/> 集合住配置方案分区），是设置窗口
    /// DataContext 的单一根源——窗口 DataContext 指向本 VM，分区控件经 <c>{Binding Appearance.*}</c>
    /// 等根路径解析，分区内部既有绑定表达式结构不变（spec 决策，issue #1）。
    /// 分区间共享状态经根协调：配置导入会替换运行态配置实例（JsonConfigService.Import），
    /// 成功后由本 VM 统一重挂各分区，并经 <see cref="PartitionsReloaded"/> 通知视图同步控件
    /// （落盘防抖与预览重绘等 View 基础设施仍由窗口承载）。
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

        // 运行态配置访问器：导入配置会替换实例，分区重挂时经此取最新引用
        //（组合根以 () => configService.Current 接线）。
        private readonly Func<AppConfig> _currentConfig;

        public RootSettingsViewModel(
            IConfigService configService,
            IDialogService dialogs,
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
            if (currentConfig == null) throw new ArgumentNullException(nameof(currentConfig));
            if (showTrayBalloonTip == null) throw new ArgumentNullException(nameof(showTrayBalloonTip));
            if (exitApplication == null) throw new ArgumentNullException(nameof(exitApplication));
            if (isAutoStartEnabled == null) throw new ArgumentNullException(nameof(isAutoStartEnabled));
            if (setAutoStart == null) throw new ArgumentNullException(nameof(setAutoStart));
            if (exportConfig == null) throw new ArgumentNullException(nameof(exportConfig));
            if (importConfig == null) throw new ArgumentNullException(nameof(importConfig));

            _currentConfig = currentConfig;
            _configService = configService;
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
