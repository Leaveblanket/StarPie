# 程序集地图（as-built）

> 本文记录四集结构（[ADR-0016](../adr/0016-assembly-split-target-and-roadmap.md) +
> [ADR-0023](../adr/0023-module-contracts-hard-boundary-and-core-narrowing.md)）的地图视图：
> 程序集划分、程序集级依赖规则、导航槽位表与注册/可见性契约。

> **形态**：`StarPie.Sdk`/`StarPie.Host`/`StarPie.Ui` + `StarPie.Sdk.Wpf` + 能力插件，
> 见 [ADR-0027](../adr/0027-plugin-architecture-and-host-sdk-ui-split.md) 与 [plugins.md](plugins.md)。
> exe 工程目录/文件名为 `StarPie.Ui`（程序集名与发布产物保持 `StarPie`）；设计期字典随 Ui 集承载；
> 注册管线统一为内置贡献者有序清单（§6）；插件面其余能力的推进按 issue 与 [modules.md](modules.md) 跟踪。
>
> **程序集现状**：四集（`StarPie.Sdk`/`StarPie.Sdk.Wpf`/`StarPie.Host` + exe `StarPie.Ui`）
> + 测试工程 + 随包插件工程 `plugins/src/StarPie.Plugin.Programs`（只引 SDK）与 `plugins/src/StarPie.Plugin.SampleUi`（引 SDK + Sdk.Wpf）（划分见 §2）。
> 代码现状以 §2–§7 与各叶子（`docs/architecture/*.md`）
> 为准，冲突时叶子优先。概念模块地图与归属裁定见 [modules.md](modules.md)（ADR-0015）。

## 1. 何时读本文

> 本文是**程序集物理面正典**：程序集地图（§2）、程序集级依赖规则（§3）、导航与“壳”的分层语义（§4–§5）、
> DI 与可见性（§6–§7）、接合缝编目（§8）。概念模块（12 模块）的划分、归属裁定与扩展点验收见
> [modules.md](modules.md)；按任务找文档见入口 [architecture.md](../architecture.md) §2。

## 2. 程序集地图（as-built）

| 程序集 | 形态 | 承载（摘要） |
|---|---|---|
| `StarPie`（项目 `StarPie.Ui`） | WinExe | Ui 集：组合根与宿主壳、导航运行时、各模块 WPF 亲和件与贡献者、插件 UI 托管 |
| `StarPie.Sdk` | 类库（net10.0） | 跨集共享的纯托管契约 / 模型 / DTO |
| `StarPie.Sdk.Wpf` | WPF 类库 | 跨集共享的 WPF 类型契约面 |
| `StarPie.Host` | 类库（net10.0） | 宿主内核：配置/本地化/壳集成、图标与程序扫描、主题引擎、手势与动作、插件运行时 |

