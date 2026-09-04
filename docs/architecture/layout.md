# 目录与文件架构

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；需要确认“某个路径放什么 / 新增文件落在哪”时读本篇。

## Canonical 目录树

以下为**应然结构**（正典）。当前代码与正典一致，无未决偏差（见文末）。

```text
WinPieGestures/
├── App.xaml / App.xaml.cs      # 宿主生命周期：单实例、异常、启动/退出编排
├── AppHost.cs                  # 宿主编排：Run/Dispose、托盘、语言资源、退出协调
├── Composition.cs              # DI 组合根（唯一）：注册与解析
├── AssemblyInfo.cs             # 程序集元数据
├── GlobalUsings.cs             # 工程级全局 using
├── WinPieGestures.csproj       # SDK 工程文件（.slnx 同层）
├── .editorconfig
├── Properties/
│   └── launchSettings.json     # 工程配置；不放源码
├── assets/
│   ├── app_icon.ico            # 应用图标（csproj ApplicationIcon 引用）
│   └── logo.png
├── Models/                     # 纯数据模型 + WPF-free 值类型
│   ├── AppConfig.cs
│   ├── WheelProfile.cs
│   ├── ActionItem.cs
│   ├── CustomColorPreset.cs
│   └── ColorMath.cs            # RgbColor（readonly struct）与纯颜色换算
├── Services/                   # 服务、副作用与横切件，按功能分子目录
│   ├── Actions/                # 动作执行
│   ├── Configuration/          # 配置读写、防抖保存、自启注册表
│   ├── Dialogs/                # 对话框服务（接口 + 实现 + 结果 record）
│   ├── Gestures/               # 手势管线、窗口上下文、轮盘工厂
│   ├── Localization/           # I18n
│   ├── Messages/               # IMessenger 消息与跨层通知载体
│   ├── Navigation/             # 导航状态与导航服务
│   ├── Programs/               # 程序扫描、目录与图标
│   └── Shell/                  # 主题、托盘、开发实例、内存整理
├── ViewModels/
│   ├── Pages/                  # 设置页 VM（单例）
│   ├── Dialogs/                # 对话框 VM
│   ├── Gestures/               # 扇区等轮盘子 VM
│   ├── Navigation/             # MainViewModel、NavigationItemViewModel
│   └── Wheel/                  # 轮盘 VM（按手势瞬态创建）
└── Views/
    ├── Pages/                  # 页面 View（XAML + code-behind）
    ├── Dialogs/                # 对话框 Window
    ├── Navigation/             # MainView、SidebarView
    ├── Wheel/                  # RadialWindow
    ├── Controls/               # 自定义控件与附加行为（纯 UI 适配）
    ├── Converters/             # 值转换器
    ├── Renderers/              # 轮盘样式渲染器（纯视觉）
    └── Styles/                 # 共享样式资源
```

## 各目录职责细则

