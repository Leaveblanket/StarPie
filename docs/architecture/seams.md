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

程序集依赖基线（as-built，15 程序集）见 [assemblies.md](assemblies.md) §3，本文不重复。

## 2. 规范内缝（approved，改动受 ADR/收口测试守护）

| 缝 | 载体（契约/实现） | 裁决/守护 |
|---|---|---|
| 契约缝·图标资产 | 契约四件（`IIconAssetService`/`IconCatalog`/`CustomIconItem`/`VectorIconItem`）驻 Icons.Contracts；`IconAssetService` + `IconsModuleRegistrar` 驻 Icons runtime（实现只被 Host/测试引用） | ADR-0023；IconCatalogTests/IconsAssemblyPlacementTests |
| 契约缝·.lnk 解析 | `IShortcutTargetResolver` 驻 Programs.Contracts ← M3 `ShortcutResolver`（ADR-0023 自 Core 迁出；Icons runtime 经契约边消费，命名空间 `StarPie.Services.Icons` 不变） | ADR-0023；ProgramsAssemblyPlacementTests |
| 契约缝·程序扫描 | `IProgramScanner`/`ProgramEntry`/`ProgramCatalog` 驻 Programs.Contracts ← M3 `ProgramScanner`（实例；Dialogs/Host 经契约边消费，命名空间 `StarPie.Services.Programs` 不变） | ADR-0023；Programs/Dialogs AssemblyPlacementTests |
| 契约缝·主题 | `IThemeService` 驻 Theme.Contracts（ADR-0023 自 M4 runtime 迁出）← 实现 `ThemeService` 驻 M4；消费方 Host/M2/Dialogs 经契约边（M2→M4、Dialogs→M4 runtime 允许边清零） | ADR-0023；Theme/Wheel/Dialogs Placement |
| 契约缝·轮盘工厂 | `IWheelFactory`/`IWheelViewModel` 驻 Wheel.Contracts（ADR-0023 自 M2 runtime 迁出）← 实现 `WheelFactory`/`WheelViewModel` 驻 M2；消费方 M1 经契约边（M1→M2 runtime 允许边清零） | D5 + ADR-0023；Wheel/Gestures Placement |
| 契约缝·预览 Profile | `IProfilePreviewSource` 驻 Gestures.Contracts（ADR-0023 自 Core 迁出，生产方语义 + 破 Wheel↔Gestures 环），别名 = M1 `ProfileListViewModel`，消费 M2 经契约边 | D5 + ADR-0023；Wheel/Gestures Placement |
| 契约缝·轮盘外观只读状态 | `IWheelAppearanceState` 驻 Wheel.Contracts（签名暴露件，ADR-0023），实现 = M2 `WheelAppearanceSettingsViewModel`，消费方 = M2 预览渲染器 + Host 外观页 | ADR-0014 决策 8 + ADR-0023；WheelAssemblyPlacementTests |
| 契约缝·对话框 | `IDialogService`/结果 record 驻 Dialogs.Contracts（纯 C#，ADR-0023 自 Core 迁出）← 实现 `DialogService` 驻 Dialogs；M1/M2/M5/Host 经契约边调用 | ADR-0023；DialogsAssemblyPlacementTests |
| 注册缝 | 7 个 `*ModuleRegistrar`（Core 除外：Programs/Theme/Shell/Wheel/Gestures/Dialogs + Icons）下放 DI/导航注册（注册的契约类型驻各自 Contracts 程序集）；组合根唯一解析 | ADR-0023；各 PlacementTests |
| 回填缝·dev 标志 | `AppDataPaths.IsDevInstance` 组合根装配前回填（消费 M1/M5/S2） | MouseHook/Autostart 测试 |
| 回填缝·宿主回调 | `AppHostDelegates` 驻 Core（可空 Action 单例），AppHost 构造后回填 | ShellAssemblyPlacementTests |
| 回填缝·对话框 Owner | `DialogService.SetOwner(MainView)` Host 建窗后回填（public 装配面） | ADR-0004；e2e |
| 导航缝 | `NavigationCatalog` + `NavigationSlots`（槽位 0–3）+ 模块注册器 `RegisterNavigation` + 页面模板字典 | NavigationCatalogTests（补注：导航运行时/执行入口 `INavigationExecutor` 随运行时整体归 Host，为宿主内部件而非跨程序集缝，本表不登记） |
| XAML 资源缝 | App.xaml 资源单点合并/实例化：主题与模板字典（+HotkeyRecorderBox 样式字典）经跨集 pack URI、ModernControls.xaml 宿主本地合并、转换器 App 级实例（ModernControls 与通用转换器迁 Host、热键样式字典随控件下沉 Gestures） | ADR-0012 |
| 消息缝 | S4 hub（`Messages.cs`/`Notices.cs`），跨模块广播；新消息 = 放行共享面 | messages.md |
| 系统调用委托缝（A 类） | 服务构造注入 `Func<bool>`/`Action` 系统探针（ThemeService/ActionExecutorService/VM 委托），生产默认值内建 | layering.md「系统调用接缝模式」；单测替身 |
| 收口测试缝 | 9 个 `*AssemblyPlacementTests`（含 Navigation、Icons；Dialogs/Programs/Theme/Wheel/Gestures/SharedUi 断言覆盖契约归属 Contracts 与 runtime 互引为零）+ `RuntimeNoCrossReferenceTests`（runtime 互不引用/引用自身契约/契约不引用 runtime 断言族）+ NavigationCatalog 收口测试 | 测试自身守护 |

## 3. 需关注缝（有意接受，但对模块化施加压力；改动前先读裁决）

| 缝 | 位置 | 压力 | 裁决/触发条件 |
|---|---|---|---|
| Host 装配面 | Composition/CreateAppHost 直取模块具体类型（MouseHook/ThemeService/两子 VM 等）；AppHost 编排托盘菜单/AppThemePaletteManager/MouseHook 暂停态；Host 聚合页拼装 M2/M4 子 VM | Host 对"模块暴露哪些 public 装配件"有编译期认知；模块不能脱离 Host 决定宿主装配 | ADR-0016 决策 13（组合根集中）；留 Host；不引入子容器/Prism |
| 导航槽位容量 | `NavigationSlot` 固定 0–3 + Validate + e2e `NavPage0..3` | 新增第 5 页需改 Core 枚举 + 收口测试（可能波及 e2e），非"纯模块内部" | 产品页面数封顶 4，改动属放行共享面；navigation.md 登记 |
| 共享配置对象 | `IConfigService.Current` 单例可变 `AppConfig`；模块 VM 构造抓引用，导入后消息自挂 | 任何模块可读写任何配置区；模块间经"同一对象 + 广播"隐式协作 | 放行共享面（modules.md §2.3）；config.json 向后兼容 Hard Constraint |
| Models 物理残留（R8） | `WheelProfile`/`ActionItem` 语义归 M1、物理 Core；`CustomColorPreset` 语义归 M2、物理 Core（AppConfig 引用） | 业务领域形状渗入共享内核 | R8 已登记；迁移触发条件 = 配置模型与模块语义解耦时再议 |

## 4. 残留缝（已裁决清理方向，未排期）

当前无残留缝。

## 维护义务

1. 新增跨程序集接触点时：先判定属哪一档，登记本表并链裁决（ADR/叶子行号）；
   不满足 ADR 三条件的小改动只改本表与对应叶子。
2. 缝归零（重构消除）时：从本表移除并在 §4 记一行，指向裁决。
3. 程序集/依赖方向变化先改 [assemblies.md](assemblies.md) §3，再同步本表基线。
