using System.Windows;
using System.Windows.Controls;

namespace WinPieGestures.Views.Pages
{
    /// <summary>
    /// 设置页面基类 (T19/T21/T24)：页面 View 经 DataTemplate 无参构造、按导航重建。
    /// T24 起页面文本声明式化——XAML 经 <c>{DynamicResource}</c> 读组合根换入的语言字典
    /// （见 <see cref="WinPieGestures.Composition"/>），本基类不再订阅 I18n 广播，只保留
    /// <see cref="OnPageLoaded"/>/<see cref="OnPageUnloaded"/> 钩子：页面挂载/卸载时做
    /// View 效果接线的成对订阅退订（页面 VM 是容器单例，页面过期引用靠退订释放）。
    /// </summary>
    public abstract class SettingsPageBase : UserControl
    {
        protected SettingsPageBase()
        {
            Loaded += (_, _) => OnPageLoaded();
            Unloaded += (_, _) => OnPageUnloaded();
        }

        /// <summary>页面挂载（首次与每次导航回到本页）：订阅 View 效果消息、控件初值回填的时机。</summary>
        protected virtual void OnPageLoaded()
        {
        }

        /// <summary>页面卸载：退订 View 效果消息与广播，防单例 VM 持有过期页面引用。</summary>
        protected virtual void OnPageUnloaded()
        {
        }
    }
}
