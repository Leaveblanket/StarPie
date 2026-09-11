# 目录与文件架构

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；需要确认“某个路径放什么 / 新增文件落在哪”时读本篇。

## Canonical 目录树

以下为**应然结构**（正典）；当前代码与正典一致（exe `Modules/` 仅余 Host 外观聚合页，见文末“当前登记”）。

```text
StarPie/
├── StarPie.slnx                   # 解决方案（登记全部工程；构建/测试入口）
├── Directory.Build.props          # 统一构建属性（TFM/可空性/隐式 using/分析器级别/根命名空间）
├── Directory.Packages.props       # 中央包管理（包版本唯一集中处；csproj 不写版本）
├── StarPie.Ui/             # Ui 集（WinExe，程序集名保持 StarPie；唯一含 XAML 与入口）：组合根、宿主壳窗口、导航运行时、外观聚合页
│   ├── App.xaml / App.xaml.cs     # 宿主生命周期：单实例、异常、启动/退出编排
│   ├── AppHost.cs                 # 宿主编排：Run/Dispose、托盘、语言资源、退出协调
│   ├── Composition.cs             # DI 组合根（唯一）：注册与解析（含跨程序集回填缝，见 layering.md）
│   ├── DevInstance.cs             # 开发实例标记（H1）：--dev 互斥/触发键/自启保护
│   ├── Modules/                   # Host 外观聚合页：HostModuleRegistrar + HostPageTemplates.xaml
│   ├── AssemblyInfo.cs            # 程序集元数据
│   ├── GlobalUsings.cs            # 工程级全局 using
│   ├── StarPie.Ui.csproj          # Ui 集工程文件（#111 起目录/文件名 StarPie.Ui，程序集名仍为 StarPie）
│   ├── Properties/
│   │   ├── DesignTimeResources.xaml  # 设计期资源锚（仅设计期合并，见 design-time-preview.md）
│   │   └── launchSettings.json    # 工程配置；不放源码
│   ├── assets/
│   │   ├── app_icon.ico           # 应用图标（csproj ApplicationIcon 引用）
│   │   └── logo.png
│   ├── Services/
│   │   └── Navigation/            # 导航运行时：NavigationStore、NavigationExecutor（含 INavigationExecutor）
│   ├── ViewModels/
│   │   ├── Pages/                 # Host 外观聚合页 VM：AppearanceSettingsViewModel（单例）
│   │   └── Navigation/            # 导航 VM：MainViewModel、NavigationItemViewModel、ShellViewModel（D3 Host 壳层 VM）
│   └── Views/
│       ├── Converters/            # 通用共享转换器：HexToBrush/StringToGeometry/IntEquals/FilePathToImage（App.xaml 单点实例化）
│       ├── DesignTime/            # 设计期样例类型（仅 d:DataContext 消费；见 design-time-preview.md）
│       ├── Pages/                 # Host 外观聚合页 View：AppearanceSettingsPage（M1/M5 页面分别在 StarPie.Gestures/StarPie.Shell）
│       ├── Styles/                # ModernControls.xaml 全局控件样式字典（App.xaml 本地合并）
│       └── Navigation/            # MainView、SidebarView
├── StarPie.Sdk/                    # SDK 集（net10.0；零 WPF 零第三方包；P1.3/#112 起迁入纯托管契约/模型/DTO）
│   ├── StarPie.Sdk.csproj         # 零 ProjectReference（引用面只有平台程序集，见 plugins.md §2）
│   ├── Models/                    # 稳定 DTO 与 WPF-free 值类型：AppConfig/WheelProfile/ActionItem/CustomColorPreset/ColorMath/GesturePoint
│   ├── Services/
│   │   ├── AppHostDelegates.cs    # 宿主回调委托包契约（Host 组合根注册单例、AppHost 回填）
│   │   ├── Messages/              # S4：IMessenger 消息与跨层通知载体（Messages.cs/Notices.cs）
│   │   ├── Navigation/            # S5：目录/槽位契约——NavigationCatalog/NavigationSlots（槽位表 0–3；运行时在 Host，仅此文件）
│   │   ├── Dialogs/               # S6 契约：IDialogService + 6 结果 record（原 Dialogs.Contracts）
│   │   └── Wheel/                 # M2 契约：IWheelFactory（原 Wheel.Contracts）
│   └── ViewModels/
│       ├── Pages/                 # M1 契约：IProfilePreviewSource（原 Gestures.Contracts）
│       └── Wheel/                 # M2 契约：IWheelViewModel/IWheelAppearanceState（原 Wheel.Contracts）
│                                  # 迁移期落位：源码镜像旧相对路径、命名空间保持 StarPie.* 不变（零 API 抖动），
│                                  #   导出面与全仓类型唯一性由 StarPie.Tests/SdkBoundaryTests 收口
├── StarPie.Sdk.Wpf/                # SDK 的 WPF 类型契约面（UseWPF；P1.2/#111 骨架，P1.4/#113 起承载 WPF 契约件）
│   ├── StarPie.Sdk.Wpf.csproj     # 唯一 ProjectReference 允许指向 StarPie.Sdk（不产出 XAML）
│   ├── Services/
│   │   ├── Icons/                 # 图标契约四件（IIconAssetService/IconCatalog/CustomIconItem/VectorIconItem，原 Icons.Contracts）+ SPI IShortcutTargetResolver（原 Programs.Contracts）
│   │   ├── Programs/              # M3 扫描契约：IProgramScanner.cs、ProgramEntry.cs、ProgramCatalog.cs（原 Programs.Contracts）
│   │   └── Shell/                 # M4 契约：IThemeService.cs（原 Theme.Contracts）
│   └── Compatibility/             # UiSdkAbi（主次版本/兼容判定）+ DefaultAlcPolicy（默认 ALC 统一加载）政策骨架
│                                  # 迁移期落位：源码镜像旧相对路径、命名空间保持 StarPie.* 不变（零 API 抖动），
│                                  #   导出面与 ABI 政策由 StarPie.Tests/SdkWpfBoundaryTests 收口
├── StarPie.Host/                   # 宿主内核（net10.0；零 WPF，可 headless 单测；P1.2/#111 骨架，P1.5 起迁入运行时）
│   └── StarPie.Host.csproj        # ProjectReference 只许 StarPie.Sdk（不引用 StarPie.Sdk.Wpf）
├── StarPie.Core/                  # 共享内核（WPF 类库，程序集 StarPie.Core；命名空间 StarPie.*；P1.3/#112 后只余 S2/S3 运行时件）
│   ├── StarPie.Core.csproj        # SDK 工程文件（RootNamespace=StarPie；resx 生成器配置于此）；引用 StarPie.Sdk 取契约/模型
│   ├── GlobalUsings.cs            # 工程级全局 using（仅 Core 命名空间）
│   └── Services/
│       ├── Configuration/         # S2：配置读写、防抖保存、AppDataPaths（dev 分支经组合根回填；P1.5 起迁 Host 内核）
│       └── Localization/          # S3：ILocalizationService + Strings*.resx（四语言）+ 设计期投影字典 DesignTimeStrings.xaml（Page 编译）与生成脚本（见 design-time-preview.md；P1.5 起迁 Host 内核）
├── StarPie.Icons.Contracts/       # S1 图标契约工程（P1.4/#113 契约迁 StarPie.Sdk.Wpf/Services/Icons/ 后暂留空壳，待 P1.9/#118 撤销）
│   └── StarPie.Icons.Contracts.csproj  # 空壳：无源码、无 ProjectReference
├── StarPie.Icons/                 # S1 图标实现程序集（WPF 类库，程序集 StarPie.Icons；命名空间 StarPie.*；ADR-0023 起；单向 Sdk + Sdk.Wpf（契约面）+ Core（S2 AppDataPaths））
│   ├── StarPie.Icons.csproj       # SDK 工程文件（RootNamespace=StarPie；引用 Sdk + Sdk.Wpf（图标契约四件与条目类型、SPI IShortcutTargetResolver，P1.4/#113）+ Core（S2 AppDataPaths）；MS.DI 包——注册器用）
│   ├── Modules/                   # IconsModuleRegistrar.cs（RegisterServices；S1 无导航页/模板字典）
│   └── Services/Icons/            # S1：IconAssetService.cs（自定义图标存储/位图源/GetIcon）
├── StarPie.Programs.Contracts/    # M3 扫描/SPI 契约工程（P1.4/#113 契约迁 StarPie.Sdk.Wpf/Services/{Programs,Icons}/ 后暂留空壳，待 P1.9/#118 撤销）
│   └── StarPie.Programs.Contracts.csproj  # 空壳：无源码、无 ProjectReference
├── StarPie.Dialogs.Contracts/     # S6 对话框契约工程（契约 P1.3/#112 迁 StarPie.Sdk/Services/Dialogs/ 后暂留空壳，待 P1.10 撤销）
│   └── StarPie.Dialogs.Contracts.csproj  # 空壳：无源码、无 ProjectReference
├── StarPie.Dialogs/               # S6 对话框实现模块程序集（WPF 类库，程序集 StarPie.Dialogs；命名空间 StarPie.*）
│   ├── StarPie.Dialogs.csproj     # SDK 工程文件（RootNamespace=StarPie；引用 Sdk（S6 契约）+ Sdk.Wpf（程序扫描/SPI、图标与主题契约面，P1.4/#113）+ Core（S2/S3/S4））
│   ├── GlobalUsings.cs            # 工程级全局 using（模块所需 Core/M4 命名空间）
│   ├── Properties/                # DesignTimeResources.xaml（设计期资源锚；见 design-time-preview.md）
│   ├── Modules/                   # DialogsModuleRegistrar.cs（RegisterServices；S6 无导航页/模板字典）
│   ├── Services/Dialogs/          # S6：DialogService（public——Host SetOwner 装配面）
│   ├── ViewModels/Dialogs/        # S6：五对对话框 VM（ProgramPicker/IconPicker/Input/ColorPicker/ScreenEyedropper）
│   └── Views/
│       ├── Dialogs/               # S6：五对对话框 Window.xaml(.cs)
│       ├── DesignTime/            # 设计期样例类型（仅 d:DataContext 消费；见 design-time-preview.md）
│       └── Controls/              # S6：SpectrumCanvasBehavior（取色对话框专用）
├── StarPie.Programs/              # M3 模块程序集（WPF 类库，程序集 StarPie.Programs；命名空间 StarPie.*）
│   ├── StarPie.Programs.csproj    # SDK 工程文件（RootNamespace=StarPie；引用 Sdk.Wpf（契约面，P1.4/#113；M3 → 契约单向，不引用 Core））
│   ├── Modules/                   # 模块注册器：ProgramsModuleRegistrar.cs（RegisterServices）
│   └── Services/Programs/         # M3：ProgramScanner（IO 扫描）、ShortcutResolver（契约本体驻 StarPie.Sdk.Wpf/Services/Programs/，P1.4/#113）
├── StarPie.Shell/                 # M5 壳层模块程序集（WPF 类库，程序集 StarPie.Shell；命名空间 StarPie.*；单向 Sdk + Core）
│   ├── StarPie.Shell.csproj       # SDK 工程文件（RootNamespace=StarPie；引用 Sdk（对话框契约/导航目录/模型）+ Core）
│   ├── GlobalUsings.cs            # 工程级全局 using（模块所需 Core 命名空间）
│   ├── Properties/                # DesignTimeResources.xaml（设计期资源锚；见 design-time-preview.md）
│   ├── Modules/                   # ShellModuleRegistrar.cs（RegisterServices+RegisterNavigation）+ ShellPageTemplates.xaml
│   ├── Services/Shell/            # M5：TrayIconManager(+TrayMenuEntry)、AutostartRegistry、MemoryOptimizer
│   ├── ViewModels/Pages/          # M5：GeneralSettingsViewModel
│   └── Views/Pages/               # M5：AdvancedSettingsPage（根直承 UserControl）
├── StarPie.Theme.Contracts/       # M4 界面主题契约工程（P1.4/#113 契约迁 StarPie.Sdk.Wpf/Services/Shell/ 后暂留空壳，待 P1.8/#117 撤销）
│   └── StarPie.Theme.Contracts.csproj  # 空壳：无源码、无 ProjectReference
├── StarPie.Theme/                 # M4 界面主题模块程序集（WPF 类库，程序集 StarPie.Theme；命名空间 StarPie.*；单向 Sdk + Sdk.Wpf + Core）
│   ├── StarPie.Theme.csproj       # SDK 工程文件（RootNamespace=StarPie；引用 Sdk（配置/消息模型）+ Sdk.Wpf（IThemeService 契约面，P1.4/#113）+ Core）
│   ├── GlobalUsings.cs            # 工程级全局 using（模块所需 Core 命名空间）
│   ├── Modules/                   # ThemeModuleRegistrar.cs（RegisterServices；M4 无导航页/模板字典）
│   ├── AppThemePaletteManager.cs     # 主题调色板整项替换（模块根，public——Host AppHost 装配面）
│   ├── Services/Shell/            # M4：ThemeService.cs（实现 IThemeService，契约驻 StarPie.Sdk.Wpf/Services/Shell/，P1.4/#113；命名空间 StarPie.Services.Shell）
│   ├── ViewModels/Pages/          # M4：InterfaceThemeSettingsViewModel、AppThemeOptionItem
│   └── Views/Styles/Themes/       # M4：五套同 key 集主题画刷令牌（Light/Dark/MidnightNavy/RoyalViolet/TitaniumGray）
├── StarPie.Wheel.Contracts/       # M2 轮盘契约工程（契约 P1.3/#112 迁 StarPie.Sdk/Services|ViewModels/Wheel/ 后暂留空壳，待 P1.7 撤销）
│   └── StarPie.Wheel.Contracts.csproj  # 空壳：无源码、无 ProjectReference
├── StarPie.Wheel/                 # M2 轮盘与渲染模块程序集（WPF 类库，程序集 StarPie.Wheel；命名空间 StarPie.*；单向 Sdk + Sdk.Wpf + Core）
│   ├── StarPie.Wheel.csproj       # SDK 工程文件（RootNamespace=StarPie；引用 Sdk（轮盘/预览/对话框契约与模型）+ Sdk.Wpf（IThemeService 与 S1 图标契约面，P1.4/#113）+ Core）
│   ├── GlobalUsings.cs            # 工程级全局 using（模块所需 Core/M2 命名空间）
│   ├── Properties/                # DesignTimeResources.xaml（设计期资源锚；见 design-time-preview.md）
│   ├── Modules/                   # WheelModuleRegistrar.cs（RegisterServices；M2 无导航页/模板字典）
│   ├── Models/                    # M2：轮盘配色 WheelPalette.cs/WheelPaletteCatalog.cs/WheelPaletteParser.cs（WPF-free）
│   ├── Services/Wheel/            # M2：WheelGeometry.cs（视觉几何出口）、WheelFactory.cs（实现 IWheelFactory，契约驻 StarPie.Sdk；命名空间 StarPie.Services.Wheel）
│   ├── ViewModels/
│   │   ├── Pages/                 # M2：WheelAppearanceSettingsViewModel（外观设置子 VM，单例；实现 IWheelAppearanceState）
│   │   └── Wheel/                 # M2：WheelViewModel.cs（实现 IWheelViewModel，契约驻 StarPie.Sdk；瞬态）
│   ├── Views/
│   │   ├── Wheel/                 # M2：RadialWindow.xaml(.cs)
│   │   ├── Renderers/             # M2：IRadialStyleRenderer/StyleRendererFactory/BaseStyleRenderer/各风格渲染器/WheelPreviewRenderer
│   │   └── Converters/            # M2：CoreIconGeometryConverter/CoreIconNameConverter（核图标预览，随 M2）
├── StarPie.Gestures.Contracts/    # M1 预览 Profile 契约工程（契约 P1.3/#112 迁 StarPie.Sdk/ViewModels/Pages/ 后暂留空壳，待 P1.6 撤销）
│   └── StarPie.Gestures.Contracts.csproj  # 空壳：无源码、无 ProjectReference
├── StarPie.Gestures/              # M1 手势与动作模块程序集（WPF 类库，程序集 StarPie.Gestures；命名空间 StarPie.*；单向 Sdk + Sdk.Wpf + Core）
│   ├── StarPie.Gestures.csproj    # SDK 工程文件（RootNamespace=StarPie；引用 Sdk（预览/轮盘/对话框契约与模型）+ Sdk.Wpf（S1 图标契约面，P1.4/#113）+ Core）
│   ├── GlobalUsings.cs            # 工程级全局 using（模块所需 Core/M1/M2 命名空间）
│   ├── Properties/                # DesignTimeResources.xaml（设计期资源锚；见 design-time-preview.md）
│   ├── Modules/                   # GesturesModuleRegistrar.cs（RegisterServices+RegisterNavigation）+ GesturesPageTemplates.xaml
│   ├── Services/Gestures/         # M1：MouseHook、GestureController、GestureEngine（+ GestureState/GestureReleaseResult）、IWindowContext/WindowContext
│   ├── Services/Actions/          # M1：IActionExecutorService/ActionExecutorService、ActionRouting（+ ActionRoute/KeyStroke/SystemCommand）
│   ├── ViewModels/
│   │   ├── Pages/                 # M1：BehaviorSettingsViewModel、ProfileListViewModel（+ ProfileItemViewModel；实现 IProfilePreviewSource，契约驻 StarPie.Sdk）
│   │   └── Gestures/              # M1：SlotViewModel（+ SystemPresetItem/ActionTypeOption）
│   ├── Views/
│   │   ├── Controls/              # 热键录制控件 HotkeyRecorderBox.cs（唯一消费方 GesturesSettingsPage）
│   │   ├── DesignTime/            # 设计期样例类型（仅 d:DataContext 消费；见 design-time-preview.md）
│   │   ├── Pages/                 # M1：TriggerSettingsPage、GesturesSettingsPage（根直承 UserControl）
│   │   └── Styles/                # HotkeyRecorderBox.xaml 热键录制控件隐式默认样式字典（App.xaml 经 pack URI 合并）
└── StarPie.Tests/          # xUnit 单测（显式引用四集、Core、Dialogs、Programs、Shell、Theme、Wheel、Gestures、Icons 与六个被收口契约工程（Dialogs/Gestures/Wheel/Icons/Programs/Theme.Contracts，空壳；见 SdkBoundaryTests））
```
> 程序集归属：目录名在所属工程内各自保持“命名空间 = 物理目录”（跨程序集共享同一棵
> `StarPie.*` 命名空间树）；各程序集的承载与依赖方向见 [assemblies.md](assemblies.md) §2/§3，
> 本目录树为物理路径正典。

