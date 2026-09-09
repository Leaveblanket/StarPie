# 目录与文件架构

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；需要确认“某个路径放什么 / 新增文件落在哪”时读本篇。

## Canonical 目录树

以下为**应然结构**（正典）。当前代码与正典一致（exe `Modules/` 的 Host 临时面见文末“当前登记偏差”；
M5 已随 B6/#79 迁出 exe，M4 主题件已随 B7/#80 迁出 exe，M2 轮盘件已随 B8/#81 迁出 exe，
M1 手势件已随 B9/#82 迁出 exe）。

```text
StarPie/
├── WinPieGestures/                # Host 宿主工程（exe，程序集 StarPie）：组合根、宿主壳窗口、导航运行时、S6 对话框与外观聚合页
│   ├── App.xaml / App.xaml.cs     # 宿主生命周期：单实例、异常、启动/退出编排
│   ├── AppHost.cs                 # 宿主编排：Run/Dispose、托盘、语言资源、退出协调
│   ├── Composition.cs             # DI 组合根（唯一）：注册与解析（含 B2 跨程序集回填缝，见 layering.md）
│   ├── DevInstance.cs             # 开发实例标记（H1）：--dev 互斥/触发键/自启保护
│   ├── Modules/                   # B3/#76 临时，B9/#82 起仅余 Host：HostModuleRegistrar + HostPageTemplates.xaml（外观聚合页，目标态留 Host）
│   ├── AssemblyInfo.cs            # 程序集元数据
│   ├── GlobalUsings.cs            # 工程级全局 using
│   ├── WinPieGestures.csproj      # SDK 工程文件（.slnx 同层）
│   ├── Properties/
│   │   └── launchSettings.json    # 工程配置；不放源码
│   ├── assets/
│   │   ├── app_icon.ico           # 应用图标（csproj ApplicationIcon 引用）
│   │   └── logo.png
│   ├── Services/
│   │   └── Navigation/            # 导航运行时（ADR-0021/#92 迁入）：NavigationStore、NavigationExecutor（含 INavigationExecutor）
│   ├── ViewModels/
│   │   ├── Pages/                 # Host 外观聚合页 VM：AppearanceSettingsViewModel（单例）
│   │   └── Navigation/            # 导航 VM：MainViewModel、NavigationItemViewModel（ADR-0021/#92 迁入）+ ShellViewModel（B1/D3 Host 壳层 VM）
│   └── Views/
│       ├── Converters/            # 通用共享转换器（ADR-0022/#94 自 Core 迁入）：HexToBrush/StringToGeometry/IntEquals/FilePathToImage（App.xaml 单点实例化）
│       ├── Pages/                 # Host 外观聚合页 View：AppearanceSettingsPage（M1 两页已迁 StarPie.Gestures，B9/#82）
│       ├── Styles/                # ModernControls.xaml 全局控件样式字典（ADR-0022/#94 自 Core 迁入；App.xaml 本地合并）
│       └── Navigation/            # MainView、SidebarView
├── StarPie.Core/                  # 共享内核（WPF 类库，程序集 StarPie.Core；命名空间 StarPie.*，B10/#83 统一）
│   ├── StarPie.Core.csproj        # SDK 工程文件（RootNamespace=StarPie；resx 生成器随 S3 迁入）
│   ├── GlobalUsings.cs            # 工程级全局 using（仅 Core 命名空间）
│   ├── Models/                    # 共享数据模型与 WPF-free 值类型（S2/R8；WheelPalette* 已随 B8/#81 收编 StarPie.Wheel/Models/）
│   │   ├── AppConfig.cs
│   │   ├── WheelProfile.cs
│   │   ├── ActionItem.cs
│   │   ├── CustomColorPreset.cs   # 自定义配色预设（语义 M2；AppConfig 配置 POCO 引用故仍居 Core）
│   │   ├── ColorMath.cs           # RgbColor（readonly struct）与纯颜色换算
│   │   └── GesturePoint.cs        # 手势坐标点（WPF-free readonly struct）
│   ├── Services/
│   │   ├── AppHostDelegates.cs    # 宿主回调委托包契约（B6/#79 上提；Host 组合根注册单例、AppHost 回填）
│   │   ├── Configuration/         # S2：配置读写、防抖保存、AppDataPaths（dev 分支经组合根回填）
│   │   ├── Dialogs/               # S6 契约：IDialogService + 结果 record
│   │   ├── Programs/              # M3 纯数据/纯规则/契约上提：ProgramEntry、ProgramCatalog、IProgramScanner（ADR-0020/#88）
│   │   ├── Icons/                 # S1：仅余 .lnk 解析契约 IShortcutTargetResolver.cs（#95 中间态，ADR-0023/#95；其余 S1 件已迁 StarPie.Icons.Contracts/StarPie.Icons，见下两工程）
│   │   ├── Localization/          # S3：ILocalizationService + Strings*.resx（四语言）
│   │   ├── Messages/              # S4：IMessenger 消息与跨层通知载体
│   │   └── Navigation/            # S5：目录/槽位契约——NavigationCatalog/NavigationSlots（槽位表 0–4；ADR-0021/#92 起运行时在 Host，仅此文件）
│   └── ViewModels/
│       └── Pages/                 # 跨 M 只读契约：IProfilePreviewSource.cs（B8/#81 上提，D5；Navigation 目录已随 ADR-0021/#92 迁 Host 清零；Views/ 目录已随 ADR-0022/#94 清空）
├── StarPie.Icons.Contracts/       # S1 图标契约程序集（WPF 类库，程序集 StarPie.Icons.Contracts；命名空间 StarPie.*；ADR-0023/#95 起）
│   ├── StarPie.Icons.Contracts.csproj  # SDK 工程文件（RootNamespace=StarPie；零 ProjectReference——薄契约）
│   └── Services/Icons/            # S1 契约四件：IIconAssetService.cs、IconCatalog.cs、CustomIconItem.cs、VectorIconItem.cs（命名空间 StarPie.Services.Icons 不变）
├── StarPie.Icons/                 # S1 图标实现程序集（WPF 类库，程序集 StarPie.Icons；命名空间 StarPie.*；ADR-0023/#95 起；单向 Icons.Contracts + Core（#95 中间态））
│   ├── StarPie.Icons.csproj       # SDK 工程文件（RootNamespace=StarPie；引用 Icons.Contracts + Core（IShortcutTargetResolver，#95 中间态）；MS.DI 包——注册器用）
│   ├── Modules/                   # IconsModuleRegistrar.cs（RegisterServices；S1 无导航页/模板字典）
│   └── Services/Icons/            # S1：IconAssetService.cs（自定义图标存储/位图源/GetIcon）
├── StarPie.Dialogs/               # S6 对话框实现模块程序集（WPF 类库，程序集 StarPie.Dialogs；命名空间 StarPie.*；ADR-0020/#88 B11 起）
│   ├── StarPie.Dialogs.csproj     # SDK 工程文件（RootNamespace=StarPie；引用 Core + Theme（IThemeService 允许边）+ Icons.Contracts（ADR-0023/#95））
│   ├── GlobalUsings.cs            # 工程级全局 using（模块所需 Core/M4 命名空间）
│   ├── Modules/                   # DialogsModuleRegistrar.cs（RegisterServices；S6 无导航页/模板字典）
│   ├── Services/Dialogs/          # S6：DialogService（public——Host SetOwner 装配面）
│   ├── ViewModels/Dialogs/        # S6：五对对话框 VM（ProgramPicker/IconPicker/Input/ColorPicker/ScreenEyedropper）
│   └── Views/
│       ├── Dialogs/               # S6：五对对话框 Window.xaml(.cs)
│       └── Controls/              # S6：SpectrumCanvasBehavior（取色对话框专用）
├── StarPie.Programs/              # M3 模块程序集（WPF 类库，程序集 StarPie.Programs；命名空间 StarPie.*，B10/#83 统一；B4/#77 起）
│   ├── StarPie.Programs.csproj    # SDK 工程文件（RootNamespace=StarPie；引用 Core + Icons.Contracts（ADR-0023/#95），M3 → Core/Contracts 单向）
│   ├── Modules/                   # 模块注册器：ProgramsModuleRegistrar.cs（RegisterServices，ADR-0019/#87 + ADR-0020/#88）
│   └── Services/Programs/         # M3：ProgramScanner（IO 扫描）、ShortcutResolver（ProgramCatalog/ProgramEntry 已上提 Core，ADR-0020/#88）
├── StarPie.Shell/                 # M5 壳层模块程序集（WPF 类库，程序集 StarPie.Shell；命名空间 StarPie.*，B10/#83 统一；B6/#79 起；单向 Core）
│   ├── StarPie.Shell.csproj       # SDK 工程文件（RootNamespace=StarPie；引用 Core）
│   ├── GlobalUsings.cs            # 工程级全局 using（模块所需 Core 命名空间）
│   ├── Modules/                   # ShellModuleRegistrar.cs（RegisterServices+RegisterNavigation）+ ShellPageTemplates.xaml
│   ├── Services/Shell/            # M5：TrayIconManager(+TrayMenuEntry)、AutostartRegistry、MemoryOptimizer
│   ├── ViewModels/Pages/          # M5：GeneralSettingsViewModel、AboutViewModel
│   └── Views/Pages/               # M5：AdvancedSettingsPage、AboutSettingsPage（根直承 UserControl，ADR-0022/#94）
├── StarPie.Theme/                 # M4 界面主题模块程序集（WPF 类库，程序集 StarPie.Theme；命名空间 StarPie.*，B10/#83 统一；B7/#80 起；单向 Core）
│   ├── StarPie.Theme.csproj       # SDK 工程文件（RootNamespace=StarPie；引用 Core）
│   ├── GlobalUsings.cs            # 工程级全局 using（模块所需 Core 命名空间）
│   ├── Modules/                   # ThemeModuleRegistrar.cs（RegisterServices；M4 无导航页/模板字典）
│   ├── ThemePaletteManager.cs     # 主题调色板整项替换（模块根，public——Host AppHost 装配面，B7/#80）
│   ├── Services/Shell/            # M4：IThemeService、ThemeService（命名空间 StarPie.Services.Shell）
│   ├── ViewModels/Pages/          # M4：InterfaceThemeSettingsViewModel、AppThemeOptionItem
│   └── Views/Styles/Themes/       # M4：五套同 key 集主题画刷令牌（Light/Dark/MidnightNavy/RoyalViolet/TitaniumGray）
├── StarPie.Wheel/                 # M2 轮盘与渲染模块程序集（WPF 类库，程序集 StarPie.Wheel；命名空间 StarPie.*，B10/#83 统一；B8/#81 起；单向 Core + M4 允许边）
│   ├── StarPie.Wheel.csproj       # SDK 工程文件（RootNamespace=StarPie；引用 Core + Theme + Icons.Contracts（ADR-0023/#95））
│   ├── GlobalUsings.cs            # 工程级全局 using（模块所需 Core/M2 命名空间）
│   ├── Modules/                   # WheelModuleRegistrar.cs（RegisterServices；M2 无导航页/模板字典）
│   ├── Models/                    # M2：轮盘配色 WheelPalette.cs/WheelPaletteCatalog.cs/WheelPaletteParser.cs（WPF-free，B8/#81 物理收编）
│   ├── Services/Wheel/            # M2：WheelGeometry.cs（视觉几何出口）、IWheelFactory.cs/WheelFactory.cs（D5 收编，命名空间 StarPie.Services.Wheel）
│   ├── ViewModels/
│   │   ├── Pages/                 # M2：WheelAppearanceSettingsViewModel（外观设置子 VM，单例）
│   │   └── Wheel/                 # M2：IWheelViewModel、WheelViewModel、IWheelAppearanceState（瞬态）
│   ├── Views/
│   │   ├── Wheel/                 # M2：RadialWindow.xaml(.cs)
│   │   ├── Renderers/             # M2：IRadialStyleRenderer/StyleRendererFactory/BaseStyleRenderer/各风格渲染器/WheelPreviewRenderer
│   │   └── Converters/            # M2：CoreIconGeometryConverter/CoreIconNameConverter（核图标预览，B8/#81 裁决随 M2）
├── StarPie.Gestures/              # M1 手势与动作模块程序集（WPF 类库，程序集 StarPie.Gestures；命名空间 StarPie.*，B10/#83 统一；B9/#82 起；单向 Core + M2 允许边）
│   ├── StarPie.Gestures.csproj    # SDK 工程文件（RootNamespace=StarPie；引用 Core + Wheel + Icons.Contracts（ADR-0023/#95））
│   ├── GlobalUsings.cs            # 工程级全局 using（模块所需 Core/M1/M2 命名空间）
│   ├── Modules/                   # GesturesModuleRegistrar.cs（RegisterServices+RegisterNavigation）+ GesturesPageTemplates.xaml
│   ├── Services/Gestures/         # M1：MouseHook、GestureController、GestureEngine（+ GestureState/GestureReleaseResult）、IWindowContext/WindowContext
│   ├── Services/Actions/          # M1：IActionExecutorService/ActionExecutorService、ActionRouting（+ ActionRoute/KeyStroke/SystemCommand）
│   ├── ViewModels/
│   │   ├── Pages/                 # M1：BehaviorSettingsViewModel、ProfileListViewModel（+ ProfileItemViewModel）
│   │   └── Gestures/              # M1：SlotViewModel（+ SystemPresetItem/ActionTypeOption）
│   ├── Views/
│   │   ├── Controls/              # 热键录制控件 HotkeyRecorderBox.cs（ADR-0022/#94 下沉，唯一消费方 GesturesSettingsPage）
│   │   ├── Pages/                 # M1：TriggerSettingsPage、GesturesSettingsPage（根直承 UserControl，ADR-0022/#94）
│   │   └── Styles/                # HotkeyRecorderBox.xaml 热键录制控件隐式默认样式字典（App.xaml 经 pack URI 合并）
└── WinPieGestures.Tests/          # xUnit 单测（显式引用 Host、Core、Dialogs、Programs、Shell、Theme、Wheel、Gestures、Icons.Contracts 与 Icons）
```

