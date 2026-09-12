# StarPie 架构文档（入口）

> **阅读方式**：先读本文，按任务跳转到 `docs/architecture/` 下的叶子文件；不要把整卷叶子一次性注入上下文。
>
> 现行架构规范（as-built normative）分散在叶子文件中；代码结构、注册、映射或规范变化时，**同步更新对应叶子**，并保持本文路由表登记正确。

## 1. 文档体系与分工

| 文档 | 内容 | 何时读 |
|---|---|---|
| `CONTEXT.md`（仓库根） | 领域术语词汇表 | 术语疑问、新增领域术语时 |
| `docs/adr/` | 难逆转/令人惊讶/真实权衡的决策理由 | 想了解“为什么这样设计”时 |
| `docs/architecture.md`（本文） | 架构文档入口与任务路由 | 任何架构问题先读这里 |
| `docs/architecture/*.md` | 各主题与模块规范（叶子） | 按下表任务跳转 |
| `docs/architecture/modules.md` | 模块划分地图（12 模块 + 修整单元判据） | 归属争议、扩展点验收时 |
| `docs/architecture/assemblies.md` | 程序集地图与依赖方向（as-built） | 程序集归属、依赖方向、导航槽位时 |
| `docs/architecture/design-time-preview.md` | 设计时预览协议（设计期资源注入/视口登记/样例数据） | XAML 设计器预览、DesignTimeResources、设计视口时 |

冲突优先级：叶子规范为准（现行规范）；ADR 解释“为什么”，不推翻现行规范；若需要改变规范且满足 ADR 三条件（难逆转 / 无上下文会惊讶 / 真实权衡），先新增 ADR 再回填叶子。

## 2. 按任务路由（渐进式披露）

| 你要做什么 | 读哪个文件 |
|---|---|
| 某个路径放什么 / 新增文件落位 | [layout.md](architecture/layout.md) |
| 分层依赖矩阵 / 可见性 / Model/Service/VM/View 边界 | [layering.md](architecture/layering.md) |
| 命名规则 / 页面映射表 / 对话框配对 | [naming.md](architecture/naming.md) |
| 注释规范（XML 文档注释 / 行注释） | [comments.md](architecture/comments.md) |
| 启动退出 / 单实例与开发实例 / AppHost 编排 / Composition 注册 / 窗口隐藏流程 | [host.md](architecture/host.md) |
| 配置读写 / 防抖保存 / 导入导出 | [config.md](architecture/config.md) |
| 设置页导航 / 页面 DataTemplate 映射 | [navigation.md](architecture/navigation.md) |
| 对话框实现 / 对话框唯一形态 | [dialogs.md](architecture/dialogs.md) |
| 手势状态机 / 动作路由与执行 | [gestures.md](architecture/gestures.md) |
| 轮盘 VM / RadialWindow / 样式渲染器 | [wheel.md](architecture/wheel.md) |
| 程序扫描与目录 | [programs.md](architecture/programs.md) |
| 界面主题(AppTheme)配置与解析 / XAML 令牌与整项替换 / 主题设置面 | [interface-theme.md](architecture/interface-theme.md) |
| VS XAML 设计器预览 / Properties/DesignTimeResources.xaml / 设计期资源与视口 | [design-time-preview.md](architecture/design-time-preview.md) |
| 托盘 / 开机自启 / 内存整理 / 主窗口壳层行为 / 高级设置面 | [shell.md](architecture/shell.md) |
| 本地化文案键(resx) / 语言切换与回退链 / 运行时语言字典投影 | [localization.md](architecture/localization.md) |
| IMessenger 消息 / 弹窗通知载体 | [messages.md](architecture/messages.md) |
| 模块划分 / 归属争议 / 扩展点验收 | [modules.md](architecture/modules.md) |
| 程序集地图 / 程序集依赖方向 / 导航槽位 | [assemblies.md](architecture/assemblies.md) |
| 模块间接合缝编目 / 缝裁决 / 程序集依赖基线 | [seams.md](architecture/seams.md) |
| 插件体系（目标态）：SDK/SDK.Wpf、装载/卸载、能力、插件 UI 托管、文件架构 | [plugins.md](architecture/plugins.md) |
| 新增功能（原型 A–F 清单） | [extending.md](architecture/extending.md) |
| 动手改代码前的底线（禁止事项） | [prohibitions.md](architecture/prohibitions.md) |

