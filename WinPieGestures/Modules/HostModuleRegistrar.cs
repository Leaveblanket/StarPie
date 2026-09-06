using WinPieGestures.Services.Navigation;
using WinPieGestures.ViewModels.Pages;

namespace WinPieGestures.Modules
{
    /// <summary>
    /// Host 侧页面注册器（B3/#76 临时形态，B6/#79 仍居 exe）：外观与形态聚合页壳（#54/#56，D6）
    /// 目标态留 Host（ADR-0016 决策 1/§2，见 assemblies.md §5.2 槽位 1），不随 M1–M5 拆集；
    /// 为统一“注册器 + 模板字典 + 目录”接线，B3 在 exe 内以同形注册器先行验证，M5 部分已随
    /// B6/#79 迁入 StarPie.Shell 的 ShellModuleRegistrar，本注册器继续由 Composition 直调。
    /// <see cref="RegisterNavigation"/> 把本页写入 <see cref="NavigationCatalog"/>，页面
    /// DataTemplate 收进 HostPageTemplates.xaml。
    /// </summary>
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

        // 导航图标（迁移前 MainViewModel 的 NavTab1 Path Data 原样搬迁，B3/#76）
        private const string IconAppearance =
            "M12,3C7.03,3 3,7.03 3,12C3,16.97 7.03,21 12,21C12.83,21 13.5,20.33 13.5,19.5C13.5,19.11 13.35,18.76 13.11,18.49C12.88,18.23 12.73,17.88 12.73,17.5A1.5,1.5 0 0,1 14.23,16H16C18.76,16 21,13.76 21,11C21,6.58 16.97,3 12,3M6.5,12A1.5,1.5 0 0,1 5,10.5A1.5,1.5 0 0,1 6.5,9A1.5,1.5 0 0,1 8,10.5A1.5,1.5 0 0,1 6.5,12M9.5,8A1.5,1.5 0 0,1 8,6.5A1.5,1.5 0 0,1 9.5,5A1.5,1.5 0 0,1 11,6.5A1.5,1.5 0 0,1 9.5,8M14.5,8A1.5,1.5 0 0,1 13,6.5A1.5,1.5 0 0,1 14.5,5A1.5,1.5 0 0,1 16,6.5A1.5,1.5 0 0,1 14.5,8M17.5,12A1.5,1.5 0 0,1 16,10.5A1.5,1.5 0 0,1 17.5,9A1.5,1.5 0 0,1 19,10.5A1.5,1.5 0 0,1 17.5,12Z";
    }
}
