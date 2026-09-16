# 目录、落位与命名

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；确认「某个路径放什么 / 新增文件落在哪 /
> 新类型与页面叫什么」时读本篇：物理路径正典在 §1，逐目录禁止项在 §2，命名与映射正典在 §3。

## 1. Canonical 目录树

以下为**应然结构**（正典）；树中路径的存在性由 `DocInvariantTests` 机械守护。

```text
StarPie/
├── StarPie.slnx                   # 解决方案（登记全部工程；构建/测试入口）
├── Directory.Build.props          # 统一构建属性（TFM/可空性/隐式 using/分析器级别/根命名空间）
├── Directory.Packages.props       # 中央包管理（包版本唯一集中处；csproj 不写版本）
├── AGENTS.md                      # agent 入口约定（技能、issue、提交、架构文档路由）
├── CONTEXT.md                     # 领域术语词汇表
├── CONTRIBUTING.md                # 贡献指南（环境准备、dev 实例、提交流程）
├── global.json                    # SDK 基线固定 + 测试运行平台（MTP）
├── scripts/                       # 工具脚本：e2e 运行器 run-e2e.ps1、审核清单签名 sign-review-catalog.ps1
├── StarPie.Ui/             # Ui 集（WinExe，程序集名保持 StarPie；唯一含 XAML 与入口）：组合根、宿主壳窗口、导航运行时、外观聚合页
│   ├── App.xaml / App.xaml.cs     # 宿主生命周期：单实例、异常、启动/退出编排
│   ├── ShellHost.cs               # 常驻壳层：Run/Dispose、托盘、语言资源、退出协调、设置台按需创建与释放
│   ├── SettingsConsole.cs         # 设置台租户：按需创建、关闭即销毁（主窗口 + 导航区/壳区 VM 树）
│   ├── Composition.cs             # DI 组合根（唯一）：三阶段（贡献者有序清单注册 → BuildServiceProvider → 解析 + 设置台会话工厂）
│   ├── DevInstance.cs             # 开发实例标记：Debug 构建即开发实例（按构建配置编译期定死，判定真相在内核 AppDataPaths）；本类只承担可见标识
│   ├── SingleInstanceRestore.cs   # 单实例重激活窗口消息：置前实例投递给主框架，由 WndProc 走 WPF 显示路径自恢复（纯 ShowWindow 不更新 IsVisible 状态）
│   ├── TestInstanceExit.cs        # 测试实例退出窗口消息：e2e 运行器以此请求被测进程走真实退出路径，取代硬杀（避免幽灵托盘图标）
│   ├── Adapters/                  # Ui 侧 WPF 适配器：DispatcherSaveDebouncer（实现 Host 内核的落盘防抖接缝）、AppThemePaletteManager（实现内核端口 IThemeApplier）
│   ├── PluginHosting/             # 插件 UI 托管（资产登记表、每插件资源根、视图/窗口/命令/菜单/定时器/动画/订阅托管、UI 线程释放编排、泄漏验证器）
│   ├── Modules/                   # 统一注册管线：ICompositionContributor + BuiltInContributors（内置有序清单）+ HostCore/HostPage 贡献者；M4：ThemeContributor；M2：WheelContributor；M1：GesturesContributor + GesturesPageTemplates.xaml；M5：ShellContributor + ShellPageTemplates.xaml；HostCore：HostCoreContributor + HostCorePageTemplates.xaml；S6：DialogsContributor
│   ├── AssemblyInfo.cs            # 程序集元数据
│   ├── GlobalUsings.cs            # 工程级全局 using
│   ├── StarPie.Ui.csproj          # Ui 集工程文件（目录/文件名 StarPie.Ui，程序集名仍为 StarPie）
│   ├── runtimeconfig.template.json # 运行时配置模板：System.GC.HeapHardLimit = 256 MiB（内存常驻约束）
│   ├── Properties/
│   │   └── DesignTimeResources.xaml  # 设计期资源锚（仅设计期合并，见 design-time-preview.md）
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
│   │   ├── Pages/                 # Host 外观聚合页 VM：AppearanceSettingsViewModel（单例）；驻留文案件 ResidentOptionRefresher（M2/M4 设置页共用）；M4：InterfaceThemeSettingsViewModel + AppThemeOptionItem；M2：WheelAppearanceSettingsViewModel；M1：BehaviorSettingsViewModel/ProfileListViewModel；M5：GeneralSettingsViewModel
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
├── StarPie.Sdk/                    # SDK 集（net10.0；零 WPF 零第三方包）
│   ├── StarPie.Sdk.csproj         # 零 ProjectReference（引用面只有平台程序集，见 plugins.md §2）
│   ├── Abstractions/              # 插件入口契约：IPlugin 与 IPluginContext 宿主服务面
│   ├── Compatibility/             # AbiVersion 版本串解析与 headless SdkAbi
│   ├── Events/                    # 插件事件契约：IPluginEvents
│   ├── Manifest/                  # plugin.json 纯数据模型
│   ├── Models/                    # 稳定 DTO 与 WPF-free 值类型：AppConfig/WheelProfile/ActionItem/CustomColorPreset/ColorMath/GesturePoint
│   ├── Services/
│   │   ├── AppHostDelegates.cs    # 宿主回调委托包契约（Host 组合根注册单例、ShellHost 回填；契约名不随类改名）
│   │   ├── Messages/              # S4：IMessenger 消息与跨层通知载体（Messages.cs/Notices.cs）
│   │   ├── Navigation/            # S5：目录/槽位契约——NavigationCatalog/NavigationSlots（槽位表 0–4；运行时在 Host，仅此文件）
│   │   ├── Dialogs/               # S6 契约：IDialogService + 6 结果 record
│   │   ├── Icons/                 # S1 契约件：CustomIconItem/VectorIconItem + .lnk SPI IShortcutTargetResolver
│   │   ├── Programs/              # M3 契约件：IProgramScanner/ProgramEntry/ProgramCatalog（纯数据，零 WPF）
│   │   ├── Wheel/                 # M2 契约：IWheelFactory
│   │   └── Themes/                # M4 契约：界面主题名目录 AppThemeNames（System+五套常量、深色集合、规范形查询）
│   └── ViewModels/
│       ├── Pages/                 # M1 契约：IProfilePreviewSource
│       └── Wheel/                 # M2 契约：IWheelViewModel/IWheelAppearanceState
│                                  # 迁移期落位：源码镜像旧相对路径、命名空间保持 StarPie.* 不变（零 API 抖动），
│                                  #   导出面与全仓类型唯一性由 StarPie.Tests/SdkBoundaryTests 收口
├── StarPie.Sdk.Wpf/                # SDK 的 WPF 类型契约面（UseWPF）
│   ├── StarPie.Sdk.Wpf.csproj     # 唯一 ProjectReference 允许指向 StarPie.Sdk（不产出 XAML）
│   ├── Abstractions/
│   │   └── Ui/                    # 插件 UI 契约：IPluginUiModule/IPluginUiContext/IUiDispatcher 与注册描述符（P3 起）
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
│   ├── Programs/                  # M3 实现：内置程序来源 ProgramScanner + 程序来源能力契约/聚合（ProgramSourceCapability/ProgramSourceAggregator）+ ShortcutResolver（.lnk 解析），命名空间 StarPie.Programs
│   ├── Themes/                    # M4 引擎：ThemeEngine（请求/有效主题状态、解析、切换与系统跟随重解析），命名空间 StarPie.Themes
│   ├── Ports/                     # Host→Ui 端口：IThemeApplier（主题应用；Ui 侧适配器实现），命名空间 StarPie.Ports
│   ├── Wheel/                     # M2 WPF-free 配色：WheelPalette/WheelPaletteCatalog/WheelPaletteParser，命名空间 StarPie.Wheel
│   ├── Gestures/                  # M1 手势内核（零 WPF）：GestureEngine/GestureState/GestureReleaseResult、IWindowContext/WindowContext，命名空间 StarPie.Gestures
│   ├── Actions/                   # M1 动作路由纯函数：ActionRouting + ActionRoute/KeyStroke/SystemCommand，命名空间 StarPie.Actions
│   ├── Configuration/             # S2：IConfigService/JsonConfigService、ISaveDebouncer/AppDataPaths、SettingsSaveOrchestrator，命名空间 StarPie.Configuration
│   ├── Localization/              # S3：ILocalizationService/LocalizationService + Strings*.resx（四语言），命名空间 StarPie.Localization
│   ├── ShellIntegration/          # M5：AutostartRegistry（HKCU Run 注册表，[SupportedOSPlatform("windows")]）+ MemoryOptimizer（纯托管 GC 收敛），命名空间 StarPie.ShellIntegration
│   ├── HostServices/              # 插件可见宿主服务实现：PluginLog/PluginEvents/PluginEventPump/PluginHostContext/PluginServiceScope（每插件一个作用域，见 plugin-contracts.md §4）
│   └── PluginRuntime/             # 插件运行时：Discovery/Manifest/Admission/State/Hosting/Loading/Unloading/Lifecycle/Registry/Diagnostics/Ui（可 headless 构造，见 plugins.md）
├── StarPie.Tests/          # xUnit 单测（显式引用四集，不依赖传递引用）
├── docs/                          # 文档体系：入口 architecture.md、叶子 architecture/、决策记录 adr/、工作流正典 agents/
├── plugins/                       # 插件包与示例：src/ 随包插件、samples/ 最小示例、review-catalog.json(.sig) 审核清单
└── tests/                         # pywinauto e2e（不在本文档体系展开；运行器 scripts/run-e2e.ps1）
```
> 程序集归属：目录名在所属工程内各自保持“命名空间 = 物理目录”（跨程序集共享同一棵
> `StarPie.*` 命名空间树）；各程序集的承载与依赖方向见 [assemblies.md](assemblies.md) §2/§3，
> 本目录树为物理路径正典。