- **`StarPie`（项目 `StarPie.Ui`，WinExe）**：
  - **入口与壳**：`App`/`ShellHost`/`SettingsConsole`/`Composition`/`DevInstance`；Host 壳窗口（`MainView` 全文件 + `ShellViewModel`）；导航运行时主体（`Services/Navigation/`：NavigationStore/NavigationExecutor（含 INavigationExecutor）；`ViewModels/Navigation/`：MainViewModel/NavigationItemViewModel，命名空间不变）。
  - **注册管线**：`Modules/`——`ICompositionContributor` + `BuiltInContributors` 有序清单 + 宿主编排贡献者 `HostCoreContributor` 与外观页 `HostPageContributor`。
  - **页面**：Appearance 聚合页与插件管理页（`PluginManagerPage`/`PluginManagerViewModel` + 插件设置区块 `PluginSettingsSectionViewModel`，模板字典 `Modules/HostCorePageTemplates.xaml`）；设计期投影字典 `Services/Localization/DesignTimeStrings.xaml`（Page 编译惰性 BAML）与生成脚本。
  - **共享 UI 基建**：通用转换器 `Views/Converters/` + `ModernControls.xaml`（`Views/Styles/`；App.xaml 本地单点实例化/合并、资源 key 不变）。
  - **模块件**：
    - S1 图标资产的 WPF 图像构造（`Services/Icons/IconAssetService`）与 M3 的扫描消费端；
    - M4：`Services/Shell/`（`IThemeService` 实现 `ThemeService`）、`Adapters/`（调色板适配器 `AppThemePaletteManager`）、`Themes/`（五套主题字典）、主题设置子 VM 与 `ThemeContributor`；
    - M2：`Services/Wheel/`（WheelGeometry/WheelFactory）、`ViewModels/Wheel|Pages/`（WheelViewModel/WheelAppearanceSettingsViewModel）、`Views/Wheel|Renderers/`（RadialWindow/样式渲染器与预览）、`Views/Converters/`（核图标预览转换器）与 `WheelContributor`；
    - M1：`Services/Gestures/`（MouseHook/GestureController）、`Services/Actions/`（IActionExecutorService/ActionExecutorService）、`ViewModels/Pages|Gestures/`（BehaviorSettingsViewModel/ProfileListViewModel/SlotViewModel）、`Views/Pages|Controls|Styles|DesignTime/`（触发+手势页、热键录制控件与样式字典）与 `GesturesContributor` + `GesturesPageTemplates.xaml`；
    - M5：`Services/Shell/`（TrayIconManager/TrayMenuEntry 与插件菜单合成 `TrayMenuComposer`）、`ViewModels/Pages/`（GeneralSettingsViewModel）、`Views/Pages/`（AdvancedSettingsPage）与 `ShellContributor` + `ShellPageTemplates.xaml`；
    - S6：`Services/Dialogs/`（DialogService）、`ViewModels/Dialogs/`（五对对话框 VM）、`Views/Dialogs|Controls/`（五对对话框 Window 与 SpectrumCanvasBehavior）与 `DialogsContributor`。
  - **插件 UI 托管**：`PluginHosting/`（资产登记表、每插件资源根、视图/窗口/命令/菜单/定时器/动画/订阅托管、UI 线程释放编排与泄漏验证器）与固定扩展点 `PluginHosting/Extensions/`（插件页 `PluginPage`、设置区块 `PluginSettingsSection`、托盘菜单项 `PluginMenuItem` 的登记与出账，导航页经宿主签发的 `NavPlugin_<插件 id>` 进目录）；宿主装配面为 `DialogService.SetOwner` 绑定/解绑等。
- **`StarPie.Sdk`（类库，net10.0）**：`Models/`（AppConfig/WheelProfile/ActionItem/CustomColorPreset/ColorMath/GesturePoint）；`Services/`（AppHostDelegates、Messages、Navigation 目录与槽位契约、Dialogs 契约与 6 结果 record、IWheelFactory、Themes 界面主题名目录 AppThemeNames、Icons 条目类型与 .lnk SPI、Programs 扫描契约与纯规则）；`ViewModels/`（Pages 预览源接口、Wheel 轮盘只读接口）。源码树镜像旧相对路径、命名空间保持 `StarPie.*` 不变，导出面与类型唯一性由 `SdkBoundaryTests` 收口。
- **`StarPie.Sdk.Wpf`（WPF 类库）**：`Services/Icons/`（IIconAssetService）、`Services/Shell/`（IThemeService）、`Abstractions/Ui/`（插件 UI 契约：IPluginUiModule/IPluginUiContext/IUiDispatcher 与注册描述符）、`Compatibility/`（UiSdkAbi 主次版本/兼容判定 + DefaultAlcPolicy 默认 ALC 统一加载政策）。导出面与 ABI 政策由 `SdkWpfBoundaryTests` 收口。
- **`StarPie.Host`（类库，net10.0）**：宿主内核（零 WPF、零 XAML）——`Configuration`（S2 配置读写/防抖落盘接缝/AppDataPaths）、`Localization`（S3 本地化实现 + `Strings*.resx` 四语言）、`ShellIntegration`（M5 开机自启注册表 + 内存整理，命名空间 `StarPie.ShellIntegration`）、`Icons/`（`IconCatalog` 静态纯目录 + `CustomIconStore` 自定义图标目录，命名空间 `StarPie.Icons`）、`Programs/`（内置来源 `ProgramScanner` + 程序来源能力契约/聚合 + `ShortcutResolver`，命名空间 `StarPie.Programs`）、`Themes/`（`ThemeEngine`，命名空间 `StarPie.Themes`）、`Ports/`（`IThemeApplier`，命名空间 `StarPie.Ports`）、`Wheel/`（`WheelPalette`/`WheelPaletteCatalog`/`WheelPaletteParser`，命名空间 `StarPie.Wheel`）、`Gestures/`（`GestureEngine`/`GestureState`/`GestureReleaseResult`/`IWindowContext`/`WindowContext`，命名空间 `StarPie.Gestures`）、`Actions/`（`ActionRouting` + `ActionRoute`/`KeyStroke`/`SystemCommand`，命名空间 `StarPie.Actions`）、`HostServices/`（插件可见宿主服务与每插件作用域）、`PluginRuntime/`（插件发现/清单/准入/状态/运行时/装载/卸载/生命周期/能力表/诊断）。件件可 headless 直接构造（Windows-only 项以 `[SupportedOSPlatform]` 标注）；导出面与零 WPF 泄漏由 `HostBoundaryTests` 收口。

