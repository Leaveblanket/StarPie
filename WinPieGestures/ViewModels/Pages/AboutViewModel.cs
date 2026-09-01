using CommunityToolkit.Mvvm.ComponentModel;

namespace WinPieGestures.ViewModels.Pages
{
    /// <summary>
    /// 关于与更新页面 ViewModel (T19)：纯静态展示内容（版本信息与演进历程），无用户状态；
    /// 作为导航目标存在（页面 VM 容器单例），页面 View 经 DataTemplate 映射呈现。
    /// </summary>
    public class AboutViewModel : ObservableObject
    {
    }
}
