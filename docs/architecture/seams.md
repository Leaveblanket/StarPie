# 模块间接合缝编目（Seams）

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；做跨模块/程序集改动前，
> 或想确认"某条缝是否规范内/需关注/残留"时读本篇。
>
> 术语：**接合缝（Seam）** = 两个程序集/模块之间一切需要同步、协议或装配的接触点
> （接口契约、DI 注册、回填、导航目录、XAML 资源合并、消息、共享数据对象）。
> **规范内缝** = ADR/叶子登记且由收口测试守护的缝；**需关注缝** = 有意接受但对模块化
> 施加压力的缝（改动前先读裁决）；**残留缝** = 已裁决要清理、待排期的缝。
>
> 本文只收**当前活缝**；历史已归零的缝（D5 WheelFactory 收编、S21 扫描委托、ThemeChanged
> 死事件、IconAssets 静态回填等）在各 ADR/叶子有记录，不在此重复。

## 1. 程序集依赖基线（as-built，8 程序集，ADR-0020/#88）

```text
StarPie (Host/exe) ──→ Core / Dialogs / Programs / Shell / Theme / Wheel / Gestures
Gestures ──→ Core + Wheel(允许边 IWheelFactory)      Wheel ──→ Core + Theme(允许边 IThemeService)
Dialogs  ──→ Core + Theme(允许边 IThemeService)
其余 M* ──→ Core 单向       Tests ──→ 全部（显式，无传递）
```

## 2. 规范内缝（approved，改动受 ADR/收口测试守护）

| 缝 | 载体（契约/实现） | 裁决/守护 |
|---|---|---|
| 契约缝·图标资产 | `IIconAssetService`/`IconCatalog` 驻 Core；`IconAssetService` 组合根注册 | ADR-0019/#87；IconCatalogTests |
| 契约缝·.lnk 解析 | `IShortcutTargetResolver` 驻 Core ← M3 `ShortcutResolver` | ADR-0019/#87；ProgramsAssemblyPlacementTests |
| 契约缝·程序扫描 | `IProgramScanner`/`ProgramEntry`/`ProgramCatalog` 驻 Core ← M3 `ProgramScanner`（实例） | ADR-0020/#88（S21 归零）；ProgramsAssemblyPlacementTests |
| 契约缝·主题 | `IThemeService` 驻 M4 ← 消费方 Host/M2/Dialogs（允许边） | B7/#80/B8/#81/ADR-0020；Theme/Wheel/Dialogs Placement |
| 契约缝·轮盘工厂 | `IWheelFactory` 驻 M2 ← 消费方 M1（允许边） | B8/#81 D5；WheelAssemblyPlacementTests |
| 契约缝·预览 Profile | `IProfilePreviewSource` 驻 Core，别名 = M1 `ProfileListViewModel`，消费 M2 | B8/#81 D5；Wheel/Gestures Placement |
| 契约缝·对话框 | `IDialogService`/结果 record 驻 Core ← 实现 `DialogService` 驻 Dialogs | ADR-0020/#88；DialogsAssemblyPlacementTests |
| 注册缝 | 6 个 `*ModuleRegistrar`（Core 除外）下放 DI/导航注册；组合根唯一解析 | B4–B9 + ADR-0020；各 PlacementTests |
| 回填缝·dev 标志 | `AppDataPaths.IsDevInstance` 组合根装配前回填（消费 M1/M5/S2） | B2/B6/B9；MouseHook/Autostart 测试 |
| 回填缝·宿主回调 | `AppHostDelegates` 驻 Core（可空 Action 单例），AppHost 构造后回填 | B6/#79；ShellAssemblyPlacementTests |
| 回填缝·对话框 Owner | `DialogService.SetOwner(MainView)` Host 建窗后回填（public 装配面） | ADR-0004/ADR-0020；e2e |
| 导航缝 | `NavigationCatalog` + `NavigationSlots`（槽位 0–4）+ 模块注册器 `RegisterNavigation` + 页面模板字典 | B3/#76；NavigationCatalogTests（ADR-0021/#92 补注：导航运行时/执行入口 `INavigationExecutor` 随运行时整体归 Host，为宿主内部件而非跨程序集缝，本表不登记） |
| XAML 资源缝 | App.xaml 资源单点合并/实例化：主题与模板字典（+HotkeyRecorderBox 样式字典）经跨集 pack URI、ModernControls.xaml 宿主本地合并、转换器 App 级实例（ADR-0022/#94：ModernControls 与通用转换器迁 Host、热键样式字典随控件下沉 Gestures） | ADR-0012/B5–B9/ADR-0022 |
| 消息缝 | S4 hub（`Messages.cs`/`Notices.cs`），跨模块广播；新消息 = 放行共享面 | B1/#64；messages.md |
| 系统调用委托缝（A 类） | 服务构造注入 `Func<bool>`/`Action` 系统探针（ThemeService/ActionExecutorService/VM 委托），生产默认值内建 | layering.md「系统调用接缝模式」；单测替身 |
| 收口测试缝 | 8 个 `*AssemblyPlacementTests`（含 ADR-0021/#92 新增 Navigation）+ NavigationCatalog 收口测试 | 各批次；ADR-0018 |

## 3. 需关注缝（有意接受，但对模块化施加压力；改动前先读裁决）

| 缝 | 位置 | 压力 | 裁决/触发条件 |
|---|---|---|---|
| Host 装配面 | Composition/CreateAppHost 直取模块具体类型（MouseHook/ThemeService/两子 VM 等）；AppHost 编排托盘菜单/ThemePaletteManager/MouseHook 暂停态；Host 聚合页拼装 M2/M4 子 VM | Host 对"模块暴露哪些 public 装配件"有编译期认知；模块不能脱离 Host 决定宿主装配 | ADR-0016 决策 13（组合根集中）；目标态留 Host；不引入子容器/Prism |
| 导航槽位容量 | `NavigationSlot` 固定 0–4 + Validate + e2e `NavTab0..4` | 新增第 6 页需改 Core 枚举 + 收口测试（可能波及 e2e），非"纯模块内部" | Q4=a：产品页面数封顶，改动属放行共享面；navigation.md 登记 |
| 共享配置对象 | `IConfigService.Current` 单例可变 `AppConfig`；模块 VM 构造抓引用，导入后消息自挂 | 任何模块可读写任何配置区；模块间经"同一对象 + 广播"隐式协作 | 放行共享面（modules.md §2.3）；config.json 向后兼容 Hard Constraint |
| Models 物理残留（R8） | `WheelProfile`/`ActionItem` 语义归 M1、物理 Core；`CustomColorPreset` 语义归 M2、物理 Core（AppConfig 引用） | 业务领域形状渗入共享内核 | R8 已登记；迁移触发条件 = 配置模型与模块语义解耦时再议 |

## 4. 残留缝（已裁决清理方向，未排期）

当前无。已归零：S21 程序扫描委托（ADR-0020/#88）、ThemeChanged 死事件（ADR-0020/#88）、
D5 WheelFactory 装配点（B8/#81）、IconAssets 静态回填（ADR-0019/#87）、M3 零 Core 例外
（ADR-0019/#87）。

## 维护义务

1. 新增跨程序集接触点时：先判定属哪一档，登记本表并链裁决（ADR/叶子行号）；
   不满足 ADR 三条件的小改动只改本表与对应叶子。
2. 缝归零（重构消除）时：从本表移除并在 §4 记一行，指向裁决。
3. 程序集/依赖方向变化先改 [assemblies.md](assemblies.md) §3，再同步本表基线。
