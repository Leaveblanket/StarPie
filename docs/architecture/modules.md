# 模块划分地图（模块地图）

> 本文记录模块划分共识（[ADR-0015](../adr/0015-module-map-and-ownership.md)）的地图视图：模块清单、职责、归属裁定、扩展点验收与模块化候选。

> **目标态变更（P1 起）**：插件化后模块口径改为「宿主内核子域 + 能力插件」，见 [ADR-0027](../adr/0027-plugin-architecture-and-host-sdk-ui-split.md) 与 [plugins.md](plugins.md)；P1 落地时本文按目标态回填。
>
> 本文按 as-built 现状撰写；代码现状与各叶子（`docs/architecture/*.md`）为准，冲突时叶子优先。
>
> 程序集化现状（as-built 程序集地图）与程序集级依赖方向见 [assemblies.md](assemblies.md)（[ADR-0016](../adr/0016-assembly-split-target-and-roadmap.md)、
> [ADR-0023](../adr/0023-module-contracts-hard-boundary-and-core-narrowing.md)）。

## 1. 何时读本文

| 想做什么                            | 读哪里                                                 |
| ----------------------------------- | ------------------------------------------------------ |
| 归属争议：某个文件/职责属于哪个模块 | 本文 §4 + ADR-0015                                    |
| 模块内代码怎么组织、关键流程        | 对应叶子（路由见[architecture.md](../architecture.md)） |
| 加/改功能应动哪些内部               | 本文 §6 验收表                                        |
| 程序集化现状 / 依赖方向 / 导航槽位  | [assemblies.md](assemblies.md)                          |
| 为什么这样划分                      | ADR-0015 + ADR-0016                                    |

## 2. 划分判据

1. **独立修整单元**：修改或新增一个功能，只动“相关模块的内部”；跨模块只经稳定契约，或触碰 §2.3 放行共享面。
2. **模块 = 领域能力**：一个模块拥有它的运行态/服务、配置面（设置子 VM/卡片）与领域数据语义；页面是聚合壳（§5 D6），不强行归单一模块。
3. **契约归属（ADR-0023 修订）**：模块出口契约（接口 + 跨模块 DTO/纯数据）随**实现方模块**
   下沉其 `*.Contracts` 程序集（取代「第二消费方族 → 上提 Core」的旧执行口径，历史见 ADR-0023 与 git）；共享件出现第二个消费方族时——若属全局
   机制/数据入 `StarPie.Sdk` 契约与模型面（跨集共享）或宿主内核（运行时设施），若属某模块出口契约下沉该模块 Contracts（单一消费方的能力留在消费
   模块内部，ADR-0014 消费方判据的推广）。
4. **无“文档分组惯性”**：没有共享领域上下文、没有耦合、只因“都小/都横切”而并在一起的概念，不得并成一个模块（历史反例：本地化与消息，已拆）。

### 2.3 放行共享面清单（不算“其它业务模块内部”）

下列改动按设计是共享面，扩展功能时允许触碰，不视为跨模块违规：

- `config.json` 模型加字段（带默认值、向后兼容，见 [config.md](config.md)）；
- i18n 文案键与四语言 resx（见 [localization.md](localization.md)）；
- `BuiltInContributors` 清单一行 / 导航登记一次：所属贡献者 `RegisterNavigation` + 模块页面模板字典 +
  [naming.md](naming.md) 映射表（M5：`StarPie.Ui` 的 `ShellContributor`；M1：
  `StarPie.Ui` 的 `GesturesContributor`；Host 外观聚合页：
  `HostPageContributor`）；页面 VM DI 注册由所属贡献者登记——M5 页面 VM 由
  ShellContributor、M4 主题服务与主题设置子 VM 由 `ThemeContributor`（`StarPie.Ui`，
  M4 无导航页）、M2 轮盘工厂与轮盘外观设置子 VM 由 `WheelContributor`（`StarPie.Ui`，
  M2 无导航页）、M1 手势管线/页面 VM/`IProfilePreviewSource` 别名由
  `GesturesContributor`（M1，驻 `StarPie.Ui`）登记；S1 图标资产与 M3 程序扫描
  （两模块无导航页）由 `HostCoreContributor` 登记；Host 外观聚合页 VM 由
  `HostPageContributor` 登记；贡献者清单 + 槽位表 + 模板字典为现状（见 [assemblies.md](assemblies.md) §5/§6））；
