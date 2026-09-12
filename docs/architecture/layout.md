# 目录与文件架构

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；需要确认“某个路径放什么 / 新增文件落在哪”时读本篇。

## Canonical 目录树

以下为**应然结构**（正典）；当前代码与正典一致（四集 + 测试工程，无旧集与壳工程；见文末“当前登记”）。

```text
StarPie/
├── StarPie.slnx                   # 解决方案（登记全部工程；构建/测试入口）
├── Directory.Build.props          # 统一构建属性（TFM/可空性/隐式 using/分析器级别/根命名空间）
├── Directory.Packages.props       # 中央包管理（包版本唯一集中处；csproj 不写版本）
├── StarPie.Ui/             # Ui 集（WinExe，程序集名保持 StarPie；唯一含 XAML 与入口）：组合根、宿主壳窗口、导航运行时、外观聚合页
│   ├── App.xaml / App.xaml.cs     # 宿主生命周期：单实例、异常、启动/退出编排
│   ├── AppHost.cs                 # 宿主编排：Run/Dispose、托盘、语言资源、退出协调
│   ├── Composition.cs             # DI 组合根（唯一）：四阶段（早期回填 → 贡献者有序清单注册 → BuildServiceProvider → eager 解析）
│   ├── DevInstance.cs             # 开发实例标记（H1）：--dev 互斥/触发键/自启保护
│   ├── Adapters/                  # Ui 侧 WPF 适配器：DispatcherSaveDebouncer（实现 Host 内核的落盘防抖接缝）、AppThemePaletteManager（实现内核端口 IThemeApplier）
│   ├── Modules/                   # 统一注册管线：ICompositionContributor + BuiltInContributors（内置有序清单）+ HostCore/HostPage 贡献者；M4：ThemeContributor；M2：WheelContributor；M1：GesturesContributor + GesturesPageTemplates.xaml；M5：ShellContributor + ShellPageTemplates.xaml；S6：DialogsContributor
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
│   │   ├── Localization/          # 设计期投影字典 DesignTimeStrings.xaml（签入生成物、Page 编译惰性 BAML）与生成脚本（源 resx 在 Host 内核；见 design-time-preview.md）
│   │   ├── Icons/                 # 图标资产 WPF 图像构造：IconAssetService（实现 Sdk.Wpf 的 IIconAssetService；委托内核自定义图标目录）
│   │   ├── Shell/                 # M4：ThemeService.cs（实现 Sdk.Wpf 的 IThemeService；窗口 DWM 应用与系统深浅色监听）；M5：TrayIconManager(+TrayMenuEntry)
│   │   ├── Navigation/            # 导航运行时：NavigationStore、NavigationExecutor（含 INavigationExecutor）
│   │   ├── Wheel/                 # M2：WheelGeometry.cs（视觉几何，直构造 WPF Geometry）、WheelFactory.cs（实现 Sdk 的 IWheelFactory）
│   │   ├── Gestures/              # M1 手势管线 WPF 亲和件：MouseHook（Win32 钩子）、GestureController（Dispatcher 封送）
│   │   ├── Actions/               # M1 动作执行：IActionExecutorService/ActionExecutorService（系统调用与默认 MessageBox 上报）
│   │   └── Dialogs/               # S6：DialogService（实现 Sdk 的 IDialogService；SetOwner 回填）
│   ├── Themes/                    # M4：五套同 key 集主题画刷令牌 XAML（App.xaml 静态合并 Light；运行时整项替换）
│   ├── ViewModels/
│   │   ├── Pages/                 # Host 外观聚合页 VM：AppearanceSettingsViewModel（单例）；M4：InterfaceThemeSettingsViewModel + AppThemeOptionItem；M2：WheelAppearanceSettingsViewModel；M1：BehaviorSettingsViewModel/ProfileListViewModel；M5：GeneralSettingsViewModel
│   │   ├── Wheel/                 # M2：WheelViewModel.cs（实现 Sdk 的 IWheelViewModel；按手势瞬态）
│   │   ├── Gestures/              # M1：SlotViewModel.cs（方向槽位 VM + SystemPresetItem/ActionTypeOption）
│   │   ├── Navigation/            # 导航 VM：MainViewModel、NavigationItemViewModel、ShellViewModel（D3 Host 壳层 VM）
│   │   └── Dialogs/               # S6：五对对话框 VM（ProgramPicker/IconPicker/Input/ColorPicker/ScreenEyedropper）
│   └── Views/
│       ├── Converters/            # 通用共享转换器：HexToBrush/StringToGeometry/IntEquals/FilePathToImage + M2 核图标预览转换器（App.xaml 单点实例化）
│       ├── Controls/              # M1：热键录制控件 HotkeyRecorderBox.cs；S6：SpectrumCanvasBehavior（取色对话框专用）
│       ├── Dialogs/               # S6：五对对话框 Window.xaml(.cs)
│       ├── DesignTime/            # 设计期样例类型（仅 d:DataContext 消费；见 design-time-preview.md）
│       ├── Pages/                 # Host 外观聚合页 View：AppearanceSettingsPage；M1：TriggerSettingsPage/GesturesSettingsPage；M5：AdvancedSettingsPage
│       ├── Renderers/             # M2：样式渲染器工厂/各风格渲染器/WheelPreviewRenderer
│       ├── Styles/                # ModernControls.xaml 全局控件样式字典 + M1 HotkeyRecorderBox.xaml（App.xaml 本地合并）
│       ├── Navigation/            # MainView、SidebarView
│       └── Wheel/                 # M2：RadialWindow.xaml(.cs)
├── StarPie.Sdk/                    # SDK 集（net10.0；零 WPF 零第三方包；P1.3/#112 起迁入纯托管契约/模型/DTO）
│   ├── StarPie.Sdk.csproj         # 零 ProjectReference（引用面只有平台程序集，见 plugins.md §2）
│   ├── Models/                    # 稳定 DTO 与 WPF-free 值类型：AppConfig/WheelProfile/ActionItem/CustomColorPreset/ColorMath/GesturePoint
│   ├── Services/
│   │   ├── AppHostDelegates.cs    # 宿主回调委托包契约（Host 组合根注册单例、AppHost 回填）
│   │   ├── Messages/              # S4：IMessenger 消息与跨层通知载体（Messages.cs/Notices.cs）
│   │   ├── Navigation/            # S5：目录/槽位契约——NavigationCatalog/NavigationSlots（槽位表 0–3；运行时在 Host，仅此文件）
│   │   ├── Dialogs/               # S6 契约：IDialogService + 6 结果 record
│   │   ├── Icons/                 # S1 契约件：CustomIconItem/VectorIconItem + .lnk SPI IShortcutTargetResolver
│   │   ├── Programs/              # M3 契约件：IProgramScanner/ProgramEntry/ProgramCatalog（纯数据，零 WPF）
│   │   └── Wheel/                 # M2 契约：IWheelFactory
│   └── ViewModels/
│       ├── Pages/                 # M1 契约：IProfilePreviewSource
│       └── Wheel/                 # M2 契约：IWheelViewModel/IWheelAppearanceState
│                                  # 迁移期落位：源码镜像旧相对路径、命名空间保持 StarPie.* 不变（零 API 抖动），
│                                  #   导出面与全仓类型唯一性由 StarPie.Tests/SdkBoundaryTests 收口
├── StarPie.Sdk.Wpf/                # SDK 的 WPF 类型契约面（UseWPF；P1.2/#111 骨架，P1.4/#113 起承载 WPF 契约件）
│   ├── StarPie.Sdk.Wpf.csproj     # 唯一 ProjectReference 允许指向 StarPie.Sdk（不产出 XAML）
│   ├── Services/
│   │   ├── Icons/                 # 图标资产服务契约：IIconAssetService（实现驻 Ui；条目类型在 SDK、目录在 Host）
│   │   └── Shell/                 # M4 契约：IThemeService.cs
│   └── Compatibility/             # UiSdkAbi（主次版本/兼容判定）+ DefaultAlcPolicy（默认 ALC 统一加载）政策骨架
│                                  # 迁移期落位：源码镜像旧相对路径、命名空间保持 StarPie.* 不变（零 API 抖动），
│                                  #   导出面与 ABI 政策由 StarPie.Tests/SdkWpfBoundaryTests 收口
├── StarPie.Host/                   # 宿主内核（net10.0；零 WPF、零 XAML，可 headless 单测）
│   ├── StarPie.Host.csproj        # ProjectReference 只许 StarPie.Sdk（不引用 StarPie.Sdk.Wpf）
│   ├── GlobalUsings.cs            # 工程级全局 using（内核命名空间 + SDK 模型/消息）
│   ├── Icons/                     # S1 WPF-free 部分：IconCatalog（静态纯目录）+ CustomIconStore（自定义图标目录），命名空间 StarPie.Icons
│   ├── Programs/                  # M3 实现：ProgramScanner（八源扫描）+ ShortcutResolver（.lnk 解析），命名空间 StarPie.Programs
│   ├── Themes/                    # M4 引擎：ThemeEngine（请求/有效主题状态、解析、切换与系统跟随重解析），命名空间 StarPie.Themes
│   ├── Ports/                     # Host→Ui 端口：IThemeApplier（主题应用；Ui 侧适配器实现），命名空间 StarPie.Ports
│   ├── Wheel/                     # M2 WPF-free 配色：WheelPalette/WheelPaletteCatalog/WheelPaletteParser，命名空间 StarPie.Wheel
│   ├── Gestures/                  # M1 手势内核（零 WPF）：GestureEngine/GestureState/GestureReleaseResult、IWindowContext/WindowContext，命名空间 StarPie.Gestures
│   ├── Actions/                   # M1 动作路由纯函数：ActionRouting + ActionRoute/KeyStroke/SystemCommand，命名空间 StarPie.Actions
│   └── Kernel/
│       ├── Configuration/         # S2：IConfigService/JsonConfigService、ISaveDebouncer/AppDataPaths、SettingsSaveOrchestrator
│       ├── Localization/          # S3：ILocalizationService/LocalizationService + Strings*.resx（四语言）
│       └── ShellIntegration/      # M5：AutostartRegistry（HKCU Run 注册表，[SupportedOSPlatform("windows")]）+ MemoryOptimizer（工作集裁剪），命名空间 StarPie.Kernel.ShellIntegration
└── StarPie.Tests/          # xUnit 单测（显式引用四集，不依赖传递引用）
```
> 程序集归属：目录名在所属工程内各自保持“命名空间 = 物理目录”（跨程序集共享同一棵
> `StarPie.*` 命名空间树）；各程序集的承载与依赖方向见 [assemblies.md](assemblies.md) §2/§3，
> 本目录树为物理路径正典。