## 2. 各目录禁止项

逐目录「放什么」见 §1 目录树（物理路径正典）；本表只列**禁止项与常见违规**。

| 目录 | 不放什么 / 常见违规 |
|---|---|
| `Adapters/`（Ui） | 不放业务逻辑/VM/View；内核零 WPF，UI 线程亲和只能在本层提供 |
| `Models/` | 不引用 WPF 类型、服务、命令、消息、IMessenger；不放可注入服务、文件 IO、静态 Win32 工具 |
| `Services/{Feature}/` | 不放 VM/View；静态工具须符合 [layering.md](layering.md)（Services 判据） |
| `Services/Actions/` | 路由决策不得散落 VM/View；实现见 [gestures.md](gestures.md) |
| `Configuration/`（Host） | 页面 VM 不得直接碰配置文件路径或 `JsonSerializer`；内核不得出现 WPF 类型；实现见 [config.md](config.md) |
| `Services/Dialogs/` | 对话框 Window/VM 不在此；文件对话框/MessageBox 不暴露给 VM/View |
| `Services/Gestures/` | 手势判定纯逻辑不得引用 WPF/Win32；实现见 [gestures.md](gestures.md) |
| `Services/Icons/` | 几何/程序解析类入口不在此（归属见 [modules.md](modules.md) §3 S1）；有状态/IO/Win32 面只经实例服务注入 |
| `Icons/` · `Programs/`（Host） | 不放 WPF 类型/XAML；程序扫描件只经 `StarPie.Sdk` 契约对外 |
| `Themes/` · `Ports/`（Host） | 不放 XAML/主题字典（在 `StarPie.Ui/Themes/`）；引擎不引用 WPF，效果一律经端口回抛 |
| `Localization/` | VM/View 不得另建文案字典；设计期字典仅由 resx 派生；实现见 [localization.md](localization.md)、[design-time-preview.md](design-time-preview.md) |
| `Services/Messages/` | 同页状态不得用消息替代绑定 |
| `Services/Navigation/` | 页面状态不得散落导航器之外；实现见 [navigation.md](navigation.md) |
| `Services/Themes/`（SDK） | 主题名不得在引擎/适配器/VM 各写一遍；界面主题与轮盘配色同名不同义，两套名录分列（见 [interface-theme.md](interface-theme.md)） |
| `Services/Wheel/` | 工厂只经 SDK 契约被 M1 消费；实现见 [wheel.md](wheel.md) |
| `ViewModels/Pages/` | 不得引用 WPF 类型；不得出现 `event Action` 临时事件 |
| `ViewModels/Dialogs/` | 不得持有 Window/MessageBox/对话框类型；形态见 [dialogs.md](dialogs.md) |
| `ViewModels/Gestures/` | 不放服务 |
| `ViewModels/Navigation/` | 导航项文案/图标规则见 [navigation.md](navigation.md) |
| `ViewModels/Wheel/` | 不注册容器；按手势由 `WheelFactory` 瞬态创建 |
| `Views/Pages/` | 不注册容器；不编排业务/写配置/调服务；页面无参构造 |
| `Views/Dialogs/` | 不放无配对 Window 的散件；配对例外见 §5 |
| `Views/Navigation/` | 其它窗口/页面不得再合并样式字典（样式已 App 级单点合并） |
| `Views/Controls/` | 有 `Command`/绑定等价物时不得新增行为；不放对话框专用行为之外的散件 |
| `Views/Styles/` | 不放主题画刷令牌（`Themes/*.xaml` 属 M4）；`ModernControls.xaml`/`HotkeyRecorderBox.xaml` 由 App.xaml 本地单点合并 |
| `Views/Converters/` | 转换器保持无状态、可静态复用；不放其它业务模块专用转换器 |
| `Views/Renderers/` | 渲染器不订阅事件、不读写 VM、不反向依赖 Composition/服务；深浅色由调用方以 bool 传入（见 [wheel.md](wheel.md)） |
| `Views/Wheel/` | 状态决策在 `WheelViewModel`；窗口只做视觉呈现与生命周期 |
| `Views/DesignTime/` | 不放运行时 VM/服务；运行时代码不得引用 |
| `Modules/` | 不承载业务；贡献者只登记不解析；Id/Order 唯一、清单按 Order 升序 |