- 「消息与通知」hub 新增消息/通知类型（ADR-0015 决策 7）；
- 共享视图基础设施（**已去共享化**，放行面不再持有 UI 实现件）：通用共享转换器与
  全局控件样式字典 `ModernControls.xaml` 落 Host `Views/Converters|Styles/`——App.xaml 仍为单点
  实例化/本地合并，资源 key 不变，Dialogs/Gestures 等模块只经 `{StaticResource}` 运行期消费；
  `HotkeyRecorderBox`（控件+样式字典）落唯一编译期消费方 M1（P1.6/#115 随归并入 `StarPie.Ui`）；
  共享页面基类 `SettingsPageBase` 已删除（Trigger/Gestures/Advanced/Appearance 四页 XAML
  根直承 `UserControl`）。扩展如需新增通用转换器/全局控件样式，仍属 Host App.xaml 资源缝放行面；
- 共享「图标资产」（S1）新增资产/能力（单一资产条目，不含业务逻辑）。

## 3. 模块地图（12 个模块）

### 业务纵向模块（5）

#### M1 手势与动作

- **职责**：手势触发判定与执行全链、动作系统端到端、配置方案（Profile）编辑面。
- **关键内部**：手势触发与执行全链（MouseHook/GestureController/GestureEngine/WindowContext）、
  动作路由/执行/系统命令映射与预设目录、动作项 `ActionItem` 语义、触发与场景设置面
  （`BehaviorSettingsViewModel`/`TriggerSettingsPage`）、配置方案设置面
  （`ProfileListViewModel`/`SlotViewModel`/`GesturesSettingsPage`）；物理路径见 [layout.md](layout.md)
  与 [gestures.md](gestures.md)。
- **对外契约**：经 SDK 契约 `IWheelFactory` 装配 M2 瞬态轮盘（M1→M2 runtime 允许
  边经契约清零，ADR-0023；P1.3/#112 随 SDK 收口）；消费 S2 配置模型、S3、S4、S6；向 M2 提供只读
  `IProfilePreviewSource`（预览上下文，实现方为配置方案设置面 VM `ProfileListViewModel`，契约
  随实现方驻 `StarPie.Sdk`）。
- **扩展局部性**：新增动作类型（原型 D）、新增触发条件/场景规则 → M1 内部；新图标资产 → S1；新文案 → S3。

#### M2 轮盘与渲染

- **职责**：手势轮盘瞬态 VM、窗口呈现、样式渲染体系、外观配置面、轮盘配色解析、实时预览。
- **关键内部**：`WheelViewModel`（实现 `IWheelViewModel`）、`WheelAppearanceSettingsViewModel`
  （实现 `IWheelAppearanceState`）、`RadialWindow`、样式渲染器与实时预览、`CoreIcon*` 核图标
  预览转换器、轮盘配色 `WheelPalette*`、视觉几何 `WheelGeometry` 与轮盘工厂 `WheelFactory`
  （出口契约 `IWheelFactory`/`IWheelViewModel`/`IWheelAppearanceState` 驻
  `StarPie.Sdk`，ADR-0023；P1.3/#112 随 SDK 收口）；物理路径见 [layout.md](layout.md) 与
  [wheel.md](wheel.md)。
- **对外契约**：由 M1 经 SDK 契约 `IWheelFactory`/`IWheelViewModel` 装配
  （ADR-0023）；动作图标渲染消费 S1；窗口主题应用消费 M4 的 `IThemeService`
  （经 Sdk.Wpf 契约边）；预览 Profile 上下文经 M1 只读
  `IProfilePreviewSource`（驻 `StarPie.Sdk`）转发。
- **扩展局部性**：新增轮盘样式（原型 E）、改几何/配色/排版/预览 → M2 内部。

#### M3 程序扫描与目录

