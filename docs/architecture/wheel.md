# 模块：轮盘与渲染

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及轮盘 VM/窗口/渲染样式时读本篇。

## 职责

轮盘 VM 状态机（每手势瞬态）与纯视觉渲染分离。

## 组成文件

M2 物理落位（独立模块程序集 `StarPie.Wheel/`；出口契约随实现方独立成集
`StarPie.Wheel.Contracts/`，ADR-0023）：

- `StarPie.Wheel.Contracts/`（M2 出口契约集，ADR-0023；命名空间不变；签名依赖共享内核
  Models 数据，仅引用 Core，不引用业务 runtime）：`Services/Wheel/IWheelFactory.cs`（轮盘工厂
  契约）、`ViewModels/Wheel/IWheelViewModel.cs`、`ViewModels/Wheel/IWheelAppearanceState.cs`
  （签名暴露件）。
- `StarPie.Wheel/Services/Wheel/`：`WheelGeometry.cs`（M2 轮盘视觉几何出口：扇区切削/核图标
  几何，R6 三分）、`WheelFactory.cs`（`IWheelFactory` 实现，D5；命名空间
  `StarPie.Services.Wheel` 与物理目录一致，见 [gestures.md](gestures.md)/[modules.md](modules.md) §5 D5）。
- `StarPie.Wheel/ViewModels/Wheel/WheelViewModel.cs`（`IWheelViewModel` 实现）、
  `StarPie.Wheel/ViewModels/Pages/WheelAppearanceSettingsViewModel.cs`（轮盘外观设置子 VM，
  实现 `IWheelAppearanceState`，单例落位页面 VM 目录，ADR-0014 决策 6）。
- `StarPie.Wheel/Views/Wheel/RadialWindow.xaml(.cs)`、`StarPie.Wheel/Views/Renderers/`
  （`IRadialStyleRenderer`、`StyleRendererFactory`、`BaseStyleRenderer`、`ClassicRingRenderer`、
  `CleanSectorsRenderer`、`GlassmorphismRenderer`、`CatPawRenderer`、`WheelPreviewRenderer`）。
- 轮盘配色解析（本集 `StarPie.Wheel/Models/`，R8 语义+物理归 M2；`CustomColorPreset` 因
  `AppConfig` 引用仍居 Core `Models/`，语义归 M2，见 [modules.md](modules.md) §4 R8）：
  `WheelPalette.cs`（色值组）、`WheelPaletteCatalog.cs`（唯一 hex 目录，含各风格默认观感/
  系统预设/紧急回落）、`WheelPaletteParser.cs`（方案名→色值组解析）。
- `StarPie.Wheel/Views/Converters/CoreIconGeometryConverter.cs`/`CoreIconNameConverter.cs`
  （核图标预览转换器，随 M2：Appearance 聚合页的核圆预览配套，Geometry 转换器直连本模块
  几何出口，Host App.xaml 经 `assembly=StarPie.Wheel` 实例化）。
- `StarPie.Wheel/Modules/WheelModuleRegistrar.cs`（M2 模块注册器：`RegisterServices` 下放
  轮盘工厂 `IWheelFactory→WheelFactory` 与轮盘外观设置子 VM 的 DI 注册；M2 无导航页，
  不提供 `RegisterNavigation`）。

> 图标/几何三分收口（R6/ADR-0015）：轮盘侧 RadialWindow/
> WheelPreviewRenderer/CoreIconGeometryConverter 直连本模块几何出口 `WheelGeometry`
> （`CreateAdvancedSectorGeometry`/`GetCoreIconGeometry`）；动作图标渲染（含核图标 Custom 分支
> 按 SVG 键回退取值）消费 S1 共享「图标资产」（双形：静态纯目录
> `IconCatalog` 取矢量 SVG；实例服务 `IIconAssetService` 取自定义图标存储/位图源/文件图标——
> RadialWindow 经 WheelFactory 注入、WheelPreviewRenderer 经外观页预览桥装配，见
> [layout.md](layout.md)/[layering.md](layering.md)）。
> R8 语义与物理归属（[modules.md](modules.md) §4）：`WheelPalette*` 语义与物理均归 M2（驻
> `StarPie.Wheel/Models/`）；`CustomColorPreset`（自定义配色预设）语义归 M2、物理仍居 Core
> `Models/`（`AppConfig.CustomColorPresets` 配置 POCO 引用，不得反向依赖模块）；动作侧
> `ActionItem`/`WheelProfile` 的语义归属见 [gestures.md](gestures.md)。

> `IWheelAppearanceState` 是轮盘模块的预览只读状态接口（ADR-0014 决策 8；驻
> `StarPie.Wheel.Contracts`，ADR-0023；外观页 code-behind 经 Wheel.Contracts 显式引用
> 消费）：`WheelPreviewRenderer` 只依赖它读取外观状态。实现方为轮盘外观设置子 VM
> `WheelAppearanceSettingsViewModel`（经外观聚合 VM 的 `WheelAppearance` 暴露给页面），外观
> 聚合 VM 不实现该接口。接口的预览 Profile 上下文成员转发自 M1 只读 `IProfilePreviewSource`
> （见 [gestures.md](gestures.md)），轮盘侧代码不引用具体配置方案列表 VM 类型。

## 外观配置面（设置子 VM）

- **范围**：外观页去掉界面主题卡后的全部轮盘外观设置——主题风格（WheelStyle）、轮盘配色方案与自定义
  预设（含预设 CRUD 与下拉选项重建）、高亮光晕、几何/尺寸、排版与文字显示、中心核图标（透传 +
  选取编排）与一键重置；界面主题（AppTheme）由 `InterfaceThemeSettingsViewModel` 独占（见
  [interface-theme.md](interface-theme.md)）。