## 各目录职责细则

| 目录 | 存放什么 | 不放什么 / 常见违规 |
|---|---|---|
| `Adapters/` | `StarPie.Ui/Adapters/`：实现 Host 内核接缝/端口的 WPF 适配器——`DispatcherSaveDebouncer` 实现 `ISaveDebouncer`、`AppThemePaletteManager` 实现 `IThemeApplier`（主题字典整项替换） | 不放业务逻辑/VM/View；内核零 WPF，UI 线程亲和只能在本层提供 |
| `Models/` | `StarPie.Sdk/`（P1.3/#112 迁入）：配置 POCO（`AppConfig`/`WheelProfile`/`ActionItem`/`CustomColorPreset`——AppConfig 引用）与 WPF-free 值类型/纯函数（`RgbColor`/`ColorMath`/`GesturePoint`）；`StarPie.Host/Wheel/`：轮盘配色 `WheelPalette`/`WheelPaletteCatalog`/`WheelPaletteParser`（WPF-free、命名空间 `StarPie.Wheel`） | 不引用 WPF 类型、服务、命令、消息、IMessenger；不放可注入服务、文件 IO、静态 Win32 工具 |
| `Services/{Feature}/` | 该功能的服务接口与实现（同目录）、编排器、纯函数、进程内 DTO | 不放 VM/View；静态工具需符合 [layering.md](layering.md)（Services） |
| `Services/Actions/` | 路由纯函数 `ActionRouting`（+ `ActionRoute`/`KeyStroke`/`SystemCommand`）驻 `StarPie.Host/Actions/`（WPF-free）；`StarPie.Ui/Services/Actions/`：`IActionExecutorService`/`ActionExecutorService`（系统调用层，默认 MessageBox 上报） | 路由决策不得散落 VM/View；实现见 [gestures.md](gestures.md) |
| `Kernel/Configuration/` | `StarPie.Host/`：`IConfigService`/`JsonConfigService`、`ISaveDebouncer`/`AppDataPaths`、`SettingsSaveOrchestrator`（dev 分支经组合根回填）；`DispatcherSaveDebouncer` 在 `StarPie.Ui/Adapters/` | 页面 VM 不得直接碰配置文件路径或 `JsonSerializer`；内核不得出现 WPF 类型；实现见 [config.md](config.md) |
| `Services/Dialogs/` | 契约 `IDialogService` + 结果 record 在 `StarPie.Sdk`；实现 `DialogService` 在 `StarPie.Ui/Services/Dialogs/`（S6 随 P1.10/#119 归并入 Ui，SetOwner 回填装配面） | 对话框 Window/VM 不在此；文件对话框/MessageBox 不暴露给 VM/View |
| `Services/Gestures/` | 手势内核 `GestureEngine`（+`GestureState`/`GestureReleaseResult`）与 `IWindowContext`/`WindowContext` 驻 `StarPie.Host/Gestures/`（WPF-free、可 headless 构造）；`StarPie.Ui/Services/Gestures/`：`MouseHook`（Win32 钩子）、`GestureController`（Dispatcher 封送副作用）（`WheelFactory` 属 M2，见 `Services/Wheel/` 行） | 手势判定纯逻辑不得引用 WPF/Win32；实现见 [gestures.md](gestures.md) |
| `Services/Icons/` | 契约分层：`IIconAssetService` 在 `StarPie.Sdk.Wpf/Services/Icons/`；条目类型 `CustomIconItem`/`VectorIconItem` 与 .lnk SPI 在 `StarPie.Sdk/Services/Icons/`；WPF 图像构造 `IconAssetService` 在 `StarPie.Ui/Services/Icons/` | 几何/程序解析类入口不在此（归属见 [modules.md](modules.md) §3 S1）；有状态/IO/Win32 面只经实例服务注入 |
| `Icons/` · `Programs/`（Host） | 宿主内核的 WPF-free 模块逻辑：`IconCatalog`（矢量清单/SVG 键目录/路径解析纯表）与 `CustomIconStore`（自定义图标目录，命名空间 `StarPie.Icons`）；`ProgramScanner`（八源扫描编排）与 `ShortcutResolver`（.lnk 解析，命名空间 `StarPie.Programs`） | 不放 WPF 类型/XAML；程序扫描件只经 `StarPie.Sdk` 契约对外 |
| `Themes/` · `Ports/`（Host） | 宿主内核的 WPF-free 主题引擎 `ThemeEngine`（请求/有效主题状态、解析、切换与系统跟随重解析，命名空间 `StarPie.Themes`）与宿主→Ui 端口 `IThemeApplier`（命名空间 `StarPie.Ports`，Ui 侧适配器实现） | 不放 XAML/主题字典（在 `StarPie.Ui/Themes/`）；引擎不引用 WPF，效果一律经端口回抛 |
| `Kernel/Localization/` | `StarPie.Host/`：`ILocalizationService`/`LocalizationService` + `Strings*.resx`（四语言）；`StarPie.Ui/Services/Localization/` 只余设计期投影 `DesignTimeStrings.xaml`（Page 编译签入生成物）与生成脚本（ADR-0025 例外，源 resx 在内核） | VM/View 不得另建文案字典；设计期字典仅由 resx 派生；实现见 [localization.md](localization.md)、[design-time-preview.md](design-time-preview.md) |
| `Services/Messages/` | `StarPie.Sdk/`（P1.3/#112 自 Core 迁入）：`Messages.cs`（IMessenger 消息）、`Notices.cs`（`NoticeKind`/`NoticeRequest`） | 同页状态不得用消息替代绑定 |
| `Services/Navigation/` | `StarPie.Sdk/`（P1.3/#112 自 Core 迁入）：目录/槽位契约 `NavigationCatalog`（`NavigationCatalog.cs`）；Host：导航运行时 `NavigationStore`/`NavigationExecutor`（含 `INavigationExecutor`） | 页面状态不得散落导航器之外；实现见 [navigation.md](navigation.md) |
| `Services/Wheel/` | `StarPie.Ui/Services/Wheel/`：`WheelGeometry`（直构造 WPF `Geometry`）、`WheelFactory`（契约 `IWheelFactory` 驻 `StarPie.Sdk`，P1.3/#112 收口） | 工厂只经 SDK 契约被 M1 消费；实现见 [wheel.md](wheel.md) |
| `ViewModels/Pages/` | Host：`AppearanceSettingsViewModel`；`StarPie.Ui`：`InterfaceThemeSettingsViewModel`/`AppThemeOptionItem`（M4）、`WheelAppearanceSettingsViewModel`（M2）、`BehaviorSettingsViewModel`/`ProfileListViewModel`（M1）、`GeneralSettingsViewModel`（M5）；`StarPie.Sdk/ViewModels/Pages/`：`IProfilePreviewSource` | 不得引用 WPF 类型；不得出现 `event Action` 临时事件 |
| `ViewModels/Dialogs/` | `StarPie.Ui/ViewModels/Dialogs/`：`{Dialog}ViewModel`（含 `ScreenEyedropperViewModel`） | 不得持有 Window/MessageBox/对话框类型；形态见 [dialogs.md](dialogs.md) |
| `ViewModels/Gestures/` | `StarPie.Ui/ViewModels/Gestures/`：`SlotViewModel`（+ `SystemPresetItem`/`ActionTypeOption`） | 不放服务 |
| `ViewModels/Navigation/` | Host：`NavigationItemViewModel`、`MainViewModel`（目录驱动）、`ShellViewModel` | 导航项文案/图标规则见 [navigation.md](navigation.md) |
| `ViewModels/Wheel/` | `StarPie.Ui/ViewModels/Wheel/`：`WheelViewModel`（契约 `IWheelViewModel`/`IWheelAppearanceState` 驻 `StarPie.Sdk`，P1.3/#112 收口） | 不注册容器；按手势由 `WheelFactory` 瞬态创建 |
| `Views/Pages/` | Host：`AppearanceSettingsPage`；`StarPie.Ui`：`TriggerSettingsPage`/`GesturesSettingsPage`（M1）、`AdvancedSettingsPage`（M5）（XAML 根直承 `UserControl`） | 不注册容器；不编排业务/写配置/调服务；页面无参构造 |
| `Views/Dialogs/` | `StarPie.Ui/Views/Dialogs/`：`{Dialog}Window.xaml(.cs)`（对话框唯一形态） | 例外见 [naming.md](naming.md)；不放无配对 Window 的散件 |
| `Views/Navigation/` | Host：`MainView`（纯壳）、`SidebarView` | 其它窗口/页面不得再合并样式字典（样式已 App 级单点合并） |
| `Views/Wheel/` | `StarPie.Ui/Views/Wheel/`：`RadialWindow` | 状态决策在 `WheelViewModel`；窗口只做视觉呈现与生命周期 |
| `Views/Controls/` | `StarPie.Ui/Views/Controls/`：`HotkeyRecorderBox`（M1，样式字典在 `Views/Styles/`）与 `SpectrumCanvasBehavior`（S6 取色对话框专用） | 有 `Command`/绑定等价物时不得新增行为 |
| `Views/Converters/` | Host：通用共享转换器（`HexToBrush`/`StringToGeometry`/`IntEquals`/`FilePathToImage`，App.xaml App 级单点持有）；`StarPie.Ui/Views/Converters/`：M2 随归并的 `CoreIconGeometryConverter`/`CoreIconNameConverter` | 转换器保持无状态、可静态复用 |
| `Views/DesignTime/` | `StarPie.Ui/`（含 M1/S6 样例）：设计期样例类型（命名空间 `StarPie.Views.DesignTime`，仅被根节点 `d:DataContext` 消费，见 design-time-preview.md） | 不放运行时 VM/服务；运行时代码不得引用 |
| `Views/Renderers/` | `StarPie.Ui/Views/Renderers/`：`IRadialStyleRenderer`/`StyleRendererFactory`/`BaseStyleRenderer`/各风格渲染器/`WheelPreviewRenderer`；渲染器只消费 `WheelPalette` 解析结果构造画刷 | 渲染器不订阅事件、不读写 VM、不反向依赖 Composition/服务；深浅色探测由调用方以 bool 传入（见 [wheel.md](wheel.md)） |
| `Modules/` | Ui 集内统一注册管线与全部贡献者：`ICompositionContributor`（Id/Order/RegisterServices + 可选 RegisterNavigation）+ `BuiltInContributors` 有序清单、宿主编排 `HostCoreContributor`、Host 外观聚合页 `HostPageContributor` + `HostPageTemplates.xaml`、M4 `ThemeContributor`、M2 `WheelContributor`、S6 `DialogsContributor`（无导航页）、M1 `GesturesContributor` 与 M5 `ShellContributor`（含导航登记，各带页面模板字典） | 不承载业务；贡献者只登记不解析；Id/Order 唯一、清单按 Order 升序 |