- **职责**：已安装程序扫描、目录合并/过滤、快捷方式目标解析（.lnk → 真实路径）。
- **关键内部**：出口契约 `IProgramScanner`/`ProgramCatalog`/`ProgramEntry` 与 SPI
  `IShortcutTargetResolver` 驻 `StarPie.Sdk/Services/Programs|Icons/`（纯数据、零 WPF）；
  实现分两层：内置来源 `ProgramScanner`、能力契约/聚合 `ProgramSourceCapability`/`ProgramSourceAggregator`
  与 `ShortcutResolver` 驻宿主内核 `StarPie.Host/Programs/`（构造注入 `IShortcutTargetResolver`，
  返回纯数据条目）；深扫来源 `InstalledProgramScanner` 驻随包插件
  `plugins/src/StarPie.Plugin.Programs/`（经 `program-source@1` 能力注册）；物理路径见
  [layout.md](layout.md) 与 [programs.md](programs.md)。
- **对外契约**：扫描/过滤数据经 SDK 契约提供给 S6 的程序选择对话框等消费方
  （DI 注册在组合根，消费方只认契约）；.lnk SPI 经 SDK 提供给 S1 图标服务与组合根；
  图标补全不在扫描面——UI 消费方（程序选择器）按路径经 `IIconAssetService` 装配。
- **扩展局部性**：新增程序来源/目录/过滤规则 → 随包插件 `StarPie.Plugin.Programs`（内置来源只留
  插件缺席时也必须可用的系统工具与快捷方式）；新增扫描/跨模块协议 →
  扩展 `StarPie.Sdk/Services/Programs|Icons/` 契约面（消费方驱动）。

#### M4 界面主题

- **职责**：窗口 UI 主题体系（AppTheme）——配置与解析、状态/切换/系统跟随、XAML 令牌集与整项替换、界面主题设置面、主题应用消息。
- **关键内部**：主题引擎 `ThemeEngine`（零 WPF，宿主内核）与端口 `IThemeApplier`、
  `IThemeService` 实现 `ThemeService`（窗口 DWM 应用与系统深浅色监听）、调色板适配器
  `AppThemePaletteManager`、五套主题画刷令牌字典、`InterfaceThemeSettingsViewModel`、
  `AppThemeChangedMessage`；出口契约 `IThemeService` 驻 `StarPie.Sdk.Wpf/Services/Shell/`
  （ADR-0023）；物理路径见 [layout.md](layout.md) 与 [interface-theme.md](interface-theme.md)。
  各窗口（MainView/对话框/RadialWindow）仅按 ADR-0009 白名单注入应用——M2/S6 消费方经
  Sdk.Wpf 契约边。
- **扩展局部性**：新增主题方案/令牌/跟随策略 → M4 内部 + S3 文案。

#### M5 壳层与系统集成

- **职责**：托盘与气泡、开机自启、内存整理、壳层服务与系统集成、高级设置面。（主窗口壳层行为按 ADR-0016 归 H1 宿主壳，见 [assemblies.md](assemblies.md) §4）
- **关键内部**：`TrayIconManager`、`AutostartRegistry`（R1）、`MemoryOptimizer`（R3）、
  `GeneralSettingsViewModel`+`AdvancedSettingsPage` 与贡献者/页面模板字典
  （物理随 M5 归并入 `StarPie.Ui/`，见 [layout.md](layout.md)）。（`MainView.xaml.cs`
  不归 M5——R4/ADR-0016 归属 Host 壳窗口）
- **子职责目录**：见 §5 D2（防“系统集成”垃圾筐）。
- **扩展局部性**：新托盘菜单项/自启策略/内存策略/系统页设置项 → M5 内部。

### 共享与基础设施模块（6）

#### S1 图标资产

