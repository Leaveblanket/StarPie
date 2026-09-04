# 模块：壳层与系统集成

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及主题、托盘、单实例/开发实例、内存、自启时读本篇。

## 职责

设置窗口之外的窗口职责集合（主题、托盘、单实例/开发实例、内存、自启）。

## 组成文件

`Services/Shell/`（`IThemeService`/`ThemeService`、`TrayIconManager`、`DevInstance`、`MemoryOptimizer`）、`Services/Configuration/AutostartRegistry.cs`、`Views/Navigation/MainView.xaml.cs`（壳层 code-behind）。

## 关键流程

1. **主题**（[ADR-0012](../adr/0012-resource-dictionary-architecture.md)）：主题画刷令牌存于 `Views/Styles/Themes/*.xaml`（五套同 key 集，`App.xaml` 静态合并 Light 作默认）；`AppHost` 是唯一加载/换入调色板的层——`ApplyTheme` 时把目标调色板写入 Application 直接资源并缓存冻结；`ThemeService` 单例只保留 `ResolveEffectiveTheme`（`System`/空值经注入探测委托读注册表实时判定）、`CurrentEffectiveTheme` 状态与 DWM 标题栏；页面不持 `IThemeService`，仅 `MainView`/对话框窗口构造注入做白名单应用（[ADR-0009](../adr/0009-view-code-behind-whitelist.md)）。
2. **托盘**：`TrayIconManager`（组合根创建）持 tooltip（暂停态实时文案）、双击直达、右键菜单（`menuProvider` 每次打开重建，`I18n.T` 即时取词）、气泡通知、`Dispose`。
3. **单实例/开发实例**：`DevInstance` 静态（`--dev` 标记 → `(Dev)` 后缀、`StarPie-Dev` 配置夹、独立 mutex）；`App.OnStartup` 互斥见 [host.md](host.md)。
4. **内存**：`MemoryOptimizer.TrimMemory()` 在启动后与设置窗口隐藏时由 App/组合根直调（不在业务层调用）。
5. **自启**：注册表读写收敛于 `AutostartRegistry` 静态工具，经组合根委托注入 `GeneralSettingsViewModel`（`isAutoStartEnabled`/`setAutoStart`），不进 VM/View。
6. **关窗驻留**：`MainView` 壳层 code-behind（`Window_Closing` 隐藏到托盘 + 淡出）属 ADR-0009 白名单；`CloseButton_Click` 纯 UI 取消语义。

## 扩展点

- 新壳层行为（如开机自启策略变化）：改 `Shell`/`AutostartRegistry` 并保持委托注入边界。
- 新托盘菜单项：在 `Composition.BuildTrayMenuEntries` 登记（见 [host.md](host.md)）。

## 参见 ADR

[0003](../adr/0003-application-host-restructure.md)（宿主重构）、[0009](../adr/0009-view-code-behind-whitelist.md)（壳层 code-behind 白名单）、[0010](../adr/0010-localization-copy-principles.md)（壳外文案）。