### 共享 UI 基建落点（已去共享化）

> 共享 UI 基建（原设计期投影壳内的 `Views/`：Converters/Controls/Pages/Styles）已随去共享化**整体
> 清空移除**；资源键集不变——模块 XAML 消费方
> （`{StaticResource}` 运行期解析）零改动。

| 目录 | 存放什么 | 不放什么 / 常见违规 |
|---|---|---|
| `StarPie.Ui/Views/Converters/` | Host 通用共享转换器：`HexToBrushConverter`（hex→Brush，配 SDK `Models/RgbColor`）、`StringToGeometryConverter`（SVG 路径→Geometry）、`IntEqualsConverter`、`FilePathToImageConverter`（本地图片→缩略图）；实例由 Host `App.xaml` App 级单点持有（ADR-0012 决策 5）；M2 核图标预览转换器随归并同驻本目录（App.xaml 本地实例化） | 不放其它业务模块专用转换器 |
| `StarPie.Ui/Views/Styles/` | Host `ModernControls.xaml` 全局控件样式字典（隐式默认/键控变体/共享模板；App.xaml **本地合并**；几何令牌经 DynamicResource 供跨字典模板引用）+ M1 `HotkeyRecorderBox.xaml` 热键录制控件隐式默认样式字典（本地单点合并） | 不放主题画刷令牌（`Themes/*.xaml` 属 M4，在 `StarPie.Ui/Themes/`） |
| `StarPie.Ui/Views/Controls/` | M1：共享自定义控件 `HotkeyRecorderBox.cs`（唯一编译期消费方 `GesturesSettingsPage.xaml`，xmlns 本地引用；隐式默认样式模板在 `StarPie.Ui/Views/Styles/HotkeyRecorderBox.xaml`；P1.6/#115 随 M1 归并入 Ui） | 不放对话框专用行为（`SpectrumCanvasBehavior` 随 S6 归并同驻本目录，`StarPie.Ui/Views/Controls/`） |

