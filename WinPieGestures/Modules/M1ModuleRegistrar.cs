using WinPieGestures.Services.Navigation;
using WinPieGestures.ViewModels.Pages;

namespace WinPieGestures.Modules
{
    /// <summary>
    /// M1 手势与动作模块注册器（B3/#76：单程序集内先行；B9 随 M1 抽取为 StarPie.Gestures）——
    /// 模块侧导航自报：<see cref="RegisterNavigation"/> 把本模块页面（触发与场景/手势与动作）
    /// 写入 <see cref="NavigationCatalog"/>（槽位/标题键/图标/目标类型），页面 DataTemplate 收进
    /// M1PageTemplates.xaml。新增页面 = 本注册器一行 + 模板字典一行，不再触碰 Host 的
    /// MainView/MainViewModel/导航清单。页面 VM 的 DI 注册暂仍集中 Composition（M5 注册器样板与
    /// AppHostDelegates 上提排 B6，见 assemblies.md §8），届时随模块程序集整段下放。
    /// </summary>
    public static class M1ModuleRegistrar
    {
        /// <summary>向导航目录注册 M1 页面（槽位 0 触发与场景 / 2 手势与动作）。</summary>
        public static void RegisterNavigation(NavigationCatalog catalog)
        {
            catalog.RegisterPage<BehaviorSettingsViewModel>(
                NavigationSlot.Trigger,
                NavigationSlots.GetAutomationId(NavigationSlot.Trigger),
                "TabTrigger",
                IconTrigger);
            catalog.RegisterPage<ProfileListViewModel>(
                NavigationSlot.Gestures,
                NavigationSlots.GetAutomationId(NavigationSlot.Gestures),
                "TabGestures",
                IconGestures);
        }

        // 导航图标（迁移前 MainViewModel 的 NavTab0/NavTab2 Path Data 原样搬迁，B3/#76）
        private const string IconTrigger =
            "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M12,6A6,6 0 0,0 6,12A6,6 0 0,0 12,18A6,6 0 0,0 18,12A6,6 0 0,0 12,6M12,8A4,4 0 0,1 16,12A4,4 0 0,1 12,16A4,4 0 0,1 8,12A4,4 0 0,1 12,8Z";
        private const string IconGestures =
            "M4,6c0-1.1,0.9-2,2-2h12c1.1,0,2,0.9,2,2v12c0,1.1-0.9,2-2,2H6c-1.1,0-2-0.9-2-2V6z M6,8h12V6H6V8z M6,12h12v-2H6V12z M6,16h6v-2H6V16z";
    }
}
