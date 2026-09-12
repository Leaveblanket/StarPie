# 模块：界面主题

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及 AppTheme 配置与解析、状态/切换与系统跟随、
> XAML 令牌集与整项替换、界面主题设置面与主题应用消息时读本篇。

## 职责

窗口 UI 主题体系（AppTheme）——配置与解析、状态/切换/系统跟随、XAML 令牌集与整项替换、界面主题设置面、
主题应用消息（[ADR-0015](../adr/0015-module-map-and-ownership.md) M4，正名衔接 ADR-0014）。与轮盘配色
（M2，见 [wheel.md](wheel.md)）分离：本模块只管窗口界面主题。

## 组成文件

M4 按目标归属拆分：主题引擎入宿主内核（`StarPie.Host/Themes/`，零 WPF），主题字典与设置 VM 入
Ui 集（`StarPie.Ui`）；出口契约 `IThemeService` 收口于 `StarPie.Sdk.Wpf/Services/Shell/`（ADR-0023）。
独立模块程序集 `StarPie.Theme` 与契约工程 `StarPie.Theme.Contracts` 均已撤销：

- `StarPie.Host/Themes/ThemeEngine.cs`（主题引擎：请求/有效主题状态、有效主题解析、切换与系统跟随
  重解析；命名空间 `StarPie.Themes`，零 WPF）。
- `StarPie.Host/Ports/IThemeApplier.cs`（宿主内核 → Ui 的主题应用端口；命名空间 `StarPie.Ports`）。
- `StarPie.Sdk.Wpf/Services/Shell/IThemeService.cs`（M4 出口契约 `IThemeService`，ADR-0023；
  命名空间 `StarPie.Services.Shell` 不变）。
- `StarPie.Ui/Services/Shell/ThemeService.cs`（`IThemeService` 实现：透传内核引擎状态，承担窗口
  DWM 标题栏应用与系统深浅色监听；命名空间 `StarPie.Services.Shell`）。
- `StarPie.Ui/Adapters/AppThemePaletteManager.cs`（主题调色板适配器，实现内核端口
  `IThemeApplier`：加载/缓存/冻结主题字典，整项替换 Application 合并字典的活动主题槽）。
- `StarPie.Ui/Themes/*.xaml`（五套同 key 集主题画刷令牌；App.xaml 静态合并 Light 作设计时/首帧默认）。
- `StarPie.Ui/ViewModels/Pages/InterfaceThemeSettingsViewModel.cs`（界面主题设置子 VM，ADR-0014 决策 6/7）。
- `StarPie.Ui/Modules/ThemeModuleRegistrar.cs`（M4 模块注册器：`RegisterServices` 下放
  `ThemeService`/`IThemeService`（契约驻 StarPie.Sdk.Wpf）/`InterfaceThemeSettingsViewModel`
  的 DI 注册；M4 无导航页，不提供 `RegisterNavigation`）。
- `AppThemeChangedMessage`（主题应用消息：语义归 M4；类型定义集中于 S4 hub
  `StarPie.Sdk/Services/Messages/Messages.cs`，放行共享面，见 [messages.md](messages.md)）。

消费接线（方向见 [assemblies.md](assemblies.md) §3）：Host（AppHost/Composition/MainView/
DialogService 装配面）消费 `IThemeService`；M2 轮盘侧（驻 StarPie.Ui）与 S6 对话框侧
（StarPie.Dialogs）只经 `StarPie.Sdk.Wpf` 契约边消费 `IThemeService`；M5 托盘深色
探针经组合根注入的 `Func<bool>` 委托（Shell 不反向引用 M4）；Ui → 宿主内核 + Sdk.Wpf 单向，
内核不反向引用 Ui。

## 关键流程

1. **XAML 令牌**（[ADR-0012](../adr/0012-resource-dictionary-architecture.md) + [ADR-0013](../adr/0013-localization-theme-overhaul.md)）：
   画刷令牌存于 `StarPie.Ui/Themes/*.xaml`（五套同 key 集）；Host `App.xaml`
   经本地绝对 pack URI 静态合并 Light 仅作设计时/首帧默认，
   并本地单点合并宿主 `Views/Styles/ModernControls.xaml`（全局控件样式字典）
   + 本地合并 M1 归并后的 `Views/Styles/HotkeyRecorderBox.xaml`。
