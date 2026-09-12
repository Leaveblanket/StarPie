# 模块间接合缝编目（Seams）

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；做跨模块/程序集改动前，
> 或想确认"某条缝是否规范内/需关注/残留"时读本篇。
>
> 术语：**接合缝（Seam）** = 两个程序集/模块之间一切需要同步、协议或装配的接触点
> （接口契约、DI 注册、回填、导航目录、XAML 资源合并、消息、共享数据对象）。
> **规范内缝** = ADR/叶子登记且由收口测试守护的缝；**需关注缝** = 有意接受但对模块化
> 施加压力的缝（改动前先读裁决）；**残留缝** = 已裁决要清理、待排期的缝。
>
> 本文只收**当前活缝**；历史已归零的缝（D5 WheelFactory 收编、扫描委托、ThemeChanged
> 死事件、IconAssets 静态回填等）在各 ADR/叶子有记录，不在此重复。

## 1. 程序集依赖基线

程序集依赖基线（as-built）见 [assemblies.md](assemblies.md) §2/§3，本文不重复。

## 2. 规范内缝（approved，改动受 ADR/收口测试守护）

| 缝 | 载体（契约/实现） | 裁决/守护 |
|---|---|---|
| 契约缝·图标资产 | `IIconAssetService` 驻 `StarPie.Sdk.Wpf/Services/Icons/`（实现 `IconAssetService` 驻 `StarPie.Ui/Services/Icons/`）；条目与 .lnk SPI 驻 `StarPie.Sdk/Services/Icons/`；静态纯目录 `IconCatalog` 与自定义图标目录 `CustomIconStore` 驻 `StarPie.Host/Icons/` | ADR-0023；IconCatalogTests |
| 契约缝·.lnk 解析 | `IShortcutTargetResolver` 驻 `StarPie.Sdk/Services/Icons/`（命名空间 `StarPie.Services.Icons` 不变）← 实现 `ShortcutResolver` 驻 `StarPie.Host/Programs/`（图标服务与程序扫描经契约边消费） | ADR-0023 |
| 契约缝·程序扫描 | `IProgramScanner`/`ProgramEntry`/`ProgramCatalog` 驻 `StarPie.Sdk/Services/Programs/`（命名空间 `StarPie.Services.Programs` 不变）← 实现 `ProgramScanner` 驻 `StarPie.Host/Programs/`（实例；候选为纯数据，图标由 UI 消费方装配） | ADR-0023 |
| 契约缝·主题 | `IThemeService` 驻 `StarPie.Sdk.Wpf/Services/Shell/`（ADR-0023）← 实现 `ThemeService` 驻 `StarPie.Ui/Services/Shell/`（状态/解析在内核 `ThemeEngine`，换肤经端口 `IThemeApplier` 回抛 Ui）；消费方 Host/M2/Dialogs 经契约边 | ADR-0023 |
| 契约缝·轮盘工厂 | `IWheelFactory`/`IWheelViewModel` 驻 `StarPie.Sdk`（ADR-0023 自 M2 runtime 迁出，P1.3/#112 随 SDK 收口）← 实现 `WheelFactory`/`WheelViewModel` 驻 M2；消费方 M1 经契约边（M1→M2 runtime 允许边清零） | D5 + ADR-0023 |
| 契约缝·预览 Profile | `IProfilePreviewSource` 驻 `StarPie.Sdk`（ADR-0023 自 Core 迁出，P1.3/#112 随 SDK 收口，生产方语义 + 破 Wheel↔Gestures 环），别名 = M1 `ProfileListViewModel`，消费 M2 经契约边 | D5 + ADR-0023 |
| 契约缝·轮盘外观只读状态 | `IWheelAppearanceState` 驻 `StarPie.Sdk`（签名暴露件，ADR-0023，P1.3/#112 随 SDK 收口），实现 = M2 `WheelAppearanceSettingsViewModel`，消费方 = M2 预览渲染器 + Host 外观页 | ADR-0014 决策 8 + ADR-0023 |
| 契约缝·对话框 | `IDialogService`/结果 record 驻 `StarPie.Sdk`（纯 C#，ADR-0023 自 Core 迁出，P1.3/#112 随 SDK 收口）← 实现 `DialogService` 驻 Dialogs；M1/M2/M5/Host 经契约边调用 | ADR-0023 |
| 注册缝 | 7 个 `*ModuleRegistrar`（宿主内核与契约不在其列：Programs/Theme/Shell/Wheel/Gestures/Dialogs + Icons）下放 DI/导航注册（注册的契约类型驻 `StarPie.Sdk`/`StarPie.Sdk.Wpf`，P1.3/#112 + P1.4/#113 收口）；组合根唯一解析 | ADR-0023 |
| 回填缝·dev 标志 | `AppDataPaths.IsDevInstance` 组合根装配前回填（宿主内核 `StarPie.Host/Kernel/Configuration/`；消费 M1/M5/S2） | MouseHook/Autostart 测试 |
| 内核消费缝 | 旧集 runtime 与 Ui 经 `StarPie.Host/Kernel/{Configuration,Localization}` 消费内核件（内核定义、消费方单向；P1.5/#114 起归并期过渡边，模块拆入 Ui 后收敛） | HostBoundaryTests |
| 回填缝·宿主回调 | `AppHostDelegates` 驻 `StarPie.Sdk`（可空 Action 单例，P1.3/#112 自 Core 收口），AppHost 构造后回填 | 无专用机械断言（#111 收窄，P1.11 配平） |
| 回填缝·对话框 Owner | `DialogService.SetOwner(MainView)` Host 建窗后回填（public 装配面） | ADR-0004；e2e |
| 导航缝 | `NavigationCatalog` + `NavigationSlots`（槽位 0–3，P1.3/#112 自 Core 收口入 `StarPie.Sdk`）+ 模块注册器 `RegisterNavigation` + 页面模板字典 | NavigationCatalogTests（补注：导航运行时/执行入口 `INavigationExecutor` 随运行时整体归 Host，为宿主内部件而非跨程序集缝，本表不登记） |
| XAML 资源缝 | App.xaml 资源单点合并/实例化：模板字典（+HotkeyRecorderBox 样式字典）经跨集 pack URI、主题字典（Ui 本地绝对 pack URI）与 ModernControls.xaml 本地合并、转换器 App 级实例 | ADR-0012 |
| 消息缝 | S4 hub（`Messages.cs`/`Notices.cs`，P1.3/#112 自 Core 收口入 `StarPie.Sdk`），跨模块广播；新消息 = 放行共享面 | messages.md |
| 系统调用委托缝（A 类） | 服务构造注入 `Func<bool>`/`Action` 系统探针（`ThemeEngine`/`ActionExecutorService`/VM 委托），生产默认值内建 | layering.md「系统调用接缝模式」；单测替身 |
| 收口测试缝 | 四集基线（P1.2/#111）+ SDK 收口（P1.3/#112 + P1.4/#113）+ Host 内核归并（P1.5/#114）：`FourSetBoundaryTests`（解决方案登记 / csproj 属性 / 跨集依赖方向 / CI 与 e2e 路径）+ `RuntimeNoCrossReferenceTests`（程序集引用面 / 平台投影 / 入口与 XAML）+ `SdkBoundaryTests`/`SdkWpfBoundaryTests`/`HostBoundaryTests`（导出面白名单 / ABI 与默认 ALC 政策 / 内核零 WPF 与 Core 设计期壳）——SDK 零第三方包零 WPF、Host 零 WPF、Sdk.Wpf 契约面、Ui 唯一入口与 XAML、跨集只经 SDK；NavigationCatalog 收口测试 | 测试自身守护 |

