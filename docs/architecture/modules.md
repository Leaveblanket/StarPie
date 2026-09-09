# 模块划分地图与模块化路线（模块地图）

> 本文记录模块划分共识（[ADR-0015](../adr/0015-module-map-and-ownership.md)）的地图视图：目标模块清单、职责、归属裁定、扩展点验收与模块化候选。
>
> 本文含**目标态与方向性**内容，不是纯 as-built。代码现状与各叶子（`docs/architecture/*.md`）为准，冲突时叶子优先；差异清单见 §7，随实施批次（§8）逐批回填叶子。
>
> 程序集化目标态（8 程序集：ADR-0016 的 7 程序集 + ADR-0020/#88 新增 StarPie.Dialogs）与
> 批次历史见 [assemblies.md](assemblies.md)（[ADR-0016](../adr/0016-assembly-split-target-and-roadmap.md)、
> [ADR-0020](../adr/0020-dialogs-assembly-and-m3-scanner-contract.md)）。

## 1. 何时读本文

| 想做什么 | 读哪里 |
|---|---|
| 归属争议：某个文件/职责属于哪个模块 | 本文 §4 + ADR-0015 |
| 模块内代码怎么组织、关键流程 | 对应叶子（路由见 [architecture.md](../architecture.md)） |
| 加/改功能应动哪些内部 | 本文 §6 验收表 |
| 程序集化目标态 / 依赖方向 / 导航槽位 / 批次 | [assemblies.md](assemblies.md) + ADR-0016 |
| 为什么这样划分 | ADR-0015 + ADR-0016 |

## 2. 划分判据

1. **独立修整单元**：修改或新增一个功能，只动“相关模块的内部”；跨模块只经稳定契约，或触碰 §2.3 放行共享面。
2. **模块 = 领域能力**：一个模块拥有它的运行态/服务、配置面（设置子 VM/卡片）与领域数据语义；页面是聚合壳（§5 D6），不强行归单一模块。
3. **消费方归属**：共享件出现第二个消费方族才提升为共享模块；单一消费方的能力留在消费模块内部（ADR-0014 消费方判据的推广）。
4. **无“文档分组惯性”**：没有共享领域上下文、没有耦合、只因“都小/都横切”而并在一起的概念，不得并成一个模块（历史反例：本地化与消息，已拆）。

### 2.3 共享内核放行清单（不算“其它业务模块内部”）

下列改动按设计是共享面，扩展功能时允许触碰，不视为跨模块违规：

- `config.json` 模型加字段（带默认值、向后兼容，见 [config.md](config.md)）；
- i18n 文案键与四语言 resx（见 [localization.md](localization.md)）；
- `Composition.cs` / 导航登记一次（B3/#76 起：exe 内 Host 临时注册器（M1/M5 已随 B9/#82/
  B6/#79 迁出）+ M1/M5 正式注册器 `GesturesModuleRegistrar`（B9/#82 迁入 `StarPie.Gestures`）/
  `ShellModuleRegistrar`（B6/#79 迁入 `StarPie.Shell`）`RegisterNavigation` + 模块页面模板字典 +
  [naming.md](naming.md) 映射表；M5 页面 VM DI 注册已下放 ShellModuleRegistrar、M4 主题服务与
  主题设置子 VM DI 注册已下放 `ThemeModuleRegistrar`（B7/#80 迁入 `StarPie.Theme`，M4 无导航页），
  M2 轮盘工厂与轮盘外观设置子 VM DI 注册已下放 `WheelModuleRegistrar`（B8/#81 迁入
  `StarPie.Wheel`，M2 无导航页），M1 手势管线/页面 VM/`IProfilePreviewSource` 别名 DI 注册已下放
  `GesturesModuleRegistrar`（B9/#82 迁入 `StarPie.Gestures`）；M3 快捷方式解析契约注册已下放
  `ProgramsModuleRegistrar`（ADR-0019/#87 迁入 `StarPie.Programs`，M3 无导航页，无
  RegisterNavigation）；仅 Host 外观聚合页 VM 仍由组合根
  注册；程序集化目标态：所属模块注册器 + 槽位表 + 模板字典，见 [assemblies.md](assemblies.md) §5）；