| 目录 | 存放什么 | 不放什么 / 常见违规 |
|---|---|---|
| `Models/` | 配置 POCO（`AppConfig`、`WheelProfile`、`ActionItem`、`CustomColorPreset`）与 WPF-free 领域值类型（`RgbColor`/`ColorMath`） | 不引用 WPF 类型、服务、命令、消息、IMessenger；不放可注入服务、文件 IO、静态 Win32 工具 |
| `Services/{Feature}/` | 该功能的服务接口与实现（同目录）、编排器、纯函数、进程内 DTO | 不放 VM/View；不跨目录“借用”他人实现；静态工具需符合 [layering.md](layering.md)（Services） |
| `Services/Actions/` | `IActionExecutorService`、`ActionExecutorService`（系统调用层）、`ActionRouting`（纯函数 + `ActionRoute`/`KeyStroke`） | 路由决策不得散落进 VM/View；实现见 [gestures.md](gestures.md) |
| `Services/Configuration/` | `IConfigService`/`JsonConfigService`、`ISaveDebouncer`/`DispatcherSaveDebouncer`、`SettingsSaveOrchestrator`、`AppDataPaths`、`AutostartRegistry` | 页面 VM 不得直接碰配置文件路径或 `JsonSerializer`；实现见 [config.md](config.md) |
| `Services/Dialogs/` | `IDialogService`/`DialogService` + 各 `ShowXxx` 的可空结果 record | 对话框 Window/VM 不在此；文件对话框/MessageBox 不暴露给 VM/View，系统弹窗边界见 [dialogs.md](dialogs.md) |
| `Services/Gestures/` | `MouseHook`、`GestureController`、`GestureEngine`（含 `GesturePoint`/`GestureState`/`GestureReleaseResult`）、`IWindowContext`/`WindowContext`、`IWheelFactory`/`WheelFactory` | 手势判定纯逻辑（引擎）不得引用 WPF/Win32；实现见 [gestures.md](gestures.md) |
| `Services/Localization/` | `I18n`（含 `LanguageCode`） | VM/View 不得另建文案字典；实现见 [localization.md](localization.md) |
| `Services/Messages/` | `Messages.cs`（IMessenger 不可变消息）、`Notices.cs`（`NoticeKind`/`NoticeRequest` 等跨层弹窗载体） | 不放绑定语义；同页状态不得用消息替代绑定 |
| `Services/Navigation/` | `NavigationStore`、`INavigationService<T>`/`NavigationService<T>` | 页面状态不得散落导航器之外；实现见 [navigation.md](navigation.md) |
| `Services/Programs/` | `ProgramScanner`（IO 扫描）、`ProgramCatalog`（纯合并/去重）、`IconHelper` | 集成性质扫描逻辑不进 VM 单测；实现见 [programs.md](programs.md) |
| `Services/Shell/` | `IThemeService`/`ThemeService`、`TrayIconManager`、`DevInstance`、`MemoryOptimizer` | 托盘/主题决策不进 VM/View；实现见 [shell.md](shell.md) |
| `ViewModels/Pages/` | `{Domain}SettingsViewModel`、`AboutViewModel`（单例） | 不得引用 WPF 类型；不得出现 `event Action` 临时事件 |
| `ViewModels/Dialogs/` | `{Dialog}ViewModel`（含 `ScreenEyedropperViewModel`） | 不得持有 Window/MessageBox/对话框类型；形态见 [dialogs.md](dialogs.md) |
| `ViewModels/Gestures/` | 轮盘扇区等子 VM（如 `SlotViewModel`） | 不放服务 |
| `ViewModels/Navigation/` | `MainViewModel`、`NavigationItemViewModel` | 导航项文案/图标规则见 [navigation.md](navigation.md) |
| `ViewModels/Wheel/` | `IWheelViewModel`、`WheelViewModel` | 不注册容器；按手势由 `WheelFactory` 瞬态创建；见 [wheel.md](wheel.md) |
| `Views/Pages/` | `{Page}Page.xaml(.cs)`、`SettingsPageBase.cs`；页面无参构造 | 不注册容器；不编排业务/写配置/调服务 |
| `Views/Dialogs/` | `{Dialog}Window.xaml(.cs)`（对话框唯一形态） | 例外见 [naming.md](naming.md)；不放置无配对 Window 的散件 |
| `Views/Navigation/` | `MainView.xaml(.cs)`、`SidebarView.xaml(.cs)`；`MainView` 内集中页面 DataTemplate 映射 | 其它窗口/页面不得再合并样式字典（样式已 App 级单点合并） |
| `Views/Wheel/` | `RadialWindow.xaml(.cs)` | 轮盘状态决策在 `WheelViewModel`，窗口只做视觉呈现与生命周期；见 [wheel.md](wheel.md) |
| `Views/Controls/` | 自定义控件与附加行为（`HotkeyRecorderBox.cs`、`SpectrumCanvasBehavior.cs`），仅纯 UI 适配 | 有 `Command`/绑定等价物时不得新增行为 |
| `Views/Converters/` | `XxxToYyyConverter` | 转换器保持无状态、可静态复用 |
| `Views/Renderers/` | `IRadialStyleRenderer`、`StyleRendererFactory`、`BaseStyleRenderer`、各风格渲染器、`WheelPreviewRenderer` | 渲染器不订阅事件、不读写 VM、不反向依赖 Composition/服务；见 [wheel.md](wheel.md) |
| `Views/Styles/` | `Themes/*.xaml`（主题画刷令牌，五套同 key 集）、`ModernControls.xaml`（隐式默认/键控变体/共享模板，仅由 `App.xaml` 合并） | 对话框/轮盘窗口不隐式继承页面级样式；窗口/页面不再各自合并样式字典 |

## 根级文件规则

- `App.xaml` / `App.xaml.cs`：只处理单实例、异常、启动、退出和资源释放，不写业务（见 [host.md](host.md)）。
- `Composition.cs`：唯一 DI 组合根——`ServiceCollection` 注册、`BuildServiceProvider`、`CreateAppHost()` 解析；不持有托盘/主窗口/语言字典等宿主状态（见 [host.md](host.md)）。
- `AppHost.cs`：宿主编排——`Run`/`Dispose`、托盘创建与菜单、退出协调、语言资源字典（见 [host.md](host.md)）。
- `Properties/`、`assets/`：工程配置与二进制资源；**不放 C#/XAML 源码**。
- 源码根目录**只允许**上表列出的项；原型、HTML、临时脚本不得留在 `WinPieGestures/` 下。

## 现状偏差与待清理项

当前代码与正典目录结构一致，无未决偏差。

已消除的历史偏差（2026-09-04）：

- 页面/侧栏各自合并 `SettingsStyles.xaml`（7 处）收敛为 App 级单点合并 `ModernControls.xaml`；样式资源字典四层化（主题令牌/排版/控件样式/宿主装配，见 [ADR-0012](../adr/0012-resource-dictionary-architecture.md)）。
- 删除空目录 `WinPieGestures/Controls/`（自定义控件统一在 `Views/Controls/`）。
- 移除源码根杂项 `DashboardPrototype.html`（原型/杂项不进源码根）。
- 把 code-only 的 `ScreenEyedropperOverlay`（位于 `ColorPickerWindow.xaml.cs` 的 `#region`）拆分为独立 `Views/Dialogs/ScreenEyedropperWindow.xaml(.cs)`（见 [dialogs.md](dialogs.md)/[naming.md](naming.md)）。
- `Models/ColorMath.cs` 含 `RgbColor` 值类型：符合 [layering.md](layering.md)（Models）定义，确认为非偏差保留。

长期接受的例外（新代码不得新增同类）：

- `InputViewModel ↔ InputDialog`：遗留命名错位，见 [naming.md](naming.md)。
- `releases/`：冻结目录，永不作为实现依据。
- 页面 VM 与页面 View 的领域/区块命名错位（`GeneralSettingsViewModel → AdvancedSettingsPage` 等）：**允许且是正典**，见 [naming.md](naming.md)。