## 3. 程序集级依赖规则

### 四集基线（机械断言在 `StarPie.Tests/FourSetBoundaryTests.cs` 与 `RuntimeNoCrossReferenceTests.cs`）

```text
StarPie.Ui（WinExe，程序集名 StarPie；唯一含 XAML 与入口）
     ├──→ StarPie.Host ──→ StarPie.Sdk
     ├──→ StarPie.Sdk
     └──→ StarPie.Sdk.Wpf ──→ StarPie.Sdk
```

- Ui 是唯一组合根：必须显式引用 `StarPie.Sdk`/`StarPie.Sdk.Wpf`/`StarPie.Host`（测试工程亦
  显式引用四集，不依赖传递引用）；其余工程不得引用 Ui。
- `StarPie.Sdk` 零 WPF、零第三方包（csproj 无 `PackageReference`，程序集引用面只含平台程序集，
  TFM 为 `net10.0` 无 windows 平台投影）、零 ProjectReference。
- `StarPie.Host` 零 WPF：TFM `net10.0`，不引用 `StarPie.Sdk.Wpf`（plugin-contracts.md §2 约束 7），
  ProjectReference 只许 `StarPie.Sdk`。
- `StarPie.Sdk.Wpf` 是 WPF 类型契约面（`UseWPF`、windows TFM、带 Windows 平台投影），不产出
  XAML；ProjectReference 只许 `StarPie.Sdk`；引用面只含平台/WPF 程序集
  （`SdkWpfBoundaryTests`/`RuntimeNoCrossReferenceTests` 机械断言）。
  本集的导出面是**引用面**（哪些类型可被引用），不等于插件的能力面——插件的可达面定义与守护见
  [plugin-contracts.md](plugin-contracts.md) §1 与 [ADR-0047](../adr/0047-plugin-reachable-surface.md)。
- 四集不得引用旧集（跨集只经 SDK）：任何旧集工程引用（含测试工程的
  传递依赖）都属违规；设计期投影字典随 Ui 集承载。

- **落位口径（WPF 亲和件落 Ui）**：`StarPie.Host` 零 WPF 硬约束细化到件——直接构造 WPF 类型
  （如 `WheelGeometry` 的 `Geometry`）、持有 `Application.Current.Dispatcher` 或默认 `MessageBox`
  的 WPF 亲和件一律留 `StarPie.Ui`；端口化推迟到出现真实 headless 需求时再引入（`Ports/` 新增项随需求走）。
- 模块 runtime 对内核/契约**单向**：M*/S* runtime 只引用自身契约与经 `StarPie.Sdk`/
  `StarPie.Sdk.Wpf` 消费的契约面能力（扫描/.lnk 与图标条目经 SDK，主题与图标资产服务经
  Sdk.Wpf；对话框/轮盘工厂与 VM 接口/预览源契约统一经 `StarPie.Sdk`），不反向引用
  其它模块 runtime；内核运行时（S2/S3）在 `StarPie.Host`，模块 runtime 驻 Ui/Host，无跨集过渡边；托底深色探针等宿主能力经组合根注入委托提供，dev 分支等
  构建期判定由内核 `AppDataPaths` 编译期定死（无跨程序集回填）；
  `Ui → 全部`（仅经贡献者清单登记各模块与装配宿主对象，不引用模块内部）。
- **件级落位**：S1/M3、M1、M4、S6 见 §2（WPF 亲和件 → Ui、纯逻辑 → Host、契约 → SDK）；
  DI 与导航登记由所属贡献者在本集完成（§6）。
- 模块 runtime 之间**零 ProjectReference**（ADR-0023）：跨模块依赖一律经 `StarPie.Sdk`/
  `StarPie.Sdk.Wpf` 契约边（对话框链的程序扫描/.lnk 与图标条目经 SDK、主题与图标资产
  服务经 Sdk.Wpf；M1/M2/M5→S6 与 M2→M1 预览源、M1→M2 经 StarPie.Sdk）；仅 Host（组合根）
  引用全部 runtime（组合根在 Ui 集 `Composition`）。