- 「消息与通知」hub 新增消息/通知类型（Q16-A，ADR-0015 决策 7）；
- 共享视图基础设施（**ADR-0022/#94 已去共享化**，共享内核不再持有 UI 实现件）：通用共享转换器与
  全局控件样式字典 `ModernControls.xaml` 落 Host `Views/Converters|Styles/`——App.xaml 仍为单点
  实例化/本地合并，资源 key 不变，Dialogs/Gestures 等模块只经 `{StaticResource}` 运行期消费；
  `HotkeyRecorderBox`（控件+样式字典）落唯一编译期消费方 `StarPie.Gestures`（模块内部）；
  共享页面基类 `SettingsPageBase` 已删除（Trigger/Gestures/Advanced/Appearance/About 五页 XAML
  根直承 `UserControl`）。扩展如需新增通用转换器/全局控件样式，仍属 Host App.xaml 资源缝放行面；
- 共享「图标资产」（S1）新增资产/能力（单一资产条目，不含业务逻辑）。

## 3. 模块地图（目标划分，12 个模块）

### 业务纵向模块（5）

#### M1 手势与动作
- **职责**：手势触发判定与执行全链、动作系统端到端、配置方案（Profile）编辑面。
- **关键内部**：`Services/Gestures/*`（MouseHook、GestureController、GestureEngine、WindowContext；
  轮盘工厂 `WheelFactory` 已随 D5/B8 收编 M2，见 [wheel.md](wheel.md)）、`Services/Actions/*`（路由/执行/系统命令映射）、动作系统预设目录、`Models/ActionItem` 语义、触发与场景设置面（`BehaviorSettingsViewModel`/`TriggerSettingsPage`）、配置方案设置面（`ProfileListViewModel`/`SlotViewModel`/`GesturesSettingsPage`）。
- **对外契约**：经 M2 侧 `IWheelFactory` 接口装配 M2 瞬态轮盘（B8/#81 D5 起工厂随 M2 收编、
  接口留 M2 侧，M1→M2 单向；原 §5 D5 例外已清零）；消费 S2 配置模型、S3、S4、S6；向 M2 提供只读
  `IProfilePreviewSource`（预览上下文，实现方为配置方案设置面 VM `ProfileListViewModel`，#69
  已落地；B8/#81 起接口上提 Core）。
- **扩展局部性**：新增动作类型（原型 D）、新增触发条件/场景规则 → M1 内部；新图标资产 → S1；新文案 → S3。

#### M2 轮盘与渲染
- **职责**：手势轮盘瞬态 VM、窗口呈现、样式渲染体系、外观配置面、轮盘配色解析、实时预览。
- **关键内部**（B8/#81 起物理居 `StarPie.Wheel/`）：`ViewModels/Wheel/*`、
  `WheelAppearanceSettingsViewModel`（`ViewModels/Pages`）、`Views/Wheel/RadialWindow`、
  `Views/Renderers/*`、`Views/Converters/CoreIcon*`（B8/#81 归属裁决随 M2）、
  `Models/WheelPalette/Catalog/Parser`（物理随 M2 收编）、轮盘视觉几何与轮盘工厂
  （`Services/Wheel/WheelGeometry.cs` + `IWheelFactory`/`WheelFactory`，R6 三分 + D5 收编）。
- **对外契约**：由 M1 经 M2 侧 `IWheelFactory` 接口装配；动作图标渲染消费 S1；窗口主题应用消费
  M4 的 `IThemeService`（允许边）；预览 Profile 上下文经 M1 只读 `IProfilePreviewSource`
  （B8/#81 起驻 Core）转发（#69 已落地）。
- **扩展局部性**：新增轮盘样式（原型 E）、改几何/配色/排版/预览 → M2 内部。

#### M3 程序扫描与目录
- **职责**：已安装程序扫描、目录合并/过滤、快捷方式目标解析（.lnk → 真实路径）。
- **关键内部**：`ProgramScanner`（IO 扫描编排；ADR-0020/#88 起实例实现 Core 契约
  `IProgramScanner`，构造注入扫描所需 Core 契约）、`ShortcutResolver`（实例实现 Core 契约
  `IShortcutTargetResolver`，ADR-0019/#87）与模块注册器 `ProgramsModuleRegistrar`。
  纯规则目录 `ProgramCatalog` 与纯数据 `ProgramEntry` 已上提 Core（ADR-0020/#88：第二消费方族
  判据——M3 扫描与 S6 程序选择器共用），不随 M3 物理居留。