- **职责**：动作图标资产与文件图标提取——矢量图标清单、SVG 键目录/取值、自定义图标存储（列表/导入/删除/图像源）、文件/程序图标提取（`GetIcon`）。
- **关键内部**（按 WPF 亲和度三分）：实例服务契约 `IIconAssetService` 驻
  `StarPie.Sdk.Wpf/Services/Icons/`，条目类型 `CustomIconItem`/`VectorIconItem` 与 `.lnk`
  解析契约 `IShortcutTargetResolver` 驻 `StarPie.Sdk/Services/Icons/`；静态纯目录 `IconCatalog`
  与自定义图标目录 `CustomIconStore` 驻宿主内核 `StarPie.Host/Icons/`；WPF 图像构造
  `IconAssetService` 驻 `StarPie.Ui/Services/Icons/`（组合内核目录与 .lnk 契约）。
  消费方：M1 动作编辑、M2 轮盘渲染、S6 图标选择器。物理路径见 [layout.md](layout.md)。
- **扩展局部性**：新增图标资产/提取能力 → S1 内部（静态纯表进 Host，图像构造进 Ui）。

#### S2 配置与保存

- **职责**：`config.json` 读写/宽松解析/默认播种/向后兼容、运行态配置、防抖与立即保存编排、导入/导出、`AppDataPaths`。
- **关键内部**：配置服务与保存编排（`IConfigService`/`JsonConfigService`/`ISaveDebouncer`/
  `SettingsSaveOrchestrator`/`AppDataPaths`，物理居宿主内核 `StarPie.Host/Kernel/Configuration/`；
  不含 `AutostartRegistry`——归 M5，见 §4）；WPF 亲和的 `DispatcherSaveDebouncer` 是
  Ui 适配器（`StarPie.Ui/Adapters/`）；配置 POCO 在 `StarPie.Sdk/Models/`（语义归属见 R8）。
- **扩展局部性**：加配置字段（原型 A 模型步）→ S2 + 所属模块 VM（放行共享面）。

#### S3 本地化

- **职责**：四语言键表与取词、语言状态/切换/回退链、运行时语言字典投影桥、文案分类语义。
- **关键内部**：宿主内核 `StarPie.Host/Kernel/Localization/`（`ILocalizationService`/`LocalizationService` + `Strings*.resx`）；设计期投影字典 `DesignTimeStrings.xaml` 与生成脚本在 `StarPie.Ui/Services/Localization/`；`AppHost` 的语言字典投影是 H1 对本模块的消费（§5 D4）。
- **扩展局部性**：新语言/新文案键/改回退链 → S3 内部。

#### S4 消息与通知

- **职责**：跨模块协调事件契约 hub 与弹窗通知载体。
- **关键内部**：`Services/Messages/Messages.cs`、`Notices.cs`（`NoticeKind`/`NoticeRequest`）。
- **扩展局部性**：新消息/通知类型 → S4 内部（放行共享面）。

#### S5 导航

- **职责**：设置控制台页面切换的**目录/槽位注册契约（纯契约共享模块）**——
  槽位表 0–3 正典、页面注册目录与完整性收口；页面模板由所属模块提供（ADR-0016）。
  导航运行时主体（当前页状态/执行入口/导航项 VM/主框架 VM）归 H1 宿主壳件
  （与 R4/D3 同判据——单一消费方在 Host，模块对运行时类型零引用）。
- **关键内部**：`StarPie.Sdk` 含目录/槽位契约 `NavigationCatalog`（`NavigationCatalog`/
  `NavigationSlot`/`NavigationSlots`/`NavigationPageRegistration`——贡献者写、控制台读；
  P1.3/#112 收口）；
  运行时主体（`NavigationStore`/`NavigationExecutor` 含 `INavigationExecutor`/`MainViewModel`/
  `NavigationItemViewModel`）在 Host（命名空间不变，见
  [host.md](host.md)/[navigation.md](navigation.md)）；`SidebarView` 属 Host。
- **扩展局部性**：新增页面（原型 B）→ 所属贡献者 `RegisterNavigation` + 页面模板字典
  （M5/M1 只动 Ui 集内各自部件，页面 VM DI 注册随各自
  ShellContributor/GesturesContributor 登记；Host 外观聚合页经
  HostPageContributor/HostPageTemplates），不碰其它模块（见 [assemblies.md](assemblies.md) §5）。

#### S6 对话框