## 各目录职责细则

| 目录 | 存放什么 | 不放什么 / 常见违规 |
|---|---|---|
| `Models/` | `StarPie.Sdk/`（P1.3/#112 自 Core 迁入）：配置 POCO（`AppConfig`/`WheelProfile`/`ActionItem`/`CustomColorPreset`——AppConfig 引用）与 WPF-free 值类型/纯函数（`RgbColor`/`ColorMath`/`GesturePoint`）；`StarPie.Wheel/Models/`：轮盘配色 `WheelPalette`/`WheelPaletteCatalog`/`WheelPaletteParser`（WPF-free） | 不引用 WPF 类型、服务、命令、消息、IMessenger；不放可注入服务、文件 IO、静态 Win32 工具 |
| `Services/{Feature}/` | 该功能的服务接口与实现（同目录）、编排器、纯函数、进程内 DTO | 不放 VM/View；静态工具需符合 [layering.md](layering.md)（Services） |
| `Services/Actions/` | `StarPie.Gestures/Services/Actions/`：`IActionExecutorService`/`ActionExecutorService`（系统调用层）、`ActionRouting`（纯函数 + `ActionRoute`/`KeyStroke`） | 路由决策不得散落 VM/View；实现见 [gestures.md](gestures.md) |
| `Services/Configuration/` | `StarPie.Core/`：`IConfigService`/`JsonConfigService`、`ISaveDebouncer`/`DispatcherSaveDebouncer`、`SettingsSaveOrchestrator`、`AppDataPaths`（dev 分支经组合根回填） | 页面 VM 不得直接碰配置文件路径或 `JsonSerializer`；实现见 [config.md](config.md) |
| `Services/Dialogs/` | 契约 `IDialogService` + 结果 record 在 `StarPie.Sdk`（P1.3/#112 自 Dialogs.Contracts 收口）；实现 `DialogService` 在 `StarPie.Dialogs` | 对话框 Window/VM 不在此；文件对话框/MessageBox 不暴露给 VM/View |
| `Services/Gestures/` | `StarPie.Gestures/Services/Gestures/`：`MouseHook`、`GestureController`、`GestureEngine`、`IWindowContext`/`WindowContext`（`WheelFactory` 属 M2，见 `Services/Wheel/` 行） | 手势判定纯逻辑不得引用 WPF/Win32；实现见 [gestures.md](gestures.md) |
| `Services/Icons/` | 契约四件与 .lnk 契约 `IShortcutTargetResolver` 在 `StarPie.Sdk.Wpf/Services/Icons/`（P1.4/#113 收口）；实现 `IconAssetService` 在 `StarPie.Icons` | 几何/程序解析类入口不在此（归属见 [modules.md](modules.md) §3 S1）；有状态/IO/Win32 面只经实例服务注入 |
| `Services/Localization/` | `StarPie.Core/`：`ILocalizationService`/`LocalizationService` + `Strings*.resx`；设计期投影 `DesignTimeStrings.xaml`（Page 编译签入生成物）与生成脚本同目录（ADR-0025 例外） | VM/View 不得另建文案字典；设计期字典仅由 resx 派生；实现见 [localization.md](localization.md)、[design-time-preview.md](design-time-preview.md) |
| `Services/Messages/` | `StarPie.Sdk/`（P1.3/#112 自 Core 迁入）：`Messages.cs`（IMessenger 消息）、`Notices.cs`（`NoticeKind`/`NoticeRequest`） | 同页状态不得用消息替代绑定 |
| `Services/Navigation/` | `StarPie.Sdk/`（P1.3/#112 自 Core 迁入）：目录/槽位契约 `NavigationCatalog`（`NavigationCatalog.cs`）；Host：导航运行时 `NavigationStore`/`NavigationExecutor`（含 `INavigationExecutor`） | 页面状态不得散落导航器之外；实现见 [navigation.md](navigation.md) |
| `Services/Wheel/` | `StarPie.Wheel/Services/Wheel/`：`WheelGeometry`、`WheelFactory`（契约 `IWheelFactory` 驻 `StarPie.Sdk`，P1.3/#112 收口） | 工厂只经 SDK 契约被 M1 消费；实现见 [wheel.md](wheel.md) |
| `ViewModels/Pages/` | Host：`AppearanceSettingsViewModel`；`StarPie.Gestures`：`BehaviorSettingsViewModel`/`ProfileListViewModel`；`StarPie.Shell`：`GeneralSettingsViewModel`；`StarPie.Theme`：`InterfaceThemeSettingsViewModel`/`AppThemeOptionItem`；`StarPie.Wheel`：`WheelAppearanceSettingsViewModel`；`StarPie.Sdk/ViewModels/Pages/`：`IProfilePreviewSource`（P1.3/#112 自 Gestures.Contracts 收口） | 不得引用 WPF 类型；不得出现 `event Action` 临时事件 |
| `ViewModels/Dialogs/` | `StarPie.Dialogs/ViewModels/Dialogs/`：`{Dialog}ViewModel`（含 `ScreenEyedropperViewModel`） | 不得持有 Window/MessageBox/对话框类型；形态见 [dialogs.md](dialogs.md) |
| `ViewModels/Gestures/` | `StarPie.Gestures/ViewModels/Gestures/`：`SlotViewModel`（+ `SystemPresetItem`/`ActionTypeOption`） | 不放服务 |
| `ViewModels/Navigation/` | Host：`NavigationItemViewModel`、`MainViewModel`（目录驱动）、`ShellViewModel` | 导航项文案/图标规则见 [navigation.md](navigation.md) |
| `ViewModels/Wheel/` | `StarPie.Wheel/ViewModels/Wheel/`：`WheelViewModel`（契约 `IWheelViewModel`/`IWheelAppearanceState` 驻 `StarPie.Sdk`，P1.3/#112 收口） | 不注册容器；按手势由 `WheelFactory` 瞬态创建 |
| `Views/Pages/` | Host：`AppearanceSettingsPage`；`StarPie.Gestures`：`TriggerSettingsPage`/`GesturesSettingsPage`；`StarPie.Shell`：`AdvancedSettingsPage`（XAML 根直承 `UserControl`） | 不注册容器；不编排业务/写配置/调服务；页面无参构造 |
| `Views/Dialogs/` | `StarPie.Dialogs/Views/Dialogs/`：`{Dialog}Window.xaml(.cs)`（对话框唯一形态） | 例外见 [naming.md](naming.md)；不放无配对 Window 的散件 |
| `Views/Navigation/` | Host：`MainView`（纯壳）、`SidebarView` | 其它窗口/页面不得再合并样式字典（样式已 App 级单点合并） |
| `Views/Wheel/` | `StarPie.Wheel/Views/Wheel/`：`RadialWindow` | 状态决策在 `WheelViewModel`；窗口只做视觉呈现与生命周期 |
| `Views/Controls/` | `StarPie.Dialogs/Views/Controls/`：`SpectrumCanvasBehavior`；`StarPie.Gestures/Views/Controls/`：`HotkeyRecorderBox`（样式字典在 `Views/Styles/`） | 有 `Command`/绑定等价物时不得新增行为 |
| `Views/Converters/` | Host：通用共享转换器（`HexToBrush`/`StringToGeometry`/`IntEquals`/`FilePathToImage`，App.xaml App 级单点持有）；`StarPie.Wheel/Views/Converters/`：`CoreIconGeometryConverter`/`CoreIconNameConverter` | 转换器保持无状态、可静态复用 |
| `Views/DesignTime/` | `StarPie.Ui/`、`StarPie.Gestures/`、`StarPie.Dialogs/`：设计期样例类型（命名空间 `StarPie.Views.DesignTime`，仅被根节点 `d:DataContext` 消费，见 design-time-preview.md） | 不放运行时 VM/服务；运行时代码不得引用 |
| `Views/Renderers/` | `StarPie.Wheel/Views/Renderers/`：`IRadialStyleRenderer`/`StyleRendererFactory`/`BaseStyleRenderer`/各风格渲染器/`WheelPreviewRenderer`；渲染器只消费 `WheelPalette` 解析结果构造画刷 | 渲染器不订阅事件、不读写 VM、不反向依赖 Composition/服务；深浅色探测由调用方以 bool 传入（见 [wheel.md](wheel.md)） |
| `Modules/` | exe 内 Host 外观聚合页：`HostModuleRegistrar`（RegisterNavigation）+ `HostPageTemplates.xaml`；`StarPie.Shell/Modules/`：`ShellModuleRegistrar` + `ShellPageTemplates.xaml`；`StarPie.Gestures/Modules/`：`GesturesModuleRegistrar` + `GesturesPageTemplates.xaml`；其余模块注册器在各自工程 `Modules/` | 不承载业务；注册器只注册不解析 |