- **对外契约**：扫描/过滤数据经 Core 契约（`IProgramScanner`/`ProgramCatalog`/`ProgramEntry`）
  提供给 S6 的程序选择对话框，不反向依赖 S6；消费共享内核 S1 契约
  （`IShortcutTargetResolver`/`IIconAssetService`，M3 → Core 单向）。
- **扩展局部性**：新增程序来源/目录/过滤规则 → M3 内部。

#### M4 界面主题
- **职责**：窗口 UI 主题体系（AppTheme）——配置与解析、状态/切换/系统跟随、XAML 令牌集与整项替换、界面主题设置面、主题应用消息。
- **关键内部**：`ThemeService`+`IThemeService`、根 `ThemePaletteManager.cs`、
  `Views/Styles/Themes/*.xaml`、`InterfaceThemeSettingsViewModel`、`AppThemeChangedMessage`；
  **B7/#80 起物理居独立模块程序集 `StarPie.Theme/`（Services/Shell、模块根 ThemePaletteManager、
  Views/Styles/Themes、ViewModels/Pages、Modules 注册器 ThemeModuleRegistrar）**；各窗口
  （MainView/对话框/RadialWindow）仅按 ADR-0009 白名单注入应用。
- **扩展局部性**：新增主题方案/令牌/跟随策略 → M4 内部 + S3 文案。

#### M5 壳层与系统集成
- **职责**：托盘与气泡、开机自启、内存整理、壳层服务与系统集成、高级与关于设置面。（主窗口壳层行为按 ADR-0016 归 H1 宿主壳，见 [assemblies.md](assemblies.md) §4）
- **关键内部**：`TrayIconManager`、`AutostartRegistry`（R1）、`MemoryOptimizer`（R3）、`GeneralSettingsViewModel`+`AdvancedSettingsPage`、`AboutViewModel`+`AboutSettingsPage`；**B6/#79 起物理居独立模块程序集 `StarPie.Shell/`（Services/Shell、ViewModels|Views/Pages、Modules 注册器/模板字典）**。（`MainView.xaml.cs` 不再归 M5——R4/ADR-0016 重新归属 Host 壳窗口）
- **子职责目录**：见 §5 D2（防“系统集成”垃圾筐）。
- **扩展局部性**：新托盘菜单项/自启策略/内存策略/系统页设置项 → M5 内部。

### 共享与基础设施模块（6）

#### S1 图标资产
- **职责**：动作图标资产与文件图标提取——矢量图标清单、SVG 键目录/取值、自定义图标存储（列表/导入/删除/图像源）、文件/程序图标提取（`GetIcon`）。
- **关键内部**（ADR-0019/#87 双形拆分）：静态纯目录 `Services/Icons/IconCatalog.cs`（`VectorIconList`/`GetSvgPathByKey`/`ExtractSvgPathData`，无状态）+ 实例服务
  `Services/Icons/IIconAssetService.cs`/`IconAssetService.cs`（自定义图标存储与 `GetIcon`，
  经注入 `IShortcutTargetResolver` 消费 .lnk 解析）+ `CustomIconItem.cs`/`VectorIconItem.cs`；
  `.lnk` 解析契约 `IShortcutTargetResolver.cs` 亦驻 `Services/Icons/`（由 M3 实现）；
  消费方：M1 动作编辑、M2 轮盘渲染、S6 图标选择器。
- **扩展局部性**：新增图标资产/提取能力 → S1 内部。

#### S2 配置与保存
- **职责**：`config.json` 读写/宽松解析/默认播种/向后兼容、运行态配置、防抖与立即保存编排、导入/导出、`AppDataPaths`。
- **关键内部**：`Services/Configuration/*`（不含 `AutostartRegistry`——R1 已随 #70 迁至 M5 侧
  `Services/Shell/`，见 §4）；`Models/` 配置 POCO 的物理居所（语义归属见 R8）。
- **扩展局部性**：加配置字段（原型 A 模型步）→ S2 + 所属模块 VM（放行共享面）。