## 3. 技术栈

- .NET 10 / WPF（`net10.0-windows10.0.19041.0`、`UseWPF`，Ui 集程序集名 `StarPie`）；运行时段与
  windows 投影段的演进政策见 [ADR-0026](adr/0026-runtime-baseline-and-windows-sdk-projection.md)。
  插件化四集骨架（P1.2/#111）：`StarPie.Sdk`/`StarPie.Host` 为 `net10.0` 零 WPF（SDK 另零第三方
  包）、`StarPie.Sdk.Wpf` 为 WPF 类型契约面、`StarPie.Ui` 为唯一含 XAML 与入口的 WinExe；
  依赖方向与机械断言见 [assemblies.md](architecture/assemblies.md) §3。
- SDK 集（P1.3/#112 headless 收口）：`StarPie.Sdk/`（net10.0、零 WPF、零第三方包）承载跨集共享的
  纯托管契约与模型——`Models/`（AppConfig/WheelProfile/ActionItem/CustomColorPreset 与
  RgbColor/ColorMath/GesturePoint）、`Services/`（AppHostDelegates、Messages 消息与通知载体、
  Navigation 目录/槽位契约、Dialogs 契约与结果 record、轮盘工厂接口、Icons 图标条目与
  .lnk 解析 SPI、Programs 扫描契约与纯规则）、`ViewModels/`
  （Pages 预览源接口、Wheel 轮盘只读接口）、`Plugins/`（`IPlugin` 入口与 `IPluginContext`
  宿主服务面）；迁移期源码镜像旧相对路径、命名空间保持
  `StarPie.*` 不变，导出面与全仓类型唯一性由 `StarPie.Tests/SdkBoundaryTests.cs` 收口；
  WPF 契约件（主题、图标资产服务）已随 P1.4/#113 迁入 `StarPie.Sdk.Wpf`，导出面与 ABI/装载政策
  由 `StarPie.Tests/SdkWpfBoundaryTests.cs` 收口。
- 宿主内核（P1.5/#114 归并）：`StarPie.Host/`（net10.0 零 WPF、零 XAML，只引用 `StarPie.Sdk`）
  承载内核运行时——`Kernel/Configuration/`（`IConfigService`/`JsonConfigService`、
  `ISaveDebouncer`/`AppDataPaths`、`SettingsSaveOrchestrator`，命名空间
  `StarPie.Kernel.Configuration`）与 `Kernel/Localization/`（`ILocalizationService`/
  `LocalizationService` + `Strings*.resx` 四语言，命名空间 `StarPie.Kernel.Localization`）；
  S1/M3 的 WPF-free 逻辑同驻本集——`Icons/`（`IconCatalog` 静态纯目录 + `CustomIconStore`
  自定义图标目录，命名空间 `StarPie.Icons`）与 `Programs/`（`ProgramScanner` 八源扫描 +
  `ShortcutResolver` .lnk 解析，命名空间 `StarPie.Programs`）；插件运行时驻
  `PluginRuntime/`——`Discovery/`（安装目录 + 用户目录发现与包内容违规）、`Manifest/`（清单解析与
  校验）、`Admission/`（准入四态与开发者模式开关）、`State/`（宿主状态 `plugin-state.json`）、
  `Loading/`（collectible ALC 与装载管线：共享契约/框架回退默认 ALC、包内私有解析、
  入口类型不缓存）、`Lifecycle/`（生命周期状态机：装载链、headless/UI 两条卸载链与隔离终态）、
  `Diagnostics/`（启动扫描与 `plugin-startup-report.json`），命名空间 `StarPie.PluginRuntime.*`。
  内核件可 headless 直接构造，导出面与零 WPF 由 `StarPie.Tests/HostBoundaryTests.cs` 收口。
  WPF 亲和的落盘防抖器 `DispatcherSaveDebouncer` 作为 Ui 侧适配器驻 `StarPie.Ui/Adapters/`
  （实现内核防抖接缝，命名空间 `StarPie.Adapters`）；S1 的 WPF 图像构造由
  `StarPie.Ui/Services/Icons/IconAssetService.cs` 承载（实现 Sdk.Wpf 的 `IIconAssetService`，
  组合内核自定义图标目录与 .lnk 契约）。