## 根级文件规则

仓库根（仓库级构建入口）：

- `StarPie.slnx`：解决方案文件——登记全部工程，构建与测试入口（仓库根 `dotnet build StarPie.slnx`）。
- `Directory.Build.props`：统一构建属性（TFM / 可空性 / 隐式 using / 分析器级别 / 根命名空间）；工程级差异（`UseWPF`/`OutputType`/`AssemblyName` 等）留在各 csproj。
- `Directory.Packages.props`：中央包管理（CPM）——包版本唯一集中处，各 csproj 的 `PackageReference` 不写 `Version`。

Ui 集工程根（`StarPie.Ui/`）：

- `App.xaml` / `App.xaml.cs`：只处理单实例、异常、启动、退出和资源释放，不写业务（见 [host.md](host.md)）。
- `Composition.cs`：唯一 DI 组合根——四阶段：早期回填 → 内置贡献者有序清单注册（导航目录 + 容器描述符）→ `BuildServiceProvider` → `CreateAppHost()` eager 解析；不持有托盘/主窗口/语言字典等宿主状态（见 [host.md](host.md)）。
- `AppHost.cs`：宿主编排——`Run`/`Dispose`、托盘创建与菜单、退出协调、语言资源字典（见 [host.md](host.md)）。
- `DevInstance.cs`：开发实例标记（H1）——`--dev` 隔离互斥/配置目录/触发键并保护正式自启项（见 [host.md](host.md)）。
- `Adapters/`：Ui 侧 WPF 适配器——实现 Host 内核接缝/端口（`DispatcherSaveDebouncer` 实现 `ISaveDebouncer`，把防抖计时绑到 UI 线程，见 [config.md](config.md)；`AppThemePaletteManager` 实现 `IThemeApplier`，整项替换主题调色板，见 [interface-theme.md](interface-theme.md)）。
- `Services/`：Ui 侧服务实现——`Services/Navigation/`（导航运行时）；`Services/Icons/`
  （图标资产的 WPF 图像构造 `IconAssetService`，实现 `StarPie.Sdk.Wpf` 的 `IIconAssetService`
  并委托宿主内核 `CustomIconStore`，见 [programs.md](programs.md)）；`Services/Shell/`
  （M4 的 `IThemeService` 实现 `ThemeService`，透传内核 `ThemeEngine` 状态并做窗口 DWM 应用与
  系统深浅色监听，见 [interface-theme.md](interface-theme.md)）；`Services/Localization/`
  （设计期投影字典 `DesignTimeStrings.xaml` + 生成脚本，只被 `Properties/DesignTimeResources.xaml`
  设计期合并，见 [design-time-preview.md](design-time-preview.md)）。
  `Modules/`：统一注册管线——`ICompositionContributor`（Id/Order/RegisterServices + 可选
  RegisterNavigation）与 `BuiltInContributors` 有序清单，及七个内置贡献者（宿主编排/外观页/
  Theme/Wheel/Gestures/Shell/Dialogs）。
