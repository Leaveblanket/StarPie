using System.Windows;
using System.Windows.Media.Animation;
using StarPie.Abstractions.Ui;
using StarPie.Services.Messages;

namespace StarPie.Plugin.SampleUi
{
    /// <summary>
    /// UI 示例插件的 UI 入口（清单 ui.entryType）：把四类固定扩展点（导航页/设置区/窗口/
    /// 托盘菜单）与四类宿主中介资产（资源字典/定时器/动画/事件订阅）各示范一遍。
    /// </summary>
    /// <remarks>
    /// <see cref="RegisterUi"/> 只做注册不创建界面；页/区块/窗口工厂由宿主在 UI 线程按自己的
    /// 时机调用（页工厂在注册期还会被宿主调用一次取 VM 类型，必须无注册副作用）。
    /// 全部创建路径都经 <see cref="IPluginUiContext"/> 契约，不经契约自建 WPF 全局对象。
    /// </remarks>
    public sealed class SampleUiUiModule : IPluginUiModule
    {
        /// <summary>窗口稳定键（插件内唯一；开窗命令与卸载矩阵共用）。</summary>
        public const string WindowKey = "sample-ui-window";

        /// <summary>导航页稳定键。</summary>
        public const string PageNavigationKey = "sample-ui-main";

        /// <summary>设置区块稳定键。</summary>
        public const string SettingsSectionKey = "sample-ui-about";

        /// <summary>托盘菜单命令 id（菜单项点击路由到这里）。</summary>
        public const string GreetCommandId = "sample-ui.greet";

        private static readonly Uri ResourceDictionaryUri = new(
            "pack://application:,,,/StarPie.Plugin.SampleUi;component/Themes/SampleUiResources.xaml",
            UriKind.Absolute);

        /// <inheritdoc/>
        public void RegisterUi(IPluginUiContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            // 资源字典：并入插件资源根（DataTemplate/样式随根摘除），页面与窗口由此取得视图映射。
            context.MergeResourceDictionary(ResourceDictionaryUri);

            // 宿主签发定时器：Tick 回调与回调目标都是插件对象，卸载矩阵据此断言回收。
            var heartbeat = new SampleUiHeartbeat();
            SampleUiProbes.Track("heartbeat", heartbeat);
            Action tick = () => heartbeat.Tick();
            SampleUiProbes.Track("timer-tick", tick);
            context.CreateTimer(TimeSpan.FromSeconds(1), tick);

            // 固定扩展点：导航页 / 设置区 / 窗口 / 托盘菜单 + 命令。
            context.RegisterPage(new PluginPageDescriptor(
                PageNavigationKey,
                "UI 示例",
                "M3,3H21V21H3V3M5,5V19H19V5H5Z",
                () => new SampleUiPageViewModel(context, heartbeat)));
            context.RegisterSettingsSection(new PluginSettingsSectionDescriptor(
                SettingsSectionKey,
                "UI 示例",
                Order: 0,
                () => new SampleUiSettingsViewModel()));
            context.RegisterWindow(new PluginWindowDescriptor(
                WindowKey,
                "UI 示例窗口",
                () => CreateWindow(context)));
            // 托盘菜单命令：点击经宿主路由回本插件命令体——示例里与页面按钮一样开窗。
            Action greet = () => context.ShowWindow(WindowKey);
            SampleUiProbes.Track("greet-command", greet);
            context.RegisterCommand(new PluginCommandDescriptor(
                GreetCommandId,
                "示例问候",
                greet));
            context.RegisterMenuItem(new PluginMenuItemDescriptor(
                "sample-ui-greet",
                "UI 示例：问候",
                GreetCommandId));

            // 宿主中介事件订阅：宿主吊销即断，处理委托随卸载回收。委托必须带捕获——
            // 无捕获 lambda 会被编译器缓存进 ALC 内的静态字段，探针因此永生（WPF 宿主不回收 ALC）。
            var probeAnchor = new object();
            Action<MinimizedToTrayMessage> handler = _ => GC.KeepAlive(probeAnchor);
            SampleUiProbes.Track("event-handler", handler);
            context.Subscribe(handler);
        }

        /// <summary>窗口工厂：宿主创建并显示；动画经宿主中介附着到窗口内元素。</summary>
        private static Window CreateWindow(IPluginUiContext context)
        {
            var window = new SampleUiWindow();
            SampleUiProbes.Track("window", window);

            var storyboard = new Storyboard();
            var animation = new DoubleAnimation
            {
                From = 1,
                To = 0.4,
                Duration = new Duration(TimeSpan.FromMilliseconds(800)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
            };
            Storyboard.SetTarget(animation, window.AnimationTarget);
            Storyboard.SetTargetProperty(animation, new PropertyPath("Opacity"));
            storyboard.Children.Add(animation);
            SampleUiProbes.Track("storyboard", storyboard);
            SampleUiProbes.Track("animation-target", window.AnimationTarget);

            context.CreateAnimation(window.AnimationTarget, storyboard);
            return window;
        }
    }
}
