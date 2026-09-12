using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StarPie.Services.Navigation
{
    /// <summary>
    /// 导航状态单一根源：持当前页面 ViewModel（容器单例引用，切换只换引用不重建状态）。
    /// </summary>
    /// <remarks>
    /// 主框架 ViewModel 经其把 <see cref="CurrentViewModel"/> 暴露给 ContentControl，
    /// 并随其变更同步导航项选中态。UI 无关，可直接单测。
    /// 页面 VM 的类型面是 <see cref="INotifyPropertyChanged"/> 而不是某个 MVVM 框架基类：
    /// 插件页 VM 活在插件自己的装载上下文里，只被允许引用 SDK 与框架程序集——要求基类等于要求
    /// 插件复刻宿主所选框架且类型身份一致，那是宿主内部实现细节，不是插件契约。
    /// </remarks>
    public sealed class NavigationStore : ObservableObject
    {
        private INotifyPropertyChanged? _currentViewModel;

        /// <summary>当前页面 ViewModel；启动初始导航前为 null。</summary>
        public INotifyPropertyChanged? CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }
    }
}