#### S3 本地化
- **职责**：四语言键表与取词、语言状态/切换/回退链、运行时语言字典投影桥、文案分类语义。
- **关键内部**：`Services/Localization/*`、`Strings*.resx`；`AppHost` 的语言字典投影是 H1 对本模块的消费（§5 D4）。
- **扩展局部性**：新语言/新文案键/改回退链 → S3 内部。

#### S4 消息与通知
- **职责**：跨模块协调事件契约 hub 与弹窗通知载体。
- **关键内部**：`Services/Messages/Messages.cs`、`Notices.cs`（`NoticeKind`/`NoticeRequest`）。
- **扩展局部性**：新消息/通知类型 → S4 内部（放行共享面，Q16-A）。

#### S5 导航
- **职责**：设置控制台页面切换的**目录/槽位注册契约（纯契约共享模块，Q4=a，ADR-0021/#92）**——
  槽位表 0–4 正典、页面注册目录与完整性收口；页面模板由所属模块提供（ADR-0016）。
  导航运行时主体（当前页状态/执行入口/导航项 VM/主框架 VM）归 H1 宿主壳件
  （ADR-0021/#92，与 R4/D3 同判据——单一消费方在 Host，模块对运行时类型零引用）。
- **关键内部**：共享内核仅留 `Services/Navigation/NavigationCatalog.cs`（`NavigationCatalog`/
  `NavigationSlot`/`NavigationSlots`/`NavigationPageRegistration`——跨模块注册契约，模块注册器写、
  控制台读）；运行时主体物理居 Host `Services/Navigation/`（`NavigationStore`/
  `NavigationExecutor` 含 `INavigationExecutor`）+ `ViewModels/Navigation/`
  （`NavigationItemViewModel`/`MainViewModel` 纯导航目录驱动，ADR-0021/#92 迁入、命名空间不变，
  见 [host.md](host.md)/[navigation.md](navigation.md)）、`SidebarView`（导航壳 UI 属 Host，见
  [assemblies.md](assemblies.md) §4）。
- **扩展局部性**：新增页面（原型 B）→ 所属模块注册器 `RegisterNavigation` + 页面模板字典
  （B3/#76 起 exe 内先行；B6/#79 起 M5 已跨程序集自治——新增 M5 页面只动模块内部，页面 VM DI 注册
  随 ShellModuleRegistrar 下放；B9/#82 起 M1 同款自治——新增 M1 页面只动
  `StarPie.Gestures` 模块内部，页面 VM DI 注册随 GesturesModuleRegistrar 下放）；目标态为 S5/H1 之外的模块自治
  （见 [assemblies.md](assemblies.md) §5）。

#### S6 对话框
- **职责**：全部对话框唯一形态——`IDialogService`/`DialogService`、VM/Window 配对、结果 record、通用选择器（程序选择、图标选择、取色、文本/热键输入、屏幕取色）。
- **关键内部**：契约 `IDialogService` + 结果 record 驻 Core `Services/Dialogs/`；实现与界面
  （`DialogService`、五对对话框 VM/Window、取色行为 `SpectrumCanvasBehavior`）物理居独立模块
  程序集 `StarPie.Dialogs/`（ADR-0020/#88，B11/#88 已落地；`ViewModels/Dialogs`、
  `Views/Dialogs`、`Views/Controls` 随迁，命名空间不变）。
- **对外契约**：领域数据经注入提供者/模块出口获得——程序扫描候选经 Core 契约
  `IProgramScanner`（M3 注册器提供实现，ADR-0020/#88 替代组合根委托注入，S21 归零），
  图标资产/快捷方式解析经 S1/M3 出口接线（R7，T3c/#67 已落地）；不直穿 M3/S1 内部。
- **扩展局部性**：新增对话框（原型 C）→ S6 内部 + 调用方一行。

### 宿主（1）

#### H1 宿主与组合根
- **职责**：进程生命周期（单实例、全局异常、启动/退出/隐藏协调）、DI 组合根注册与解析、宿主回调委托、开发实例。
- **关键内部**：`App`/`AppHost`/`Composition`/`DevInstance`（R2）；宿主回调委托包
  `AppHostDelegates` 为 H1 职责——类型本体已上提 Core 契约（B6/#79，`StarPie.Core/Services/`），
  回填实现仍归 Host（见 [host.md](host.md)/[layering.md](layering.md)）。