- `Themes/`：M4 主题画刷令牌字典（五套同 key 集 XAML；`App.xaml` 静态合并 Light 作设计时/首帧
  默认，运行时由 `Adapters/AppThemePaletteManager` 整项替换，见 [interface-theme.md](interface-theme.md)）。
- `Properties/`、`assets/`：工程配置与二进制资源；**不放 C#/XAML 源码**（唯一例外：
  `Properties/DesignTimeResources.xaml` 设计期资源锚，仅设计期合并，见
  [design-time-preview.md](design-time-preview.md)；设计期字符串字典本体在
  `Services/Localization/`）。
- `StarPie.Sdk.csproj`：SDK 集工程入口（net10.0，零 WPF 零第三方包、零 ProjectReference；
  P1.2/#111 建骨架，P1.3/#112 迁入纯托管契约/模型/DTO）；`StarPie.Sdk/` 源码根目录**只允许**
  `Models/`、`Services/`、`ViewModels/`（迁移期镜像旧相对路径、命名空间保持 `StarPie.*` 不变，
  避免 API 抖动；`Services/Icons|Programs/` 分别承载 S1/M3 契约件；导出面与全仓类型唯一性由
   `StarPie.Tests/SdkBoundaryTests.cs` 收口）与插件面落点 `Manifest/`（plugin.json 纯数据模型）、
   `Plugins/`（`IPlugin` 入口与 `IPluginContext` 宿主服务面）、
   `Compatibility/`（`AbiVersion` 版本串解析与 headless `SdkAbi`）。目标树
   `Abstractions/`、`Capabilities/`、`Settings/`、`Events/`（见 [plugins.md](plugins.md) §2）
   随插件面其余能力落地启用。