- 设计期投影字典（ADR-0025）：`StarPie.Ui/Services/Localization/DesignTimeStrings.xaml`
  （Page 编译的惰性 BAML，只被 `Properties/DesignTimeResources.xaml` 资源锚在设计期合并，
  运行时永不合并）与同目录生成脚本——设计期投影壳 `StarPie.Core` 已删除，字典随 Ui 集承载
  （见 [design-time-preview.md](architecture/design-time-preview.md)）。
- 契约与模型已迁 `StarPie.Sdk`（P1.3/#112：Models/Messages/导航目录与槽位/`AppHostDelegates`、
  对话框与预览 Profile 契约）与
  `StarPie.Sdk.Wpf`（P1.4/#113：主题与图标资产服务契约件）；导航运行时主体（NavigationStore/NavigationExecutor/MainViewModel/
  NavigationItemViewModel）归 Ui 集 `StarPie.Ui/Services/Navigation/` 与
  `StarPie.Ui/ViewModels/Navigation/`，命名空间不变；共享 UI 基建已去共享化——通用转换器与
  `ModernControls.xaml`（全局控件样式字典）归 `StarPie.Ui/Views/Converters|Styles/`、
  `HotkeyRecorderBox`（控件+样式字典）归 `StarPie.Ui/Views/Controls|Styles/`（P1.6/#115 随
  M1 归并入 Ui）、共享页面基类 `SettingsPageBase`
  已删除（四页 XAML 根直承 `UserControl`）；命名空间统一为 `StarPie.*`（跨程序集共享命名空间树）。
- 模块程序集（S1/M3 归并后不再独立成集）：S1 图标资产的 WPF-free 部分与 M3 程序扫描/目录
  驻宿主内核（`StarPie.Host/Icons|Programs/`），图像构造驻 Ui
  （`StarPie.Ui/Services/Icons/`），契约（图标条目与 .lnk SPI、扫描三件与纯规则）在
  `StarPie.Sdk`、`IIconAssetService` 在 `StarPie.Sdk.Wpf`；旧 `StarPie.Icons`/
  `StarPie.Programs` 与各自 Contracts 工程已撤销，DI 注册回到组合根（`HostCoreContributor`）登记。
- M5 壳层与系统设置面（P1.10/#119 归并，不再独立成集）：开机自启注册表 `AutostartRegistry`
  （`[SupportedOSPlatform("windows")]`）与内存整理 `MemoryOptimizer` 落
  `StarPie.Host/Kernel/ShellIntegration/`（零 WPF、纯托管 + P/Invoke）；托盘
  `TrayIconManager`/`TrayMenuEntry` 驻 `StarPie.Ui/Services/Shell/`，高级页
  `GeneralSettingsViewModel`+`AdvancedSettingsPage` 驻 Ui 的 `ViewModels/Pages|Views/Pages/`，
  贡献者 `ShellContributor`+`ShellPageTemplates.xaml` 驻 `StarPie.Ui/Modules/`；
  壳窗口 `MainView`/`ShellViewModel` 仍属 Host 壳（见前）——命名空间统一为 `StarPie.*`。
- M4 界面主题（P1.8/#117 归并，不再独立成集）：主题引擎 `ThemeEngine`（状态/解析/切换与系统
  跟随重解析，零 WPF）与宿主→Ui 端口 `IThemeApplier` 驻 `StarPie.Host/Themes|Ports/`；
  `IThemeService` 实现 `ThemeService`（窗口 DWM 应用与系统深浅色监听）、调色板适配器
  `AppThemePaletteManager`、五套主题字典 `Themes/`、主题设置子 VM 与贡献者
  `ThemeContributor` 驻 `StarPie.Ui/`；出口契约 `IThemeService` 驻 `StarPie.Sdk.Wpf/Services/Shell/`；
  命名空间统一为 `StarPie.*`。
- M2 轮盘与渲染（P1.7/#116 归并，不再独立成集）：配色目录与色值解析
  `WheelPalette`/`WheelPaletteCatalog`/`WheelPaletteParser`（WPF-free）驻 `StarPie.Host/Wheel/`
  （命名空间 `StarPie.Wheel`）；WPF 亲和件——`WheelGeometry`（直接构造 `Geometry`）与
  `WheelFactory` 驻 `StarPie.Ui/Services/Wheel/`，`WheelViewModel` 与外观设置子 VM 驻
  `StarPie.Ui/ViewModels/{Wheel,Pages}/`，`RadialWindow`、样式渲染器/预览与核图标预览转换器分驻
  `StarPie.Ui/Views/{Wheel,Renderers,Converters}/`，DI 注册由 `StarPie.Ui/Modules/`
  的 `WheelContributor` 登记（无导航页）；出口契约
  （IWheelFactory/IWheelViewModel/IWheelAppearanceState）驻 `StarPie.Sdk`（命名空间不变）；
  命名空间统一为 `StarPie.*`。
