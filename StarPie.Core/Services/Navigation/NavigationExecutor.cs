using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace StarPie.Services.Navigation
{
    /// <summary>
    /// 导航目录执行缝（S5，ADR-0016 决策 3/8，B3/#76）：目录驱动的导航执行入口——按
    /// <see cref="NavigationSlot"/> 从 <see cref="NavigationCatalog"/> 取注册项，再从容器
    /// 惰性解析目标页面 VM（单例——多次导航同一实例，状态常驻，与
    /// <see cref="NavigationService{TViewModel}"/> 同属已批准解析缝）。
    /// B3 起主框架导航项/托盘直达/初始导航均经本接口按槽位导航，消费方不再持有页面类型。
    /// </summary>
    public interface INavigationExecutor
    {
        /// <summary>按目录槽位导航到注册的目标页面 VM（未注册槽位抛
        /// <see cref="InvalidOperationException"/>；完整目录由装配时 Validate 收口）。</summary>
        void Navigate(NavigationSlot slot);
    }

    /// <summary><see cref="INavigationExecutor"/> 默认实现：经 <see cref="NavigationCatalog"/>
    /// 槽位表解析目标类型并交给容器解析（解析点收在导航执行缝，组合根只注册）。</summary>
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