## 3. 命名与映射

### 3.1 命名规则

| 类型 | 规则 | 示例 |
|---|---|---|
| 服务接口/实现 | `IXxxService` / `XxxService` | `IDialogService` / `DialogService` |
| 页面 VM | `{Domain}SettingsViewModel`（按设置域） | `BehaviorSettingsViewModel` |
| 页面 View | `{Page}Page`（按导航区块） | `TriggerSettingsPage` |
| 对话框 VM / Window | `{Dialog}ViewModel` / `{Dialog}Window`（**必须同名配对**） | `ColorPickerViewModel` + `ColorPickerWindow` |
| 对话框结果 | `{Dialog}Result`（可空 record，定义在接口文件） | `ColorPickResult` |
| IMessenger 消息 | `XxxRequestedMessage` / `XxxChangedMessage` / `XxxImportedMessage` / 单例空载体 | `DebouncedSaveRequestedMessage` |
| 跨层通知载体 | `NoticeKind` / `NoticeRequest`（`Notices.cs`） | — |
| 转换器 | `XxxToYyyConverter` | `HexToBrushConverter` |
| 渲染器 | `XxxRenderer`（样式） / `XxxRenderer`（预览） | `GlassmorphismRenderer`、`WheelPreviewRenderer` |
| 测试 | `{被测类型}Tests.cs`（平铺于测试工程根） | `GestureEngineTests.cs` |