- 共享放行清单（config 模型字段、i18n 键、消息/通知类型、图标资产；共享视图基础设施不放行 UI 实现件，
  落点与资源缝放行面见 modules.md §2.3）维持 modules.md §2.3，不视为跨模块违规。

## 4. 导航与“壳”的分层语义（防混淆）

导航契约与“壳”相关概念分四层，术语别混用：

| 层 | 归属 | 程序集 | 内容 |
|---|---|---|---|
| 导航目录/槽位契约 | S5（纯契约共享模块） | `StarPie.Sdk` | NavigationCatalog、NavigationSlot/NavigationSlots、NavigationPageRegistration |
| 导航运行时/状态 | H1（宿主壳，与 R4/D3 同判据——单一消费方在 Host） | StarPie（exe） | NavigationStore、NavigationExecutor（含 INavigationExecutor）、MainViewModel、NavigationItemViewModel（命名空间不变） |
| 壳层服务与系统集成 | M5 | `StarPie.Ui`（+ `StarPie.Host/ShellIntegration`） | 托盘、自启、内存、Advanced 设置面 |
| Host 壳窗口 | H1（宿主壳） | StarPie（exe） | MainView 全文件、ShellViewModel、SettingsConsole/ShellHost/App/Composition（导航 VM 与主框架同窗，物理同居 Host） |

`MainView.xaml.cs` 与 `ShellViewModel` **归 Host 壳窗口**（ADR-0016）；M5 只拥有壳层服务与设置面。
导航运行时（含主框架导航区 VM `MainViewModel`）与壳窗口同判据归 Host（R9）。

## 5. 导航架构（as-built：模块自治注册）

### 5.1 机制

- `StarPie.Sdk` 提供目录契约 `NavigationCatalog`：固定页经
  `RegisterPage<TViewModel>(槽位, automationId, titleKey, iconData, …)` 注册；插件页经
  `RegisterPluginPage(pluginId, identifier, automationId, titleKey, iconData, viewModelType, factory)`
  追加、`RemovePluginPage(identifier)` 摘除，条目按「固定槽位升序 → 插件页注册顺序」返回，
  目录变更经 `Changed` 通知（纯契约；运行时不居 SDK）。
- Host 提供导航执行入口（`NavigationExecutor`/`INavigationExecutor`）：归 H1，为宿主内部件
  （非跨程序集"已批准解析缝"，见本文 §8；第二消费方出现时按
  ADR-0023 契约归属判据裁决落点——属全局机制/数据入共享内核，属某模块出口契约下沉该模块
  Contracts（如 S6 先例：契约 `IDialogService` 入 `StarPie.Sdk`））。
- 内置贡献者自报导航项与页面模板字典（`DataTemplate DataType=VM → View`）；Host 在 App 资源里**每模块一次** pack URI 静态合并。
- 新增页面 = 所属模块内部（贡献者声明导航项 + 模板字典加条目），**不碰 Host**。
- 新增模块 = Host 登记：程序集引用 + 贡献者清单一行 + 模板字典合并（各一次，放行共享面）。
- e2e `AutomationId` 沿用 `NavPage0..4`，随槽位稳定；插件页 AutomationId 由宿主按
  `NavPlugin_<插件 id>` 签发，固定页标识不受插件增删影响。

  as-built：`GesturesContributor`/`ShellContributor` 与其页面模板字典均驻
  Ui 集，模板字典本地合并；exe 内还承载 Host 外观聚合页的
  HostPageContributor/HostPageTemplates 与宿主直持页（插件管理）的
  HostCoreContributor/HostCorePageTemplates（留 Host，模板归属与导航登记同一贡献者）。

  as-built：插件页运行期注册——`PluginUiCoordinator` 把宿主导航目录交给
  `PluginUiHost`，`IPluginUiContext.RegisterPage` 写入目录并计入资产登记表，卸载随资产出账
  摘除；`MainViewModel` 订阅目录变更重建导航项（固定页在前、插件页按注册顺序在后）。

### 5.2 导航槽位表（正典）

