using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using StarPie.Services;

namespace StarPie.ViewModels.Pages
{
    /// <summary>
    /// 外观设置页聚合 ViewModel：页面整体 DataContext 的薄页壳——不持有任何轮盘外观
    /// 状态/命令，只暴露两个设置子 VM：
    /// <list type="bullet">
    /// <item><see cref="InterfaceTheme"/>：界面主题设置子 VM（独占 AppTheme 透传、选项目录
    /// 与主题应用消息）；</item>
    /// <item><see cref="WheelAppearance"/>：轮盘外观设置子 VM（独占轮盘外观状态与命令，
    /// 实现轮盘模块只读状态接口 <see cref="IWheelAppearanceState"/>）。</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// 页面各设置卡 DataContext 指向对应子 VM（界面主题卡 = InterfaceTheme；其余轮盘外观卡 =
    /// WheelAppearance）；页面整体 DataContext 仍是本聚合 VM。
    /// 配置导入后，两个子 VM 各自订阅 <see cref="ConfigImportedMessage"/> 自行重挂；本聚合 VM
    /// 保留导入订阅只为页面级收尾——广播
    /// <see cref="PageConfigReloadedMessage"/>（typeof 本 VM）通知外观页 View 重绘实时预览等 View
    /// 效果；窗口主题应用由 InterfaceTheme 子 VM 发 AppThemeChangedMessage、壳层主窗口订阅执行。
    /// 系统深浅色取值经注入的无状态探针 <see cref="WindowsInDarkMode"/> 暴露给页面：
    /// 页面（View）不得做服务调用，也不得用 messenger 替代同页绑定，故由 VM 取值、页面读属性。
    /// 释放链：随设置台会话释放时先释放两个子 VM（各自退订语言订阅）；
    /// 幂等——容器随后对子 VM 的直接释放亦安全。
    /// </remarks>
    public partial class AppearanceSettingsViewModel : ObservableObject, IDisposable
    {
        private readonly IMessenger _messenger;
        private readonly Func<bool> _windowsInDarkModeProbe;
        private bool _disposed;

        /// <summary>界面主题设置子 VM（构造注入；会话作用域注册）：外观页界面主题卡 DataContext 指向
        /// 本属性；AppTheme 透传/选项目录/主题应用消息由该子 VM 独占。</summary>
        public InterfaceThemeSettingsViewModel InterfaceTheme { get; }

        /// <summary>轮盘外观设置子 VM（构造注入；会话作用域注册）：界面主题卡之外全部设置卡
        /// DataContext 指向本属性；轮盘外观状态/命令与预览只读状态接口
        /// <see cref="IWheelAppearanceState"/> 由该子 VM 独占。</summary>
        public WheelAppearanceSettingsViewModel WheelAppearance { get; }

        /// <summary>共享图标资产实例服务（S1，ADR-0019/#87）：外观页实时预览渲染器为
        /// View 层无 DI 构造对象，经本聚合 VM（设置台会话作用域）暴露的已批准预览桥取得服务，
        /// 供页面 OnPageLoaded 装配 <c>WheelPreviewRenderer</c>。</summary>
        public IIconAssetService IconAssetService { get; }

        /// <summary>
        /// Windows 当前是否处于深色模式（实时读注册表键的无状态探针，见 ADR-0039 决策 3）：
        /// 外观页实时预览渲染取用。探针由宿主注入，本 VM 不引用主题服务。
        /// </summary>
        public bool WindowsInDarkMode => _windowsInDarkModeProbe();

        public AppearanceSettingsViewModel(
            IMessenger messenger,
            InterfaceThemeSettingsViewModel interfaceTheme,
            WheelAppearanceSettingsViewModel wheelAppearance,
            IIconAssetService iconAssetService,
            Func<bool> windowsInDarkModeProbe)
        {
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _windowsInDarkModeProbe = windowsInDarkModeProbe ?? throw new ArgumentNullException(nameof(windowsInDarkModeProbe));
            InterfaceTheme = interfaceTheme ?? throw new ArgumentNullException(nameof(interfaceTheme));
            WheelAppearance = wheelAppearance ?? throw new ArgumentNullException(nameof(wheelAppearance));
            IconAssetService = iconAssetService ?? throw new ArgumentNullException(nameof(iconAssetService));

            // 导入成功广播 → 子 VM 各自订阅自行重挂；聚合壳只做页面级收尾广播——外观页
            // View 收到后重绘实时预览（状态与下拉项已声明式绑定，随子 VM 通知自动刷新）。
            messenger.Register<ConfigImportedMessage>(this, (_, _) =>
                _messenger.Send(new PageConfigReloadedMessage(typeof(AppearanceSettingsViewModel))));
        }

        /// <summary>释放链：释放两个设置子 VM（各自成对退订本地化事件）。
        /// 幂等——设置台会话作用域释放与聚合页显式释放重复调用亦安全。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _messenger.UnregisterAll(this);
            // 两个设置子 VM 与聚合页同作用域：作用域释放会各自 Dispose，此处显式释放保证
            // 聚合页被单独释放（测试/显式路径）时子 VM 也成对退订；重复 Dispose 幂等。
            InterfaceTheme.Dispose();
            WheelAppearance.Dispose();
        }
    }
}
