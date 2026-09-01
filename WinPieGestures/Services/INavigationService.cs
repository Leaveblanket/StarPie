using CommunityToolkit.Mvvm.ComponentModel;

namespace WinPieGestures.Services
{
    /// <summary>导航服务接口（容器开放泛型注册的解析面；组合根按目标类型取用）。</summary>
    public interface INavigationService
    {
        /// <summary>把导航当前页切到目标 ViewModel（经容器解析出的同一单例）。</summary>
        void Navigate();
    }

    /// <summary>
    /// 类型化导航服务 (T19)：按目标页面 ViewModel 类型切换 <see cref="NavigationStore.CurrentViewModel"/>。
    /// 页面 VM 容器单例常驻（状态跨导航不丢）；页面 View 由 DataTemplate 无参重建，不经导航。
    /// 新增页面只需注册 VM 与 DataTemplate，不改导航 switch（ShowSettings(int) 已删）。
    /// </summary>
    public interface INavigationService<TViewModel> : INavigationService
        where TViewModel : ObservableObject
    {
    }
}