- M1 手势与动作（P1.6/#115 归并，不再独立成集）：可 headless 的手势内核
  （`GestureEngine`+`GestureState`/`GestureReleaseResult`、`IWindowContext`/`WindowContext`）与
  动作路由纯函数（`ActionRouting` + `ActionRoute`/`KeyStroke`/`SystemCommand`）分驻
  `StarPie.Host/Gestures|Actions/`（命名空间 `StarPie.Gestures`/`StarPie.Actions`，零 WPF、
  可 headless 直接构造）；WPF 亲和件——`MouseHook`、`GestureController`（Dispatcher 封送）与
  `ActionExecutorService`/`IActionExecutorService`（默认 MessageBox 错误上报）驻
  `StarPie.Ui/Services/{Gestures,Actions}/`，触发/手势两页 VM 与 View、热键录制控件
  （`Views/Controls|Styles/`）、设计期样例与贡献者 `GesturesContributor` 驻 Ui 集
  对应目录；出口契约 `IProfilePreviewSource` 驻 `StarPie.Sdk`（命名空间不变）；
  命名空间统一为 `StarPie.*`。
- S6 对话框实现（P1.10/#119 归并，不再独立成集）：`DialogService` 驻
  `StarPie.Ui/Services/Dialogs/`，五对对话框 VM/Window 与 `SpectrumCanvasBehavior` 分驻
  `StarPie.Ui/ViewModels/Dialogs|Views/Dialogs|Views/Controls/`，贡献者
  `DialogsContributor` 驻 `StarPie.Ui/Modules/`（端口只在 Ui 内部）；契约
  `IDialogService` 与结果 record 驻 `StarPie.Sdk`（命名空间不变）。
- S1 图标资产三分解（契约/目录/图像构造）：`IIconAssetService` 契约驻 `StarPie.Sdk.Wpf`；
  条目类型 `CustomIconItem`/`VectorIconItem` 与 .lnk SPI `IShortcutTargetResolver` 驻
  `StarPie.Sdk/Services/Icons/`（命名空间 `StarPie.Services.Icons` 不变）；静态纯目录
  `IconCatalog` 与自定义图标目录 `CustomIconStore` 驻 `StarPie.Host/Icons/`（命名空间
  `StarPie.Icons`）；WPF 图像构造 `IconAssetService` 驻 `StarPie.Ui/Services/Icons/`，
  实现 Sdk.Wpf 契约并委托内核图标目录。
- `CommunityToolkit.Mvvm`：MVVM 唯一框架（`ObservableObject`、`[ObservableProperty]`、`[RelayCommand]`、`WeakReferenceMessenger`）。
- `Microsoft.Extensions.DependencyInjection`：仅用于 `Composition.cs` 组合根。
- 本地化：`Strings*.resx`（zh-CN 中性 + zh-TW/en/ja 卫星），`VocaDb.ResXFileCodeGenerator` 强类型 + `ILocalizationService` 实例服务。
- 单元测试：`StarPie.Tests`（xUnit v3，运行平台 Microsoft.Testing.Platform，直接 `new` + 手写替身，不用 mocking 框架）。
- e2e 测试：`tests/`（pywinauto，pytest），规范不在此文档体系展开；验证义务分层（提交级全量 xUnit + e2e 免跑判定、合入门全量）见 [git-commits](agents/git-commits.md)。
- 运行配置：`config.json`（宽松读取：大小写不敏感、允许注释与尾逗号；缺文件自动播种默认值；向后兼容为 Hard Constraint）。

## 4. 仓库边界