2. **整项替换**：`AppThemePaletteManager`（驻 `StarPie.Ui/Adapters/`，实现内核端口）加载/缓存/
   冻结主题字典，把目标调色板**整项替换** Application `MergedDictionaries` 中含 `/Themes/` 的
   活动槽（切 Light 亦整项替换，无直接键残留）。
3. **宿主编排（H1 放行面）**：`AppHost` 只编排（宿主流程见 [host.md](host.md)）：构造时把
   Ui 侧调色板适配器接到主题服务（`AttachApplier`），初始主题经
   `MainView.ApplyAppTheme(_interfaceTheme.AppTheme)`（`SetTheme` + 本窗口 DWM 应用），`Run()` 末尾
   `EnableSystemThemeTracking()` 启动系统跟随。
4. **界面主题设置面（ADR-0014 决策 6/7）**：`InterfaceThemeSettingsViewModel`
   （`StarPie.Ui/ViewModels/Pages`，DI 单例，由 `ThemeModuleRegistrar.RegisterServices`
   注册、注入外观聚合 VM 暴露为 `InterfaceTheme`）；写穿配置后发布
   `AppThemeChangedMessage`，由 `MainView` 壳层 code-behind（文件归属见 [shell.md](shell.md)）订阅执行
   `ApplyAppTheme`——外观页不再挂主题 `SelectionChanged` 处理器；配置导入后的窗口主题应用重挂路径
   同样经该消息由壳层执行。外观聚合 VM 注入两个设置子 VM（另一为轮盘外观设置子 VM
   `WheelAppearanceSettingsViewModel`，见 [wheel.md](wheel.md)）。
5. **主题引擎与服务**：`ThemeEngine`（宿主内核，零 WPF）持有
   `RequestedTheme`/`CurrentEffectiveTheme` 状态、`ResolveEffectiveTheme`（`System`/空经注册表探测
   实时判定）、`SetTheme`（唯一状态/资源入口，解析→记录→经端口触发调色板替换；同有效主题 no-op、
   首次应用恒执行）与 `RefreshSystemTheme`（跟随系统下按最新系统状态重解析）。主题
   变更的唯一通知通道是 `AppThemeChangedMessage`（见流程 4）。
   `ThemeService`（`StarPie.Ui/Services/Shell`，实现 `IThemeService`）把引擎状态透传给消费方，并承担
   WPF/WinRT 侧效果：`EnableSystemThemeTracking`（`UISettings.ColorValuesChanged` 后台线程 → UI Dispatcher
   封送 → 仅 System/空模式重解析）与 `ApplyWindowTheme`（DWM 沉浸式暗色，属性 19/20）。
6. **窗口白名单应用**：页面不持 `IThemeService`；`MainView`（Host）与对话框窗口
   （`StarPie.Dialogs`）构造注入做白名单应用
   （[ADR-0009](../adr/0009-view-code-behind-whitelist.md)）。

## 扩展点

- 新增主题方案/令牌/跟随策略：M4 内部（`StarPie.Ui/Themes/*.xaml`、`ThemeEngine`、解析）+ S3 新文案键
  （见 [localization.md](localization.md)）；新增主题应用消息类型时经 S4 hub 登记（放行共享面，
  见 [messages.md](messages.md)）。

## 参见 ADR

[0009](../adr/0009-view-code-behind-whitelist.md)（窗口白名单应用）、
[0012](../adr/0012-resource-dictionary-architecture.md)/[0013](../adr/0013-localization-theme-overhaul.md)
（主题令牌与整项替换）、[0014](../adr/0014-wheel-palette-module-boundary-and-appearance-split.md)
（界面主题模块边界/正名）、[0015](../adr/0015-module-map-and-ownership.md)（12 模块地图：M4）。