### 3.2 页面映射表（正典）

| ViewModel（设置域命名） | View（导航区块命名） | 说明 |
|---|---|---|
| `BehaviorSettingsViewModel` | `TriggerSettingsPage` | 触发与场景 |
| `AppearanceSettingsViewModel` | `AppearanceSettingsPage` | 外观与形态 |
| `ProfileListViewModel` | `GesturesSettingsPage` | 手势与动作 |
| `GeneralSettingsViewModel` | `AdvancedSettingsPage` | 高级与系统 |
| `PluginManagerViewModel` | `PluginManagerPage` | 插件管理 |

规则：VM 名与页面名**允许错位**（VM 按领域、View 按区块），但**新增页面必须在所属贡献者
`RegisterNavigation`（M5 为 `StarPie.Ui` 的 `ShellContributor`、M1 为
`StarPie.Ui` 的 `GesturesContributor`，Host 外观聚合页为 `HostPageContributor`，
宿主直持页为 `HostCoreContributor`）
+ 所属模块页面模板字典 DataTemplate（M5 在 `StarPie.Ui/Modules/ShellPageTemplates.xaml`、M1 在
`StarPie.Ui/Modules/GesturesPageTemplates.xaml`、插件管理在
`StarPie.Ui/Modules/HostCorePageTemplates.xaml`）+ 本表各登记一行**；映射表是唯一事实来源
（接线流程见 [navigation.md](navigation.md)）。

