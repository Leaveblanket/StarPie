namespace WinPieGestures.Services.Messages
{
    /// <summary>
    /// 设置域跨页协调消息 (T19, ADR-0005)：页面 ViewModel 发、组合根订阅者或页面自身收，
    /// 取代 RootSettingsViewModel 的聚合根协调（该类已随 T19 拆散删除）。
    /// 消息为不可变空载体/载荷类型（满足 IMessenger 的 class 约束）；
    /// 静态已知依赖不走消息（如外观页预览读方案列表 VM，构造注入）。
    /// </summary>

    /// <summary>页面 VM → 组合根落盘编排：立即落盘（取消挂起防抖后即刻保存）。</summary>
    public sealed class ImmediateSaveRequestedMessage
    {
        public static ImmediateSaveRequestedMessage Instance { get; } = new();
        private ImmediateSaveRequestedMessage() { }
    }

    /// <summary>页面 VM → 组合根落盘编排：防抖落盘（连续变更折叠为一次延迟保存）。</summary>
    public sealed class DebouncedSaveRequestedMessage
    {
        public static DebouncedSaveRequestedMessage Instance { get; } = new();
        private DebouncedSaveRequestedMessage() { }
    }

    /// <summary>
    /// 配置导入成功（运行态配置实例已被 IConfigService 替换）：
    /// 各页面 VM 订阅后自行重挂状态（绑定控件随属性通知自动刷新）；外观页另经
    /// <see cref="PageConfigReloadedMessage"/> 收尾主题应用/预览重绘等 View 效果。
    /// </summary>
    public sealed class ConfigImportedMessage
    {
        public ConfigImportedMessage(AppConfig importedConfig)
        {
            ImportedConfig = importedConfig ?? throw new System.ArgumentNullException(nameof(importedConfig));
        }

        /// <summary>导入后的新运行态配置。</summary>
        public AppConfig ImportedConfig { get; }
    }

    /// <summary>设置窗口已隐藏到托盘：组合根据此发驻留气泡（托盘气泡由组合根订阅或直调）。</summary>
    public sealed class MinimizedToTrayMessage
    {
        public static MinimizedToTrayMessage Instance { get; } = new();
        private MinimizedToTrayMessage() { }
    }

    /// <summary>
    /// 界面主题（AppTheme）变更消息（#54，ADR-0014 决策 7）：<see cref="WinPieGestures.ViewModels.Pages.InterfaceThemeSettingsViewModel"/>
    /// 写穿运行态配置后发布，由壳层主窗口（MainView，ADR-0009 白名单）订阅执行窗口主题应用
    /// （<c>ApplyAppTheme</c>）——页面 SelectionChanged 处理器移除后主题应用改归消息驱动；
    /// 配置导入后的重挂路径同样经本消息触发壳层执行。
    /// </summary>
    public sealed class AppThemeChangedMessage
    {
        public AppThemeChangedMessage(string theme)
        {
            Theme = theme ?? string.Empty;
        }

        /// <summary>变更后的界面主题名（System/Light/Dark/MidnightNavy/RoyalViolet/TitaniumGray）。</summary>
        public string Theme { get; }
    }

    /// <summary>外观设置页请求 View 重绘实时预览。</summary>
    public sealed class AppearancePreviewInvalidatedMessage
    {
        public static AppearancePreviewInvalidatedMessage Instance { get; } = new();
        private AppearancePreviewInvalidatedMessage() { }
    }

    /// <summary>导入配置后某页面 ViewModel 已重挂，外观页 View 订阅后只做主题应用/预览重绘等
    /// View 效果（T21：状态与下拉项已声明式绑定，不再同步控件）。</summary>
    public sealed class PageConfigReloadedMessage
    {
        public Type ViewModelType { get; }

        public PageConfigReloadedMessage(Type viewModelType)
        {
            ViewModelType = viewModelType ?? throw new System.ArgumentNullException(nameof(viewModelType));
        }
    }

    /// <summary>黑名单新增条目后请求页面 View 滚动并选中该条目。</summary>
    public sealed class BlacklistEntryAddedMessage
    {
        public string ProcessName { get; }

        public BlacklistEntryAddedMessage(string processName)
        {
            ProcessName = processName ?? string.Empty;
        }
    }

    /// <summary>通用设置页请求 View 显示通知。</summary>
    public sealed class GeneralNoticeRequestedMessage
    {
        public NoticeRequest Notice { get; }

        public GeneralNoticeRequestedMessage(NoticeRequest notice)
        {
            Notice = notice ?? throw new System.ArgumentNullException(nameof(notice));
        }
    }
}