- **扩展局部性**：新服务/页面 VM 注册一行（放行）；不承载业务逻辑。

## 4. 归属裁定表（R1–R8）

| # | 项 | 归属 | 物理现状 | 迁移 |
|---|---|---|---|---|
| R1 | `AutostartRegistry` | M5 壳层 | `Services/Shell/` | 已落地（#70：物理迁至 M5 侧目录并同步命名空间） |
| R2 | `DevInstance` | H1 宿主 | `WinPieGestures/`（工程根） | 已落地（#70：物理迁至工程根并同步命名空间） |
| R3 | `MemoryOptimizer` | M5 壳层 | `Services/Shell/` | 已清零（B1/#64：host.md 组成摘除） |
| R4 | `MainView.xaml` / `MainView.xaml.cs` | **全文件 → H1 宿主壳（Host 壳窗口，ADR-0016 决策 6/7）**；xaml.cs 不再归 M5；页面 DataTemplate 已随 B3/#76 迁出 MainView（App 级模块模板字典，B6/B9 随程序集再迁） | `Views/Navigation/` | B1/#74 已落地（MainView 分区 DataContext + ShellViewModel）；B3/#76 已落地（页面 DataTemplate 迁至 exe `Modules/` 模块模板字典，MainView 纯壳）；B6/#79 M5 模板字典随 `StarPie.Shell` 迁出（ShellModuleRegistrar/ShellPageTemplates.xaml）；B9/#82 M1 模板字典随 `StarPie.Gestures` 迁出（GesturesModuleRegistrar/GesturesPageTemplates.xaml），exe 仅余 Host 外观页模板；目标态见 [assemblies.md](assemblies.md) §4 |
| R5 | `GesturePoint` | 共享内核值类型（目标迁 `Models`） | `Models/` | 已落地（#70：自 `GestureEngine.cs` 提取独立文件并迁入 `Models/`） |
| R6 | `IconHelper` | **三分**：图标资产 → S1；几何（`CreateAdvancedSectorGeometry`/`GetCoreIconGeometry`）→ M2；程序侧（`ResolveShortcutTarget`）→ M3 | 原 `Services/Programs/IconHelper.cs`（T3d/#68 已删）；收编结果：S1 `Services/Icons/IconAssets.cs`+`VectorIconItem.cs`（ADR-0019/#87 双形拆为 `IconCatalog.cs`+`CustomIconItem.cs`+`IconAssetService.cs` 等，见 §3 S1）、M2 `Services/Wheel/WheelGeometry.cs`（B8/#81 起物理随 M2 迁 `StarPie.Wheel/Services/Wheel/`）、M3 `Services/Programs/ShortcutResolver.cs` | 已落地（B3/T3a–T3d/#65–#68 接线迁移 + 物理收编 + 叶子回填；B8/#81 物理落位随 M2 收编；ADR-0019/#87 收口 M3 边界） |
| R7 | `ProgramPicker`/`IconPicker` | S6 对话框（通用选择器） | `StarPie.Dialogs/ViewModels|Views/Dialogs/`（ADR-0020/#88 随 S6 实现迁入，原 Host 目录已清空） | 已落地（B4/T3c–#67：数据经注入提供者 + S1/M3 出口接线；#71 登记清零；ADR-0020/#88 扫描改经 `IProgramScanner` 契约） |
| R8 | `Models` 语义归属与物理落位 | `WheelProfile`/`ActionItem` → M1（物理 Core `Models/`，配置 POCO）；`WheelPalette*` → M2（B8/#81 起物理随 M2 收编 `StarPie.Wheel/Models/`，语义+物理均归 M2）；`CustomColorPreset` → M2（语义；物理仍 Core `Models/`——`AppConfig.CustomColorPresets` 配置 POCO 引用） | `Models/`（Core）+ `StarPie.Wheel/Models/`（B8/#81） | B1/#64 已登记语义；B8/#81 起 WheelPalette* 物理随 M2 收编，wheel.md/layout.md 同步回填 |
| R9 | 导航运行时主体（`NavigationStore`/`NavigationExecutor`（含 `INavigationExecutor`）/`MainViewModel`/`NavigationItemViewModel`） | H1 宿主壳（与 R4/D3 同判据——运行时消费者全部在 Host，模块程序集零引用） | `WinPieGestures/Services/Navigation/` + `WinPieGestures/ViewModels/Navigation/`（命名空间不变，B10/#83 共享命名空间树） | 已落地（ADR-0021/#92：运行时四类迁 Host；共享内核仅留目录/槽位契约 `NavigationCatalog` 四件；C1 死代码 `INavigationService`/`NavigationService` 删除） |

