using WinPieGestures.Services.Navigation;
using WinPieGestures.ViewModels.Pages;

namespace WinPieGestures.Modules
{
    /// <summary>
    /// M5 壳层与系统集成模块注册器（B3/#76：单程序集内先行；B6 随 M5 抽取为 StarPie.Shell）——
    /// 模块侧导航自报：<see cref="RegisterNavigation"/> 把本模块页面（高级与系统/关于与更新）
    /// 写入 <see cref="NavigationCatalog"/>（槽位/标题键/图标/目标类型），页面 DataTemplate 收进
    /// M5PageTemplates.xaml。新增页面 = 本注册器一行 + 模板字典一行，不再触碰 Host 的
    /// MainView/MainViewModel/导航清单。页面 VM 的 DI 注册暂仍集中 Composition（含宿主回调经
    /// AppHostDelegates 的接线，AppHostDelegates 上提 Core 排 B6，见 assemblies.md §8）。
    /// </summary>
    public static class M5ModuleRegistrar
    {
        /// <summary>向导航目录注册 M5 页面（槽位 3 高级与系统 / 4 关于与更新）。</summary>
        public static void RegisterNavigation(NavigationCatalog catalog)
        {
            catalog.RegisterPage<GeneralSettingsViewModel>(
                NavigationSlot.Advanced,
                NavigationSlots.GetAutomationId(NavigationSlot.Advanced),
                "TabAdvanced",
                IconAdvanced);
            catalog.RegisterPage<AboutViewModel>(
                NavigationSlot.About,
                NavigationSlots.GetAutomationId(NavigationSlot.About),
                "TabAbout",
                IconAbout);
        }

        // 导航图标（迁移前 MainViewModel 的 NavTab3/NavTab4 Path Data 原样搬迁，B3/#76）
        private const string IconAdvanced =
            "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L2.34,18.73C2.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z";
        private const string IconAbout =
            "M11,9H13V7H11M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M11,17H13V11H11V17Z";
    }
}