| 槽位 | AutomationId | TitleKey | 页面 VM | 页面 View | 程序集 |
|---|---|---|---|---|---|
| 0 | `NavPage0` | `PageTrigger` | `BehaviorSettingsViewModel` | `TriggerSettingsPage` | Ui（M1） |
| 1 | `NavPage1` | `PageAppearance` | `AppearanceSettingsViewModel`（聚合壳） | `AppearanceSettingsPage` | Host |
| 2 | `NavPage2` | `PageGestures` | `ProfileListViewModel` | `GesturesSettingsPage` | Ui（M1） |
| 3 | `NavPage3` | `PageAdvanced` | `GeneralSettingsViewModel` | `AdvancedSettingsPage` | Ui（M5） |
| 4 | `NavPage4` | `PagePlugins` | `PluginManagerViewModel` | `PluginManagerPage` | Host（直持页） |

缺失/重复/未知槽位由收口测试拦截；槽位表是侧边栏顺序唯一正典。MainViewModel 按目录注册
构造导航项，导航执行走 `INavigationExecutor` 目录执行缝；运行时主体（含 MainViewModel）在
Host、目录契约驻 `StarPie.Sdk`，程序集归属见 §2/§4。

## 6. DI 与注册契约（as-built）

- **统一注册管线（内置贡献者与未来插件贡献者共用入口）**：`ICompositionContributor`（`Id`/`Order`/`RegisterServices(IServiceCollection)`/可选 `RegisterNavigation(NavigationCatalog)`，末者以接口默认实现表达“无导航页”）驻 `StarPie.Ui/Modules/`；`BuiltInContributors.CreateAll` 给出**有序清单**——`HostCoreContributor`（宿主编排与内核接入：宿主回调委托包、程序扫描/.lnk、图标资产、配置/本地化/防抖、消息、导航运行时、壳层 VM、插件运行时与插件管理页；槽位 4）、`HostPageContributor`（槽位 1 聚合页）、`ThemeContributor`（M4：主题服务与主题设置子 VM；无导航页）、`WheelContributor`（M2：轮盘工厂 `IWheelFactory→WheelFactory` 与外观设置子 VM；无导航页）、`GesturesContributor`（M1：手势管线、两页 VM 与 `IProfilePreviewSource` 别名；槽位 0/2）、`ShellContributor`（M5：高级页 VM；槽位 3）、`DialogsContributor`（S6：`IDialogService→DialogService`；无导航页）；S1/M3 无导航页，其服务由 `HostCoreContributor` 登记。
- **注册顺序 ≠ 解析时机**：组合根（`Composition`，驻 Ui 集）按三阶段显式分离——(1) 注册期（有序清单先写导航目录并 `Validate()` 收口五槽，再写容器描述符）→ (2) 唯一 `BuildServiceProvider` → (3) `CreateShellHost` 在配置加载后目录驱动 eager 解析全部页面 VM 与壳层直持 VM，并交付设置台会话工厂。贡献者只登记不解析，解析点只在组合根。（dev 实例标记不占装配阶段：由内核 `AppDataPaths` 按构建配置编译期定死，见 [host.md](host.md)。）
- **M3（程序扫描，插件化）**：由 `HostCoreContributor` 登记
  `IShortcutTargetResolver→ShortcutResolver`、能力表（声明 `program-source@1` 契约 + 内置来源）
  与 `IProgramScanner→ProgramSourceAggregator`（契约驻 `StarPie.Sdk/Services/Programs|Icons/`，
  内置来源与聚合驻 `StarPie.Host/Programs/`，深扫来源由随包插件经能力表贡献）。
- **S6（`DialogsContributor`，驻 Ui）**：RegisterServices 登记
  `IDialogService→DialogService` 注册（工厂经容器解析 SDK 的程序扫描/.lnk 契约与 Sdk.Wpf 的
  `IIconAssetService`/`IThemeService` 契约面），S6 无导航页
  故走接口默认空实现；组合根仅保留 `DialogService.SetOwner(MainView)` 回填面。
- **S1（图标资产）**：由 `HostCoreContributor` 登记内核 `CustomIconStore`（`StarPie.Host/Icons/`）与
  Ui 侧 `IIconAssetService→IconAssetService`（`StarPie.Ui/Services/Icons/`，构造解析内核图标
  目录与 `IShortcutTargetResolver`）。
- **根解析集中**：Host Composition 唯一 `BuildServiceProvider` / `CreateShellHost`；贡献者不解析、不持容器。
- **已批准解析缝**：`WheelFactory`、`DialogService`、贡献者注册体（仅登记不解析）。导航目录执行缝
  不是跨程序集缝（`INavigationExecutor` 归 Host，为宿主内部件，见本文 §8）。