### 共享 UI 基建落点（已去共享化）

> `StarPie.Core/Views/` 目录（Converters/Controls/Pages/Styles）已随去共享化**整体清空移除**；
> 资源键集不变——模块 XAML 消费方（`{StaticResource}` 运行期解析）零改动。

| 目录 | 存放什么 | 不放什么 / 常见违规 |
|---|---|---|
| `StarPie.Ui/Views/Converters/` | Host 通用共享转换器：`HexToBrushConverter`（hex→Brush，配 SDK `Models/RgbColor`）、`StringToGeometryConverter`（SVG 路径→Geometry）、`IntEqualsConverter`、`FilePathToImageConverter`（本地图片→缩略图）；实例由 Host `App.xaml` App 级单点持有（ADR-0012 决策 5） | 不放业务模块专用转换器（M2 核图标预览转换器在 `StarPie.Wheel/Views/Converters/`） |
| `StarPie.Ui/Views/Styles/` | Host `ModernControls.xaml` 全局控件样式字典（隐式默认/键控变体/共享模板；App.xaml **本地合并**；几何令牌经 DynamicResource 供跨字典模板引用） | 不放主题画刷令牌（`Themes/*.xaml` 属 M4，在 `StarPie.Theme/Views/Styles/Themes/`）；不放 HotkeyRecorderBox 专用样式段（在 Gestures） |
| `StarPie.Gestures/Views/Controls/` | M1：共享自定义控件 `HotkeyRecorderBox.cs`（唯一编译期消费方 `GesturesSettingsPage.xaml`，xmlns 本地引用；隐式默认样式模板在同模块 `Views/Styles/HotkeyRecorderBox.xaml`） | 不放对话框专用行为（`SpectrumCanvasBehavior` 已随 S6 迁 `StarPie.Dialogs`） |
| `StarPie.Gestures/Views/Styles/` | M1：`HotkeyRecorderBox.xaml` 热键录制控件样式字典（由 Host `App.xaml` 经 `/StarPie.Gestures;component/Views/Styles/HotkeyRecorderBox.xaml` 单点合并） | 不放全局控件样式（`ModernControls.xaml` 在 Host） |