> 程序集归属：目录名在 `StarPie.Core/`、`StarPie.Programs/`、`StarPie.Shell/`、`StarPie.Theme/`、`StarPie.Wheel/`、`StarPie.Gestures/` 与 `WinPieGestures/`
> 中各自保持“命名空间 = 物理目录”；
> 共享内核目录（Models、Services/Configuration|Dialogs(契约)|Localization|Messages|Navigation（目录契约 NavigationCatalog.cs，ADR-0021/#92 起运行时不在 Core）|Programs（ADR-0020/#88：ProgramEntry/ProgramCatalog/IProgramScanner）、
> Services/AppHostDelegates.cs（B6/#79）、ViewModels/Pages/IProfilePreviewSource.cs（B8/#81 上提））
> 只存在于 `StarPie.Core/`（S1 图标资产已随 ADR-0023/#95 成集迁出，`Services/Icons/` 仅余 #95
> 中间态 `IShortcutTargetResolver.cs`；共享 UI 基建已随 ADR-0022/#94 去共享化：通用转换器/ModernControls.xaml
> 在 Host `Views/Converters|Styles/`、HotkeyRecorderBox（控件+样式字典）在
> `StarPie.Gestures/Views/Controls|Styles/`、共享页面基类 SettingsPageBase 已删除）；M3 业务目录
> （ProgramScanner/ShortcutResolver）只存在于 `StarPie.Programs/`（B4/#77 起；ProgramEntry/ProgramCatalog/IProgramScanner 上提 Core 同目录，ADR-0020/#88）；M5 业务目录
> （`Services/Shell/`、`ViewModels/Pages/` 的 M5 两 VM、`Views/Pages/` 的 M5 两页、`Modules/`）只
> 存在于 `StarPie.Shell/`（B6/#79 起）；M4 业务目录（`Services/Shell/` 的 M4 两服务、
> `ViewModels/Pages/` 的 M4 主题设置子 VM、`Views/Styles/Themes/`、`Modules/` 的 ThemeModuleRegistrar
> 与模块根 `ThemePaletteManager.cs`）只存在于 `StarPie.Theme/`（B7/#80 起）；M2 业务目录
> （`Models/` 的 WheelPalette*、`Services/Wheel/`、`ViewModels/Wheel/`、
> `ViewModels/Pages/` 的 WheelAppearanceSettingsViewModel、`Views/Wheel/`、`Views/Renderers/`、
> `Views/Converters/` 的 CoreIcon*、`Modules/` 的 WheelModuleRegistrar）只存在于 `StarPie.Wheel/`
> （B8/#81 起）；M1 业务目录（`Services/Gestures/`、`Services/Actions/`、`ViewModels/Gestures/`、
> `ViewModels/Pages/` 的 BehaviorSettingsViewModel/ProfileListViewModel、`Views/Pages/` 的
> TriggerSettingsPage/GesturesSettingsPage、`Views/Controls/`+`Views/Styles/` 的
> HotkeyRecorderBox（控件+样式字典，ADR-0022/#94）、`Modules/` 的 GesturesModuleRegistrar/
> GesturesPageTemplates.xaml）只存在于 `StarPie.Gestures/`（B9/#82 起）；
> S1 契约目录（`Services/Icons/` 的 IIconAssetService/IconCatalog/CustomIconItem/VectorIconItem）
> 只存在于 `StarPie.Icons.Contracts/`，S1 实现目录（`Services/Icons/` 的 IconAssetService +
> `Modules/` 的 IconsModuleRegistrar）只存在于 `StarPie.Icons/`（ADR-0023/#95 起）；
> 其余业务目录（Host 外观聚合页/壳窗口/导航运行时（`Services/Navigation/` +
> `ViewModels/Navigation/`，ADR-0021/#92 迁入）等）留 `WinPieGestures/`；S6 对话框实现目录
（`Services/Dialogs`、`ViewModels/Dialogs`、`Views/Dialogs`、`Views/Controls/SpectrumCanvasBehavior`）在 `StarPie.Dialogs/`（ADR-0020/#88）。
> 依赖方向见 [assemblies.md](assemblies.md) §3。