- `AppHostDelegates` 是 SDK 公开契约（`StarPie.Sdk/Services/AppHostDelegates.cs`）；
  Host 组合根持有实例并由 `HostCoreContributor` 登记单例，ShellHost 构造后回填实现（契约名不随类改名）。贡献者只依赖
  SDK/Sdk.Wpf 契约面与 Host 内核面，不在贡献者内引用其它模块 runtime 类型。
- 页面 VM 不在启动期 eager 解析：作用域是设置台会话（scoped 注册），由导航执行缝经
  `ConsolePageSession` 取用；设置台会话对象图（导航区 VM + 壳区 VM + `SettingsConsole`）
  由组合根交付的工厂在每次开窗时构造（工厂内开启会话作用域）。
- 不引入子容器、Generic Host、Autofac、Prism（ADR-0016）。

## 7. 可见性（as-built）

- 不引入 `InternalsVisibleTo`（layering.md 维持）。
- 模块公开面 = 贡献者清单/接口入口 + 被测 public 类型；贡献者实现类与模块内部细节保持 internal。
- Host 只经贡献者清单登记模块，不引用模块内部。
- 跨集必需的内部件（如 `TrayIconManager`）单独裁决为 public 或经接口注入。

  既有先例：`TrayIconManager`/`TrayMenuEntry` 驻 Ui 集，由同集 `ShellHost` 装配，维持
  public（被测/装配类型）；`AutostartRegistry` 驻 `StarPie.Host/ShellIntegration/`，
  裁决为 **public**（`[SupportedOSPlatform("windows")]`）——由 Ui 集的 `ShellContributor`
  委托接线，跨集边界使 internal 不成立。

  同判据先例：M4 的 `AppThemePaletteManager` 驻 Ui 集，归 **internal**——
  装配方 `ShellHost` 与实现在同一程序集，无跨集消费即不留公开面；模块内主题文件映射/缓存/冻结等
  实现细节保持私有。

  同判据先例：M2 的 `WheelFactory`/`IWheelFactory` 与外观设置子 VM 只经同集贡献者
  接线/容器解析，维持 public（被测类型），**Host 装配面不涉 public 裁决**——RadialWindow
  由 WheelFactory 在同集内创建，不经 Host 直接 new；`WheelPreviewRenderer` 深浅色探测由
  调用方（Host 外观页）以 `bool` 传入（M2 不反向引用 Host `MainView`）。

  同判据先例：S6 的 `DialogService` 裁决 **public**——Host `SettingsConsole` 建窗后调
  `SetOwner(MainView)` 惰性回填 Owner（ADR-0004），接口 `IDialogService` 不含 SetOwner（Owner
  是实现内部自由，不泄露进契约）；对话框 VM/Window 与 `SpectrumCanvasBehavior` 维持 public
  （被测/装配类型），不借 `InternalsVisibleTo`。

## 8. 接合缝编目（Seams）

> **接合缝（Seam）** = 两个程序集/模块之间一切需要同步、协议或装配的接触点（接口契约、DI 注册、
> 回填、导航目录、XAML 资源合并、消息、共享数据对象）。
> **规范内缝** = ADR/叶子登记且由收口测试守护的缝；**需关注缝** = 有意接受但对模块化施加压力的缝
> （改动前先读裁决）；**残留缝** = 已裁决要清理、待排期的缝。
> 只收**当前活缝**。

### 8.1 规范内缝（approved，改动受 ADR/收口测试守护）