## 根级文件规则

仓库根（仓库级构建入口）：

- `StarPie.slnx`：解决方案文件——登记全部工程，构建与测试入口（仓库根 `dotnet build StarPie.slnx`）。
- `Directory.Build.props`：统一构建属性（TFM / 可空性 / 隐式 using / 分析器级别 / 根命名空间）；工程级差异（`UseWPF`/`OutputType`/`AssemblyName` 等）留在各 csproj。
- `Directory.Packages.props`：中央包管理（CPM）——包版本唯一集中处，各 csproj 的 `PackageReference` 不写 `Version`。

Ui 集工程根（`StarPie.Ui/`）：

- `App.xaml` / `App.xaml.cs`：只处理单实例、异常、启动、退出和资源释放，不写业务（见 [host.md](host.md)）。
- `Composition.cs`：唯一 DI 组合根——`ServiceCollection` 注册、`BuildServiceProvider`、`CreateAppHost()` 解析；不持有托盘/主窗口/语言字典等宿主状态（见 [host.md](host.md)）。
- `AppHost.cs`：宿主编排——`Run`/`Dispose`、托盘创建与菜单、退出协调、语言资源字典（见 [host.md](host.md)）。
- `DevInstance.cs`：开发实例标记（H1）——`--dev` 隔离互斥/配置目录/触发键并保护正式自启项（见 [host.md](host.md)）。
- `Properties/`、`assets/`：工程配置与二进制资源；**不放 C#/XAML 源码**（唯一例外：
  `Properties/DesignTimeResources.xaml` 设计期资源锚，仅设计期合并，见
  [design-time-preview.md](design-time-preview.md)）。
