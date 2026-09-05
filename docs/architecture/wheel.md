# 模块：轮盘与渲染

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及轮盘 VM/窗口/渲染样式时读本篇。

## 职责

轮盘 VM 状态机（每手势瞬态）与纯视觉渲染分离。

## 组成文件

`ViewModels/Wheel/`（`IWheelViewModel`、`WheelViewModel`、`IWheelAppearanceState`）、`ViewModels/Pages/WheelAppearanceSettingsViewModel.cs`（轮盘外观设置子 VM，单例落位页面 VM 目录，ADR-0014 决策 6）、`Views/Wheel/RadialWindow.xaml(.cs)`、`Views/Renderers/`（`IRadialStyleRenderer`、`StyleRendererFactory`、`BaseStyleRenderer`、`ClassicRingRenderer`、`CleanSectorsRenderer`、`GlassmorphismRenderer`、`CatPawRenderer`、`WheelPreviewRenderer`）；轮盘配色解析属本模块：`Models/WheelPalette.cs`（色值组）、`Models/WheelPaletteCatalog.cs`（唯一 hex 目录，含各风格默认观感/系统预设/紧急回落）、`Models/WheelPaletteParser.cs`（方案名→色值组解析）。

> R8 语义归属（[modules.md](modules.md) §4）：`WheelPalette*`/`CustomColorPreset`（自定义配色预设）语义归 M2；
> 动作侧 `ActionItem`/`WheelProfile` 的语义归属见 [gestures.md](gestures.md)；物理文件均居 `Models/` 共享内核。

> `IWheelAppearanceState` 是轮盘模块的预览只读状态接口（ADR-0014 决策 8）：`WheelPreviewRenderer`
> 只依赖它读取外观状态。#56 起实现方为轮盘外观设置子 VM `WheelAppearanceSettingsViewModel`
>（经外观聚合 VM 的 `WheelAppearance` 暴露给页面），外观聚合 VM 不再实现该接口。#69（B2）起该接口的
> 预览 Profile 上下文成员转发自 M1 只读 `IProfilePreviewSource`（见 [gestures.md](gestures.md)），
> 轮盘侧代码不引用具体配置方案列表 VM 类型。

## 外观配置面（设置子 VM，#56）

- **范围**：外观页去掉界面主题卡后的全部轮盘外观设置——皮肤（UiStyle）、轮盘配色方案与自定义
  预设（含预设 CRUD 与下拉选项重建）、高亮光晕、几何/尺寸、排版与文字显示、中心核图标（透传 +
  选取编排）与一键重置；界面主题（AppTheme）由 `InterfaceThemeSettingsViewModel` 独占（见
  [interface-theme.md](interface-theme.md)）。
- **承载**：`WheelAppearanceSettingsViewModel`（`ViewModels/Pages`，DI 单例）实现
  `IWheelAppearanceState`；构造注入 M1 只读 `IProfilePreviewSource`（预览 Profile 来源，静态已知
  依赖走接口；#69 起不再引用具体方案列表 VM 类型）、`IConfigService`/`IDialogService`/`IMessenger`/
  `ILocalizationService`；全部状态写穿运行态配置（立即生效），落盘经防抖/立即消息上报；配色下拉
  选项（`ThemeOptions`）随语言切换重建并补发选中通知，`Dispose` 成对退订（ADR-0010 第 3 条）。
- **页面接线**：外观聚合 VM `AppearanceSettingsViewModel` 收薄为页壳，只暴露
  `InterfaceTheme`/`WheelAppearance` 两个子 VM（页面整体 DataContext 仍为聚合 VM；各设置卡
  DataContext 指向对应子 VM，不新增导航页）。预览属性变更（含 ShowCoreIcon，#56 起补发）经
  `AppearancePreviewInvalidatedMessage` 触发页面重绘；配置导入后子 VM 自订阅重挂、聚合壳广播
  `PageConfigReloadedMessage` 收尾 View 效果（ShowCoreIconCheckBox 事件处理器已删除）。

## 关键流程

1. `WheelFactory`（见 [gestures.md](gestures.md)）按手势创建 `WheelViewModel(center, profile, config.Current)`：从配置/Profile 快照扇区、几何尺寸、主题、样式；`IWheelViewModel` 暴露 `Show`/`HighlightSector`/`SetOuterEscapeState`/`Close`（经 Dispatcher 包装）。
2. `RadialWindow` 观察 `WheelViewModel`（`PropertyChanged` 仅驱动纯视觉重绘与窗口生命周期：`IsShown→Show`、`IsClosed→Close`；`Closed` 成对退订，防每手势窗口实例被 VM 事件滞留）。
3. 样式渲染：`RadialWindow`/`WheelPreviewRenderer` 经 `StyleRendererFactory.CreateRenderer(UiStyle)` 获取 `IRadialStyleRenderer`（`ClassicRing` 默认；`CatPaw`/`Glassmorphism`/`CleanSectors`），`Initialize(theme, config, windowsInDarkMode)` 后绘制装饰、高亮扇区与外甩图标。画刷数据流唯一路径为 `config → WheelPaletteParser（+ WheelPaletteCatalog）→ 渲染器 Initialize → Brush`：System↔OS 深浅、固定方案、自定义预设（id/name/CustomPreset_ 前缀）与 Custom 微调、坏值/空值回落都在解析层完成，渲染器只消费 `WheelPalette` 色值组并构造画刷。
4. `IRadialStyleRenderer` 是纯视觉契约：只消费主题/配置与绘制参数；不订阅事件、不读写 VM、不反向依赖 Composition/服务；实例随窗口/预览随用随建。
5. 外观页 Canvas 预览走 `WheelPreviewRenderer`（与实轮盘同一渲染契约），保证所见即所得；渲染器输入
   为 `IWheelAppearanceState`（皮肤/配色、几何/排版、核图标、运行态配置与预览 Profile 上下文），
   不依赖具体聚合 VM 类型；预览 Profile 上下文由外观设置子 VM 经 M1 的 `IProfilePreviewSource`
   转发取值（#69），选中/首项回落语义由该来源实现方维护。

## 扩展点

新增样式 = 新 `XxxRenderer : BaseStyleRenderer` + 在 `WheelPaletteCatalog` 登记风格键/默认深浅观感/标准浅色回落行为 + `StyleRendererFactory` 分支 + 配置/UI 选项 + i18n（清单见 [extending.md](extending.md) 原型 E）。

## 参见 ADR

[0009](../adr/0009-view-code-behind-whitelist.md)（渲染器白名单）、[0014](../adr/0014-wheel-palette-module-boundary-and-appearance-split.md)（轮盘配色模块边界与解析收拢）。