```text
StarPie/
├── CONTEXT.md
├── AGENTS.md
├── StarPie.slnx                 # 解决方案（登记全部工程；构建/测试入口，见 layout.md）
├── Directory.Build.props        # 统一构建属性（TFM/可空性/隐式 using/分析器级别/根命名空间）
├── Directory.Packages.props     # 中央包管理（包版本唯一集中处，csproj 不写版本）
├── docs/
│   ├── architecture.md          # 本文（入口）
│   ├── architecture/            # 架构叶子文档
│   ├── adr/                     # 决策记录（ADR-0001 ~ 0029，编号保留历史断档）
│   ├── agents/                  # Agent 工作流文档
├── StarPie.Ui/                  # Ui 集（WinExe，程序集名保持 StarPie；唯一含 XAML 与入口；含图标资产 WPF 图像构造）
├── StarPie.Sdk/                 # SDK 集（net10.0；零 WPF 零第三方包；目标态插件唯一引用面）
├── StarPie.Sdk.Wpf/             # SDK 的 WPF 类型契约面（UseWPF；不产出 XAML；承载主题/图标资产服务契约与 ABI 政策）
├── StarPie.Host/                # 宿主内核集（net10.0；零 WPF，可 headless 单测；内核运行时在 Kernel/，图标目录/程序扫描在 Icons|Programs/）
├── StarPie.Tests/               # xUnit 单元测试（显式引用四集，不依赖传递引用）
└── tests/                       # pywinauto e2e（不在本文档体系展开）
```

> 插件化目标态（三集 `StarPie.Sdk` / `StarPie.Host` / `StarPie.Ui` + `StarPie.Sdk.Wpf` + `plugins/`）见 [ADR-0027](adr/0027-plugin-architecture-and-host-sdk-ui-split.md) 与 [plugins.md](architecture/plugins.md)；P1 已完成三集物理形态（15 集全部撤销、设计期字典随 Ui 承载、内置与未来插件贡献者共用注册管线），`plugins/` 自 P2 起加入。

测试约定：单测文件平铺于 `StarPie.Tests` 根、命名 `{被测类型}Tests.cs`、命名空间镜像被测类型；测试工程**显式** `ProjectReference` 四集（不依赖传递引用，见 [assemblies.md](architecture/assemblies.md)）；页面/服务/对话框 VM 单测直接构造并注入依赖，不从容器解析；被测类型保持 `public`（不使用 `InternalsVisibleTo`，见 [layering.md](architecture/layering.md)）。

## 5. 分层速览

```text
App / AppHost / Composition  # 宿主编排（AppHost）+ 装配与解析（Composition，唯一解析点）
      |
      v
ViewModels ---> Views        # 经 DataContext/DataTemplate；View 不反向引用 VM 之外
      |
      v
Services ---> Models
```

完整依赖矩阵、命名空间与可见性、Models/Services/ViewModels/Views 边界见 [layering.md](architecture/layering.md)。

## 6. 维护义务

1. 规范内容变更只改**对应叶子文件**；新增主题时先建叶子并在本文路由表登记。
2. 新增决策若满足 ADR 三条件（难逆转 / 无上下文会惊讶 / 真实权衡），先新增 ADR，再把结论回填对应叶子；反之只改叶子。**ADR 只记决策理由四要素（问题 / 选择 / 为什么 / 代价）**，禁止写 grill 会话 Q/A 纪要、issue 号、日期、实施批次、回填清单与“已落地”流水——这些归 git 与 issue。
3. 新增用户可见文案时补齐四语言键值（zh-CN / zh-TW / en / ja）——声明式文案经 XAML `{DynamicResource}`、动态文案经 `ILocalizationService` 即时取词（见 [localization.md](architecture/localization.md)）。
4. 叶子增删、文件路径变化时同步更新本文（文档体系表 + 路由表 + 仓库边界树）。
5. 验证义务与测试策略分层：提交级 build + 全量 xUnit + e2e 免跑判定；合入 main 前全量 xUnit + 全量 e2e；不按模块拆测试、不移除 e2e 每用例冷启动（见 [git-commits](agents/git-commits.md)）。
6. ADR 头部必带状态（Active / Superseded by NNN / Active（部分被 NNN 修订））与修订指针；状态速览见附录。
7. 叶子与 ADR 不记录“已落地/已清零/批次流水/日期快照”：完成即删，历史归 git 与 issue。
8. 程序集地图与依赖方向只许 `assemblies.md` §2/§3 一份正典，其它叶子引用不抄写。
9. CONTEXT 只收领域术语；架构词（宿主/模块/程序集/M1–M5 等）正典在 `modules.md`/`assemblies.md`。
10. 任务型盘点文档必须带“关闭即删”义务，不保留为常驻文档。

## 附录：ADR 索引