## 各目录职责细则

> 目录相对所属工程：共享内核件位于 `StarPie.Core/`——`Models/`、`Services/Configuration`|
> `Dialogs`(契约)|`Localization`|`Messages`|`Navigation`（ADR-0021/#92 起仅目录契约
> `NavigationCatalog.cs`；运行时主体已迁 Host）、`Services/AppHostDelegates.cs`（B6/#79 上提）、
> `ViewModels/Pages/IProfilePreviewSource.cs`（B8/#81 上提，D5）与 `Services/Icons/` 的
> `IShortcutTargetResolver.cs`（#95 中间态，ADR-0023/#95）——**不再含共享 UI 基建
> （ADR-0022/#94 去共享化）**：通用转换器与 `ModernControls.xaml` 在 Host
> `Views/Converters|Styles/`，`HotkeyRecorderBox`（控件+样式字典）在
> `StarPie.Gestures/Views/Controls|Styles/`，共享页面基类已删除；M3 三件（`ProgramScanner`/`ProgramCatalog`/
> `ShortcutResolver`，B4/#77 迁入）位于 `StarPie.Programs/Services/Programs/`；M5 三件与两页
> （B6/#79 迁入）位于 `StarPie.Shell/`；M4 主题件（`IThemeService`/`ThemeService`、
> `ThemePaletteManager.cs`、`Views/Styles/Themes/*.xaml`、`InterfaceThemeSettingsViewModel`、
> `ThemeModuleRegistrar`，B7/#80 迁入）位于 `StarPie.Theme/`（见下模块程序集目录表）；
> M2 轮盘件（WheelPalette*/WheelGeometry/轮盘 VM/RadialWindow/渲染器/工厂/核图标转换器/
> WheelAppearanceSettingsViewModel，B8/#81 迁入）位于 `StarPie.Wheel/`（见下模块程序集目录表）；
> M1 手势件（手势管线/动作执行/触发+手势设置页/SlotViewModel，B9/#82 迁入）位于
> `StarPie.Gestures/`（见下模块程序集目录表）；导航运行时（`Services/Navigation/` 的
> `NavigationStore`/`NavigationExecutor`（含 `INavigationExecutor`）、`ViewModels/Navigation/`
> 的 `MainViewModel`/`NavigationItemViewModel`，ADR-0021/#92 迁入、命名空间不变）；S1 契约件
> （IIconAssetService/IconCatalog/CustomIconItem/VectorIconItem，ADR-0023/#95 迁入）位于
> `StarPie.Icons.Contracts/`、S1 实现件（IconAssetService + IconsModuleRegistrar，
> ADR-0023/#95 迁入）位于 `StarPie.Icons/`（见下模块程序集目录表）；其余业务
> 目录位于 `WinPieGestures/`（Host）。