| 缝 | 载体（契约/实现） | 裁决/守护 |
|---|---|---|
| 契约缝·图标资产 | `IIconAssetService` 驻 `StarPie.Sdk.Wpf/Services/Icons/`（实现 `IconAssetService` 驻 `StarPie.Ui/Services/Icons/`）；条目与 .lnk SPI 驻 `StarPie.Sdk/Services/Icons/`；静态纯目录 `IconCatalog` 与自定义图标目录 `CustomIconStore` 驻 `StarPie.Host/Icons/` | ADR-0023；IconCatalogTests |
| 契约缝·.lnk 解析 | `IShortcutTargetResolver` 驻 `StarPie.Sdk/Services/Icons/`（命名空间 `StarPie.Services.Icons` 不变）← 实现 `ShortcutResolver` 驻 `StarPie.Host/Programs/`（图标服务与程序扫描经契约边消费） | ADR-0023 |
| 契约缝·程序扫描 | `IProgramScanner`/`ProgramEntry`/`ProgramCatalog` 驻 `StarPie.Sdk/Services/Programs/`（命名空间 `StarPie.Services.Programs` 不变）← DI 实现 `ProgramSourceAggregator`、内置来源 `ProgramScanner` 驻 `StarPie.Host/Programs/`（候选为纯数据，图标由 UI 消费方装配） | ADR-0023 |
| 契约缝·主题 | `IThemeService` 驻 `StarPie.Sdk.Wpf/Services/Shell/`（ADR-0023）← 实现 `ThemeService` 驻 `StarPie.Ui/Services/Shell/`（状态/解析在内核 `ThemeEngine`，换肤经端口 `IThemeApplier` 回抛 Ui）；消费方 Host/Dialogs 经契约边（M2 轮盘侧不经本契约，改经无状态 `Func<bool>` 探针：ADR-0039 决策 3） | ADR-0023；**不构成插件可达面**——插件拿不到本服务实例（无注入边），插件侧深浅色同样走探针（定义与守护见 [plugin-contracts.md](plugin-contracts.md) §1、[ADR-0047](../adr/0047-plugin-reachable-surface.md)） |
| 契约缝·轮盘工厂 | `IWheelFactory`/`IWheelViewModel` 驻 `StarPie.Sdk`（ADR-0023）← 实现 `WheelFactory`/`WheelViewModel` 驻 M2；消费方 M1 经契约边（M1→M2 runtime 允许边清零） | D5 + ADR-0023 |
| 契约缝·预览 Profile | `IProfilePreviewSource` 驻 `StarPie.Sdk`（ADR-0023，生产方语义 + 破 Wheel↔Gestures 环），别名 = M1 `ProfileListViewModel`，消费 M2 经契约边 | D5 + ADR-0023 |
| 契约缝·轮盘外观只读状态 | `IWheelAppearanceState` 驻 `StarPie.Sdk`（签名暴露件，ADR-0023），实现 = M2 `WheelAppearanceSettingsViewModel`，消费方 = M2 预览渲染器 + Host 外观页 | ADR-0014 决策 8 + ADR-0023 |
| 契约缝·对话框 | `IDialogService`/结果 record 驻 `StarPie.Sdk`（纯 C#，ADR-0023）← 实现 `DialogService` 驻 Dialogs；M1/M2/M5/Host 经契约边调用 | ADR-0023 |
| 注册缝 | 统一注册管线：`ICompositionContributor`（`Id`/`Order`/`RegisterServices` + 可选 `RegisterNavigation`）+ `BuiltInContributors` 有序清单（`HostCoreContributor`/`HostPageContributor`/`ThemeContributor`/`WheelContributor`/`GesturesContributor`/`ShellContributor`/`DialogsContributor` 七个内置贡献者）下放 DI/导航登记；注册的契约类型驻 `StarPie.Sdk`/`StarPie.Sdk.Wpf`；组合根唯一解析、插件贡献者接同一接口 | ADR-0023；BuiltInContributorsTests |
| 内核消费缝 | 模块 runtime 与 Ui 经 `StarPie.Host/{Configuration,Localization}` 消费内核件（内核定义、消费方单向） | HostBoundaryTests |
| 内核内互连·配置→本地化（同集，非跨集缝） | `StarPie.Host/Configuration` 的 `JsonConfigService` 持同集 `Localization` 的 `ILocalizationService`：替换运行态配置的两个入口（加载、导入）都在替换后立即应用配置的 `Language`，把「运行态语言跟随当前配置」收成服务的单一不变式，不留给各调用方自觉 | JsonConfigServiceTests（加载与导入两条路径各一条「语言跟随」用例） |
| 回填缝·宿主回调 | `AppHostDelegates` 驻 `StarPie.Sdk`（可空 Action 单例），由 `HostCoreContributor` 登记单例、`ShellHost` 构造后回填 | 无专用机械断言（缝本身无解析时机；注册体由 BuiltInContributorsTests 覆盖） |
| 回填缝·对话框 Owner | `DialogService.SetOwner(MainView)` Host 建窗后回填（public 装配面） | ADR-0004；e2e |
| 导航缝 | `NavigationCatalog` + `NavigationSlots`（槽位 0–4，驻 `StarPie.Sdk`）+ 贡献者 `RegisterNavigation` + 页面模板字典 | NavigationCatalogTests + BuiltInContributorsTests（补注：导航运行时/执行入口 `INavigationExecutor` 归 Host，为宿主内部件而非跨程序集缝，本表不登记） |
| XAML 资源缝 | App.xaml 资源单点合并/实例化：页面模板字典、主题字典、ModernControls.xaml 与 HotkeyRecorderBox 样式字典均为 Ui 集内本地合并（无跨集 pack URI），转换器 App 级实例；`Properties/DesignTimeResources.xaml` 设计期资源锚是唯一 pack URI 缝（仅设计期、运行时永不合并，见 design-time-preview.md） | ADR-0012、ADR-0025 |
| 消息缝 | S4 hub（`Messages.cs`/`Notices.cs`，驻 `StarPie.Sdk`），跨模块广播；新消息 = 放行共享面 | messages.md |
| 系统调用委托缝（A 类） | 服务构造注入 `Func<bool>`/`Action` 系统探针（`ThemeEngine`/`ActionExecutorService`/VM 委托），生产默认值内建；轮盘扇区内容内核的 SVG 可解析性探针 `WheelSectorContentKernel.Build(..., Func<string,bool> isParsableSvg)` 由 WPF-free 的 Host 内核声明、Ui 侧以 `WheelGeometry.IsParsablePathData` 注入（解析是 WPF 面），省略即视为全部可解析 | layering.md「系统调用接缝模式」；单测替身 |
| 收口测试缝 | 四集基线：`FourSetBoundaryTests`（解决方案登记 / 根 props 生效值与工程差异 / 跨集依赖方向 / CI 与 e2e 路径）+ `RuntimeNoCrossReferenceTests`（产物恰为四集 / 旧集文件不存在 / 引用面与平台投影 / 入口与 XAML 唯一 / 全类型空壳检查）+ `SdkBoundaryTests`/`SdkWpfBoundaryTests`/`HostBoundaryTests`（导出面白名单 / ABI 与默认 ALC 政策 / 内核零 WPF / 设计期字典随 Ui 编译且资源锚唯一）；`BuiltInContributorsTests` + `NavigationCatalogTests` 收口注册管线与目录 | 测试自身守护 |

