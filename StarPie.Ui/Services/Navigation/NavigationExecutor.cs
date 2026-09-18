using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Sdk.Services.Navigation;

namespace StarPie.Ui.Services.Navigation
{
    /// <summary>
    /// 目录驱动导航执行入口：按 <see cref="NavigationSlot"/> 从
    /// <see cref="NavigationCatalog"/> 取注册项，再从设置台会话缓存取目标页面 VM。
    /// </summary>
    /// <remarks>
    /// 页面 VM 的作用域是**设置台会话**：同一会话内多次导航同一实例（来回切页保留页内状态），
    /// 会话结束整批释放（<see cref="ConsolePageSession"/>）。本执行缝是规范允许的页面 VM 解析例外
    /// （[ADR-0039](docs/adr/0039-resident-shell-and-transient-settings-console.md) 决策 9），
    /// 缓存由本缝消费，不新增解析点。接口驻 Host，属宿主内部件而非跨程序集解析缝——
    /// 消费方（主框架导航项、托盘直达与初始导航）均在 Host，经本接口按槽位导航，
    /// 不持有页面类型；出现第二个消费方时按 <see cref="IDialogService"/> 先例把接口
    /// 提到共享内核。
    /// </remarks>
    public interface INavigationExecutor
    {
        /// <summary>按目录槽位导航到注册的目标页面 VM（未注册槽位抛
        /// <see cref="InvalidOperationException"/>；完整目录由装配时 Validate 收口）。</summary>
        void Navigate(NavigationSlot slot);

        /// <summary>按目录标识导航（插件页经此入口；未注册标识抛
        /// <see cref="InvalidOperationException"/>）。</summary>
        void Navigate(string identifier);
    }

    /// <summary><see cref="INavigationExecutor"/> 默认实现：经 <see cref="NavigationCatalog"/>
    /// 槽位表取目标类型，从设置台会话缓存解析实例（解析点收在本执行缝，组合根只注册）。</summary>
    public sealed class NavigationExecutor : INavigationExecutor
    {
        private readonly NavigationStore _store;
        private readonly NavigationCatalog _catalog;
        private readonly ConsolePageSession _session;

        public NavigationExecutor(
            NavigationStore store,
            NavigationCatalog catalog,
            ConsolePageSession session)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public void Navigate(NavigationSlot slot)
        {
            Show(_catalog.GetEntry(slot));
        }

        public void Navigate(string identifier)
        {
            Show(_catalog.GetEntry(identifier));
        }

        /// <summary>
        /// 固定页经设置台会话缓存取实例（会话内保留、会话结束释放；常驻型页面注册为 singleton，
        /// 不随会话销毁）；插件页经注册工厂创建（工厂返回值须是页面 VM，每次导航新建）。
        /// </summary>
        /// <remarks>
        /// 页面 VM 只要求实现 <see cref="INotifyPropertyChanged"/>：绑定要的通知面是框架契约，
        /// MVVM 基类是宿主内部选择，插件页 VM 不随宿主框架走。
        /// </remarks>
        private void Show(NavigationPageRegistration entry)
        {
            object viewModel = entry.ViewModelFactory is { } factory
                ? factory()
                : _session.Resolve(entry.ViewModelType);

            _store.CurrentViewModel = viewModel as INotifyPropertyChanged
                ?? throw new InvalidOperationException(
                    $"导航目标不是页面 VM：{entry.Identifier}");
        }
    }
}