> **P1.2/#111 收窄**：上表各行原先引用的 9 个 `*AssemblyPlacementTests` 与旧
> `RuntimeNoCrossReferenceTests` 模块内断言族（runtime 互不引用 / 引用自身契约 / 契约不引用
> runtime）已随四集骨架退役；P1.3–P1.10 归并期的机械守护为四集基线（见"收口测试缝"行），
> 目标态逐缝配平在 P1.11（#120）随架构文档回填完成。

## 3. 需关注缝（有意接受，但对模块化施加压力；改动前先读裁决）

| 缝 | 位置 | 压力 | 裁决/触发条件 |
|---|---|---|---|
| Host 装配面 | Composition/CreateAppHost 直取 Host 侧可见具体类型（MouseHook/主题服务/两子 VM 等）；AppHost 把 Ui 侧调色板适配器接到主题服务（内核端口 `IThemeApplier`）/编排托盘菜单/MouseHook 暂停态；Host 聚合页拼装 M2/M4 子 VM | Host 对"哪些装配件可见"有编译期认知；模块不能脱离 Host 决定宿主装配 | ADR-0016 决策 13（组合根集中）；留 Host；不引入子容器/Prism |
| 导航槽位容量 | `NavigationSlot` 固定 0–3 + Validate + e2e `NavPage0..3` | 新增第 5 页需改 Core 枚举 + 收口测试（可能波及 e2e），非"纯模块内部" | 产品页面数封顶 4，改动属放行共享面；navigation.md 登记 |
| 共享配置对象 | `IConfigService.Current` 单例可变 `AppConfig`；模块 VM 构造抓引用，导入后消息自挂 | 任何模块可读写任何配置区；模块间经"同一对象 + 广播"隐式协作 | 放行共享面（modules.md §2.3）；config.json 向后兼容 Hard Constraint |
| Models 物理残留（R8） | `WheelProfile`/`ActionItem` 语义归 M1、物理 `StarPie.Sdk/Models/`；`CustomColorPreset` 语义归 M2、物理 `StarPie.Sdk/Models/`（AppConfig 引用） | 业务领域形状渗入 SDK 模型面 | R8 已登记；迁移触发条件 = 配置模型与模块语义解耦时再议 |

## 4. 残留缝（已裁决清理方向，未排期）

当前无残留缝。

## 维护义务

1. 新增跨程序集接触点时：先判定属哪一档，登记本表并链裁决（ADR/叶子行号）；
   不满足 ADR 三条件的小改动只改本表与对应叶子。
2. 缝归零（重构消除）时：从本表移除并在 §4 记一行，指向裁决。
3. 程序集/依赖方向变化先改 [assemblies.md](assemblies.md) §3，再同步本表基线。