- `StarPie.Sdk.Wpf.csproj`：SDK 的 WPF 类型契约面工程入口（UseWPF；P1.2/#111 骨架，P1.4/#113
  起承载 WPF 契约件）；唯一允许的 ProjectReference 是 `StarPie.Sdk`；不产出 XAML；
  `StarPie.Sdk.Wpf/` 源码根目录**只允许** `Services/Icons/`、`Services/Shell/`
  （迁移期镜像旧相对路径）与 `Compatibility/`（UiSdkAbi/DefaultAlcPolicy）——
  导出面与 ABI 政策由 `StarPie.Tests/SdkWpfBoundaryTests.cs` 收口。
- `StarPie.Host.csproj` / `GlobalUsings.cs`：宿主内核工程入口（net10.0 零 WPF；ProjectReference
  只许 `StarPie.Sdk`；不引用 `StarPie.Sdk.Wpf`）；`StarPie.Host/` 源码根目录**只允许**
  `Kernel/`（`Kernel/Configuration/`、`Kernel/Localization/` 与 `Kernel/ShellIntegration/`
  ——M5 自启注册表/内存整理）、`Icons/`（`IconCatalog`/
  `CustomIconStore`）、`Programs/`（`ProgramScanner`/`ShortcutResolver`）、`Themes/`（`ThemeEngine`
  主题引擎）、`Ports/`（`IThemeApplier` 等宿主→Ui 端口）、`Wheel/`（`WheelPalette*` 配色目录与
  解析）、`Gestures/`（手势内核）与 `Actions/`（动作路由纯函数）以及工程级
   `GlobalUsings.cs`；插件面落点 `PluginRuntime/`（`Discovery/`、`Manifest/`、`Admission/`、
   `State/`、`Loading/`、`Lifecycle/`、`Diagnostics/`——插件发现/清单校验/准入/宿主状态/
   collectible ALC 装载管线与生命周期状态机/启动报告，可 headless 直接构造）——
   目标树其余目录（`HostServices/` 与 `PluginRuntime/` 的能力表/配置/隔离）随插件面后续落地，
   目标树见 [plugins.md](plugins.md) §2；
  导出面与零 WPF 由 `StarPie.Tests/HostBoundaryTests.cs` 收口。