- `StarPie.Sdk.csproj`：SDK 集工程入口（net10.0，零 WPF 零第三方包、零 ProjectReference；
  P1.2/#111 建骨架，P1.3/#112 迁入纯托管契约/模型/DTO）；`StarPie.Sdk/` 源码根目录**只允许**
  `Models/`、`Services/`、`ViewModels/`（迁移期镜像旧相对路径、命名空间保持 `StarPie.*` 不变，
  避免 API 抖动；导出面与全仓类型唯一性由 `StarPie.Tests/SdkBoundaryTests.cs` 收口）。目标树
  `Abstractions/`、`Capabilities/`、`Models/`、`Settings/`、`Events/`、`Manifest/`、
  `Compatibility/`（见 [plugins.md](plugins.md) §2）随插件面落地启用。
- `StarPie.Sdk.Wpf.csproj`：SDK 的 WPF 类型契约面工程入口（UseWPF；P1.2/#111 骨架，P1.4/#113
  起承载 WPF 契约件）；唯一允许的 ProjectReference 是 `StarPie.Sdk`；不产出 XAML；
  `StarPie.Sdk.Wpf/` 源码根目录**只允许** `Services/Icons/`、`Services/Programs/`、
  `Services/Shell/`（迁移期镜像旧相对路径）与 `Compatibility/`（UiSdkAbi/DefaultAlcPolicy）——
  导出面与 ABI 政策由 `StarPie.Tests/SdkWpfBoundaryTests.cs` 收口。