## 5. 登记表（子职责 / 双职责 / 装配点）

### D1 M1 内部子面
触发（MouseHook/Controller/Engine/Behavior 页）、动作执行（Actions）、配置方案编辑（ProfileList/Slot/Gestures 页）三个子面；共享上下文 = Gesture 语义含“松开执行动作”、Profile=扇区动作集合。**观察信号**：触发与动作各自膨胀成独立服务族、或 M1 出现第二个外部“动作执行”消费方时，再评估拆为两个模块。

### D2 M5 子职责目录与护栏
子职责：托盘 / 自启 / 内存 / 高级与关于设置面。主窗口壳层行为按 ADR-0016 归 H1 宿主壳（Host 壳窗口，见 [assemblies.md](assemblies.md) §4），不再属 M5。护栏：新 OS 集成功能必须先对号入座；放不进任何现有子职责时，须先论证与壳层上下文的共享关系，否则不得并入 M5。

### D3 MainViewModel / ShellViewModel 拆分（ADR-0016，B1/#74 已落地）
原登记：主归属 **S5 导航**（导航项/当前页/选中同步），壳层职责成员（`WindowTitle`、`IsExiting`、`Save()`）借调 M5，类型级双职责例外。

ADR-0016 决策 7（Q18）已落地（B1/#74）：`MainViewModel` 收敛为纯导航；壳成员迁出为
`ShellViewModel`（`WindowTitle`/`IsExiting`/`Save()`，留 Host 壳窗口，与 R4 同判据）；
`MainView` 分区 DataContext（导航区绑导航 VM、壳区绑壳 VM）。本登记清零；B3/#76 完成目录驱动
迁 Core（MainViewModel 无页面类型硬编码，导航项来自 `NavigationCatalog` 模块注册）。
**物理落点修订（ADR-0021/#92，决策 3）**：ADR-0016 决策 7 中"MainViewModel 随 S5 导航内核进
Core"的物理落点表述被部分推翻——导航运行时主体（含 `MainViewModel`）迁回 Host
（`WinPieGestures/Services/Navigation/` 与 `WinPieGestures/ViewModels/Navigation/`，命名空间
不变）；职责拆分语义（纯导航 vs 壳层职责）与 B3/#76 目录驱动设计全部保留（见 R9）。

### D4 AppHost 语言字典投影
`AppHost.cs` 归 H1；其运行时语言字典投影与壳外文案刷新是 H1 消费 S3 的行为，不是双归属（防旧 localization.md 把 AppHost 列入“组成文件”造成的误解；随 B1 修订叶子表述）。

（B1/#64 已修订：localization.md 不再把 AppHost 列入组成文件，host.md 登记投影为 H1 对 S3 的消费。）

### D5 WheelFactory 装配点例外
本登记已清零（B8/#81 落地，ADR-0016 决策 11）：`WheelFactory` 已随 M2 收编
`StarPie.Wheel/Services/Wheel/`，`IWheelFactory` 留 M2 侧接口（M1→M2 单向成立），
`IProfilePreviewSource` 已上提 Core（`StarPie.Core/ViewModels/Pages/`，实现方 M1
ProfileListViewModel 与消费方 M2 WheelAppearanceSettingsViewModel 均只依赖 Core）。
M2 构造契约变更不再波及 Host/M1 装配点；M1 手势侧自 B9/#82 起随 `StarPie.Gestures` 成集，
仍只经接口引用 M2（M1→M2 单向）。

### D6 页面壳
- Trigger/Gestures 设置页 = M1 的设置面（整页 VM 属 M1；B9/#82 已随 `StarPie.Gestures` 成集：
  VM+View+注册器+模板字典均在模块程序集内，新增页面不碰 Host）；
