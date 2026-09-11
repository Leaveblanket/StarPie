# 模块：壳层与系统集成

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及托盘、开机自启、内存整理、主窗口壳层行为
> 与高级设置面时读本篇；界面主题见 [interface-theme.md](interface-theme.md)。

## 职责

托盘与气泡、开机自启、内存整理、主窗口壳层行为、高级设置面；子职责目录与护栏（D2）见
[modules.md](modules.md) §5。

## 组成文件

M5 物理落位（独立模块程序集 `StarPie.Shell/`）：

- `StarPie.Shell/Services/Shell/TrayIconManager.cs`（含 `TrayMenuEntry`；托盘类与菜单行为驻模块，
  由 Host `AppHost.Run` 装配实例——见下方关键流程 1）、`AutostartRegistry.cs`（R1）、
  `MemoryOptimizer.cs`（R3）。
- `StarPie.Shell/ViewModels/Pages/GeneralSettingsViewModel.cs` 与
  `StarPie.Shell/Views/Pages/AdvancedSettingsPage.xaml(.cs)`
  （D6：M5 设置面；页面 XAML 根直承 `UserControl`——共享页面基类 `SettingsPageBase` 已删除）。
- `StarPie.Shell/Modules/ShellModuleRegistrar.cs` + `ShellPageTemplates.xaml`（正式模块注册器与
  页面模板字典，自报导航项/模板并下放页面 VM 的 DI 注册；见 [navigation.md](navigation.md)）。
- SDK 同时登记宿主回调契约 `StarPie.Sdk/Services/AppHostDelegates.cs`（托盘气泡/退出，
  P1.3/#112 自 Core 收口；见 [host.md](host.md)）。

M4 的主题服务（`IThemeService` 实现 `ThemeService`）在 Ui 集 `StarPie.Ui/Services/Shell/`
（命名空间 `StarPie.Services.Shell`；主题引擎 `ThemeEngine` 在宿主内核，见
[interface-theme.md](interface-theme.md)），不在 M5；各业务目录不跨模块登记。

- `ViewModels/Navigation/ShellViewModel.cs`（D3：Host 壳窗口壳层 VM——`WindowTitle`/`IsExiting`/`Save()`；
  归 H1 留 Host，不随 M5，见 [assemblies.md](assemblies.md) §4）。
- `Views/Navigation/MainView.xaml(.cs)`（R4/ADR-0016：Host 壳窗口（H1）；`MainView.xaml`
  为纯壳——页面 DataTemplate 在 App 级模块页面模板字典（M5 在 `StarPie.Shell`、M1 在
   `StarPie.Gestures`、Host 外观聚合页在 `StarPie.Ui/Modules/`，见 [navigation.md](navigation.md)），
  分区 DataContext 接线见下关键流程 4）。

## 关键流程

1. **托盘**：`TrayIconManager`（驻 `StarPie.Shell`，`AppHost.Run` 创建）持 tooltip
   （暂停态实时文案）、双击直达、
   右键菜单（`AppHost.BuildTrayMenuEntries` 每次打开重建，`ILocalizationService` 即时取词）、
   气泡通知、`Dispose`；tooltip 在语言切换时由宿主 `AppHost.RefreshTrayTooltip` 按暂停态刷新
   （宿主编排见 [host.md](host.md)）。托盘菜单深色配色不直读 M4；Shell
   不反向引用 Host/M4，`AppHost` 装配时注入 `Func<bool>` 深色探针
   （`ThemeService.IsWindowsInDarkTheme`；该服务驻 `StarPie.Ui`，Host 经 `IThemeService` 契约消费）。
2. **内存**：`MemoryOptimizer.TrimMemory()` 在 `App` 启动兜底与 `AppHost` 主框架隐藏时直调
   （不进业务层，调用点见 [host.md](host.md)）；“立即清理”由 `GeneralSettingsViewModel` 直调
   （VM 与工具同驻 `StarPie.Shell`，行为不变）。
3. **自启**：注册表读写收敛于 `AutostartRegistry` 静态工具（与 VM 同驻
   `StarPie.Shell`），经模块注册器 `ShellModuleRegistrar.RegisterServices` 委托注入
   `GeneralSettingsViewModel`（`isAutoStartEnabled`/`setAutoStart`），不进 VM/View。
4. **关窗驻留**：`MainView` 壳层 code-behind（`Window_Closing` 隐藏到托盘 + 淡出，退出态读
   `ShellViewModel.IsExiting`）属 ADR-0009 白名单；壳层成员（`WindowTitle`/`IsExiting`/`Save()`）在
   `ShellViewModel`（D3：Host 壳窗口 VM，H1），`MainView` 分区 DataContext——壳区（窗口标题/底部
   操作区）绑 `ShellViewModel`、导航区（侧栏/页面）绑 `MainViewModel`（见 [navigation.md](navigation.md)）；
   `CloseButton_Click` 纯 UI 取消语义。
5. **高级设置面**：导入/导出、内存清理、自启开关、托盘气泡与退出等宿主接线经
   SDK 契约 `AppHostDelegates` 转发（模块注册器只依赖 SDK，宿主回填实现，
   见 [host.md](host.md)），页面绑定规范见 [layering.md](layering.md)
   （`AdvancedSettingsPage` 示例）。

## 扩展点

- 新壳层行为（如开机自启策略变化）：改 M5 内部（`StarPie.Shell` 的 `TrayIconManager`/
  `AutostartRegistry` 等）并保持委托注入边界；新增 M5 设置页只动模块内部（注册器 + 模板字典 +
  VM 注册，见 [navigation.md](navigation.md)），不碰 Host。
- 新托盘菜单项：在 `AppHost.BuildTrayMenuEntries` 登记（宿主接线见 [host.md](host.md)）。
- 新 OS 集成功能按 D2 护栏先对号入座（[modules.md](modules.md) §5 D2）。

## 参见 ADR

[0003](../adr/0003-application-host-restructure.md)（宿主重构）、
[0009](../adr/0009-view-code-behind-whitelist.md)（壳层 code-behind 白名单）、
[0015](../adr/0015-module-map-and-ownership.md)（12 模块地图：M5 与 R1/R3/R4/D2/D3）。
