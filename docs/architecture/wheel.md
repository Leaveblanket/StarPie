# 模块：轮盘与渲染

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及轮盘 VM/窗口/渲染样式时读本篇。

## 职责

轮盘 VM 状态机（每手势瞬态）与纯视觉渲染分离。

## 组成文件

`ViewModels/Wheel/`（`IWheelViewModel`、`WheelViewModel`、`IWheelAppearanceState`）、`Views/Wheel/RadialWindow.xaml(.cs)`、`Views/Renderers/`（`IRadialStyleRenderer`、`StyleRendererFactory`、`BaseStyleRenderer`、`ClassicRingRenderer`、`CleanSectorsRenderer`、`GlassmorphismRenderer`、`CatPawRenderer`、`WheelPreviewRenderer`）。

> `IWheelAppearanceState` 是轮盘模块的预览只读状态接口（ADR-0014 决策 8）：`WheelPreviewRenderer`
> 只依赖它读取外观状态。外观聚合页 VM 当前临时实现该接口；#56 抽取轮盘外观设置子 VM 后由后者
> 承接实现。

## 关键流程

1. `WheelFactory`（见 [gestures.md](gestures.md)）按手势创建 `WheelViewModel(center, profile, config.Current)`：从配置/Profile 快照扇区、几何尺寸、主题、样式；`IWheelViewModel` 暴露 `Show`/`HighlightSector`/`SetOuterEscapeState`/`Close`（经 Dispatcher 包装）。
2. `RadialWindow` 观察 `WheelViewModel`（`PropertyChanged` 仅驱动纯视觉重绘与窗口生命周期：`IsShown→Show`、`IsClosed→Close`；`Closed` 成对退订，防每手势窗口实例被 VM 事件滞留）。
3. 样式渲染：`RadialWindow`/`WheelPreviewRenderer` 经 `StyleRendererFactory.CreateRenderer(UiStyle)` 获取 `IRadialStyleRenderer`（`ClassicRing` 默认；`CatPaw`/`Glassmorphism`/`CleanSectors`），`Initialize(theme, config, windowsInDarkMode)` 后绘制装饰、高亮扇区与外甩图标。
4. `IRadialStyleRenderer` 是纯视觉契约：只消费主题/配置与绘制参数；不订阅事件、不读写 VM、不反向依赖 Composition/服务；实例随窗口/预览随用随建。
5. 外观页 Canvas 预览走 `WheelPreviewRenderer`（与实轮盘同一渲染契约），保证所见即所得；渲染器输入
   为 `IWheelAppearanceState`（皮肤/配色、几何/排版、核图标、运行态配置与预览 Profile 上下文），
   不依赖具体聚合 VM 类型。

## 扩展点

新增样式 = 新 `XxxRenderer : BaseStyleRenderer` + `StyleRendererFactory` 分支 + 配置/UI 选项 + i18n（清单见 [extending.md](extending.md) 原型 E）。

## 参见 ADR

[0009](../adr/0009-view-code-behind-whitelist.md)（渲染器白名单）。