- **职责**：全部对话框唯一形态——`IDialogService`/`DialogService`、VM/Window 配对、结果 record、通用选择器（程序选择、图标选择、取色、文本/热键输入、屏幕取色）。
- **关键内部**：契约 `IDialogService` + 结果 record 驻 `StarPie.Sdk/Services/Dialogs/`
  （纯 C#，命名空间不变，ADR-0023）；实现与界面
  （`DialogService`、五对对话框 VM/Window、取色行为 `SpectrumCanvasBehavior`）随 P1.10/#119 驻
  `StarPie.Ui`（`Services|ViewModels|Views/Dialogs|Controls/`）。
- **对外契约**：领域数据经注入提供者/模块出口获得——程序扫描候选经 SDK 契约
  `IProgramScanner`（组合出口 `ProgramSourceAggregator` 由 `HostCoreContributor` 登记，内含内置来源与插件来源），
  图标资产/快捷方式解析经 Sdk.Wpf 出口接线；
  窗口主题应用消费 M4 `IThemeService`（经 Sdk.Wpf 契约边，ADR-0023）；不直穿 M3/S1 与 M4 实现内部；消费方
  （M1/M2/M5/Host）只显式引用 `StarPie.Sdk` 调 `IDialogService`（P1.3/#112）。
- **扩展局部性**：新增对话框（原型 C）→ S6 内部 + 调用方一行；新增结果 record/对话框契约 →
  扩展 `StarPie.Sdk` 的 Dialogs 契约（P1.3/#112 收口）。

### 宿主（1）

#### H1 宿主与组合根

- **职责**：进程生命周期（单实例、全局异常、启动/退出/隐藏协调）、DI 组合根注册与解析、宿主回调委托、开发实例。
- **关键内部**：`App`/`AppHost`/`Composition`/`DevInstance`（R2）；宿主回调委托包
  `AppHostDelegates` 为 H1 职责——类型本体为 SDK 公开契约（`StarPie.Sdk/Services/`，
  P1.3/#112 自 Core 收口），
  回填实现归 Host（见 [host.md](host.md)/[layering.md](layering.md)）。
- **扩展局部性**：新服务/页面 VM 注册一行（放行）；不承载业务逻辑。

## 4. 归属裁定表（R1–R8）

| #  | 项                                                                                                                                       | 归属                                                                                                                                                                                                                                                                                       | 物理现状                                                                                                                                                                                                                                                                                                                                                                   |
| -- | ---------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| R1 | `AutostartRegistry`                                                                                                                    | M5 壳层                                                                                                                                                                                                                                                                                    | `Services/Shell/`                                                                                                                                                                                                                                                                                                                                                        |
| R2 | `DevInstance`                                                                                                                          | H1 宿主                                                                                                                                                                                                                                                                                    | `StarPie.Ui/`（工程根）                                                                                                                                                                                                                                                                                                                                                  |
| R3 | `MemoryOptimizer`                                                                                                                      | M5 壳层                                                                                                                                                                                                                                                                                    | `Services/Shell/`                                                                                                                                                                                                                                                                                                                                                        |
| R4 | `MainView.xaml` / `MainView.xaml.cs`                                                                                                 | **全文件 → H1 宿主壳（Host 壳窗口，ADR-0016 决策 6/7）**；xaml.cs 不再归 M5；页面 DataTemplate 已迁出 MainView，模块模板字典随所属模块程序集（见 [assemblies.md](assemblies.md) §5.1）                                                                                                                  | `Views/Navigation/`；exe 仅余 Host 外观页模板                                                                                                                                                                                                                                                                                                                              |
| R5 | `GesturePoint`                                                                                                                         | 共享值类型（SDK）                                                                                                                                                                                                                                                                             | `StarPie.Sdk/Models/`（P1.3/#112）                                                                                                                                                                                                                                                                                                                                        |
| R6 | `IconHelper`                                                                                                                           | **三分**：图标资产 → S1；几何（`CreateAdvancedSectorGeometry`/`GetCoreIconGeometry`）→ M2；程序侧（`ResolveShortcutTarget`）→ M3                                                                                                                                            | 原 `IconAssets.cs` 已拆：S1 契约与资产表（`IconCatalog.cs`/`CustomIconItem.cs`/`VectorIconItem.cs`，双形拆为静态目录 + 实例服务，见 §3 S1）、M2 `StarPie.Ui/Services/Wheel/WheelGeometry.cs`、M3 `StarPie.Programs` 的 `Services/Programs/ShortcutResolver.cs` |
| R7 | `ProgramPicker`/`IconPicker`                                                                                                         | S6 对话框（通用选择器）                                                                                                                                                                                                                                                                    | `StarPie.Ui/ViewModels/Dialogs` 与 `Views/Dialogs/`                                                                                                                                                                                                                                                                                                                       |
| R8 | `Models` 语义归属与物理落位                                                                                                            | `WheelProfile`/`ActionItem` → M1（物理 `StarPie.Sdk/Models/`，配置 POCO）；`WheelPalette*` → M2（语义归 M2；物理随归并驻 `StarPie.Host/Wheel/`，WPF-free）；`CustomColorPreset` → M2（语义；物理 `StarPie.Sdk/Models/`——`AppConfig.CustomColorPresets` 配置 POCO 引用） | `StarPie.Sdk/Models/` + `StarPie.Host/Wheel/`                                                                                                                                                                                                                                                                                                                            |
| R9 | 导航运行时主体（`NavigationStore`/`NavigationExecutor`（含 `INavigationExecutor`）/`MainViewModel`/`NavigationItemViewModel`） | H1 宿主壳（与 R4/D3 同判据——运行时消费者全部在 Host，模块程序集零引用）                                                                                                                                                                                                                  | `StarPie.Ui/Services/Navigation/` + `StarPie.Ui/ViewModels/Navigation/`（命名空间不变，共享命名空间树）                                                                                                                                                                                                                                                                    |

