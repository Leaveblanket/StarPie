namespace WinPieGestures.Services
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
    /// 各页面 VM 订阅后自行重挂状态，各页 View 订阅后同步控件显示。
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
}