| 目录 | 存放什么 | 不放什么 / 常见违规 |
|---|---|---|
| `Models/` | 共享内核（Core）：配置 POCO（`AppConfig`、`WheelProfile`、`ActionItem`、`CustomColorPreset`（语义 M2，B8/#81 起仍居此——AppConfig 引用））与 WPF-free 领域值类型/纯函数（`RgbColor`/`ColorMath`、`GesturePoint`）；M2（`StarPie.Wheel/Models/`，B8/#81 物理收编）：轮盘配色 `WheelPalette`/`WheelPaletteCatalog`/`WheelPaletteParser`（WPF-free，语义+物理均归 M2） | 不引用 WPF 类型、服务、命令、消息、IMessenger；不放可注入服务、文件 IO、静态 Win32 工具 |
| `Services/{Feature}/` | 该功能的服务接口与实现（同目录）、编排器、纯函数、进程内 DTO | 不放 VM/View；不跨目录“借用”他人实现；静态工具需符合 [layering.md](layering.md)（Services） |
| `Services/Actions/` | **B9/#82 起在 `StarPie.Gestures/Services/Actions/`**：`IActionExecutorService`、`ActionExecutorService`（系统调用层）、`ActionRouting`（纯函数 + `ActionRoute`/`KeyStroke`） | 路由决策不得散落进 VM/View；实现见 [gestures.md](gestures.md) |
| `Services/Configuration/` | `IConfigService`/`JsonConfigService`、`ISaveDebouncer`/`DispatcherSaveDebouncer`、`SettingsSaveOrchestrator`、`AppDataPaths`；**B2/#75 起在 `StarPie.Core/`（dev 目录分支经组合根回填 `AppDataPaths.IsDevInstance`）** | 页面 VM 不得直接碰配置文件路径或 `JsonSerializer`；实现见 [config.md](config.md) |
| `Services/Dialogs/` | **分置**：契约 `IDialogService` + 各 `ShowXxx` 的可空结果 record 在 `StarPie.Core/Services/Dialogs/`（B2/#75 起）；实现 `DialogService` 在 `StarPie.Dialogs/Services/Dialogs/`（ADR-0020/#88 B11 起） | 对话框 Window/VM 不在此；文件对话框/MessageBox 不暴露给 VM/View，系统弹窗边界见 [dialogs.md](dialogs.md) |
| `Services/Gestures/` | **B9/#82 起在 `StarPie.Gestures/Services/Gestures/`**：`MouseHook`、`GestureController`、`GestureEngine`（+ `GestureState`/`GestureReleaseResult`）、`IWindowContext`/`WindowContext`（M1；`IWheelFactory`/`WheelFactory` 已随 B8/#81 D5 收编 M2，见 `Services/Wheel/` 行） | 手势判定纯逻辑（引擎）不得引用 WPF/Win32；实现见 [gestures.md](gestures.md) |
| `Services/Icons/` | **分置（ADR-0023/#95 S1 成集）**：契约四件（静态纯目录 `IconCatalog`、实例服务契约 `IIconAssetService`、`CustomIconItem`/`VectorIconItem`）在 `StarPie.Icons.Contracts/Services/Icons/`；实现 `IconAssetService` 在 `StarPie.Icons/Services/Icons/`；.lnk 契约 `IShortcutTargetResolver` #95 中间态仍驻 `StarPie.Core/Services/Icons/`（M3 实现，#96 随 Programs.Contracts 迁出） | 几何/程序解析类入口不在此目录（R6 三分，T3a–T3d/#65–#68 收口）；有状态/IO/Win32 面只经实例服务注入，不进 VM/View；归属见 [modules.md](modules.md) §3 S1 |
| `Services/Localization/` | `ILocalizationService`/`LocalizationService` + `Strings*.resx`（语言状态以规范 BCP-47 码字符串为唯一表示，别名表在服务内）；**B2/#75 起在 `StarPie.Core/`** | VM/View 不得另建文案字典；实现见 [localization.md](localization.md) |
| `Services/Messages/` | `Messages.cs`（IMessenger 不可变消息）、`Notices.cs`（`NoticeKind`/`NoticeRequest` 等跨层弹窗载体）；**B2/#75 起在 `StarPie.Core/`** | 不放绑定语义；同页状态不得用消息替代绑定 |
| `Services/Navigation/` | **分置（ADR-0021/#92）**：共享内核（`StarPie.Core/Services/Navigation/`，B2/#75 起）：目录/槽位契约 `NavigationCatalog`/`NavigationSlot`/`NavigationSlots`/`NavigationPageRegistration`（仅 `NavigationCatalog.cs`）；宿主（`WinPieGestures/Services/Navigation/`）：导航运行时 `NavigationStore`、`NavigationExecutor`（含 `INavigationExecutor`，命名空间 `StarPie.Services.Navigation` 不变） | 页面状态不得散落导航器之外；实现见 [navigation.md](navigation.md) |
| `Services/Wheel/` | **B8/#81 起在 `StarPie.Wheel/Services/Wheel/`**：`WheelGeometry`（M2 轮盘视觉几何出口：扇区/核图标几何）、`IWheelFactory`/`WheelFactory`（D5 收编，命名空间 `StarPie.Services.Wheel` 与物理目录一致） | 实现见 [wheel.md](wheel.md)；工厂只经 M2 侧接口被 M1 消费 |
| `ViewModels/Pages/` | Host：外观聚合页 VM `AppearanceSettingsViewModel`（单例）；M1 两 VM（`BehaviorSettingsViewModel`/`ProfileListViewModel`）已迁 `StarPie.Gestures/ViewModels/Pages/`（B9/#82）；M5 两 VM（`GeneralSettingsViewModel`/`AboutViewModel`）已迁 `StarPie.Shell/ViewModels/Pages/`（B6/#79）；M4 主题设置子 VM（`InterfaceThemeSettingsViewModel`/`AppThemeOptionItem`）已迁 `StarPie.Theme/ViewModels/Pages/`（B7/#80）；M2 轮盘外观设置子 VM `WheelAppearanceSettingsViewModel` 已迁 `StarPie.Wheel/ViewModels/Pages/`（B8/#81）；Core 含跨 M 只读契约 `IProfilePreviewSource.cs`（B8/#81 上提） | 不得引用 WPF 类型；不得出现 `event Action` 临时事件 |
| `ViewModels/Dialogs/` | **ADR-0020/#88 起在 `StarPie.Dialogs/ViewModels/Dialogs/`**：`{Dialog}ViewModel`（含 `ScreenEyedropperViewModel`） | 不得持有 Window/MessageBox/对话框类型；形态见 [dialogs.md](dialogs.md) |
| `ViewModels/Gestures/` | **B9/#82 起在 `StarPie.Gestures/ViewModels/Gestures/`**：方向槽位等子 VM（`SlotViewModel`，+ `SystemPresetItem`/`ActionTypeOption`） | 不放服务 |
| `ViewModels/Navigation/` | **Host（ADR-0021/#92 迁入，命名空间不变）**：`NavigationItemViewModel`、`MainViewModel`（B3/#76 目录驱动）、`ShellViewModel`（B1/D3 Host 壳窗口壳层 VM）——与 `ShellViewModel` 同目录族 | 导航项文案/图标规则见 [navigation.md](navigation.md) |
| `ViewModels/Wheel/` | **B8/#81 起在 `StarPie.Wheel/ViewModels/Wheel/`**：`IWheelViewModel`、`WheelViewModel`、`IWheelAppearanceState` | 不注册容器；按手势由 `WheelFactory` 瞬态创建；见 [wheel.md](wheel.md) |
| `Views/Pages/` | Host：外观聚合页 `AppearanceSettingsPage`（无参构造）；M1 两页（`TriggerSettingsPage`/`GesturesSettingsPage`）已迁 `StarPie.Gestures/Views/Pages/`（B9/#82）；M5 两页（`AdvancedSettingsPage`/`AboutSettingsPage`）已迁 `StarPie.Shell/Views/Pages/`（B6/#79）；共享基类 `SettingsPageBase.cs` 已随 ADR-0022/#94 删除——五页 XAML 根直承 `UserControl` | 不注册容器；不编排业务/写配置/调服务 |
| `Views/Dialogs/` | **ADR-0020/#88 起在 `StarPie.Dialogs/Views/Dialogs/`**：`{Dialog}Window.xaml(.cs)`（对话框唯一形态） | 例外见 [naming.md](naming.md)；不放置无配对 Window 的散件 |
| `Views/Navigation/` | `MainView.xaml(.cs)`、`SidebarView.xaml(.cs)`；`MainView` 为纯壳（B3/#76 起页面 DataTemplate 已迁出至 App 级模块页面模板字典——M5 在 `StarPie.Shell/Modules/`，M1 在 `StarPie.Gestures/Modules/`，Host 在 exe `Modules/`） | 其它窗口/页面不得再合并样式字典（样式已 App 级单点合并） |
| `Views/Wheel/` | **B8/#81 起在 `StarPie.Wheel/Views/Wheel/`**：`RadialWindow.xaml(.cs)` | 轮盘状态决策在 `WheelViewModel`，窗口只做视觉呈现与生命周期；见 [wheel.md](wheel.md) |
| `Views/Controls/` | **B11/#88 起在 `StarPie.Dialogs/Views/Controls/`**：取色行为 `SpectrumCanvasBehavior.cs`（S6 取色对话框专用，依赖 `ColorPickerViewModel.SpectrumPoint`）；共享自定义控件 `HotkeyRecorderBox.cs` 已随 ADR-0022/#94 下沉 `StarPie.Gestures/Views/Controls/`（见下“共享 UI 基建落点”） | 有 `Command`/绑定等价物时不得新增行为 |
| `Views/Converters/` | Host：通用共享转换器（`HexToBrushConverter`/`StringToGeometryConverter`/`IntEqualsConverter`/`FilePathToImageConverter`，ADR-0022/#94 自 Core 迁入；实例仍由 App.xaml App 级单点持有）；M2 核图标预览转换器 `CoreIconGeometryConverter`/`CoreIconNameConverter` 在 `StarPie.Wheel/Views/Converters/`（B8/#81 裁决随 M2） | 转换器保持无状态、可静态复用 |
| `Views/Renderers/` | **B8/#81 起在 `StarPie.Wheel/Views/Renderers/`**：`IRadialStyleRenderer`、`StyleRendererFactory`、`BaseStyleRenderer`、各风格渲染器、`WheelPreviewRenderer`；渲染器只消费 `WheelPalette` 解析结果构造画刷，不内联方案 hex 表 | 渲染器不订阅事件、不读写 VM、不反向依赖 Composition/服务；深浅色探测由调用方以 bool 传入（不引用 Host MainView）；见 [wheel.md](wheel.md) |
| `Modules/`（B3/#76 临时，B9/#82 起仅剩 Host） | exe 内 Host 模块注册器 `HostModuleRegistrar`（`RegisterNavigation`）+ 页面模板字典 `HostPageTemplates.xaml`（App 级每模块一次静态合并）；M5/M1 对应件已迁 `StarPie.Shell/Modules/`（B6/#79）/`StarPie.Gestures/Modules/`（B9/#82） | 不承载业务；目标态 Host 外观聚合页，新增 Host 页仍须经本注册器+模板字典登记 |

### 共享 UI 基建落点（ADR-0022/#94 去共享化）

> `StarPie.Core/Views/` 目录（Converters/Controls/Pages/Styles）已随去共享化**整体清空移除**；
> 资源键集不变——模块 XAML 消费方（`{StaticResource}` 运行期解析）零改动。

| 目录 | 存放什么 | 不放什么 / 常见违规 |
|---|---|---|
| `WinPieGestures/Views/Converters/` | Host 通用共享转换器：`HexToBrushConverter`（hex→Brush，配 Core `Models/RgbColor`）、`StringToGeometryConverter`（SVG 路径→Geometry）、`IntEqualsConverter`、`FilePathToImageConverter`（本地图片→缩略图）；实例由 Host `App.xaml` App 级单点持有（ADR-0012 决策 5） | 不放业务模块专用转换器（M2 核图标预览转换器在 `StarPie.Wheel/Views/Converters/`，B8/#81） |
| `WinPieGestures/Views/Styles/` | Host `ModernControls.xaml` 全局控件样式字典（隐式默认/键控变体/共享模板；App.xaml **本地合并**；几何令牌经 DynamicResource 供跨字典模板引用） | 不放主题画刷令牌（`Themes/*.xaml` 属 M4，B7/#80 起居 `StarPie.Theme/Views/Styles/Themes/`）；不放 HotkeyRecorderBox 专用样式段（在 Gestures） |
| `StarPie.Gestures/Views/Controls/` | M1：共享自定义控件 `HotkeyRecorderBox.cs`（唯一编译期消费方 `GesturesSettingsPage.xaml`，xmlns 本地引用；隐式默认样式模板在同模块 `Views/Styles/HotkeyRecorderBox.xaml`） | 不放对话框专用行为（`SpectrumCanvasBehavior` 已随 S6 迁 `StarPie.Dialogs`，ADR-0020/#88） |
| `StarPie.Gestures/Views/Styles/` | M1：`HotkeyRecorderBox.xaml` 热键录制控件样式字典（由 Host `App.xaml` 经 `/StarPie.Gestures;component/Views/Styles/HotkeyRecorderBox.xaml` 单点合并） | 不放全局控件样式（`ModernControls.xaml` 在 Host） |

