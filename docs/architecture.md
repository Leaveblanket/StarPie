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
| `docs/architecture/assemblies.md` | 程序集地图与依赖方向（15 程序集现状） | 程序集归属、依赖方向、导航槽位时 |
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
  Navigation 目录/槽位契约、Dialogs 契约与结果 record、轮盘工厂接口）、`ViewModels/`
  （Pages 预览源接口、Wheel 轮盘只读接口）；迁移期源码镜像旧相对路径、命名空间保持
  `StarPie.*` 不变，导出面与全仓类型唯一性由 `StarPie.Tests/SdkBoundaryTests.cs` 收口；
  WPF 契约件（主题/图标/扫描）留 P1.4 入 `StarPie.Sdk.Wpf`。
- 共享内核（P1.3/#112 收窄）：`StarPie.Core/`（WPF 类库，程序集 `StarPie.Core`）只余 S2 配置与
  S3 本地化实现（Configuration/Localization，P1.5 起迁 Host 内核）——Models/Messages/导航
  目录契约与宿主回调契约 `Services/AppHostDelegates` 已迁 `StarPie.Sdk`，对话框与预览 Profile
  契约随 SDK 收口一并迁入（旧 `Dialogs.Contracts`/`Gestures.Contracts` 暂留空壳），扫描/SPI
  契约仍驻 Programs.Contracts（WPF 面，P1.4）；导航运行时主体
  （NavigationStore/NavigationExecutor/MainViewModel/NavigationItemViewModel）归 Host
  `StarPie.Ui/Services/Navigation/` 与 `StarPie.Ui/ViewModels/Navigation/`，命名空间不变；
  S1 图标资产仍独立成集（契约 IIconAssetService 驻 Icons.Contracts，WPF 面）；共享 UI 基建已去共享化——
  通用转换器与 `ModernControls.xaml`（全局
  控件样式字典）归 Host `StarPie.Ui/Views/Converters|Styles/`、`HotkeyRecorderBox`（控件+样式
  字典）归 `StarPie.Gestures`、共享页面基类 `SettingsPageBase` 已删除（四页 XAML 根直承
  `UserControl`）；命名空间统一为 `StarPie.*`（跨程序集共享命名空间树）。
- 模块程序集：`StarPie.Programs/`（WPF 类库，程序集 `StarPie.Programs`）承载 M3 程序扫描
  与目录（ProgramScanner/ProgramCatalog/ShortcutResolver 与模块注册器
  ProgramsModuleRegistrar），M3 出口契约（IProgramScanner/ProgramEntry/ProgramCatalog/SPI
  IShortcutTargetResolver）驻 `StarPie.Programs.Contracts`，runtime 只引用自身契约 +
  `StarPie.Icons.Contracts`（S1 契约边），不引用共享内核；命名空间统一为 `StarPie.*`。
- 模块程序集（首个带 DI 的模块程序集）：`StarPie.Shell/`（WPF 类库，程序集 `StarPie.Shell`）
  承载 M5 壳层服务与系统设置面（TrayIconManager/AutostartRegistry/MemoryOptimizer/
  GeneralSettingsViewModel+AdvancedSettingsPage 与正式模块注册器 ShellModuleRegistrar），
  单向依赖共享内核；命名空间统一为 `StarPie.*`。
- 模块程序集（出口契约随实现方下沉）：`StarPie.Theme/`（WPF 类库，程序集 `StarPie.Theme`）
  承载 M4 界面主题实现（ThemeService、AppThemePaletteManager（public，Host AppHost 装配面）、
  五套主题字典 Views/Styles/Themes、InterfaceThemeSettingsViewModel 与模块注册器
  ThemeModuleRegistrar），`StarPie.Theme.Contracts/`（WPF 类库，程序集
  `StarPie.Theme.Contracts`）承载出口契约 `IThemeService`（命名空间不变）；Theme runtime →
  Core + Theme.Contracts 单向；命名空间统一为 `StarPie.*`。