## 5. 登记表（子职责 / 双职责 / 装配点）

### D1 M1 内部子面

触发（MouseHook/Controller/Engine/Behavior 页）、动作执行（Actions）、配置方案编辑（ProfileList/Slot/Gestures 页）三个子面；共享上下文 = Gesture 语义含“松开执行动作”、Profile=扇区动作集合。**观察信号**：触发与动作各自膨胀成独立服务族、或 M1 出现第二个外部“动作执行”消费方时，再评估拆为两个模块。

### D2 M5 子职责目录与护栏

子职责：托盘 / 自启 / 内存 / 高级设置面。主窗口壳层行为按 ADR-0016 归 H1 宿主壳（Host 壳窗口，见 [assemblies.md](assemblies.md) §4），不再属 M5。护栏：新 OS 集成功能必须先对号入座；放不进任何现有子职责时，须先论证与壳层上下文的共享关系，否则不得并入 M5。

### D3 MainViewModel / ShellViewModel 拆分（ADR-0016）

原登记：主归属 **S5 导航**（导航项/当前页/选中同步），壳层职责成员（`WindowTitle`、`IsExiting`、`Save()`）借调 M5，类型级双职责例外。

ADR-0016 决策 7：`MainViewModel` 收敛为纯导航；壳成员迁出为
`ShellViewModel`（`WindowTitle`/`IsExiting`/`Save()`，留 Host 壳窗口，与 R4 同判据）；
`MainView` 分区 DataContext（导航区绑导航 VM、壳区绑壳 VM）；目录驱动——MainViewModel
无页面类型硬编码，导航项来自 `NavigationCatalog` 模块注册。
**物理落点修订**：ADR-0016 决策 7 中"MainViewModel 随 S5 导航内核进
Core"的物理落点表述被部分推翻——导航运行时主体（含 `MainViewModel`）迁回 Host
（`StarPie.Ui/Services/Navigation/` 与 `StarPie.Ui/ViewModels/Navigation/`，命名空间
不变）；职责拆分语义（纯导航 vs 壳层职责）与目录驱动设计全部保留（见 R9）。

### D4 AppHost 语言字典投影

`AppHost.cs` 归 H1；其运行时语言字典投影与壳外文案刷新是 H1 消费 S3 的行为，不是双归属（防旧表述把 AppHost 列入“组成文件”造成的误解）。

### D5 WheelFactory 装配点例外

