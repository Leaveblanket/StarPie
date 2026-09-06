# 目录与文件架构

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；需要确认“某个路径放什么 / 新增文件落在哪”时读本篇。

## Canonical 目录树

以下为**应然结构**（正典）。当前代码与正典一致（B3/#76 临时面见文末“当前登记偏差”）。

```text
StarPie/
├── WinPieGestures/                # Host 宿主工程（exe，程序集 StarPie）：组合根、宿主壳窗口与业务纵向模块
│   ├── App.xaml / App.xaml.cs     # 宿主生命周期：单实例、异常、启动/退出编排
│   ├── AppHost.cs                 # 宿主编排：Run/Dispose、托盘、语言资源、退出协调
│   ├── Composition.cs             # DI 组合根（唯一）：注册与解析（含 B2 跨程序集回填缝，见 layering.md）
│   ├── DevInstance.cs             # 开发实例标记（H1）：--dev 互斥/触发键/自启保护
│   ├── Modules/                   # B3/#76 临时：M1/M5/Host 注册器 + 页面模板字典（随模块拆集迁出，见 navigation.md）
│   ├── ThemePaletteManager.cs     # 主题调色板整项替换（宿主层 internal，#46/ADR-0013）
│   ├── AssemblyInfo.cs            # 程序集元数据
│   ├── GlobalUsings.cs            # 工程级全局 using
│   ├── WinPieGestures.csproj      # SDK 工程文件（.slnx 同层）
│   ├── Properties/
│   │   └── launchSettings.json    # 工程配置；不放源码
│   ├── assets/
│   │   ├── app_icon.ico           # 应用图标（csproj ApplicationIcon 引用）
│   │   └── logo.png
│   ├── Services/
│   │   ├── Actions/               # 动作执行（M1）
│   │   ├── Dialogs/               # DialogService.cs 实现（S6 契约在 Core，见 dialogs.md）
│   │   ├── Gestures/              # 手势管线、窗口上下文、轮盘工厂（M1）
│   │   ├── Shell/                 # 主题（M4）、托盘/自启/内存（M5）
│   │   └── Wheel/                 # 轮盘视觉几何（M2 出口）
│   ├── ViewModels/
│   │   ├── Pages/                 # 设置页 VM（单例）
│   │   ├── Dialogs/               # 对话框 VM
│   │   ├── Gestures/              # 扇区等轮盘子 VM
│   │   ├── Navigation/            # ShellViewModel（MainViewModel 已迁 Core，B3/#76）
│   │   └── Wheel/                 # 轮盘 VM（按手势瞬态创建）
│   └── Views/
│       ├── Pages/                 # 页面 View（XAML + code-behind）
│       ├── Dialogs/               # 对话框 Window
│       ├── Navigation/            # MainView、SidebarView
│       ├── Wheel/                 # RadialWindow
│       ├── Controls/              # 自定义控件与附加行为（纯 UI 适配）
│       ├── Converters/            # 值转换器
│       ├── Renderers/             # 轮盘样式渲染器（纯视觉）
│       └── Styles/                # 共享样式资源
├── StarPie.Core/                  # 共享内核（WPF 类库，程序集 StarPie.Core；命名空间 WinPieGestures.*，B10 收口）
│   ├── StarPie.Core.csproj        # SDK 工程文件（RootNamespace=WinPieGestures；resx 生成器随 S3 迁入）
│   ├── GlobalUsings.cs            # 工程级全局 using（仅 Core 命名空间）
│   ├── Models/                    # 共享数据模型与 WPF-free 值类型（S2/R8）
│   │   ├── AppConfig.cs
│   │   ├── WheelProfile.cs
│   │   ├── ActionItem.cs
│   │   ├── CustomColorPreset.cs
│   │   ├── ColorMath.cs           # RgbColor（readonly struct）与纯颜色换算
│   │   ├── GesturePoint.cs        # 手势坐标点（WPF-free readonly struct）
│   │   ├── WheelPalette.cs        # 轮盘配色色值组（WPF-free）
│   │   ├── WheelPaletteCatalog.cs # 轮盘配色静态色值目录（唯一 hex 来源）
│   │   └── WheelPaletteParser.cs  # 轮盘配色方案解析（System/预设/Custom/坏值回落）
│   ├── Services/
│   │   ├── Configuration/         # S2：配置读写、防抖保存、AppDataPaths（dev 分支经组合根回填）
│   │   ├── Dialogs/               # S6 契约：IDialogService + 结果 record
│   │   ├── Icons/                 # S1：IconAssets/VectorIconItem（.lnk 解析经组合根回填缝）
│   │   ├── Localization/          # S3：ILocalizationService + Strings*.resx（四语言）
│   │   ├── Messages/              # S4：IMessenger 消息与跨层通知载体
│   │   └── Navigation/            # S5：导航内核 + NavigationCatalog/NavigationSlots（槽位表 0–4）
│   └── ViewModels/
│       └── Navigation/            # S5：NavigationItemViewModel、MainViewModel（B3/#76 迁入，目录驱动）
├── StarPie.Programs/              # M3 模块程序集（WPF 类库，程序集 StarPie.Programs；命名空间 WinPieGestures.*，B10 收口；B4/#77 起）
│   ├── StarPie.Programs.csproj    # SDK 工程文件（RootNamespace=WinPieGestures；零 Core/Host 依赖）
│   └── Services/Programs/         # M3：ProgramScanner、ProgramCatalog(+ProgramEntry)、ShortcutResolver
└── WinPieGestures.Tests/          # xUnit 单测（显式引用 Host、Core 与 Programs）
```