- 模块程序集（出口契约随实现方下沉）：`StarPie.Wheel/`（WPF 类库，程序集 `StarPie.Wheel`）
  承载 M2 轮盘与渲染（WheelViewModel/RadialWindow/Views/Renderers 样式渲染器与预览、
  Views/Converters 核图标预览转换器、Models/WheelPalette* 配色目录与解析、Services/Wheel
  WheelGeometry 视觉几何与 WheelFactory 实现、模块注册器 WheelModuleRegistrar），
  出口契约（IWheelFactory/IWheelViewModel/IWheelAppearanceState）原驻 `StarPie.Wheel.Contracts`、
  P1.3/#112 随 SDK 收口迁入 `StarPie.Sdk`（命名空间不变，工程暂留空壳）；runtime → Sdk +
  Core + Theme.Contracts + Icons.Contracts 等契约单向（runtime 允许边清零）；
  命名空间统一为 `StarPie.*`。
- 模块程序集：`StarPie.Gestures/`（WPF 类库，程序集 `StarPie.Gestures`）承载 M1 手势与动作
  （手势管线 Services/Gestures（MouseHook/GestureController/GestureEngine/IWindowContext/
  WindowContext）、动作执行 Services/Actions（IActionExecutorService/ActionExecutorService/
  ActionRouting）、触发+手势设置页（BehaviorSettingsViewModel+TriggerSettingsPage、
  ProfileListViewModel+SlotViewModel+GesturesSettingsPage）与模块注册器
  GesturesModuleRegistrar），出口契约 `IProfilePreviewSource` 原驻 `StarPie.Gestures.Contracts`、
  P1.3/#112 随 SDK 收口迁入 `StarPie.Sdk`（命名空间不变，工程暂留空壳）；
  runtime → Sdk + Core + Icons.Contracts 等契约单向（runtime 允许边清零）；
  命名空间统一为 `StarPie.*`。
- 共享基础设施模块的独立落点：`StarPie.Dialogs/`（WPF 类库，程序集 `StarPie.Dialogs`）承载
  S6 对话框实现（DialogService、五对对话框 VM/Window、SpectrumCanvasBehavior 与模块注册器
  DialogsModuleRegistrar；契约 `IDialogService` 与结果 record 原驻 `StarPie.Dialogs.Contracts`、
  P1.3/#112 随 SDK 收口迁入 `StarPie.Sdk`），
  runtime → Sdk + Programs.Contracts + Icons.Contracts + Core + Theme.Contracts
  单向（runtime 允许边清零）；命名空间统一为 `StarPie.*`。