> 表内各缝的现行机械守护为四集基线 + 注册管线清单（见“收口测试缝”行），其余以产物引用面/导出面/行为测试逐条落地。

### 8.2 需关注缝（有意接受，但对模块化施加压力；改动前先读裁决）

| 缝 | 位置 | 压力 | 裁决/触发条件 |
|---|---|---|---|
| Host 装配面 | Composition/CreateShellHost 直取 Host 侧可见具体类型（MouseHook/主题服务/两子 VM 等）；ShellHost 把 Ui 侧调色板适配器接到主题服务（内核端口 `IThemeApplier`）/编排托盘菜单/MouseHook 暂停态；Host 聚合页拼装 M2/M4 子 VM | Host 对"哪些装配件可见"有编译期认知；模块不能脱离 Host 决定宿主装配 | ADR-0016（组合根集中）；留 Host；不引入子容器/Prism |
| 导航槽位容量 | `NavigationSlot` 固定 0–4 + Validate + e2e `NavPage0..4` | 新增第 6 页需改 SDK 槽位枚举 + 收口测试（可能波及 e2e），非"纯模块内部" | 产品页面数封顶 5，改动属放行共享面；navigation.md 登记 |
| 共享配置对象 | `IConfigService.Current` 单例可变 `AppConfig`；模块 VM 构造抓引用，导入后消息自挂 | 任何模块可读写任何配置区；模块间经"同一对象 + 广播"隐式协作 | 放行共享面（modules.md §2.3）；config.json 向后兼容 Hard Constraint |
| Models 物理残留（R8） | `WheelProfile`/`ActionItem` 语义归 M1、物理 `StarPie.Sdk/Models/`；`CustomColorPreset` 语义归 M2、物理 `StarPie.Sdk/Models/`（AppConfig 引用） | 业务领域形状渗入 SDK 模型面 | R8 已登记；迁移触发条件 = 配置模型与模块语义解耦时再议 |

### 8.3 残留缝

当前无残留缝。

### 8.4 维护义务

1. 新增跨程序集接触点时：先判定属哪一档，登记本表并链裁决（ADR/叶子行号）；不满足 ADR 三条件的小改动只改本表与对应叶子。
2. 缝归零（重构消除）时：从本表移除并在本叶 §8.3 记一行，指向裁决。
3. 程序集/依赖方向变化先改本文 §3，再同步本表基线。

## 参见 ADR

[0016](../adr/0016-assembly-split-target-and-roadmap.md)（程序集化目标态与分批执行）、[0015](../adr/0015-module-map-and-ownership.md)（12 模块地图与归属裁定）。