> 程序集归属：目录名在 `StarPie.Core/` 与 `WinPieGestures/` 中各自保持“命名空间 = 物理目录”；
> 共享内核目录（Models、Services/Configuration|Dialogs(契约)|Icons|Localization|Messages|Navigation、
> ViewModels/Navigation/NavigationItemViewModel.cs）只存在于 `StarPie.Core/`，业务目录只存在于
> `WinPieGestures/`；M3 业务目录（`Services/Programs/`）只存在于 `StarPie.Programs/`（B4/#77 起），
> 其余业务目录在 B5–B10 前仍留 `WinPieGestures/`。依赖方向见 [assemblies.md](assemblies.md) §3。

## 各目录职责细则

> 目录相对所属工程：共享内核件（`Models/`、`Services/Configuration/`、`Services/Dialogs/` 契约、
> `Services/Icons/`、`Services/Localization/`、`Services/Messages/`、`Services/Navigation/`、
> `ViewModels/Navigation/NavigationItemViewModel.cs` 与 `MainViewModel.cs`（B3/#76 迁入））位于
> `StarPie.Core/`；M3 三件（`ProgramScanner`/`ProgramCatalog`/`ShortcutResolver`，B4/#77 迁入）位于
> `StarPie.Programs/Services/Programs/`；其余位于 `WinPieGestures/`（Host）。

