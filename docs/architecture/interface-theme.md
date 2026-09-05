# 模块：界面主题

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及 AppTheme 配置与解析、状态/切换与系统跟随、
> XAML 令牌集与整项替换、界面主题设置面与主题应用消息时读本篇（B1/#64 自原「壳层与系统集成」叶拆出）。

## 职责

窗口 UI 主题体系（AppTheme）——配置与解析、状态/切换/系统跟随、XAML 令牌集与整项替换、界面主题设置面、
主题应用消息（[ADR-0015](../adr/0015-module-map-and-ownership.md) M4，正名衔接 ADR-0014）。与轮盘配色
（M2，见 [wheel.md](wheel.md)）分离：本模块只管窗口界面主题。

## 组成文件

- `Services/Shell/`：`IThemeService`/`ThemeService`（物理暂居水平目录，按类型归 M4；同目录另含 M5 的
  `TrayIconManager`/`MemoryOptimizer` 与 H1 的 `DevInstance`，见 [shell.md](shell.md)/[host.md](host.md)）。
- `ThemePaletteManager.cs`（项目根，宿主层主题调色板整项替换）。
- `Views/Styles/Themes/*.xaml`（五套同 key 集）。
- `ViewModels/Pages/InterfaceThemeSettingsViewModel.cs`（界面主题设置子 VM，#54/ADR-0014 决策 6/7）。
- `AppThemeChangedMessage`（主题应用消息：语义归 M4；类型定义集中于 S4 hub `Services/Messages/Messages.cs`，
  放行共享面，见 [messages.md](messages.md)）。

## 关键流程

1. **XAML 令牌**（[ADR-0012](../adr/0012-resource-dictionary-architecture.md) + [ADR-0013](../adr/0013-localization-theme-overhaul.md)）：
   画刷令牌存于 `Views/Styles/Themes/*.xaml`（五套同 key 集；`App.xaml` 静态合并 `Light.xaml` 仅作
   设计时/首帧默认，并单点合并 `ModernControls.xaml`）。
2. **整项替换**：`ThemePaletteManager`（宿主层，自包含）加载/缓存/冻结主题 XAML，把目标调色板**整项替换**
   Application `MergedDictionaries` 中含 `/Themes/` 的活动槽（切 Light 亦整项替换，无直接键残留）。
3. **宿主编排（H1 放行面）**：`AppHost`（归 H1，见 [host.md](host.md)）只编排：构造时
   `AttachPaletteApplier(effectiveTheme => paletteManager.Apply(...))`，初始主题经
   `MainView.ApplyAppTheme(_interfaceTheme.AppTheme)`（`SetTheme` + 本窗口 DWM 应用），`Run()` 末尾
   `EnableSystemThemeTracking()` 启动系统跟随。
4. **界面主题设置面（#54，ADR-0014 决策 6/7）**：`InterfaceThemeSettingsViewModel`
   （`ViewModels/Pages`，DI 单例，注入外观聚合 VM 暴露为 `InterfaceTheme`）；写穿配置后发布
   `AppThemeChangedMessage`，由 `MainView` 壳层 code-behind（文件归属见 [shell.md](shell.md)）订阅执行
   `ApplyAppTheme`——外观页不再挂主题 `SelectionChanged` 处理器；配置导入后的窗口主题应用重挂路径
   同样经该消息由壳层执行。#56 起外观聚合 VM 注入两个设置子 VM（另一为轮盘外观设置子 VM
   `WheelAppearanceSettingsViewModel`，见 [wheel.md](wheel.md)）。
5. **ThemeService**（`Services/Shell` 单例，不接触 Views 资源）：`RequestedTheme`/`CurrentEffectiveTheme`
   状态、`ResolveEffectiveTheme`（`System`/空经注册表探测实时判定）、`SetTheme`（唯一状态/资源入口，
   解析→记录→触发调色板替换→广播 `ThemeChanged`；同有效主题 no-op）、`EnableSystemThemeTracking`
   （`UISettings.ColorValuesChanged` 后台线程 → UI Dispatcher 封送 → 仅 System/空模式重解析）、
   `ApplyWindowTheme`（DWM 沉浸式暗色，属性 19/20）。
6. **窗口白名单应用**：页面不持 `IThemeService`；`MainView` 与四个对话框窗口构造注入做白名单应用
   （[ADR-0009](../adr/0009-view-code-behind-whitelist.md)）。

## 扩展点

- 新增主题方案/令牌/跟随策略：M4 内部（`Themes/*.xaml`、`ThemeService`、解析）+ S3 新文案键
  （见 [localization.md](localization.md)）；新增主题应用消息类型时经 S4 hub 登记（放行共享面，
  见 [messages.md](messages.md)）。

## 参见 ADR

[0009](../adr/0009-view-code-behind-whitelist.md)（窗口白名单应用）、
[0012](../adr/0012-resource-dictionary-architecture.md)/[0013](../adr/0013-localization-theme-overhaul.md)
（主题令牌与整项替换）、[0014](../adr/0014-wheel-palette-module-boundary-and-appearance-split.md)
（界面主题模块边界/正名）、[0015](../adr/0015-module-map-and-ownership.md)（12 模块地图：M4）。