- S1 图标服务独立成集：`StarPie.Icons.Contracts/`（WPF 类库，程序集
  `StarPie.Icons.Contracts`）承载契约四件（`IIconAssetService`/`IconCatalog`/`CustomIconItem`/
  `VectorIconItem`，命名空间 `StarPie.Services.Icons` 不变、零程序集依赖）；`StarPie.Icons/`
  （WPF 类库，程序集 `StarPie.Icons`）承载实现 `IconAssetService` 与注册器
  `IconsModuleRegistrar`——Icons → Icons.Contracts + Programs.Contracts（SPI 契约边）+
  Core（S2 AppDataPaths）单向，实现 runtime 只被 Host/测试引用。
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
├── StarPie.Ui/                  # Ui 集（WinExe，程序集名保持 StarPie；唯一含 XAML 与入口）
├── StarPie.Sdk/                 # SDK 集（net10.0；零 WPF 零第三方包；目标态插件唯一引用面）
├── StarPie.Sdk.Wpf/             # SDK 的 WPF 类型契约面（UseWPF；不产出 XAML）
├── StarPie.Host/                # 宿主内核集（net10.0；零 WPF，可 headless 单测）
├── StarPie.Core/                # 共享内核程序集（只余 S2/S3 运行时件；契约/模型已迁 SDK，见 layout.md）
├── StarPie.Icons.Contracts/     # S1 图标契约程序集（见 layout.md）
├── StarPie.Icons/               # S1 图标实现程序集（见 layout.md）
├── StarPie.Programs.Contracts/  # M3 扫描/SPI 契约程序集（见 layout.md）
├── StarPie.Dialogs.Contracts/   # S6 对话框契约工程（P1.3/#112 迁 SDK 后暂留空壳，见 layout.md）
├── StarPie.Theme.Contracts/     # M4 界面主题契约程序集（见 layout.md）
├── StarPie.Wheel.Contracts/     # M2 轮盘契约工程（P1.3/#112 迁 SDK 后暂留空壳，见 layout.md）
├── StarPie.Gestures.Contracts/  # M1 预览 Profile 契约工程（P1.3/#112 迁 SDK 后暂留空壳，见 layout.md）
├── StarPie.Dialogs/             # S6 对话框实现模块程序集（见 layout.md）
├── StarPie.Programs/            # M3 程序扫描与目录模块程序集（见 layout.md）
├── StarPie.Shell/               # M5 壳层与系统设置模块程序集（见 layout.md）
├── StarPie.Theme/               # M4 界面主题模块程序集（见 layout.md）
├── StarPie.Wheel/               # M2 轮盘与渲染模块程序集（见 layout.md）
├── StarPie.Gestures/            # M1 手势与动作模块程序集（见 layout.md）
├── StarPie.Tests/        # xUnit 单元测试
└── tests/                       # pywinauto e2e（不在本文档体系展开）
```

> 插件化目标态（三集 `StarPie.Sdk` / `StarPie.Host` / `StarPie.Ui` + `StarPie.Sdk.Wpf` + `plugins/`）见 [ADR-0027](adr/0027-plugin-architecture-and-host-sdk-ui-split.md) 与 [plugins.md](architecture/plugins.md)；P1.2/#111 已建四集骨架并把 exe 工程改名为 `StarPie.Ui`，15 集归并在 P1.3–P1.10 分批落地，`plugins/` 自 P2 起加入。

测试约定：单测文件平铺于 `StarPie.Tests` 根、命名 `{被测类型}Tests.cs`、命名空间镜像被测类型；测试工程**显式** `ProjectReference` 四集、Core 与已拆模块程序集（不依赖传递引用，见 [assemblies.md](architecture/assemblies.md)）；页面/服务/对话框 VM 单测直接构造并注入依赖，不从容器解析；被测类型保持 `public`（不使用 `InternalsVisibleTo`，见 [layering.md](architecture/layering.md)）。

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
| 0023 | `docs/adr/0023-module-contracts-hard-boundary-and-core-narrowing.md` | 模块契约硬边界与共享内核收窄（契约入 *.Contracts、S1 成集） | Active |
| 0024 | `docs/adr/0024-terminology-final-state-and-full-rename.md` | 术语终态与全仓正名（WheelPalette/WheelStyle/NavPage/工程名 + config 迁移） | Active |
| 0025 | `docs/adr/0025-design-time-preview.md` | 设计时预览协议（设计期资源注入与运行视口锚定） | Active |
| 0026 | `docs/adr/0026-runtime-baseline-and-windows-sdk-projection.md` | 运行时基线与 Windows SDK 投影版本政策 | Active |
| 0027 | `docs/adr/0027-plugin-architecture-and-host-sdk-ui-split.md` | 插件体系与三集物理形态（第三方能力插件 / ALC 真卸载 / 宿主独占呈现） | Active（部分被 0028 修订） |
| 0028 | `docs/adr/0028-plugin-ui-hosting-and-host-managed-lifecycle.md` | 插件 UI 宿主化（允许 XAML/Window/资源字典；宿主托管登记、清理与验证） | Active（决策 4/6 被 0030 修订） |
| 0029 | `docs/adr/0029-plugin-trust-model.md` | 插件信任模型（目标态签名 + 审核白名单；首期开发者模式准入；进程内全信任披露） | Active |
| 0030 | `docs/adr/0030-ui-plugin-unload-semantics-downgrade.md` | UI 插件不承诺 ALC 真卸载（卸载语义降级为托管清理 + 可验证 + 泄漏隔离 + 重启生效） | Active |
| 0031 | `docs/adr/0031-e2e-silent-background-run.md` | e2e 静默后台化（`--background` 窗口形态 + 选中态驱动导航 + 运行器脚本） | Active |

状态取值：`Active` 现行；`Superseded by NNN` 被 NNN 整体取代；`Active（被 NNN 修订）` 部分条款被演进。历史决策记录（0002/0006/0007/0008/0010/0017/0018/0019/0020/0021/0022）已删除——其现行规范在对应叶子、历史在 git，编号不再复用。各文件头部 Status 为权威，本表为速览。