- Appearance 设置页 = M4（界面主题卡）+ M2（轮盘外观卡）的聚合壳（#56 已实现）；
- Advanced/About 设置页 = M5 的设置面（B6/#79 已随 `StarPie.Shell` 成集：VM+View+注册器+模板字典
  均在模块程序集内，新增页面不碰 Host）；
- 新增设置页按原型 B 走导航登记，不预设归属模块。

## 6. 扩展点验收表（“只动相关内部”）

| 原型/场景 | 示例 | 只动 | 放行共享面 |
|---|---|---|---|
| A 新增设置项 | 现有页加开关 | 所属模块 VM | S2 模型字段、S3 文案键 |
| B 新增设置页面 | 新导航页 | 新域/所属模块（注册器 + 页面模板字典，目标态见 [assemblies.md](assemblies.md) §5） | B6/#79 起 M5：只动 `StarPie.Shell` 模块内部；B9/#82 起 M1：只动 `StarPie.Gestures` 模块内部（Shell/GesturesModuleRegistrar 的 RegisterNavigation/RegisterServices + Shell/GesturesPageTemplates.xaml + 页面 VM/View），不碰 Host；Host 外观聚合页仍 exe 内注册器 + 模板字典、页面 VM DI 注册在组合根（目标态 Host 页）；目标态：新增页面不碰 Host，仅新增模块才 H1 登记；S3 文案 |
| C 新增对话框 | 新模态 | S6 内部 | 调用方模块一行（经 `IDialogService`） |
| D 新增动作类型 | 新 Launch/Folder/Hotkey/System 值 | M1 内部（路由/执行/预设/槽位编辑/图标键映射） | 新图标资产 → S1；S3 文案；config 兼容 |
| E 新增轮盘样式 | 新 Renderer | M2 内部（渲染器/工厂/配色目录/外观选项） | S3 文案 |
| F 新增后台服务/监听器 | 新 Hook/Service | 所属模块内部 | B3 前：H1 注册一行；目标态（B4 起）：所属模块注册器一行、新增模块才 H1 |
| 附加：新语言 | — | S3 | — |
| 附加：新消息/通知类型 | — | S4 | 放行共享面（Q16-A） |
| 附加：新图标资产 | — | S1 | 放行共享面 |
| 附加：新主题方案 | — | M4 | S3 文案 |
| 附加：新程序来源 | — | M3 | — |

## 7. 现状叶子 → 目标模块对照与差异

> **B1（#64，纯文档基线）已完成**：`localization.md`/`shell.md` 按 S3+S4 / M4+M5 拆分表述（新建
> [messages.md](messages.md)/[interface-theme.md](interface-theme.md)），`config.md`/`host.md`/
> `navigation.md`/`gestures.md`/`wheel.md` 按 §4 归属裁定回填（R1 文档摘除、R2/R3 去重、R4 文件级登记、
> R8 语义登记、D3/D4 叶子表述）。**B3（图标/几何/解析三分，T3a–T3d/#65–#68）已完成**：S1/M2/M3
> 出口与接线落地（#65–#67）、旧入口删除与条目物理收编（#68），programs.md/wheel.md 差异行随本批
> 清零。**B5（#70，物理小件迁移）已完成**：`GesturePoint`→`Models/`（R5）、`AutostartRegistry`→
> `Services/Shell/`（R1）、`DevInstance`→工程根（R2），config.md/host.md/gestures.md/shell.md/layout.md
> 差异行随本批清零；MainViewModel 未拆分（D3 非目标登记）。**B2/B4/B6 已按 #71 收口**：gestures.md 按 as-built 补全 M1 配置方案设置面（B2，见 [gestures.md](gestures.md)）；dialogs.md 的 R7 接缝整理代码已在 T3c/#67 落地，本批登记清零（B4）；
> B6 降级为方向性注记（见 §8）。**B7/#80（模块化 M4 Theme 抽取）已落地**：界面主题体系
> （ThemeService/IThemeService、ThemePaletteManager、五套主题字典、InterfaceThemeSettingsViewModel、
> ThemeModuleRegistrar）迁入独立模块程序集 `StarPie.Theme`（依赖方向/现状见
> [assemblies.md](assemblies.md) §3/§9；本节 M4 归属与差异行维持清零）。**B8/#81（模块化 M2
> Wheel 抽取，含 D5 解结）已落地**：M2 轮盘件（VM/窗口/渲染器/配色/工厂 + 核图标预览转换器）迁入
> 独立模块程序集 `StarPie.Wheel`；D5 清零——`WheelFactory` 随 M2 收编、`IWheelFactory` 留 M2 侧
> 接口、`IProfilePreviewSource` 上提 Core；R8 物理落位同步（WheelPalette* 随 M2 收编、
> CustomColorPreset 仍 Core）。**B9/#82（模块化 M1 Gestures 抽取·收口）已落地**：M1 手势件
> （手势管线/动作执行/触发+手势设置页）迁入独立模块程序集 `StarPie.Gestures`，模块注册器
> GesturesModuleRegistrar 下放 DI 与 `IProfilePreviewSource` 别名，7 程序集目标态除命名空间外
> 达成（该目标态已于 ADR-0020/#88 扩展为 8 程序集，现状以 [assemblies.md](assemblies.md) §2 为准）。
> 下表逐叶对照已无差异。