### 3.3 对话框配对

正典配对（同名）：`ColorPickerViewModel ↔ ColorPickerWindow`、`IconPickerViewModel ↔ IconPickerWindow`、`ProgramPickerViewModel ↔ ProgramPickerWindow`、`ScreenEyedropperViewModel ↔ ScreenEyedropperWindow`。唯一形态与实现流程见 [dialogs.md](dialogs.md)。

## 4. 根级文件规则

仓库根（仓库级构建入口）：

- `StarPie.slnx`：解决方案文件——登记全部工程，构建与测试入口（仓库根 `dotnet build StarPie.slnx`）。
- `Directory.Build.props`：统一构建属性（TFM / 可空性 / 隐式 using / 分析器级别 / 根命名空间）；工程级差异（`UseWPF`/`OutputType`/`AssemblyName` 等）留在各 csproj。
- `Directory.Packages.props`：中央包管理（CPM）——包版本唯一集中处，各 csproj 的 `PackageReference` 不写 `Version`。

Ui 集工程根（`StarPie.Ui/`）：

- `App.xaml` / `App.xaml.cs`：只处理单实例、异常、启动、退出和资源释放，不写业务（见 [host.md](host.md)）。
- `Composition.cs`：唯一 DI 组合根——三阶段：内置贡献者有序清单注册（导航目录 + 容器描述符）→ `BuildServiceProvider` → `CreateShellHost()` 解析常驻件并交付设置台会话工厂；不持有托盘/主窗口/语言字典等宿主状态（见 [host.md](host.md)）。
- `ShellHost.cs`：常驻壳层——`Run`/`Dispose`、托盘创建与菜单、插件运行时驱动、单实例恢复接收、退出协调、语言资源字典、设置台按需创建与释放（见 [host.md](host.md)）。
- `SettingsConsole.cs`：设置台租户——按需创建设置控制台会话（主窗口 + 导航区/壳区 VM 树），开窗绑对话框 Owner、关窗走瞬态窗口收尾纪律并释放 VM 树（见 [host.md](host.md)）。
- `DevInstance.cs`：开发实例标记——Debug 构建即 dev（判定唯一真相在内核 `AppDataPaths`，编译期定死）；配置目录隔离与自启注册表保护生效，窗口/托盘带 `(Dev)` 可见标记；与正式实例同闸互斥、同为右键触发，不并行（见 [host.md](host.md)）。
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
  `StarPie.Sdk/` 源码根目录**只允许**
  `Models/`、`Services/`、`ViewModels/`（迁移期镜像旧相对路径、命名空间保持 `StarPie.*` 不变，
  避免 API 抖动；`Services/Icons|Programs/` 分别承载 S1/M3 契约件；导出面与全仓类型唯一性由
   `StarPie.Tests/SdkBoundaryTests.cs` 收口）与插件面落点 `Manifest/`（plugin.json 纯数据模型）、
   `Abstractions/`（`IPlugin` 入口与 `IPluginContext` 宿主服务面）、
   `Compatibility/`（`AbiVersion` 版本串解析与 headless `SdkAbi`）、
   `Events/`（`IPluginEvents`）。`Capabilities/`、`Settings/` 尚未落地（见 [plugins.md](plugins.md) §11），
   随其余能力启用。
