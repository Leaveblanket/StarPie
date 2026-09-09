# 模块：界面主题

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及 AppTheme 配置与解析、状态/切换与系统跟随、
> XAML 令牌集与整项替换、界面主题设置面与主题应用消息时读本篇（B1/#64 自原「壳层与系统集成」叶拆出）。

## 职责

窗口 UI 主题体系（AppTheme）——配置与解析、状态/切换/系统跟随、XAML 令牌集与整项替换、界面主题设置面、
主题应用消息（[ADR-0015](../adr/0015-module-map-and-ownership.md) M4，正名衔接 ADR-0014）。与轮盘配色
（M2，见 [wheel.md](wheel.md)）分离：本模块只管窗口界面主题。

## 组成文件

M4 物理落位（B7/#80 起迁入独立模块程序集 `StarPie.Theme/`，命名空间统一为 `StarPie.*`
（B10/#83））：

- `StarPie.Theme/Services/Shell/`：`IThemeService`/`ThemeService`（命名空间
  `StarPie.Services.Shell` 与物理目录一致，B10/#83 统一；Host 侧原
  `WinPieGestures/Services/Shell/` 物理目录已随 B7 清空移除）。
- `StarPie.Theme/ThemePaletteManager.cs`（模块根，主题调色板整项替换；B7/#80 可见性裁决为
  public——Host `AppHost` 装配面，同 B6/#79 `TrayIconManager` 先例，见
  [assemblies.md](assemblies.md) §7）。
- `StarPie.Theme/Views/Styles/Themes/*.xaml`（五套同 key 集）。
- `StarPie.Theme/ViewModels/Pages/InterfaceThemeSettingsViewModel.cs`（界面主题设置子 VM，
  #54/ADR-0014 决策 6/7）。
- `StarPie.Theme/Modules/ThemeModuleRegistrar.cs`（M4 模块注册器：`RegisterServices` 下放
  `ThemeService`/`IThemeService`/`InterfaceThemeSettingsViewModel` 的 DI 注册；M4 无导航页，
  不提供 `RegisterNavigation`）。
- `AppThemeChangedMessage`（主题应用消息：语义归 M4；类型定义集中于 S4 hub
  `Services/Messages/Messages.cs`（Core），放行共享面，见 [messages.md](messages.md)）。

消费接线（方向见 [assemblies.md](assemblies.md) §3）：Host（AppHost/Composition/MainView/
DialogService 装配面）与 M2 轮盘侧（B8/#81 起 StarPie.Wheel，经允许边 Wheel → Theme）及 S6
对话框侧（B11/#88 起 StarPie.Dialogs，对话框窗口主题应用经允许边 Dialogs → Theme，ADR-0020）
经模块程序集引用消费 `IThemeService`/`ThemeService`；M5 托盘深色探针经组合根注入的
`Func<bool>` 委托（B6/#79 起，Shell 不反向引用 M4）；Theme → Core 单向，不反向引用
Host/其它业务模块。

## 关键流程

1. **XAML 令牌**（[ADR-0012](../adr/0012-resource-dictionary-architecture.md) + [ADR-0013](../adr/0013-localization-theme-overhaul.md)）：
   画刷令牌存于 `StarPie.Theme/Views/Styles/Themes/*.xaml`（五套同 key 集，B7/#80 起随 M4
   成集）；Host `App.xaml` 经跨程序集 pack URI
   `/StarPie.Theme;component/Views/Styles/Themes/Light.xaml` 静态合并 Light 仅作设计时/首帧默认，
   并本地单点合并宿主 `Views/Styles/ModernControls.xaml`（全局控件样式字典，ADR-0022/#94 起
   迁 Host、改本地合并；原 `/StarPie.Core;component/Views/Styles/ModernControls.xaml` 跨集合并
   已移除）+ 跨集合并 `StarPie.Gestures` 的 `Views/Styles/HotkeyRecorderBox.xaml`。
2. **整项替换**：`ThemePaletteManager`（B7/#80 起驻 `StarPie.Theme` 且 public，自包含）加载/缓存/
   冻结主题 XAML，把目标调色板**整项替换** Application `MergedDictionaries` 中含 `/Themes/` 的
   活动槽（切 Light 亦整项替换，无直接键残留）。
3. **宿主编排（H1 放行面）**：`AppHost` 只编排（宿主流程见 [host.md](host.md)）：构造时
   `new ThemePaletteManager()`（StarPie.Theme public 装配面）并
   `themeService.AttachPaletteApplier(effectiveTheme => paletteManager.Apply(...))`，初始主题经
   `MainView.ApplyAppTheme(_interfaceTheme.AppTheme)`（`SetTheme` + 本窗口 DWM 应用），`Run()` 末尾
   `EnableSystemThemeTracking()` 启动系统跟随。
4. **界面主题设置面（#54，ADR-0014 决策 6/7）**：`InterfaceThemeSettingsViewModel`
   （`StarPie.Theme/ViewModels/Pages`，DI 单例，B7/#80 起由 `ThemeModuleRegistrar.RegisterServices`
   注册、注入外观聚合 VM 暴露为 `InterfaceTheme`）；写穿配置后发布
   `AppThemeChangedMessage`，由 `MainView` 壳层 code-behind（文件归属见 [shell.md](shell.md)）订阅执行
   `ApplyAppTheme`——外观页不再挂主题 `SelectionChanged` 处理器；配置导入后的窗口主题应用重挂路径
   同样经该消息由壳层执行。#56 起外观聚合 VM 注入两个设置子 VM（另一为轮盘外观设置子 VM
   `WheelAppearanceSettingsViewModel`，见 [wheel.md](wheel.md)）。
5. **ThemeService**（`StarPie.Theme/Services/Shell` 单例，不接触 Views 资源）：`RequestedTheme`/`CurrentEffectiveTheme`
   状态、`ResolveEffectiveTheme`（`System`/空经注册表探测实时判定）、`SetTheme`（唯一状态/资源入口，
   解析→记录→触发调色板替换；同有效主题 no-op。ADR-0020/#88 起不再广播 `ThemeChanged`——主题
   变更的唯一通知通道是 `AppThemeChangedMessage`（见上流程 4），接口事件已移除）、
   `EnableSystemThemeTracking`（`UISettings.ColorValuesChanged` 后台线程 → UI Dispatcher 封送 →
   仅 System/空模式重解析）、`ApplyWindowTheme`（DWM 沉浸式暗色，属性 19/20）。
6. **窗口白名单应用**：页面不持 `IThemeService`；`MainView`（Host）与对话框窗口
   （`StarPie.Dialogs`，B11/#88 起）构造注入做白名单应用
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
