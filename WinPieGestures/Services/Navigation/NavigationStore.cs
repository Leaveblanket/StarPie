using CommunityToolkit.Mvvm.ComponentModel;

namespace WinPieGestures.Services.Navigation
{
    /// <summary>
    /// 导航状态单一根源 (T19)：持当前页面 ViewModel（容器单例引用，切换只换引用不重建状态）。
    /// 主框架 ViewModel 经其把 <see cref="CurrentViewModel"/> 暴露给 ContentControl，
    /// 并随其变更同步导航项选中态。UI 无关，可直接单测（Spec 预定缝①）。
    /// </summary>
    public sealed class NavigationStore : ObservableObject
    {
        private ObservableObject? _currentViewModel;

        /// <summary>当前页面 ViewModel；启动初始导航前为 null。</summary>
        public ObservableObject? CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }
    }
}