- **承载**：`WheelAppearanceSettingsViewModel`（`StarPie.Wheel/ViewModels/Pages/`，DI 单例）
  实现 `IWheelAppearanceState`；构造注入 M1 只读 `IProfilePreviewSource`（预览 Profile 来源，
  静态已知依赖走接口，不引用具体方案列表 VM 类型）、`IConfigService`/`IDialogService`/
  `IMessenger`/`ILocalizationService`；DI 注册由 `WheelModuleRegistrar.RegisterServices` 下放
  模块（`IProfilePreviewSource` 随实现方 M1 驻 `StarPie.Gestures.Contracts`（ADR-0023，
  D5——实现方 `ProfileListViewModel` 别名由 GesturesModuleRegistrar 下放），消费方本子 VM
  只依赖契约程序集）；全部状态写穿运行态配置（立即生效），落盘经防抖/立即消息上报；配色下拉
  选项（`PaletteOptions`）随语言切换重建并补发选中通知，`Dispose` 成对退订。
- **页面接线**：外观聚合 VM `AppearanceSettingsViewModel` 收薄为页壳，只暴露
  `InterfaceTheme`/`WheelAppearance` 两个子 VM（页面整体 DataContext 仍为聚合 VM；各设置卡
  DataContext 指向对应子 VM，不新增导航页）。预览属性变更（含 `ShowCoreIcon`）经
  `AppearancePreviewInvalidatedMessage` 触发页面重绘；配置导入后子 VM 自订阅重挂、聚合壳广播
  `PageConfigReloadedMessage` 收尾 View 效果（ShowCoreIconCheckBox 事件处理器已删除）。

## 关键流程

1. `WheelFactory`（驻 `StarPie.Wheel/Services/Wheel/`，DI 注册经 WheelModuleRegistrar
   下放；见 [gestures.md](gestures.md) 关键流程 5）按手势创建 `WheelViewModel(center, profile, config.Current)`：
   从配置/Profile 快照扇区、几何尺寸、主题、样式；`IWheelViewModel` 暴露
   `Show`/`HighlightSector`/`SetOuterEscapeState`/`Close`（经 Dispatcher 包装）。
2. `RadialWindow` 观察 `WheelViewModel`（`PropertyChanged` 仅驱动纯视觉重绘与窗口生命周期：`IsShown→Show`、`IsClosed→Close`；`Closed` 成对退订，防每手势窗口实例被 VM 事件滞留）。
3. 样式渲染：`RadialWindow`/`WheelPreviewRenderer` 经 `StyleRendererFactory.CreateRenderer(WheelStyle)` 获取 `IRadialStyleRenderer`（`ClassicRing` 默认；`CatPaw`/`Glassmorphism`/`CleanSectors`），`Initialize(theme, config, windowsInDarkMode)` 后绘制装饰、高亮扇区与外甩图标。画刷数据流唯一路径为 `config → WheelPaletteParser（+ WheelPaletteCatalog）→ 渲染器 Initialize → Brush`：System↔OS 深浅、固定方案、自定义预设（id/name/CustomPreset_ 前缀）与 Custom 微调、坏值/空值回落都在解析层完成，渲染器只消费 `WheelPalette` 色值组并构造画刷。
4. `IRadialStyleRenderer` 是纯视觉契约：只消费主题/配置与绘制参数；不订阅事件、不读写 VM、不反向依赖 Composition/服务；实例随窗口/预览随用随建。
5. 外观页 Canvas 预览走 `WheelPreviewRenderer`（与实轮盘同一渲染契约），保证所见即所得；渲染器输入
   为 `IWheelAppearanceState`（主题风格与配色、几何/排版、核图标、运行态配置与预览 Profile 上下文），
   不依赖具体聚合 VM 类型；预览 Profile 上下文由外观设置子 VM 经 M1 的 `IProfilePreviewSource`
   转发取值（契约随实现方 M1 驻 `StarPie.Gestures.Contracts`，ADR-0023），选中/首项回落
   语义由该来源实现方维护。深浅色探测不以 Host `MainView` 作参数（模块不反向依赖宿主）：
   `WheelPreviewRenderer` 的 `Render` 收 `bool windowsInDarkMode`，由外观页（Host）经壳层
   `MainView.IsWindowsInDarkTheme()` 取值传入。渲染器经**已批准预览桥**取得 `IIconAssetService`
   ：外观聚合 VM（`AppearanceSettingsViewModel`，容器单例）暴露该服务，页面在
   `Loaded` 事件处理器（原基类 virtual 钩子已改为自订阅；方法名 `OnPageLoaded`
   保留）装配 `new WheelPreviewRenderer(iconAssetService)`（layering Views 例外登记）。

## 扩展点

新增样式 = 新 `XxxRenderer : BaseStyleRenderer` + 在 `WheelPaletteCatalog`（驻
`StarPie.Wheel/Models/`）登记风格键/默认深浅观感/标准浅色回落行为 +
`StyleRendererFactory` 分支 + 配置/UI 选项 + i18n（清单见 [extending.md](extending.md) 原型 E；
只动 M2 内部 + S3 文案，不碰 Core/Host）。

## 参见 ADR

[0009](../adr/0009-view-code-behind-whitelist.md)（渲染器白名单）、[0014](../adr/0014-wheel-palette-module-boundary-and-appearance-split.md)（轮盘配色模块边界与解析收拢）、
[0016](../adr/0016-assembly-split-target-and-roadmap.md)（程序集化目标态：M2 StarPie.Wheel、D5 决策 11）。
