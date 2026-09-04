# 模块：壳层与系统集成

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及主题、托盘、单实例/开发实例、内存、自启时读本篇。

## 职责

设置窗口之外的窗口职责集合（主题、托盘、单实例/开发实例、内存、自启）。

## 组成文件

`Services/Shell/`（`IThemeService`/`ThemeService`、`TrayIconManager`、`DevInstance`、
`MemoryOptimizer`）、`ThemePaletteManager.cs`（项目根，宿主层主题调色板整项替换）、
`Services/Configuration/AutostartRegistry.cs`、`Views/Navigation/MainView.xaml.cs`（壳层 code-behind）。

## 关键流程

1. **主题**（[ADR-0012](../adr/0012-resource-dictionary-architecture.md) + [ADR-0013](../adr/0013-localization-theme-overhaul.md)）：
   - 画刷令牌存于 `Views/Styles/Themes/*.xaml`（五套同 key 集；`App.xaml` 静态合并
     `Light.xaml` 仅作设计时/首帧默认，并单点合并 `ModernControls.xaml`）。
   - `ThemePaletteManager`（宿主层，自包含）加载/缓存/冻结主题 XAML，把目标调色板**整项替换**
     Application `MergedDictionaries` 中含 `/Themes/` 的活动槽（切 Light 亦整项替换，无直接键残留）。
   - `AppHost` 只编排：构造时 `AttachPaletteApplier(effectiveTheme => paletteManager.Apply(...))`，
     初始主题经 `MainView.ApplyAppTheme(_appearance.AppTheme)`（`SetTheme` + 本窗口 DWM 应用），
     `Run()` 末尾 `EnableSystemThemeTracking()` 启动系统跟随。
   - `ThemeService`（`Services/Shell` 单例，不接触 Views 资源）：`RequestedTheme`/
     `CurrentEffectiveTheme` 状态、`ResolveEffectiveTheme`（`System`/空经注册表探测实时判定）、
     `SetTheme`（唯一状态/资源入口，解析→记录→触发调色板替换→广播 `ThemeChanged`；同有效主题 no-op）、
     `EnableSystemThemeTracking`（`UISettings.ColorValuesChanged` 后台线程 → UI Dispatcher 封送 →
     仅 System/空模式重解析）、`ApplyWindowTheme`（DWM 沉浸式暗色，属性 19/20）。
   - 页面不持 `IThemeService`；`MainView` 与四个对话框窗口构造注入做白名单应用
     （[ADR-0009](../adr/0009-view-code-behind-whitelist.md)）。
2. **托盘**：`TrayIconManager`（`AppHost.Run` 创建）持 tooltip（暂停态实时文案）、双击直达、
   右键菜单（`AppHost.BuildTrayMenuEntries` 每次打开重建，`ILocalizationService` 即时取词）、
   气泡通知、`Dispose`；tooltip 在语言切换时由 `AppHost.RefreshTrayTooltip` 按暂停态刷新。
3. **单实例/开发实例**：`DevInstance` 静态（`--dev` 标记 → `(Dev)` 后缀、`StarPie-Dev` 配置夹、
   独立 mutex）；`App.OnStartup` 互斥见 [host.md](host.md)。
4. **内存**：`MemoryOptimizer.TrimMemory()` 在 `App` 启动兜底与 `AppHost` 主框架隐藏时直调
   （不进业务层）；“立即清理”由 `GeneralSettingsViewModel` 经组合根注入调用。
5. **自启**：注册表读写收敛于 `AutostartRegistry` 静态工具，经组合根委托注入
   `GeneralSettingsViewModel`（`isAutoStartEnabled`/`setAutoStart`），不进 VM/View。
6. **关窗驻留**：`MainView` 壳层 code-behind（`Window_Closing` 隐藏到托盘 + 淡出，退出态读
   `MainViewModel.IsExiting`）属 ADR-0009 白名单；`CloseButton_Click` 纯 UI 取消语义。

## 扩展点

- 新壳层行为（如开机自启策略变化）：改 `Shell`/`AutostartRegistry` 并保持委托注入边界。
- 新托盘菜单项：在 `AppHost.BuildTrayMenuEntries` 登记（见 [host.md](host.md)）。

## 参见 ADR

[0003](../adr/0003-application-host-restructure.md)（宿主重构）、
[0009](../adr/0009-view-code-behind-whitelist.md)（壳层 code-behind 白名单）、
[0012](../adr/0012-resource-dictionary-architecture.md)/[0013](../adr/0013-localization-theme-overhaul.md)（主题令牌与整项替换）。