### 模块程序集目录（B4/#77 起；B6/#79 起含首个带 DI 的模块程序集；B7/#80 起含 M4；B8/#81 起含 M2；B9/#82 起含 M1；ADR-0023/#95 起含 S1 契约/实现）

| 目录 | 存放什么 | 不放什么 / 常见违规 |
|---|---|---|
| `StarPie.Icons.Contracts/Services/Icons/` | S1 契约四件：`IIconAssetService.cs`/`IconCatalog.cs`/`CustomIconItem.cs`/`VectorIconItem.cs`（命名空间 `StarPie.Services.Icons` 不变，零 ProjectReference，ADR-0023/#95） | 不放实现/注册器（在 StarPie.Icons）；契约程序集只承载类型面 |
| `StarPie.Icons/Services/Icons/` | S1 实现：`IconAssetService.cs`（自定义图标存储/位图源/文件图标提取，Win32；构造注入 Core `IShortcutTargetResolver`，#95 中间态） | 不反向引用 Host/业务模块；有状态/IO/Win32 面只经契约注入消费方，不进 VM/View |
| `StarPie.Icons/Modules/` | 模块注册器 `IconsModuleRegistrar.cs`（`RegisterServices(IServiceCollection)` 下放 `IIconAssetService→IconAssetService`；S1 无导航页，无 RegisterNavigation/页面模板字典） | 只注册不解析；不承载业务 |
| `StarPie.Programs/Services/Programs/` | M3 程序扫描与目录：`ProgramScanner`（IO 扫描；ADR-0020/#88 起实例实现 `IProgramScanner`）、`ShortcutResolver`（快捷方式解析出口，实例实现 Core 契约）；`ProgramCatalog`/`ProgramEntry` 已上提 Core `Services/Programs/` | 集成性质扫描逻辑不进 VM 单测；图标资产契约在 `StarPie.Icons.Contracts`（`IIconAssetService` 注入）、.lnk 契约 #95 中间态在 Core `Services/Icons/`（`IShortcutTargetResolver` 注入，M3 → Core/Contracts 单向，ADR-0019/#87 + ADR-0023/#95）；实现见 [programs.md](programs.md) |
| `StarPie.Dialogs/Services/Dialogs/` | S6：`DialogService`（public——Host `SetOwner(MainView)` 装配面，ADR-0020/#88） | 只注册不解析；Owner 是内部自由不泄露进契约；见 [dialogs.md](dialogs.md) |
| `StarPie.Dialogs/ViewModels/Dialogs/` | S6：五对对话框 VM（`ProgramPickerViewModel`/`IconPickerViewModel`/`ColorPickerViewModel`/`InputViewModel`/`ScreenEyedropperViewModel`；每次 Show 新建，不注册容器） | 不得引用 WPF 类型；不得反向引用 Host/Programs（扫描经 Core `IProgramScanner`） |
| `StarPie.Dialogs/Views/Dialogs/` | S6：五对对话框 Window.xaml(.cs)（唯一形态，code-behind 白名单见 [dialogs.md](dialogs.md)） | 不注册容器；不编排业务 |
| `StarPie.Dialogs/Views/Controls/` | S6：`SpectrumCanvasBehavior`（取色对话框专用行为，依赖 `ColorPickerViewModel.SpectrumPoint`） | 行为需经 ADR-0009 输入适配裁决 |
| `StarPie.Dialogs/Modules/` | 模块注册器 `DialogsModuleRegistrar.cs`（`RegisterServices(IServiceCollection)`；S6 无导航页，无 RegisterNavigation/页面模板字典） | 只注册不解析；不承载业务 |
| `StarPie.Shell/Services/Shell/` | M5 壳层服务：`TrayIconManager`（+ `TrayMenuEntry`，public——Host AppHost 装配托盘与菜单 provider 用）、`AutostartRegistry`（internal，注册器接线）、`MemoryOptimizer` | 托盘/自启/内存决策不进 VM/View；不反向引用 Host/M4（深色配色经组合根注入 `Func<bool>` 探针）；实现见 [shell.md](shell.md) |
| `StarPie.Shell/ViewModels/Pages/` | M5 设置页 VM：`GeneralSettingsViewModel`、`AboutViewModel`（容器单例，由 `ShellModuleRegistrar.RegisterServices` 注册） | 不得引用 WPF 类型；不得反向引用 Host 类 |
| `StarPie.Shell/Views/Pages/` | M5 页面 View：`AdvancedSettingsPage`、`AboutSettingsPage`（XAML 根直承 `UserControl`，ADR-0022/#94） | 不注册容器；不编排业务/写配置/调服务；页面无参构造 |
| `StarPie.Shell/Modules/` | 正式模块注册器 `ShellModuleRegistrar.cs`（`RegisterServices(IServiceCollection)` + `RegisterNavigation(NavigationCatalog)`）+ 模块页面模板字典 `ShellPageTemplates.xaml`（Host App.xaml 经跨程序集 pack URI 单点合并） | 不承载业务；注册器只注册不解析；M1/Host 临时注册器不在此目录（M1 已迁 `StarPie.Gestures/Modules/`，B9/#82；Host 仍 exe `Modules/`） |
| `StarPie.Theme/Services/Shell/` | M4 主题服务：`IThemeService`/`ThemeService`（命名空间 `StarPie.Services.Shell`，B10/#83 统一；public——Host/测试/轮盘侧消费） | 不反向引用 Host/其它业务模块；托盘等消费方经接口或组合根委托注入；实现见 [interface-theme.md](interface-theme.md) |
| `StarPie.Theme/ThemePaletteManager.cs`（模块根） | M4 主题调色板整项替换：加载/缓存/冻结 `Views/Styles/Themes/*.xaml` 并整项替换 MergedDictionaries 主题槽（B7/#80 裁决 public——Host `AppHost` 装配面，同 B6/#79 `TrayIconManager` 先例） | 主题文件映射/缓存/冻结等实现细节保持私有；不经容器注册 |
| `StarPie.Theme/Views/Styles/Themes/` | M4 五套同 key 集主题画刷令牌 XAML（Light/Dark/MidnightNavy/RoyalViolet/TitaniumGray；Host App.xaml 经 `/StarPie.Theme;component/Views/Styles/Themes/Light.xaml` 静态合并 Light 作设计时/首帧，运行时由 ThemePaletteManager 同源整项替换） | 不放轮盘配色（M2）/ModernControls 控件样式（Host）/HotkeyRecorderBox 样式字典（Gestures） |
| `StarPie.Theme/ViewModels/Pages/` | M4 设置子 VM：`InterfaceThemeSettingsViewModel` + `AppThemeOptionItem`（容器单例，由 `ThemeModuleRegistrar.RegisterServices` 注册；外观聚合 VM 经容器解析注入） | 不得引用 WPF 类型；不得反向引用 Host 类 |
| `StarPie.Theme/Modules/` | 模块注册器 `ThemeModuleRegistrar.cs`（`RegisterServices(IServiceCollection)`；M4 无导航页，无 RegisterNavigation/页面模板字典） | 只注册不解析；不承载业务 |
| `StarPie.Wheel/Models/` | M2 轮盘配色：`WheelPalette`（色值组）、`WheelPaletteCatalog`（唯一 hex 目录）、`WheelPaletteParser`（方案名→色值组解析；WPF-free，B8/#81 自 Core Models 物理收编，语义 R8 归 M2） | 不引用 WPF/服务/VM；`CustomColorPreset` 不在此（Core Models，AppConfig 引用） |
| `StarPie.Wheel/Services/Wheel/` | M2：`WheelGeometry`（视觉几何出口）、`IWheelFactory`/`WheelFactory`（D5 收编；工厂只注册不解析，M1 手势侧经接口消费） | 不反向引用 Host；工厂/几何实现见 [wheel.md](wheel.md) |
| `StarPie.Wheel/ViewModels/Wheel/` | M2：`IWheelViewModel`、`WheelViewModel`、`IWheelAppearanceState`（瞬态，不注册容器；由 WheelFactory 按手势创建） | 不放单例设置 VM（在 Pages） |
| `StarPie.Wheel/ViewModels/Pages/` | M2 外观设置子 VM：`WheelAppearanceSettingsViewModel`（容器单例，由 `WheelModuleRegistrar.RegisterServices` 注册；外观聚合 VM 经容器解析注入） | 不得引用 WPF 类型；不得反向引用 Host/M1 具体 VM（预览 Profile 经 Core `IProfilePreviewSource`） |
| `StarPie.Wheel/Views/Wheel/` | M2：`RadialWindow.xaml(.cs)`（纯视觉呈现与生命周期；WheelFactory 同集创建） | 轮盘状态决策在 VM；窗口只做视觉与生命周期 |
| `StarPie.Wheel/Views/Renderers/` | M2 样式渲染器与预览：`IRadialStyleRenderer`/`StyleRendererFactory`/`BaseStyleRenderer`/各风格渲染器/`WheelPreviewRenderer` | 不订阅事件、不读写 VM、不反向依赖 Composition/Host（深浅色探测由调用方以 bool 传入）；见 [wheel.md](wheel.md) |
| `StarPie.Wheel/Views/Converters/` | M2 核图标预览转换器：`CoreIconGeometryConverter`/`CoreIconNameConverter`（Appearance 聚合页 App 级资源实例，B8/#81 裁决随 M2） | 保持无状态、可静态复用 |
| `StarPie.Wheel/Modules/` | 模块注册器 `WheelModuleRegistrar.cs`（`RegisterServices(IServiceCollection)`；M2 无导航页，无 RegisterNavigation/页面模板字典） | 只注册不解析；不承载业务 |
| `StarPie.Gestures/Services/Gestures/` | M1 手势管线：`MouseHook`（dev 触发键读 Core `AppDataPaths.IsDevInstance` 回填缝）、`GestureController`（App 侧适配器，UI 线程副作用分发）、`GestureEngine`（纯状态机，无 WPF/Win32）、`IWindowContext`/`WindowContext`（Win32 前台/全屏/修饰键） | 引擎不引用 WPF/Win32；不反向引用 Host（dev 分支经 Core 回填）；实现见 [gestures.md](gestures.md) |
| `StarPie.Gestures/Services/Actions/` | M1 动作执行：`IActionExecutorService`/`ActionExecutorService`（系统调用层，构造注入接缝）、`ActionRouting`（纯函数 + `ActionRoute`/`KeyStroke`/`SystemCommand`） | 路由决策不散落 VM/View；系统调用全部经注入接缝；实现见 [gestures.md](gestures.md) |
| `StarPie.Gestures/ViewModels/Gestures/` | M1 方向槽位子 VM：`SlotViewModel`（+ `SystemPresetItem`/`ActionTypeOption`） | 不放服务；槽位经 ProfileListViewModel 重建/Dispose |
| `StarPie.Gestures/ViewModels/Pages/` | M1 设置页 VM：`BehaviorSettingsViewModel`、`ProfileListViewModel`（+ `ProfileItemViewModel`；实现 Core `IProfilePreviewSource`，容器单例，由 `GesturesModuleRegistrar.RegisterServices` 注册并登记别名） | 不得引用 WPF 类型；不得反向引用 Host/M2 具体 VM（轮盘预览经 Core `IProfilePreviewSource` 由 M2 消费） |
| `StarPie.Gestures/Views/Pages/` | M1 页面 View：`TriggerSettingsPage`、`GesturesSettingsPage`（XAML 根直承 `UserControl`，ADR-0022/#94；HotkeyRecorderBox 经本地 `Views/Controls/` 消费） | 不注册容器；不编排业务/写配置/调服务；页面无参构造 |
| `StarPie.Gestures/Modules/` | 正式模块注册器 `GesturesModuleRegistrar.cs`（`RegisterServices(IServiceCollection)` + `RegisterNavigation(NavigationCatalog)`）+ 模块页面模板字典 `GesturesPageTemplates.xaml`（Host App.xaml 经跨程序集 pack URI 单点合并） | 不承载业务；注册器只注册不解析 |

