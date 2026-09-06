using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace WinPieGestures.Services.Navigation
{
    /// <summary>
    /// <see cref="INavigationService{TViewModel}"/> 默认实现 (T19)：目标页面 VM 从容器解析
    /// （单例——多次导航拿到同一实例，状态常驻）。解析点收在导航服务一处，组合根只注册开放泛型。
    /// </summary>
    public sealed class NavigationService<TViewModel> : INavigationService<TViewModel>
        where TViewModel : ObservableObject
    {
        private readonly NavigationStore _store;
        private readonly IServiceProvider _services;

        public NavigationService(NavigationStore store, IServiceProvider services)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public void Navigate()
        {
            _store.CurrentViewModel = _services.GetRequiredService<TViewModel>();
        }
    }
}
