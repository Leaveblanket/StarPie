# 预览与运行时轮盘同源：扇区内容构建下沉 Host 内核

> Status: Active
>
> 本文收口 M2 轮盘与渲染模块（`docs/architecture/wheel.md`）内部的视图层重复：
> 外观页实时预览（`WheelPreviewRenderer`）与运行时轮盘（`RadialWindow.RenderSectors`）
> 长期是同一套"扇区内容构建"逻辑的两份实现。契约与边界背景见
> [ADR-0023](0023-module-contracts-hard-boundary-and-core-narrowing.md)；
> 配色模块边界先例见 [ADR-0014](0014-wheel-palette-module-boundary-and-appearance-split.md)。

## 动机

1. **两份实现已经漂移到逐字符相同**：图标五级回退链（custom: 前缀 → IsSvg → IconCatalog 键 →
   Launch exe 图标 → 内置向量）在 `RadialWindow.xaml.cs` 与 `WheelPreviewRenderer.cs` 各有一份，
   连硬编码的键盘 SVG 路径字面量都逐字符相同——双份维护的漂移风险不是假设，已是事实。
2. **预览的意义就是"所见即所得"**：预览渲染器与运行时轮盘各自实现内容构建，任何一侧的改动
   都可能让预览与实际轮盘不一致，直接背叛外观页存在的目的。
3. **重复的不止回退链**：按扇区数的排版缩放（`n == 12 ? ... : n == 4 ? ...` 模式）与
   键盘图标 SVG 字面量同样双份；风格名字符串（`ClassicRing` 等）散落 6+ 文件，
   轮盘半径默认值 `138`、内半径 `52` 在 4 处各自硬编码。

## Considered Options

- **抽共享内容构建层，落 `StarPie.Ui/Services/Wheel/`** → 否。图标目录 `IconCatalog` 本就在
  `StarPie.Host/Icons/`，回退决策链可以做成纯逻辑（输入 ActionItem + 配置，输出 SVG path /
  图标 key / 布局参数），放 Ui 会让 Host 内已有的目录知识反向依赖 WPF 亲和集。
- **接受双实现，靠测试对齐** → 否。逐字符相同的字面量证明测试对齐挡不住复制粘贴；
  下一个改图标回退的人仍会只改一侧。
- **只收敛图标回退链，排版缩放与 SVG 字面量各自保留** → 否。三者在两份实现里是同一个
  函数级的重复，半收会让"同源"目标落空。
- **扇区内容构建下沉 `StarPie.Host/Wheel/` 纯逻辑层，WPF 几何构造留在 Ui 消费** → 采纳。
  与配色解析（WheelPaletteParser/Catalog）同层，可 headless 测试；`WheelGeometry`
  （WPF Geometry 直构）继续留在 Ui，消费内核输出的纯数据。

## Decision

1. **扇区内容构建（图标五级回退、按扇区数排版缩放、内置 SVG 字面量）下沉
   `StarPie.Host/Wheel/`**，作为纯逻辑共享内核；`RadialWindow` 与 `WheelPreviewRenderer`
   同源消费，两处不再各持实现。
   **内核入参只允许窄字段与 `ActionItem`（扇区数据），禁止接收 `AppConfig`**——否则
   [ADR-0044](0044-wheel-config-projection.md) 收窄运行时配置面时会反过来逼迫内核改签名，
   此约束替代一条反向阻塞边。
2. **排版缩放做成纯数据表**（`sectorCount → (缩放, 偏移)`），不再散落条件表达式。
3. **风格名与几何默认值归一**：风格名字符串收敛为 Sdk 常量类（不引入枚举——配置文件里
   风格名是字符串，枚举会引入序列化兼容问题，收益不抵）；轮盘半径默认值 `138`/内半径 `52`
   归一处 `WheelGeometryDefaults`，与模型默认值同处。
4. **配套小修正随本收敛落地，不单独立篇**：
   - `ShellHost` 预热绕过 `IWheelFactory` 直接 `new WheelViewModel` 的跨界，改为经
     **`IWheelFactory.Warmup()`** 封口。**不另立 `IWheelWarmup` 契约**：`WheelFactory` 已持有
     预热所需的全部四项依赖（配置/主题/本地化/图标资产），扩展单方法即可；另立契约需要同一组
     依赖再走一次 DI 注册，只在 Sdk 导出面新增一型，换来「壳层看不见 `Create`」这点隔离收益，
     不抵面价。`Create` 的唯一消费方是 `GestureEngine`，多看见一个预热方法无副作用。
   - `MouseHook` 健康检查里的 `System.Windows.Application.Current` 引用移除（与该适配器
     "不携带 UI 框架类型"的自身声明矛盾），改经注入的调度接缝。
5. **渲染职责仍各自保留**：共享的是"内容是什么"（图标、缩放、字面量），不是"怎么画"
   （RadialWindow 的窗口动画/生命周期与 WheelPreviewRenderer 的 Canvas 预览绘制，
   仍由各自承担）。

## Consequences

- **运行时 mousemove 链路上的同步 `Dispatcher.Invoke` 不在本次处理**：`WheelFactory` 的
  同步封送意味着每次移动都阻塞钩子线程等 UI 线程往返（低级鼠标钩子有约 300ms 系统超时）。
  改异步涉及手势时序语义（高亮与取消的竞态），作为 known-issue 记入
  `docs/architecture/wheel.md`，单独立项，不随本收敛顺手改。
- `RadialWindow` 与 `WheelViewModel.Config` 的宽耦合由
  [ADR-0044](0044-wheel-config-projection.md) 承载，本文不处理。
- `IWheelAppearanceState.CurrentConfig`（整个 AppConfig 透传给预览渲染器）的漏口在共享内核
  落地后自然收窄：预览渲染器改吃内核输出的纯数据，不再直读全局配置。
