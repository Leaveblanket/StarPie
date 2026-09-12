using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace StarPie.Abstractions.Ui
{
    /// <summary>
    /// 宿主交给 UI 插件的注册上下文：插件的全部 UI 资产只经本接口进入宿主资产登记表。
    /// </summary>
    /// <remarks>
    /// 每个注册返回值进登记表；插件可自行 Dispose，卸载时宿主仍会强制清理。Descriptor 只描述、
    /// 不承载已构造实例——创建时机由宿主在 UI 线程决定。绕过本接口自建 WPF 全局对象（自建 Window、
    /// 直接 merge <c>Application.Current.Resources</c>、自建静态事件/缓存）属不受支持行为，
    /// 泄漏扫描命中即隔离。
    /// </remarks>
    public interface IPluginUiContext
    {
        /// <summary>当前插件 id：与宿主状态条目、数据目录键一致。</summary>
        string PluginId { get; }

        /// <summary>UI 线程端口（后台线程触碰 WPF 对象的唯一入口）。</summary>
        IUiDispatcher Dispatcher { get; }

        /// <summary>注册导航页（纯数据 + 工厂；宿主调用工厂创建 VM）。</summary>
        /// <param name="descriptor">页面描述符（非 null）。</param>
        /// <returns>注销句柄；Dispose 即从登记表摘除该注册项。</returns>
        IDisposable RegisterPage(PluginPageDescriptor descriptor);

        /// <summary>注册设置页区块（宿主渲染或插件自绘）。</summary>
        /// <param name="descriptor">设置区块描述符（非 null）。</param>
        /// <returns>注销句柄。</returns>
        IDisposable RegisterSettingsSection(PluginSettingsSectionDescriptor descriptor);

        /// <summary>注册窗口（只注册工厂；宿主创建、显示、跟踪实例）。</summary>
        /// <param name="descriptor">窗口描述符（非 null）。</param>
        /// <returns>注销句柄。</returns>
        IDisposable RegisterWindow(PluginWindowDescriptor descriptor);

        /// <summary>注册托盘/菜单项（纯数据，宿主渲染入口）。</summary>
        /// <param name="descriptor">菜单项描述符（非 null）。</param>
        /// <returns>注销句柄。</returns>
        IDisposable RegisterMenuItem(PluginMenuItemDescriptor descriptor);

        /// <summary>注册命令（宿主记入资产表并在卸载时注销；命令体不得自行触碰全局 WPF 状态）。</summary>
        /// <param name="descriptor">命令描述符（非 null）。</param>
        /// <returns>注销句柄。</returns>
        IDisposable RegisterCommand(PluginCommandDescriptor descriptor);

        /// <summary>并入插件资源根（宿主为插件持有独立 ResourceDictionary 容器，卸载整体摘除）。</summary>
        /// <param name="packUri">插件包内资源字典的 pack URI（非 null）。</param>
        /// <returns>注销句柄；卸载 = 从容器摘除并回收根。</returns>
        IDisposable MergeResourceDictionary(Uri packUri);

        /// <summary>创建宿主签发的定时器：随卸载整体停止，Tick 回调不落在已释放资源上。</summary>
        /// <param name="interval">触发间隔（必须为正）。</param>
        /// <param name="tick">Tick 回调（非 null），在 UI 线程执行。</param>
        /// <returns>停止并摘除句柄。</returns>
        IDisposable CreateTimer(TimeSpan interval, Action tick);

        /// <summary>宿主中介的简单动画：宿主附着并登记，摘除时用 <c>Storyboard.Remove</c> 而不是只 <c>Stop</c>。</summary>
        /// <param name="target">动画作用的元素（非 null）。</param>
        /// <param name="storyboard">要附着的动画（非 null）。</param>
        /// <returns>摘除句柄；Dispose 即 <c>Remove</c> 并从登记表摘除。</returns>
        IDisposable CreateAnimation(FrameworkElement target, Storyboard storyboard);

        /// <summary>订阅宿主中介事件（订阅句柄进资产表，卸载即断）。</summary>
        /// <typeparam name="TEvent">事件负载类型。</typeparam>
        /// <param name="handler">处理委托（非 null）。</param>
        /// <returns>退订句柄。</returns>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;
    }
}
