using StarPie.Services.Navigation;
using StarPie.ViewModels.Pages;

namespace StarPie.Modules
{
    /// <summary>
    /// 宿主侧页面注册器：注册仍留在宿主程序集的外观与形态聚合页。
    /// </summary>
    /// <remarks>
    /// 外观聚合页壳的目标归属为宿主，不随业务模块拆集；<see cref="RegisterNavigation"/>
    /// 把页面写入 <see cref="NavigationCatalog"/>，页面 DataTemplate 收进
    /// HostPageTemplates.xaml。
    /// </remarks>
    public static class HostModuleRegistrar
    {
        /// <summary>向导航目录注册 Host 页面（槽位 1 外观与形态）。</summary>
        public static void RegisterNavigation(NavigationCatalog catalog)
        {
            catalog.RegisterPage<AppearanceSettingsViewModel>(
                NavigationSlot.Appearance,
                NavigationSlots.GetAutomationId(NavigationSlot.Appearance),
                "TabAppearance",
                IconAppearance);
        }

        // 外观槽位导航图标 Path Data
        private const string IconAppearance =
            "M12,3C7.03,3 3,7.03 3,12C3,16.97 7.03,21 12,21C12.83,21 13.5,20.33 13.5,19.5C13.5,19.11 13.35,18.76 13.11,18.49C12.88,18.23 12.73,17.88 12.73,17.5A1.5,1.5 0 0,1 14.23,16H16C18.76,16 21,13.76 21,11C21,6.58 16.97,3 12,3M6.5,12A1.5,1.5 0 0,1 5,10.5A1.5,1.5 0 0,1 6.5,9A1.5,1.5 0 0,1 8,10.5A1.5,1.5 0 0,1 6.5,12M9.5,8A1.5,1.5 0 0,1 8,6.5A1.5,1.5 0 0,1 9.5,5A1.5,1.5 0 0,1 11,6.5A1.5,1.5 0 0,1 9.5,8M14.5,8A1.5,1.5 0 0,1 13,6.5A1.5,1.5 0 0,1 14.5,5A1.5,1.5 0 0,1 16,6.5A1.5,1.5 0 0,1 14.5,8M17.5,12A1.5,1.5 0 0,1 16,10.5A1.5,1.5 0 0,1 17.5,9A1.5,1.5 0 0,1 19,10.5A1.5,1.5 0 0,1 17.5,12Z";
    }
}
