# 模块：壳层与系统集成

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及托盘、开机自启、内存整理、主窗口壳层行为
> 与高级/关于设置面时读本篇；界面主题见 [interface-theme.md](interface-theme.md)。

## 职责

托盘与气泡、开机自启、内存整理、主窗口壳层行为、高级与关于设置面；子职责目录与护栏（D2）见
[modules.md](modules.md) §5。

## 组成文件

- `Services/Shell/TrayIconManager.cs`、`Services/Shell/MemoryOptimizer.cs`（R3：`MemoryOptimizer` 归 M5；
  同目录另含 M4 的 `IThemeService`/`ThemeService` 与 H1 的 `DevInstance`，水平目录不按模块分属，按类型登记）。
- `Services/Configuration/AutostartRegistry.cs`（R1：`AutostartRegistry` 归 M5，物理暂居 S2 目录）。
- `Views/Navigation/MainView.xaml.cs`（R4：壳层 code-behind 归 M5；`MainView.xaml` 的导航映射归 S5，
  见 [navigation.md](navigation.md)）。
- 设置面：`GeneralSettingsViewModel`+`AdvancedSettingsPage`、`AboutViewModel`+`AboutSettingsPage`
  （D6：M5 设置面；VM 注册与宿主回调见 [host.md](host.md)，页面绑定规范见 [layering.md](layering.md)）。

## 关键流程

1. **托盘**：`TrayIconManager`（`AppHost.Run` 创建）持 tooltip（暂停态实时文案）、双击直达、
   右键菜单（`AppHost.BuildTrayMenuEntries` 每次打开重建，`ILocalizationService` 即时取词）、
   气泡通知、`Dispose`；tooltip 在语言切换时由宿主 `AppHost.RefreshTrayTooltip` 按暂停态刷新
   （宿主编排见 [host.md](host.md)）。
2. **内存**：`MemoryOptimizer.TrimMemory()` 在 `App` 启动兜底与 `AppHost` 主框架隐藏时直调
   （不进业务层，调用点见 [host.md](host.md)）；“立即清理”由 `GeneralSettingsViewModel` 经组合根注入调用。
3. **自启**：注册表读写收敛于 `AutostartRegistry` 静态工具，经组合根委托注入
   `GeneralSettingsViewModel`（`isAutoStartEnabled`/`setAutoStart`），不进 VM/View。
4. **关窗驻留**：`MainView` 壳层 code-behind（`Window_Closing` 隐藏到托盘 + 淡出，退出态读
   `MainViewModel.IsExiting`）属 ADR-0009 白名单；`MainViewModel` 主归 S5、壳层成员按 D3 借调 M5
   （见 [navigation.md](navigation.md)）；`CloseButton_Click` 纯 UI 取消语义。
5. **高级与关于设置面**：导入/导出、内存清理、自启开关、托盘气泡与退出等宿主接线经
   `AppHostDelegates` 注入（见 [host.md](host.md)），页面绑定规范见 [layering.md](layering.md)
   （`AdvancedSettingsPage` 示例）。

## 扩展点

- 新壳层行为（如开机自启策略变化）：改 M5 内部（`TrayIconManager`/`AutostartRegistry` 等）并保持委托注入边界。
- 新托盘菜单项：在 `AppHost.BuildTrayMenuEntries` 登记（宿主接线见 [host.md](host.md)）。
- 新 OS 集成功能按 D2 护栏先对号入座（[modules.md](modules.md) §5 D2）。

## 参见 ADR

[0003](../adr/0003-application-host-restructure.md)（宿主重构）、
[0009](../adr/0009-view-code-behind-whitelist.md)（壳层 code-behind 白名单）、
[0015](../adr/0015-module-map-and-ownership.md)（12 模块地图：M5 与 R1/R3/R4/D2/D3）。
