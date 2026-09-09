using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace StarPie.Services.Navigation
{
    /// <summary>
    /// 目录驱动导航执行入口：按 <see cref="NavigationSlot"/> 从
    /// <see cref="NavigationCatalog"/> 取注册项，再从容器惰性解析目标页面 VM。
    /// </summary>
    /// <remarks>
    /// 页面 VM 为容器单例，多次导航同一实例、状态常驻。接口随实现整体归 Host
    /// （ADR-0021/#92），属宿主内部件而非跨程序集解析缝——消费方（主框架导航项、
    /// 托盘直达与初始导航）均在 Host，经本接口按槽位导航，不持有页面类型；
    /// 第二消费方出现时按 <see cref="IDialogService"/> 先例把接口上提共享内核。
    /// </remarks>
    public interface INavigationExecutor
    {
        /// <summary>按目录槽位导航到注册的目标页面 VM（未注册槽位抛
        /// <see cref="InvalidOperationException"/>；完整目录由装配时 Validate 收口）。</summary>
        void Navigate(NavigationSlot slot);
    }

    /// <summary><see cref="INavigationExecutor"/> 默认实现：经 <see cref="NavigationCatalog"/>
    /// 槽位表解析目标类型并交给容器解析（解析点收在本执行缝，组合根只注册）。</summary>
    public sealed class NavigationExecutor : INavigationExecutor
    {
        private readonly NavigationStore _store;
        private readonly NavigationCatalog _catalog;
        private readonly IServiceProvider _services;

        public NavigationExecutor(
            NavigationStore store,
            NavigationCatalog catalog,
            IServiceProvider services)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public void Navigate(NavigationSlot slot)
        {
            NavigationPageRegistration entry = _catalog.GetEntry(slot);
            _store.CurrentViewModel = (ObservableObject)_services.GetRequiredService(entry.ViewModelType);
        }
    }
}