## 根级文件规则

- `App.xaml` / `App.xaml.cs`：只处理单实例、异常、启动、退出和资源释放，不写业务（见 [host.md](host.md)）。
- `Composition.cs`：唯一 DI 组合根——`ServiceCollection` 注册、`BuildServiceProvider`、`CreateAppHost()` 解析；不持有托盘/主窗口/语言字典等宿主状态（见 [host.md](host.md)）。
- `AppHost.cs`：宿主编排——`Run`/`Dispose`、托盘创建与菜单、退出协调、语言资源字典（见 [host.md](host.md)）。
- `DevInstance.cs`：开发实例标记（H1）——`--dev` 隔离互斥/配置目录/触发键并保护正式自启项（见 [host.md](host.md)）。
- `Properties/`、`assets/`：工程配置与二进制资源；**不放 C#/XAML 源码**。
- `StarPie.Core.csproj` / `GlobalUsings.cs`：共享内核工程入口；`StarPie.Core/` 源码根目录**只允许**
  上表列出的共享内核目录与文件（B2/#75 起；含 `Services/AppHostDelegates.cs`（B6/#79 上提）；
  B8/#81 起含 `ViewModels/Pages/IProfilePreviewSource.cs`；ADR-0020/#88 起含 `Services/Programs/`（ProgramEntry/ProgramCatalog/IProgramScanner）；
  ADR-0021/#92 起 `Services/Navigation/` 仅留 `NavigationCatalog.cs`，`ViewModels/Navigation/` 目录在 Core 清零——运行时在 Host；
  ADR-0023/#95 起 `Services/Icons/` 仅留 `IShortcutTargetResolver.cs`（#95 中间态，#96 随 Programs.Contracts 迁出后目录在 Core 清零）；
  ADR-0022/#94 起 `Views/`（共享 UI 基建）目录在 Core 清零——通用转换器/ModernControls.xaml 在 Host
  `Views/Converters|Styles/`、HotkeyRecorderBox（控件+样式字典）在 `StarPie.Gestures/`、共享页面基类 SettingsPageBase 已删除）。
- `StarPie.Icons.Contracts.csproj`：S1 契约程序集工程入口（ADR-0023/#95 起，WPF 类库、零
  ProjectReference）；`StarPie.Icons.Contracts/` 源码根目录**只允许** `Services/Icons/`
  （`IIconAssetService.cs`/`IconCatalog.cs`/`CustomIconItem.cs`/`VectorIconItem.cs`）。
- `StarPie.Icons.csproj`：S1 实现程序集工程入口（ADR-0023/#95 起，WPF 类库；引用
  Icons.Contracts + Core（#95 中间态 `IShortcutTargetResolver`）+ MS.DI 包）；
  `StarPie.Icons/` 源码根目录**只允许** `Modules/`（`IconsModuleRegistrar`）与
  `Services/Icons/`（`IconAssetService.cs`）。
- `StarPie.Dialogs.csproj` / `GlobalUsings.cs`：S6 对话框实现模块程序集工程入口（B11/#88 起，
  ADR-0020：单向引用 Core + 允许引用 Theme（IThemeService 允许边）+ 引用 Icons.Contracts
  （ADR-0023/#95，S1 契约边））；`StarPie.Dialogs/` 源码根目录
  **只允许** `Modules/`（DialogsModuleRegistrar）、`Services/Dialogs/`（DialogService）、
  `ViewModels/Dialogs/`（五对对话框 VM）与 `Views/Dialogs/`、`Views/Controls/`（窗口与取色行为）。
- `StarPie.Programs.csproj`：M3 模块程序集工程入口（B4/#77 起；ADR-0019/#87 起单向引用 Core +
  ADR-0023/#95 起引用 Icons.Contracts）；
  `StarPie.Programs/` 源码根目录**只允许** `Modules/`（`ProgramsModuleRegistrar`）、
  `Services/Programs/`（`ProgramScanner`/`ShortcutResolver`）。
- `StarPie.Shell.csproj` / `GlobalUsings.cs`：M5 模块程序集工程入口（B6/#79 起，单向引用 Core）；
  `StarPie.Shell/` 源码根目录**只允许** `Modules/`、`Services/Shell/`、`ViewModels/Pages/`、
  `Views/Pages/`（仅上表列出的 M5 文件）。
- `StarPie.Theme.csproj` / `GlobalUsings.cs`：M4 界面主题模块程序集工程入口（B7/#80 起，
  单向引用 Core）；`StarPie.Theme/` 源码根目录**只允许** `Modules/`（ThemeModuleRegistrar）、
  根级 `ThemePaletteManager.cs`、`Services/Shell/`（M4 两服务）、`ViewModels/Pages/`
  （M4 主题设置子 VM）与 `Views/Styles/Themes/`（五套主题字典）。
