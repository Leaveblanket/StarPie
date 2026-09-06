using System;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Services;
using StarPie.Services.Navigation;
using StarPie.ViewModels.Pages;

namespace StarPie.Modules
{
    /// <summary>
    /// M5 壳层与系统集成模块注册器（B6/#79：正式模块注册器样板，ADR-0016 决策 8/9；
    /// 原 exe 内 M5ModuleRegistrar 随 M5 抽取为 StarPie.Shell 后改名本类）——
    /// 模块侧注册自治：<see cref="RegisterNavigation"/> 自报本模块页面（高级与系统/关于与更新）
    /// 写入 <see cref="NavigationCatalog"/>（槽位/标题键/图标/目标类型），页面 DataTemplate 收进
    /// ShellPageTemplates.xaml（Host App.xaml 经跨程序集 pack URI 每模块一次静态合并）；
    /// <see cref="RegisterServices"/> 把本模块页面 VM 的 DI 注册下放本程序集（组合根仍唯一
    /// BuildServiceProvider，本注册器只注册不解析）。新增页面 = 本注册器 + 模板字典，不碰 Host。
    /// 宿主回调（托盘气泡/退出）经 Core 的 <see cref="AppHostDelegates"/>（组合根注册单例、
    /// AppHost 构造后回填）转发；自启注册表经本集 AutostartRegistry 静态委托接线；配置
    /// 导入/导出经 Core 的 JsonConfigService 具体服务——模块只依赖 Core 契约，不反向引用 Host。
    /// </summary>
    public static class ShellModuleRegistrar
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

        /// <summary>
        /// 注册 M5 页面 VM（容器单例，B6/#79 自 Composition 下放）：高级与系统页/关于页。
        /// 工厂经 ServiceProvider 惰性解析共享内核件（S2/S3/S4/S6 与 AppHostDelegates）；
        /// 开机自启/提权探测/更新日志打开等 M5 内静态行为在本注册器接线，VM 保持委托注入可测。
        /// </summary>
        public static void RegisterServices(IServiceCollection services)
        {
            services.AddSingleton(sp => new GeneralSettingsViewModel(
                sp.GetRequiredService<IConfigService>().Current,
                sp.GetRequiredService<IDialogService>(),
                // 宿主回调经 Core 委托包转发（B6/#79 上提）：AppHost 构造后回填，VM 不反向依赖宿主类。
                (title, text) => sp.GetRequiredService<AppHostDelegates>().ShowTrayBalloonTip?.Invoke(title, text),
                () => sp.GetRequiredService<AppHostDelegates>().ExitApplication?.Invoke(),
                isAutoStartEnabled: AutostartRegistry.IsAutoStartEnabled,
                setAutoStart: AutostartRegistry.SetAutoStart,
                exportConfig: path => sp.GetRequiredService<JsonConfigService>().Export(path),
                importConfig: path =>
                {
                    // Spec 冲刷时机"导入前"：先冲刷挂起的防抖，再替换运行态配置。
                    sp.GetRequiredService<SettingsSaveOrchestrator>().FlushPendingSave();
                    return sp.GetRequiredService<JsonConfigService>().Import(path);
                },
                currentConfig: () => sp.GetRequiredService<IConfigService>().Current,
                messenger: sp.GetRequiredService<IMessenger>(),
                localization: sp.GetRequiredService<ILocalizationService>(),
                isAdministrator: IsRunningAsAdministrator));
            services.AddSingleton<AboutViewModel>(sp => new AboutViewModel(
                sp.GetRequiredService<IDialogService>(),
                OpenChangelog,
                sp.GetRequiredService<ILocalizationService>()));
        }

        /// <summary>当前进程是否以管理员身份运行（Advanced 页提权卡片用；原 Composition 私有方法随 B6 迁入）。</summary>
        private static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                return new System.Security.Principal.WindowsPrincipal(identity)
                    .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>打开随程序分发的 CHANGELOG.md（About 页外部文件打开编排；无文件返回 false）。</summary>
        private static bool OpenChangelog()
        {
            string changelogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CHANGELOG.md");
            if (!File.Exists(changelogPath)) return false;
            Process.Start(new ProcessStartInfo(changelogPath) { UseShellExecute = true });
            return true;
        }

        // 导航图标（迁移前 MainViewModel 的 NavTab3/NavTab4 Path Data 原样搬迁，B3/#76）
        private const string IconAdvanced =
            "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L2.34,18.73C2.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z";
        private const string IconAbout =
            "M11,9H13V7H11M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M11,17H13V11H11V17Z";
    }
}