| 现状叶子 | 目标归属 | 差异（批次登记） |
|---|---|---|
| [dialogs.md](dialogs.md) | S6 | —（B4/T3c–#67 接线落地 + #71 登记清零） |
| [gestures.md](gestures.md) | M1 | —（B2/#71 已清零：配置方案设置面叶子补全；B8/#81 D5 已清零：工厂随 M2、M1 只经 IWheelFactory 接口引用；B9/#82 已清零：M1 成集 StarPie.Gestures + GesturesModuleRegistrar） |
| [localization.md](localization.md) | S3 | —（B1/#64 已清零） |
| [messages.md](messages.md)（B1 新叶） | S4 | —（B1/#64 已清零） |
| [navigation.md](navigation.md) | S5 | —（B1/#64 已清零：R4/D3） |
| [programs.md](programs.md) | M3 | —（B3/T3a–T3d/#65–#68 已清零：三分收口与叶子回填） |
| [shell.md](shell.md) | M5 | —（B1/#64 已清零） |
| [interface-theme.md](interface-theme.md)（B1 新叶） | M4 | —（B1/#64 已清零） |
| [wheel.md](wheel.md) | M2 | —（B3/T3a–T3d/#65–#68 已清零：几何收编与叶子回填；B8/#81 已清零：M2 成集 StarPie.Wheel + 配色物理收编 + D5 工厂收编） |

## 8. 模块化路线（ADR-0016：B0–B10 排期）

> ADR-0015 时代的候选路线 B1–B6（#64–#71）已全部完成并归档（见 §7 注记）。自 [ADR-0016](../adr/0016-assembly-split-target-and-roadmap.md) 起，模块化升级为**程序集化排期批次 B0–B10**；路线、每批内容与验收见 [assemblies.md](assemblies.md) §8。
>
> 每批：独立 issue；构建 + xUnit 绿；涉及可见文案时 e2e 绿；完成后回填对应叶子并从路线移除。
>
> **B0（本批，纯文档）**：ADR-0016 + assemblies.md + 本节修订 + architecture.md 路由/索引。B1 起为代码批次。
>
> **B8/#81（M2 Wheel 抽取，含 D5）已落地**：见 [assemblies.md](assemblies.md) §9 现状补记。
>
> **B9/#82（M1 Gestures 抽取，收口）已落地**：见 [assemblies.md](assemblies.md) §9 现状补记；
> 本节与 §7/§4/§5 差异行随代码与叶子回填同步清零（7 程序集目标态除命名空间外达成，余 B10；
> 该目标态已于 ADR-0020/#88 扩展为 8 程序集，现状以 [assemblies.md](assemblies.md) §2 为准）。
>
> §7 差异表为 ADR-0015 基线的清零状态。**ADR-0016 程序集化批次差异（B1 起）另见 [assemblies.md](assemblies.md) §8/§9**，§7 不再逐行登记。

## 参见 ADR

[0015](../adr/0015-module-map-and-ownership.md)（模块划分共识：12 模块地图、归属裁定与修整单元判据）、[0016](../adr/0016-assembly-split-target-and-roadmap.md)（程序集化目标态与分批执行）。