| 编号 | 文件 | 主题 | 状态 |
|---|---|---|---|
| 0001 | `docs/adr/0001-mvvm-with-communitytoolkit.md` | MVVM 采用 CommunityToolkit | Active |
| 0003 | `docs/adr/0003-application-host-restructure.md` | 应用宿主重构 | Active（演进见 0011） |
| 0004 | `docs/adr/0004-dialog-service-design.md` | 对话框服务设计 | Active（实现落点见 dialogs.md/assemblies.md） |
| 0005 | `docs/adr/0005-di-container-for-navigation.md` | 容器化导航 | Active（注册源被 0016 修订） |
| 0009 | `docs/adr/0009-view-code-behind-whitelist.md` | View code-behind 白名单 | Active（0014 补充） |
| 0011 | `docs/adr/0011-composition-apphost-split.md` | Composition 与 AppHost 拆分 | Active（注册源被 0016 修订） |
| 0012 | `docs/adr/0012-resource-dictionary-architecture.md` | 样式资源架构（主题令牌 XAML 化与单点合并） | Active（决策 2 被 0013 修订） |
| 0013 | `docs/adr/0013-localization-theme-overhaul.md` | 本地化/主题推翻性重构（resx+强类型+实例服务 / 主题整项替换+实时跟随） | Active |
| 0014 | `docs/adr/0014-wheel-palette-module-boundary-and-appearance-split.md` | 轮盘配色模块归属与外观 VM 拆分 | Active |
| 0015 | `docs/adr/0015-module-map-and-ownership.md` | 模块划分共识（12 模块地图、归属裁定与修整单元判据） | Active（决策 3 被 0016、判据被 0023 修订） |
| 0016 | `docs/adr/0016-assembly-split-target-and-roadmap.md` | 程序集化目标态与分批执行 | Active（目标态被 0023 演进） |
| 0023 | `docs/adr/0023-module-contracts-hard-boundary-and-core-narrowing.md` | 模块契约硬边界与共享内核收窄（契约入 *.Contracts、S1 成集） | Active（部分被 0027 修订） |
| 0024 | `docs/adr/0024-terminology-final-state-and-full-rename.md` | 术语终态与全仓正名（WheelPalette/WheelStyle/NavPage/工程名 + config 迁移） | Active |
| 0025 | `docs/adr/0025-design-time-preview.md` | 设计时预览协议（设计期资源注入与运行视口锚定） | Active（字典落点被 0027 修订） |
| 0026 | `docs/adr/0026-runtime-baseline-and-windows-sdk-projection.md` | 运行时基线与 Windows SDK 投影版本政策 | Active |
| 0027 | `docs/adr/0027-plugin-architecture-and-host-sdk-ui-split.md` | 插件体系与三集物理形态（第三方能力插件 / ALC 真卸载 / 宿主独占呈现） | Active（部分被 0028 修订） |
| 0028 | `docs/adr/0028-plugin-ui-hosting-and-host-managed-lifecycle.md` | 插件 UI 宿主化（允许 XAML/Window/资源字典；宿主托管登记、清理与验证） | Active（决策 4/6 被 0030 修订） |
| 0029 | `docs/adr/0029-plugin-trust-model.md` | 插件信任模型（目标态签名 + 审核白名单；首期开发者模式准入；进程内全信任披露） | Active |
| 0030 | `docs/adr/0030-ui-plugin-unload-semantics-downgrade.md` | UI 插件不承诺 ALC 真卸载（卸载语义降级为托管清理 + 可验证 + 泄漏隔离 + 重启生效） | Active |
| 0031 | `docs/adr/0031-e2e-silent-background-run.md` | e2e 静默后台化（`--background` 窗口形态 + 选中态驱动导航 + 运行器脚本） | Active（窗口形态/托盘/截图被 0032 修订） |
| 0032 | `docs/adr/0032-e2e-silent-visible-window.md` | e2e 静默形态改屏内左上角（点击穿透 + 托盘可见 + 失败截图可用） | Active |

状态取值：`Active` 现行；`Superseded by NNN` 被 NNN 整体取代；`Active（被 NNN 修订）` 部分条款被演进。历史决策记录（0002/0006/0007/0008/0010/0017/0018/0019/0020/0021/0022）已删除——其现行规范在对应叶子、历史在 git，编号不再复用。各文件头部 Status 为权威，本表为速览。