- `StarPie.Wheel.csproj` / `GlobalUsings.cs`：M2 轮盘与渲染模块程序集工程入口（B8/#81 起，
  单向引用 Core + 允许引用 Theme（IThemeService 边）+ 引用 Icons.Contracts（ADR-0023/#95））；
  `StarPie.Wheel/` 源码根目录**只允许**
  `Modules/`（WheelModuleRegistrar）、`Models/`（WheelPalette* 三件）、`Services/Wheel/`
  （WheelGeometry/IWheelFactory/WheelFactory）、`ViewModels/Pages/`（WheelAppearanceSettingsViewModel）、
  `ViewModels/Wheel/`（IWheelViewModel/WheelViewModel/IWheelAppearanceState）、`Views/Wheel/`
  （RadialWindow）、`Views/Renderers/`（渲染器与预览）与 `Views/Converters/`（CoreIcon* 两转换器）。
- `StarPie.Gestures.csproj` / `GlobalUsings.cs`：M1 手势与动作模块程序集工程入口（B9/#82 起，
  单向引用 Core + 允许引用 Wheel（M1→M2 允许边）+ 引用 Icons.Contracts（ADR-0023/#95））；
  `StarPie.Gestures/` 源码根目录**只允许**
  `Modules/`（GesturesModuleRegistrar + GesturesPageTemplates.xaml）、`Services/Gestures/`
  （手势管线五件）、`Services/Actions/`（动作执行三件）、`ViewModels/Gestures/`（SlotViewModel）、
  `ViewModels/Pages/`（BehaviorSettingsViewModel/ProfileListViewModel）、`Views/Controls/`
  （HotkeyRecorderBox.cs，ADR-0022/#94）、`Views/Styles/`（HotkeyRecorderBox.xaml，ADR-0022/#94）
  与 `Views/Pages/`（TriggerSettingsPage/GesturesSettingsPage）。
- 各工程源码根目录**只允许**上表与本小节列出的项；原型、HTML、临时脚本不得留在
  `WinPieGestures/`、`StarPie.Core/`、`StarPie.Dialogs/`、`StarPie.Programs/`、`StarPie.Shell/`、
  `StarPie.Theme/`、`StarPie.Wheel/`、`StarPie.Gestures/`、`StarPie.Icons.Contracts/` 或
  `StarPie.Icons/` 下。

## 现状偏差与待清理项

当前代码与正典目录结构一致（exe `Modules/` 仅余 Host 外观聚合页注册器/模板字典，见下；
M5 已随 B6/#79 迁出，M4 主题件已随 B7/#80 迁出，M2 轮盘件已随 B8/#81 迁出，
M1 手势件已随 B9/#82 迁出）。

### 当前登记（Host 外观聚合页，B3/#76 临时面；B6/#79 迁 M5、B9/#82 迁 M1 后仅剩 Host）

- `WinPieGestures/Modules/`：Host 外观聚合页注册器 `HostModuleRegistrar` 与页面模板字典
  `HostPageTemplates.xaml`（App 级每模块一次静态合并）。外观聚合页目标态留 Host
  （[assemblies.md](assemblies.md) §5.2 槽位 1），B3 以同形临时注册器先行验证“新增页面不碰 Host”
  路径（M5 已随 B6/#79 迁入 `StarPie.Shell/Modules/`、M1 已随 B9/#82 迁入
  `StarPie.Gestures/Modules/`）；本目录维持 Host 形态，属目标态而非待迁出偏差。

已消除的历史偏差（2026-09-04）：

- **ADR-0023/#95（2026-09-09）**：S1 图标资产成集——契约四件（`IIconAssetService.cs`/
  `IconCatalog.cs`/`CustomIconItem.cs`/`VectorIconItem.cs`）自 `StarPie.Core/Services/Icons/`
  迁 `StarPie.Icons.Contracts/Services/Icons/`（命名空间不变，零依赖薄契约）；`IconAssetService.cs`
  迁 `StarPie.Icons/Services/Icons/`，新增 `Modules/IconsModuleRegistrar.cs`；`IShortcutTargetResolver.cs`
  **未随迁**（#95 中间态暂留 `StarPie.Core/Services/Icons/`，#96 随 Programs.Contracts 迁出后
  Core 该目录清零）；Dialogs/Wheel/Gestures/Programs/Host csproj 增 `StarPie.Icons.Contracts`
  显式引用（Host 另引 `StarPie.Icons`），组合根 `AddSingleton<IIconAssetService>(new IconAssetService(...))`
  改调 `IconsModuleRegistrar.RegisterServices`；slnx/测试工程登记两新工程
  （见 [assemblies.md](assemblies.md) §9）。
- **ADR-0022/#94（2026-09-09）**：共享 UI 基建去共享化——4 个通用转换器（HexToBrushConverter/
  StringToGeometryConverter/IntEqualsConverter/FilePathToImageConverter）与全局控件样式字典
  `ModernControls.xaml` 自 `StarPie.Core/Views/` 迁 Host `Views/Converters|Styles/`（App.xaml 改
  本地 xmlns/本地合并，资源 key 不变、运行期消费方零改动）；`HotkeyRecorderBox`（控件+样式段）
  下沉 `StarPie.Gestures/Views/Controls/`，样式段摘为同模块新字典
  `Views/Styles/HotkeyRecorderBox.xaml`（App.xaml 经跨程序集 pack URI 单点合并，跨字典几何令牌
  改 DynamicResource）；共享页面基类 `SettingsPageBase` 删除——Trigger/Gestures/Advanced/
  Appearance/About 五页 XAML 根改 `UserControl`，Trigger/Advanced/Appearance 三页 code-behind 改
  `Loaded`/`Unloaded` 成对自订阅（ADR-0009 白名单第 1 条，与 InputDialog/RadialWindow 同款纪律）；
  Core `Views/`（Converters/Controls/Pages/Styles）目录整体清空移除
  （见 [assemblies.md](assemblies.md) §9）。
- **ADR-0021/#92（2026-09-09）**：导航运行时主体迁 Host——`StarPie.Core/Services/Navigation/`
  的 `NavigationStore.cs`/`NavigationExecutor.cs` → `WinPieGestures/Services/Navigation/`；
  `StarPie.Core/ViewModels/Navigation/` 的 `MainViewModel.cs`/`NavigationItemViewModel.cs` →
  `WinPieGestures/ViewModels/Navigation/`（命名空间不变，与 `ShellViewModel` 同目录族）；
  Core 仅留 `NavigationCatalog.cs`（目录/槽位契约），C1 死代码 `INavigationService.cs`/
  `NavigationService.cs` 删除，Core 侧 `ViewModels/Navigation/` 目录清零
  （见 [assemblies.md](assemblies.md) §9）。
- **ADR-0020/#88（2026-09-08）**：S6 对话框实现迁入独立模块程序集 `StarPie.Dialogs/`——
  DialogService（Services/Dialogs）、五对对话框 VM/Window（ViewModels|Views/Dialogs）与取色行为
  SpectrumCanvasBehavior（Views/Controls）随迁（命名空间沿用 StarPie.* 树，B10/#83 同构）；新增
  DialogsModuleRegistrar（RegisterServices 下放）；M3 扫描契约收口——ProgramEntry/ProgramCatalog/
  IProgramScanner 上提 `StarPie.Core/Services/Programs/`，ProgramScanner 改实例实现，组合根删除
  委托行（S21 归零）；Host 侧 `Services/Dialogs/`、`ViewModels/Dialogs/`、`Views/Dialogs/`、
  `Views/Controls/` 目录清空移除（见 [assemblies.md](assemblies.md) §9）。
- **B9/#82（2026-09-06）**：M1 手势与动作迁入独立模块程序集 `StarPie.Gestures/`——手势管线
  （MouseHook/GestureController/GestureEngine/IWindowContext/WindowContext，Services/Gestures）、
  动作执行（IActionExecutorService/ActionExecutorService/ActionRouting，Services/Actions）、
  触发+手势设置页（BehaviorSettingsViewModel/TriggerSettingsPage、ProfileListViewModel/
  GesturesSettingsPage、SlotViewModel）随迁（命名空间当时维持 WinPieGestures.*，B10/#83
  统一为 StarPie.*）；exe 内 M1 临时注册器/
  模板字典替换为 `GesturesModuleRegistrar` + `GesturesPageTemplates.xaml`（Host App.xaml 经
  跨程序集 pack URI `/StarPie.Gestures;component/Modules/GesturesPageTemplates.xaml` 合并）；
  手势管线/页面 VM/`IProfilePreviewSource` 别名 DI 注册随注册器下放；MouseHook dev 分支改读
  Core `AppDataPaths.IsDevInstance`（不反向引用 Host）；Host/Tests 显式引用、slnx 登记；
  Host 侧 `Services/Actions/`、`Services/Gestures/`、`ViewModels/Gestures/` 目录清空移除
  （见 [assemblies.md](assemblies.md) §9）。