- 各工程源码根目录**只允许**上表与本小节列出的项；原型、HTML、临时脚本不得留在
  `StarPie.Ui/`、`StarPie.Sdk/`、`StarPie.Sdk.Wpf/`、`StarPie.Host/`、`StarPie.Tests/`
  目录下。

## 现状偏差与待清理项

当前代码与正典目录结构一致；四集之外的旧集（含设计期投影壳 `StarPie.Core`）已全部撤销，
exe `Modules/` 承载统一注册管线与全部内置贡献者（见下）。

### 当前登记（统一注册管线）

- `StarPie.Ui/Modules/`：注册管线 `ICompositionContributor` + `BuiltInContributors`（`CreateAll`
  返回按 `Order` 升序的有序清单，Id/Order 唯一）；贡献者 `HostCoreContributor`（宿主编排与内核
  接入）、`HostPageContributor`（外观聚合页 VM 与槽位 1 + `HostPageTemplates.xaml`）、
  `ThemeContributor`、`WheelContributor`、`DialogsContributor`（均无导航页）与
  `GesturesContributor`、`ShellContributor`（含导航登记，各带页面模板字典）。外观聚合页留 Host
  （[assemblies.md](assemblies.md) §5.2 槽位 1），属正典形态而非待迁出偏差。

长期接受的例外（新代码不得新增同类）：

- `InputViewModel ↔ InputDialog`：遗留命名错位，见 [naming.md](naming.md)。
- 页面 VM 与页面 View 的领域/区块命名错位（`GeneralSettingsViewModel → AdvancedSettingsPage` 等）：**允许且是正典**，见 [naming.md](naming.md)。