| 目录 | 存放什么 | 不放什么 / 常见违规 |
|---|---|---|
| `Models/` | 配置 POCO（`AppConfig`、`WheelProfile`、`ActionItem`、`CustomColorPreset`）与 WPF-free 领域值类型/纯函数（`RgbColor`/`ColorMath`、`GesturePoint`、轮盘配色 `WheelPalette`/`WheelPaletteCatalog`/`WheelPaletteParser`）；**B2/#75 起在 `StarPie.Core/Models/`** | 不引用 WPF 类型、服务、命令、消息、IMessenger；不放可注入服务、文件 IO、静态 Win32 工具 |
| `Services/{Feature}/` | 该功能的服务接口与实现（同目录）、编排器、纯函数、进程内 DTO | 不放 VM/View；不跨目录“借用”他人实现；静态工具需符合 [layering.md](layering.md)（Services） |
| `Services/Actions/` | `IActionExecutorService`、`ActionExecutorService`（系统调用层）、`ActionRouting`（纯函数 + `ActionRoute`/`KeyStroke`） | 路由决策不得散落进 VM/View；实现见 [gestures.md](gestures.md) |
| `Services/Configuration/` | `IConfigService`/`JsonConfigService`、`ISaveDebouncer`/`DispatcherSaveDebouncer`、`SettingsSaveOrchestrator`、`AppDataPaths`；**B2/#75 起在 `StarPie.Core/`（dev 目录分支经组合根回填 `AppDataPaths.IsDevInstance`）** | 页面 VM 不得直接碰配置文件路径或 `JsonSerializer`；实现见 [config.md](config.md) |
| `Services/Dialogs/` | **B2/#75 起分置**：契约 `IDialogService` + 各 `ShowXxx` 的可空结果 record 在 `StarPie.Core/`；实现 `DialogService` 在 `WinPieGestures/` | 对话框 Window/VM 不在此；文件对话框/MessageBox 不暴露给 VM/View，系统弹窗边界见 [dialogs.md](dialogs.md) |
| `Services/Gestures/` | `MouseHook`、`GestureController`、`GestureEngine`（+ `GestureState`/`GestureReleaseResult`）、`IWindowContext`/`WindowContext`、`IWheelFactory`/`WheelFactory` | 手势判定纯逻辑（引擎）不得引用 WPF/Win32；实现见 [gestures.md](gestures.md) |
| `Services/Icons/` | `IconAssets`（S1 共享图标资产出口：矢量清单/SVG 键目录/自定义图标存储/文件图标提取）、`VectorIconItem`；**B2/#75 起在 `StarPie.Core/`（.lnk 解析经组合根回填 `IconAssets.ResolveShortcutTarget`）** | 几何/程序解析类入口不在此目录（R6 三分，T3a–T3d/#65–#68 收口）；归属见 [modules.md](modules.md) §3 S1 |
| `Services/Localization/` | `ILocalizationService`/`LocalizationService` + `Strings*.resx`（`LanguageCode` 枚举随接口）；**B2/#75 起在 `StarPie.Core/`** | VM/View 不得另建文案字典；实现见 [localization.md](localization.md) |
| `Services/Messages/` | `Messages.cs`（IMessenger 不可变消息）、`Notices.cs`（`NoticeKind`/`NoticeRequest` 等跨层弹窗载体）；**B2/#75 起在 `StarPie.Core/`** | 不放绑定语义；同页状态不得用消息替代绑定 |
| `Services/Navigation/` | `NavigationStore`、`INavigationService<T>`/`NavigationService<T>`、`NavigationCatalog`/`NavigationSlots`（槽位表 0–4）；**B2/#75 起在 `StarPie.Core/`** | 页面状态不得散落导航器之外；实现见 [navigation.md](navigation.md) |
| `Services/Shell/` | `IThemeService`/`ThemeService`、`TrayIconManager`、`AutostartRegistry`（R1，M5）、`MemoryOptimizer` | 托盘/自启/主题决策不进 VM/View；实现见 [shell.md](shell.md) |
| `Services/Wheel/` | `WheelGeometry`（M2 轮盘视觉几何出口：扇区/核图标几何） | 实现见 [wheel.md](wheel.md) |
| `ViewModels/Pages/` | `{Domain}SettingsViewModel`、`AboutViewModel`（单例） | 不得引用 WPF 类型；不得出现 `event Action` 临时事件 |
| `ViewModels/Dialogs/` | `{Dialog}ViewModel`（含 `ScreenEyedropperViewModel`） | 不得持有 Window/MessageBox/对话框类型；形态见 [dialogs.md](dialogs.md) |
| `ViewModels/Gestures/` | 轮盘扇区等子 VM（如 `SlotViewModel`） | 不放服务 |
| `ViewModels/Navigation/` | Core：`NavigationItemViewModel`（B2/#75）、`MainViewModel`（B3/#76 迁入且目录驱动）；Host：`ShellViewModel`（B1/D3 Host 壳窗口壳层 VM） | 导航项文案/图标规则见 [navigation.md](navigation.md) |
| `ViewModels/Wheel/` | `IWheelViewModel`、`WheelViewModel` | 不注册容器；按手势由 `WheelFactory` 瞬态创建；见 [wheel.md](wheel.md) |
| `Views/Pages/` | `{Page}Page.xaml(.cs)`、`SettingsPageBase.cs`；页面无参构造 | 不注册容器；不编排业务/写配置/调服务 |
| `Views/Dialogs/` | `{Dialog}Window.xaml(.cs)`（对话框唯一形态） | 例外见 [naming.md](naming.md)；不放置无配对 Window 的散件 |
| `Views/Navigation/` | `MainView.xaml(.cs)`、`SidebarView.xaml(.cs)`；`MainView` 为纯壳（B3/#76 起页面 DataTemplate 已迁出至 `Modules/` 模块模板字典） | 其它窗口/页面不得再合并样式字典（样式已 App 级单点合并） |
| `Views/Wheel/` | `RadialWindow.xaml(.cs)` | 轮盘状态决策在 `WheelViewModel`，窗口只做视觉呈现与生命周期；见 [wheel.md](wheel.md) |
| `Views/Controls/` | 自定义控件与附加行为（`HotkeyRecorderBox.cs`、`SpectrumCanvasBehavior.cs`），仅纯 UI 适配 | 有 `Command`/绑定等价物时不得新增行为 |
| `Views/Converters/` | `XxxToYyyConverter` | 转换器保持无状态、可静态复用 |
| `Views/Renderers/` | `IRadialStyleRenderer`、`StyleRendererFactory`、`BaseStyleRenderer`、各风格渲染器、`WheelPreviewRenderer`；渲染器只消费 `WheelPalette` 解析结果构造画刷，不内联方案 hex 表 | 渲染器不订阅事件、不读写 VM、不反向依赖 Composition/服务；见 [wheel.md](wheel.md) |
| `Views/Styles/` | `Themes/*.xaml`（主题画刷令牌，五套同 key 集）、`ModernControls.xaml`（隐式默认/键控变体/共享模板，仅由 `App.xaml` 合并） | 对话框/轮盘窗口不隐式继承页面级样式；窗口/页面不再各自合并样式字典 |
| `Modules/`（B3/#76 临时） | M1/M5/Host 模块注册器（`RegisterNavigation`）+ 页面模板字典（`M1/M5/HostPageTemplates.xaml`，App 级每模块一次静态合并） | 不承载业务；导航自治样板，随 B6/B9 模块拆集迁出 |

