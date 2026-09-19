using System;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using StarPie.Sdk.Services;
using StarPie.Sdk.Services.Navigation;
using StarPie.Ui.Services.Navigation;
using StarPie.Sdk.ViewModels.Pages;
using StarPie.Ui.ViewModels.Pages;

namespace StarPie.Ui.Modules
{
    /// <summary>
    /// M5 常驻壳层与系统集成贡献者：模块侧注册自治。<see cref="RegisterNavigation"/> 自报本模块
    /// 页面（高级与系统）写入 <see cref="NavigationCatalog"/>（槽位/标题键/图标/
    /// 目标类型），页面 DataTemplate 收进 SystemIntegrationPageTemplates.xaml（宿主 App.xaml 经跨程序集
    /// pack URI 每模块一次静态合并）；<see cref="RegisterServices"/> 把本模块页面 VM 的 DI 注册
    /// 下放本程序集（组合根仍唯一 BuildServiceProvider，本贡献者只注册不解析）。
    /// </summary>
    /// <remarks>
    /// 新增页面 = 本注册器 + 模板字典，不碰宿主。宿主回调（托盘气泡/退出）经共享内核的
    /// <see cref="AppHostDelegates"/> 转发（组合根注册单例、宿主 AppHost 构造后回填）；
    /// 自启注册表经本集 AutostartRegistry 静态委托接线；配置导入/导出用共享内核的
    /// <see cref="JsonConfigService"/> 具体服务——模块只依赖共享内核契约，不反向引用宿主。
    /// </remarks>
    internal sealed class SystemIntegrationContributor : ICompositionContributor
    {
        public string Id => "system.integration";

        public int Order => 50;

        /// <summary>向导航目录注册本模块页面（槽位：3 高级与系统）。</summary>
        public void RegisterNavigation(NavigationCatalog catalog)
        {
            catalog.RegisterPage<GeneralSettingsViewModel>(
                NavigationSlot.Advanced,
                NavigationSlots.GetAutomationId(NavigationSlot.Advanced),
                "PageAdvanced",
                IconAdvanced);
        }

        /// <summary>
        /// 注册本模块页面 VM（设置台会话作用域）：高级与系统页。工厂经 ServiceProvider
        /// 惰性解析共享内核件（配置/对话框/本地化/消息与 AppHostDelegates）；开机自启
        /// 等本模块内静态行为在此接线，VM 保持委托注入可测。
        /// 托盘气泡归常驻壳层，故作用域随设置台会话（关窗即销毁、重开重建）。
        /// </summary>
        public void RegisterServices(IServiceCollection services)
        {
            services.AddScoped(sp => new GeneralSettingsViewModel(
                sp.GetRequiredService<IConfigService>().Current,
                sp.GetRequiredService<IDialogService>(),
                isAutoStartEnabled: AutostartRegistry.IsAutoStartEnabled,
                // 自启形态整体落位（注册表 Run 与提权计划任务同源），成败由系统实况回读；
                // 提权形态需要管理员权限，失败（UAC 取消/无管理员凭据）由 VM 提示并拨回实况。
                applyAutoStart: AutostartRegistry.ApplyAutoStart,
                isAdminAutoStartEnabled: AutostartRegistry.IsAdminAutoStartEnabled,
                // 「立即提权」入口只在非提权态出现：提权态探测与常驻壳层同源（ProcessElevation）。
                isRunningElevated: ProcessElevation.IsRunningAsAdministrator,
                // 触发经宿主委托包转发（常驻壳层回填实现）：VM 不反依赖宿主类，也不给应用内入口开专用通道。
                hostDelegates: sp.GetRequiredService<AppHostDelegates>(),
                exportConfig: path => sp.GetRequiredService<JsonConfigService>().Export(path),
                importConfig: path =>
                {
                    // Spec 冲刷时机"导入前"：先冲刷挂起的防抖，再替换运行态配置。
                    sp.GetRequiredService<SettingsSaveOrchestrator>().FlushPendingSave();
                    return sp.GetRequiredService<JsonConfigService>().Import(path);
                },
                currentConfig: () => sp.GetRequiredService<IConfigService>().Current,
                messenger: sp.GetRequiredService<IMessenger>(),
                localization: sp.GetRequiredService<ILocalizationService>()));
        }

        // 导航图标 Path Data（对应本模块页签）。
        private const string IconAdvanced =
            "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L2.34,18.73C2.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z";
    }
}