- `StarPie.Host.csproj`：宿主内核工程入口（net10.0 零 WPF；ProjectReference 只许
  `StarPie.Sdk`；P1.5+ 起迁入 `Kernel/`、`Actions/`、`Gestures/`、`Wheel/`、`Icons/`、
  `Themes/`、`Ports/`、`HostServices/`、`PluginRuntime/`，目标树见 [plugins.md](plugins.md) §2）。
- `StarPie.Core.csproj` / `GlobalUsings.cs`：共享内核工程入口（P1.3/#112 后只余 S2/S3 运行时件，
  经 `StarPie.Sdk` 引用取契约/模型）；`StarPie.Core/` 源码根目录**只允许**
  `Services/Configuration/`（S2）与 `Services/Localization/`（S3）——`Models/`、
  `Services/Messages/`、`Services/Navigation/`、`Services/AppHostDelegates.cs` 已随 P1.3/#112 迁
  `StarPie.Sdk`，`Services/Icons/`（S1 契约面驻 StarPie.Sdk.Wpf、实现驻 Icons，P1.4/#113）、
  `Services/{Programs,Dialogs}`（契约分别驻 StarPie.Sdk.Wpf/StarPie.Sdk，ADR-0023）、
  `ViewModels/Pages/`（IProfilePreviewSource 在 StarPie.Sdk）与 `Views/`（共享 UI 基建：通用转换器/ModernControls.xaml
  在 Host `Views/Converters|Styles/`、HotkeyRecorderBox（控件+样式字典）在 `StarPie.Gestures/`、
  共享页面基类 SettingsPageBase 已删除）在 Core 均已清空）——唯一 XAML 例外：
  `Services/Localization/DesignTimeStrings.xaml`（设计期投影字典，Page 编译签入生成物）与同目录
  生成脚本 `GenerateDesignTimeStrings.ps1`（见 [design-time-preview.md](design-time-preview.md)）。