- `StarPie.Sdk.Wpf.csproj`：SDK 的 WPF 类型契约面工程入口（UseWPF）；唯一允许的 ProjectReference 是 `StarPie.Sdk`；不产出 XAML；
  `StarPie.Sdk.Wpf/` 源码根目录**只允许** `Services/Icons/`、`Services/Shell/`
  （迁移期镜像旧相对路径）、`Compatibility/`（UiSdkAbi/DefaultAlcPolicy）与
  `Abstractions/Ui/`（插件 UI 契约，P3 起）——
  导出面与 ABI 政策由 `StarPie.Tests/SdkWpfBoundaryTests.cs` 收口。
- `StarPie.Host.csproj` / `GlobalUsings.cs`：宿主内核工程入口（net10.0 零 WPF；ProjectReference
  只许 `StarPie.Sdk`；不引用 `StarPie.Sdk.Wpf`）；`StarPie.Host/` 源码根目录**只允许**
  `Configuration/`（S2 配置读写/防抖落盘接缝/`AppDataPaths`）、`Localization/`（S3 本地化
  实现与 `Strings*.resx` 四语言）、`ShellIntegration/`（M5 自启注册表/内存整理）、`Icons/`（`IconCatalog`/
  `CustomIconStore`）、`Programs/`（内置来源 `ProgramScanner`、能力契约/聚合
  `ProgramSourceCapability`/`ProgramSourceAggregator`、`ShortcutResolver`）、`Themes/`（`ThemeEngine`
  主题引擎）、`Ports/`（`IThemeApplier` 等宿主→Ui 端口）、`Wheel/`（`WheelPalette*` 配色目录与
  解析）、`Gestures/`（手势内核）、`Actions/`（动作路由纯函数）与 `HostServices/`（插件可见宿主服务实现与每插件作用域）以及工程级
   `GlobalUsings.cs`；插件面落点 `PluginRuntime/`（`Discovery/`、`Manifest/`、`Admission/`、
   `State/`、`Hosting/`、`Loading/`、`Unloading/`、`Lifecycle/`、`Registry/`、`Diagnostics/`——
   插件发现/清单校验/准入/宿主状态/宿主侧运行时（启用装载、停用再启用、重载、更新与彻底移除）/collectible ALC 装载与
   安全点卸载管线/生命周期状态机/能力表/启动报告，可 headless 直接构造；随包插件工程落
   `plugins/src/StarPie.Plugin.Programs/`（首个 headless 插件），物理形态见 [plugins.md](plugins.md) §2；
  导出面与零 WPF 由 `StarPie.Tests/HostBoundaryTests.cs` 收口。
- 各工程源码根目录**只允许**上表与本小节列出的项；原型、HTML、临时脚本不得留在
  `StarPie.Ui/`、`StarPie.Sdk/`、`StarPie.Sdk.Wpf/`、`StarPie.Host/`、`StarPie.Tests/`
  目录下。

## 5. 例外登记（长期接受，新代码不得新增同类）

- `InputViewModel ↔ InputDialog`：遗留窗口名未对齐，仅保留不改名。
- 页面 VM 与页面 View 的领域/区块命名错位（`GeneralSettingsViewModel → AdvancedSettingsPage` 等）：**允许且是正典**（规则见 §3.2）。
- 外观聚合页留 Host（[assemblies.md](assemblies.md) §5.2 槽位 1）：属正典形态，非待迁出偏差。