### 模块程序集目录（B4/#77 起）

| 目录 | 存放什么 | 不放什么 / 常见违规 |
|---|---|---|
| `StarPie.Programs/Services/Programs/` | M3 程序扫描与目录：`ProgramScanner`（IO 扫描）、`ProgramCatalog`（纯合并/去重）+ `ProgramEntry`、`ShortcutResolver`（快捷方式解析出口） | 集成性质扫描逻辑不进 VM 单测；图标资产在 Core `Services/Icons/`（组合根注入的 S1 委托补全，本程序集零 Core 依赖）；实现见 [programs.md](programs.md) |

## 根级文件规则

- `App.xaml` / `App.xaml.cs`：只处理单实例、异常、启动、退出和资源释放，不写业务（见 [host.md](host.md)）。
- `Composition.cs`：唯一 DI 组合根——`ServiceCollection` 注册、`BuildServiceProvider`、`CreateAppHost()` 解析；不持有托盘/主窗口/语言字典等宿主状态（见 [host.md](host.md)）。
- `AppHost.cs`：宿主编排——`Run`/`Dispose`、托盘创建与菜单、退出协调、语言资源字典（见 [host.md](host.md)）。
- `DevInstance.cs`：开发实例标记（H1）——`--dev` 隔离互斥/配置目录/触发键并保护正式自启项（见 [host.md](host.md)）。
- `ThemePaletteManager.cs`：宿主层主题调色板整项替换（internal，自包含加载/缓存/冻结；仅 `AppHost` 编排调用，见 [shell.md](shell.md)）。
- `Properties/`、`assets/`：工程配置与二进制资源；**不放 C#/XAML 源码**。
- `StarPie.Core.csproj` / `GlobalUsings.cs`：共享内核工程入口；`StarPie.Core/` 源码根目录**只允许**
  上表列出的共享内核目录与文件（B2/#75 起）。
- `StarPie.Programs.csproj`：M3 模块程序集工程入口（B4/#77 起）；`StarPie.Programs/` 源码根目录
  **只允许** `Services/Programs/`（`ProgramScanner`/`ProgramCatalog`/`ShortcutResolver`）。
- 各工程源码根目录**只允许**上表与本小节列出的项；原型、HTML、临时脚本不得留在
  `WinPieGestures/`、`StarPie.Core/` 或 `StarPie.Programs/` 下。

## 现状偏差与待清理项

当前代码与正典目录结构一致（B3/#76 临时面见下）。

### 当前登记偏差（临时，B3/#76）

- `WinPieGestures/Modules/`：M1/M5/Host 模块注册器与页面模板字典（`M1/M5/HostPageTemplates.xaml`，
  App 级每模块一次静态合并）。ADR-0016 目标态中注册器/模板字典属模块程序集，B3 在单程序集内以临时面
  先行验证“新增页面不碰 Host”路径；本目录随 B6/B9 模块拆集迁出，届时自本表移除。

已消除的历史偏差（2026-09-04）：

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