- `StarPie.Icons.Contracts.csproj`：S1 契约工程入口（ADR-0023；P1.4/#113 契约迁
  `StarPie.Sdk.Wpf/Services/Icons/` 后暂留空壳：无源码、无 ProjectReference、不再被消费方引用，
  待 P1.9/#118 撤销）。
- `StarPie.Icons.csproj`：S1 实现程序集工程入口（ADR-0023，WPF 类库；引用
  Sdk + Sdk.Wpf（图标契约四件与 SPI `IShortcutTargetResolver`，P1.4/#113）+ Core（S2 AppDataPaths）
  + MS.DI 包）；
  `StarPie.Icons/` 源码根目录**只允许** `Modules/`（`IconsModuleRegistrar`）与
  `Services/Icons/`（`IconAssetService.cs`）。
- `StarPie.Programs.Contracts.csproj`：M3 契约工程入口（ADR-0023；P1.4/#113 契约迁
  `StarPie.Sdk.Wpf/Services/{Programs,Icons}/` 后暂留空壳：无源码、无 ProjectReference、
  不再被消费方引用，待 P1.9/#118 撤销）。
- `StarPie.Dialogs.Contracts.csproj`：S6 契约工程入口（ADR-0023；P1.3/#112 契约迁
  `StarPie.Sdk/Services/Dialogs/` 后暂留空壳：无源码、无 ProjectReference、不再被消费方引用，
  待 P1.10 撤销）。
- `StarPie.Dialogs.csproj` / `GlobalUsings.cs`：S6 对话框实现模块程序集工程入口（
  引用 Sdk（自身契约 IDialogService，P1.3/#112 收口）+ Sdk.Wpf（程序扫描/SPI、图标与主题
  契约面，P1.4/#113）+ Core（S2/S3/S4））；`StarPie.Dialogs/` 源码根目录
  **只允许** `Modules/`（DialogsModuleRegistrar）、`Services/Dialogs/`（DialogService）、
  `ViewModels/Dialogs/`（五对对话框 VM）与 `Views/Dialogs/`、`Views/Controls/`（窗口与取色行为）、
  `Views/DesignTime/`（设计期样例类型）与 `Properties/DesignTimeResources.xaml`（设计期资源锚；
  见 [design-time-preview.md](design-time-preview.md)）。
- `StarPie.Programs.csproj`：M3 模块程序集工程入口（引用 Sdk.Wpf（自身契约与 S1 契约面，
  P1.4/#113），不再引用 Core）；
  `StarPie.Programs/` 源码根目录**只允许** `Modules/`（`ProgramsModuleRegistrar`）、
  `Services/Programs/`（`ProgramScanner`/`ShortcutResolver`）。
- `StarPie.Shell.csproj` / `GlobalUsings.cs`：M5 模块程序集工程入口（单向引用 Sdk + Core）；
  `StarPie.Shell/` 源码根目录**只允许** `Modules/`、`Services/Shell/`、`ViewModels/Pages/`、
  `Views/Pages/`（仅上表列出的 M5 文件）与 `Properties/DesignTimeResources.xaml`（设计期资源锚，
  见 [design-time-preview.md](design-time-preview.md)）。