ADR-0016 决策 11：`WheelFactory` 随 M2 收编
`StarPie.Ui/Services/Wheel/`（P1.7/#116 归并入 Ui），工厂/轮盘 VM/外观只读状态契约为薄契约程序集（M1→M2 runtime
允许边清零，M1 手势侧只经契约接口消费），P1.3/#112 随 SDK 收口迁入 `StarPie.Sdk/`；
`IProfilePreviewSource` 随实现方 M1 下沉
（契约随实现方下沉；实现方 M1 ProfileListViewModel 与消费方 M2
WheelAppearanceSettingsViewModel 均只依赖契约程序集），同样 P1.3/#112 收口入 `StarPie.Sdk/`。
M2 构造契约变更不再波及 Host/M1
装配点；M1 手势侧随归并入 Ui 集，仍只经 SDK 契约引用 M2。

### D6 页面壳

- Trigger/Gestures 设置页 = M1 的设置面（整页 VM 属 M1，随归并入 `StarPie.Ui`：
  VM+View+贡献者+模板字典均在 Ui 集内，新增页面不碰 Host）；
- Appearance 设置页 = M4（界面主题卡）+ M2（轮盘外观卡）的聚合壳；
- Advanced 设置页 = M5 的设置面（随归并入 `StarPie.Ui`：VM+View+贡献者+模板字典
  均在 Ui 集内，新增页面不碰 Host）；
- 新增设置页按原型 B 走导航登记，不预设归属模块。

## 6. 扩展点验收表（“只动相关内部”）

| 原型/场景             | 示例                              | 只动                                                                        | 放行共享面                                                                                                                                                                                                                                                                                                                             |
| --------------------- | --------------------------------- | --------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A 新增设置项          | 现有页加开关                      | 所属模块 VM                                                                 | S2 模型字段、S3 文案键                                                                                                                                                                                                                                                                                                                 |
| B 新增设置页面        | 新导航页                          | 新域/所属模块（贡献者 + 页面模板字典，见[assemblies.md](assemblies.md) §5） | M5/M1 只动 Ui 集内各自部件（Shell/GesturesContributor 的 RegisterNavigation/RegisterServices + Shell/GesturesPageTemplates.xaml + 页面 VM/View），不碰 Host；Host 外观聚合页经 HostPageContributor + 模板字典与 VM 登记；新增页面不碰其它模块，仅新增模块才 H1 登记；S3 文案 |
| C 新增对话框          | 新模态                            | S6 内部                                                                     | 调用方模块一行（经`IDialogService`）                                                                                                                                                                                                                                                                                                 |
| D 新增动作类型        | 新 Launch/Folder/Hotkey/System 值 | M1 内部（路由/执行/预设/槽位编辑/图标键映射）                               | 新图标资产 → S1；S3 文案；config 兼容                                                                                                                                                                                                                                                                                                 |
| E 新增轮盘样式        | 新 Renderer                       | M2 内部（渲染器/工厂/配色目录/外观选项）                                    | S3 文案                                                                                                                                                                                                                                                                                                                                |
| F 新增后台服务/监听器 | 新 Hook/Service                   | 所属模块内部                                                                | 所属贡献者一行、新增模块才 H1                                                                                                                                                                                                                                                                                                          |
| 附加：新语言          | —                                | S3                                                                          | —                                                                                                                                                                                                                                                                                                                                     |
| 附加：新消息/通知类型 | —                                | S4                                                                          | 放行共享面                                                                                                                                                                                                                                                                                                                             |
| 附加：新图标资产      | —                                | S1                                                                          | 放行共享面                                                                                                                                                                                                                                                                                                                             |
| 附加：新主题方案      | —                                | M4                                                                          | S3 文案                                                                                                                                                                                                                                                                                                                                |
| 附加：新程序来源      | —                                | M3                                                                          | —                                                                                                                                                                                                                                                                                                                                     |

## 参见 ADR

[0015](../adr/0015-module-map-and-ownership.md)（模块划分共识：12 模块地图、归属裁定与修整单元判据）、[0016](../adr/0016-assembly-split-target-and-roadmap.md)（程序集化目标态与分批执行）。