- **B8/#81（2026-09-06）**：M2 轮盘与渲染迁入独立模块程序集 `StarPie.Wheel/`——轮盘 VM
  （ViewModels/Wheel）、RadialWindow（Views/Wheel）、样式渲染器与实时预览（Views/Renderers）、
  轮盘配色 WheelPalette*/WheelPaletteParser/WheelPaletteCatalog（自 Core Models 物理收编
  StarPie.Wheel/Models）、WheelGeometry（Services/Wheel）、轮盘工厂 IWheelFactory/WheelFactory
  （Services/Wheel，命名空间 `WinPieGestures.Services.Gestures` → `WinPieGestures.Services.Wheel`
  与物理目录一致）与核图标预览转换器 CoreIconGeometryConverter/CoreIconNameConverter
  （Views/Converters，B5/#78 暂留 Host 的归属裁决：随 M2）随迁（命名空间当时维持
  WinPieGestures.*，B10/#83 统一为 StarPie.*）；
  外观设置子 VM WheelAppearanceSettingsViewModel 随迁 ViewModels/Pages；预览 Profile 只读契约
  IProfilePreviewSource 上提 `StarPie.Core/ViewModels/Pages/`（D5）；新增 `WheelModuleRegistrar`
  （RegisterServices 下放 M2 的 DI 注册，M2 无导航页）；Host/Tests 显式引用、slnx 登记；
  WheelPreviewRenderer 深浅色探测改由外观页以 bool 传入（不再引用 Host MainView）；
  Host 侧 `Services/Wheel/`、`ViewModels/Wheel/`、`Views/Wheel/`、`Views/Renderers/`、
  `Views/Converters/` 目录清空移除（见 [assemblies.md](assemblies.md) §9）。
- **B7/#80（2026-09-06）**：M4 界面主题体系迁入独立模块程序集 `StarPie.Theme/`——
  IThemeService/ThemeService（Services/Shell）、ThemePaletteManager（模块根，internal → public，
  Host AppHost 装配面）、五套主题字典（Views/Styles/Themes）、InterfaceThemeSettingsViewModel +
  AppThemeOptionItem（ViewModels/Pages）随迁（命名空间不变）；Host App.xaml 对 Light 字典改经
  跨程序集 pack URI `/StarPie.Theme;component/Views/Styles/Themes/Light.xaml` 静态合并、
  ThemePaletteManager 加载源同步改集；新增 `ThemeModuleRegistrar`（RegisterServices 下放 M4
  的 DI 注册，M4 无导航页）；Host/Tests 显式引用、slnx 登记；Host 侧 `Services/Shell/` 与
  `Views/Styles/Themes/` 目录清空移除（见 [assemblies.md](assemblies.md) §9）。
- **B6/#79（2026-09-06）**：M5 壳层服务与系统设置面迁入首个带 DI 的独立模块程序集
  `StarPie.Shell/`——TrayIconManager/AutostartRegistry/MemoryOptimizer、GeneralSettingsViewModel+
  AdvancedSettingsPage、AboutViewModel+AboutSettingsPage 随迁（命名空间不变）；共享页面基类
  `SettingsPageBase` 迁入 `StarPie.Core/Views/Pages/`；宿主回调委托包 `AppHostDelegates` 上提
  `StarPie.Core/Services/`；exe 内 M5 临时注册器/模板字典替换为 `ShellModuleRegistrar` +
  `ShellPageTemplates.xaml`（Host App.xaml 经跨程序集 pack URI 合并）；Host/Tests 显式引用、
  slnx 登记（见 [assemblies.md](assemblies.md) §9；其中 `SettingsPageBase` 已随 ADR-0022/#94
  删除，见上方首条）。
- **B5/#78（2026-09-06）**：共享 UI 基建迁共享内核——通用转换器（`HexToBrushConverter`/
  `StringToGeometryConverter`/`IntEqualsConverter`/`FilePathToImageConverter`）、共享自定义控件
  `HotkeyRecorderBox` 与全局控件样式字典 `ModernControls.xaml` 迁入 `StarPie.Core/Views/`
  （Converters/Controls/Styles，命名空间不变）；Host `App.xaml` 对该字典改经跨程序集 pack URI
  单点合并；M2 核图标预览转换器（CoreIconGeometry/Name）与 S6 取色对话框行为
  （SpectrumCanvasBehavior）因宿主/M2 依赖暂留 Host（B8 前 Core 不得反向依赖宿主，见
  [assemblies.md](assemblies.md) §9）；B8/#81 起其中 CoreIcon* 两转换器已随 M2 收编
  `StarPie.Wheel/Views/Converters/`（见本表 B8 条目），SpectrumCanvasBehavior 仍留 Host（B11/#88
  起已随 S6 实现迁 `StarPie.Dialogs`，见下方 ADR-0020/#88 条目）。**上述迁入 Core 的 UI 件已于
  ADR-0022/#94 全部去共享化**（转换器/ModernControls.xaml → Host、HotkeyRecorderBox → Gestures），
  见上方首条。
- **ADR-0019/#87（2026-09-08）**：S1 图标资产双形拆分——`IconAssets.cs` 拆为静态纯目录
  `IconCatalog.cs`（矢量清单/SVG 键目录/`ExtractSvgPathData`）+ 实例服务
  `IIconAssetService.cs`/`IconAssetService.cs`（自定义图标存储/位图源/`GetIcon`）+ 独立
  `CustomIconItem.cs`，`.lnk` 契约 `IShortcutTargetResolver.cs` 新增并驻 Core；M3 边界收口——
  `StarPie.Programs` 增引用 Core（M3 → Core 单向），`ShortcutResolver` 改实例实现契约，
  `ProgramScanner` 签名改经 Core 契约注入，新增 `StarPie.Programs/Modules/ProgramsModuleRegistrar.cs`；
  Host 组合根删除 `IconAssets.ResolveShortcutTarget` 静态回填行并注册
  `IIconAssetService`；WheelPreviewRenderer 经外观聚合 VM 预览桥装配
  （见 [assemblies.md](assemblies.md) §9 / [modules.md](modules.md) §3 S1）。
- **B4/#77（2026-09-06）**：M3 三件（`ProgramScanner`/`ProgramCatalog`/`ShortcutResolver`）与
  `ProgramEntry` 迁入首个独立模块程序集 `StarPie.Programs/`（WPF 类库，程序集 `StarPie.Programs`）；
  Host/Tests 显式引用、slnx 登记；M3 零 Core 依赖——扫描结果的图标补全改经组合根注入的 S1
  `IconAssets.GetIcon` 委托（见 [programs.md](programs.md)）。
- **B2/#75（2026-09-06）**：共享内核件（Models/S2/S3/S4/S1/S6 契约/S5 导航内核 + NavigationCatalog/
  槽位表）迁入 `StarPie.Core/`，Host exe 显式引用 Core；跨程序集回填缝（`AppDataPaths.IsDevInstance`、
  `IconAssets.ResolveShortcutTarget`）由组合根装配前回填。
- 页面/侧栏各自合并 `SettingsStyles.xaml`（7 处）收敛为 App 级单点合并 `ModernControls.xaml`；样式资源字典四层化（主题令牌/排版/控件样式/宿主装配，见 [ADR-0012](../adr/0012-resource-dictionary-architecture.md)）。
- 删除空目录 `WinPieGestures/Controls/`（自定义控件统一在 `Views/Controls/`）。
- 移除源码根杂项 `DashboardPrototype.html`（原型/杂项不进源码根）。
- 把 code-only 的 `ScreenEyedropperOverlay`（位于 `ColorPickerWindow.xaml.cs` 的 `#region`）拆分为独立 `Views/Dialogs/ScreenEyedropperWindow.xaml(.cs)`（见 [dialogs.md](dialogs.md)/[naming.md](naming.md)）。
- `Models/ColorMath.cs` 含 `RgbColor` 值类型：符合 [layering.md](layering.md)（Models）定义，确认为非偏差保留。

长期接受的例外（新代码不得新增同类）：

- `InputViewModel ↔ InputDialog`：遗留命名错位，见 [naming.md](naming.md)。
- 页面 VM 与页面 View 的领域/区块命名错位（`GeneralSettingsViewModel → AdvancedSettingsPage` 等）：**允许且是正典**，见 [naming.md](naming.md)。