- `StarPie.Theme.Contracts.csproj`：M4 主题契约工程入口（ADR-0023；P1.4/#113 契约迁
  `StarPie.Sdk.Wpf/Services/Shell/` 后暂留空壳：无源码、无 ProjectReference、不再被消费方引用，
  待 P1.8/#117 撤销）。
- `StarPie.Theme.csproj` / `GlobalUsings.cs`：M4 界面主题模块程序集工程入口（单向引用 Sdk +
  Sdk.Wpf（IThemeService 契约面，P1.4/#113）+ Core）；`StarPie.Theme/` 源码根目录**只允许**
  `Modules/`（ThemeModuleRegistrar）、根级 `AppThemePaletteManager.cs`、`Services/Shell/`
  （`ThemeService.cs`）、`ViewModels/Pages/`（M4 主题设置子 VM）与 `Views/Styles/Themes/`
  （五套主题字典）。
- `StarPie.Wheel.Contracts.csproj`：M2 轮盘契约工程入口（ADR-0023；P1.3/#112 契约迁
  `StarPie.Sdk/Services|ViewModels/Wheel/` 后暂留空壳：无源码、无 ProjectReference、不再被消费方
  引用，待 P1.7 撤销）。
- `StarPie.Wheel.csproj` / `GlobalUsings.cs`：M2 轮盘与渲染模块程序集工程入口（单向引用 Sdk +
  Sdk.Wpf（IThemeService 与 S1 图标契约面，P1.4/#113）+ Core）；
  `StarPie.Wheel/` 源码根目录**只允许**
  `Modules/`（WheelModuleRegistrar）、`Models/`（WheelPalette* 三件）、`Services/Wheel/`
  （WheelGeometry/WheelFactory）、`ViewModels/Pages/`（WheelAppearanceSettingsViewModel）、
  `ViewModels/Wheel/`（WheelViewModel）、`Views/Wheel/`
  （RadialWindow）、`Views/Renderers/`（渲染器与预览）与 `Views/Converters/`（CoreIcon* 两转换器）、
  `Properties/DesignTimeResources.xaml`（设计期资源锚，见
  [design-time-preview.md](design-time-preview.md)）。
- `StarPie.Gestures.Contracts.csproj`：M1 预览 Profile 契约工程入口（ADR-0023；P1.3/#112 契约迁
  `StarPie.Sdk/ViewModels/Pages/` 后暂留空壳：无源码、无 ProjectReference、不再被消费方引用，
  待 P1.6 撤销）。
- `StarPie.Gestures.csproj` / `GlobalUsings.cs`：M1 手势与动作模块程序集工程入口（单向引用 Sdk
  （预览/轮盘/对话框契约与模型）+ Sdk.Wpf（S1 图标契约面，P1.4/#113）+ Core）；
  `StarPie.Gestures/` 源码根目录**只允许**
  `Modules/`（GesturesModuleRegistrar + GesturesPageTemplates.xaml）、`Services/Gestures/`
  （手势管线五件）、`Services/Actions/`（动作执行三件）、`ViewModels/Gestures/`（SlotViewModel）、
  `ViewModels/Pages/`（BehaviorSettingsViewModel/ProfileListViewModel）、`Views/Controls/`
  （HotkeyRecorderBox.cs）、`Views/Styles/`（HotkeyRecorderBox.xaml）
  与 `Views/Pages/`（TriggerSettingsPage/GesturesSettingsPage）、`Views/DesignTime/`（设计期样例
  类型）与 `Properties/DesignTimeResources.xaml`（设计期资源锚；见
  [design-time-preview.md](design-time-preview.md)）。
- 各工程源码根目录**只允许**上表与本小节列出的项；原型、HTML、临时脚本不得留在
  `StarPie.Ui/`、`StarPie.Sdk/`、`StarPie.Sdk.Wpf/`、`StarPie.Host/`、`StarPie.Core/`、
  `StarPie.Dialogs/`、`StarPie.Programs/`、`StarPie.Shell/`、
  `StarPie.Theme/`、`StarPie.Theme.Contracts/`、`StarPie.Wheel/`、`StarPie.Wheel.Contracts/`、
  `StarPie.Gestures/`、`StarPie.Gestures.Contracts/`、`StarPie.Icons.Contracts/`、
  `StarPie.Icons/`、`StarPie.Programs.Contracts/` 或 `StarPie.Dialogs.Contracts/` 下。

## 现状偏差与待清理项

当前代码与正典目录结构一致；exe `Modules/` 仅余 Host 外观聚合页注册器/模板字典（见下）。

### 当前登记（Host 外观聚合页）

- `StarPie.Ui/Modules/`：Host 外观聚合页注册器 `HostModuleRegistrar` 与页面模板字典
  `HostPageTemplates.xaml`（App 级每模块一次静态合并）。外观聚合页留 Host
  （[assemblies.md](assemblies.md) §5.2 槽位 1）；M5/M1 的注册器与模板字典在
  `StarPie.Shell/Modules/` 与 `StarPie.Gestures/Modules/`，本目录只承载 Host 外观聚合页，属
  正典形态而非待迁出偏差。

长期接受的例外（新代码不得新增同类）：

- `InputViewModel ↔ InputDialog`：遗留命名错位，见 [naming.md](naming.md)。
- 页面 VM 与页面 View 的领域/区块命名错位（`GeneralSettingsViewModel → AdvancedSettingsPage` 等）：**允许且是正典**，见 [naming.md](naming.md)。
