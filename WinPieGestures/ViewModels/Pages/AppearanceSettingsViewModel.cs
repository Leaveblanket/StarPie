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
    /// 释放链：随容器释放时先释放两个子 VM（各自成对退订本地化事件）；
    /// 幂等——容器随后对子 VM 单例的直接释放亦安全。
    /// </remarks>
    public partial class AppearanceSettingsViewModel : ObservableObject, IDisposable
    {
        private readonly IMessenger _messenger;
        private bool _disposed;

        /// <summary>界面主题设置子 VM 单例（构造注入）：外观页界面主题卡 DataContext 指向
        /// 本属性；AppTheme 透传/选项目录/主题应用消息由该子 VM 独占。</summary>
        public InterfaceThemeSettingsViewModel InterfaceTheme { get; }

        /// <summary>轮盘外观设置子 VM 单例（构造注入）：界面主题卡之外全部设置卡
        /// DataContext 指向本属性；轮盘外观状态/命令与预览只读状态接口
        /// <see cref="IWheelAppearanceState"/> 由该子 VM 独占。</summary>
        public WheelAppearanceSettingsViewModel WheelAppearance { get; }

        public AppearanceSettingsViewModel(
            IMessenger messenger,
            InterfaceThemeSettingsViewModel interfaceTheme,
            WheelAppearanceSettingsViewModel wheelAppearance)
        {
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            InterfaceTheme = interfaceTheme ?? throw new ArgumentNullException(nameof(interfaceTheme));
            WheelAppearance = wheelAppearance ?? throw new ArgumentNullException(nameof(wheelAppearance));

            // 导入成功广播 → 子 VM 各自订阅自行重挂；聚合壳只做页面级收尾广播——外观页
            // View 收到后重绘实时预览（状态与下拉项已声明式绑定，随子 VM 通知自动刷新）。
            messenger.Register<ConfigImportedMessage>(this, (_, _) =>
                _messenger.Send(new PageConfigReloadedMessage(typeof(AppearanceSettingsViewModel))));
        }

        /// <summary>释放链：释放两个设置子 VM（各自成对退订本地化事件）。
        /// 幂等——组合根随 Composition.Dispose 对每个单例再释放一次亦安全。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            InterfaceTheme.Dispose();
            WheelAppearance.Dispose();
        }
    }
}
