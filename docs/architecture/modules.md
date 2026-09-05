# 模块划分地图与模块化路线（模块地图）

> 本文记录模块划分共识（[ADR-0015](../adr/0015-module-map-and-ownership.md)）的地图视图：目标模块清单、职责、归属裁定、扩展点验收与模块化候选。
>
> 本文含**目标态与方向性**内容，不是纯 as-built。代码现状与各叶子（`docs/architecture/*.md`）为准，冲突时叶子优先；差异清单见 §7，随实施批次（§8）逐批回填叶子。

## 1. 何时读本文

| 想做什么 | 读哪里 |
|---|---|
| 归属争议：某个文件/职责属于哪个模块 | 本文 §4 + ADR-0015 |
| 模块内代码怎么组织、关键流程 | 对应叶子（路由见 [architecture.md](../architecture.md)） |
| 加/改功能应动哪些内部 | 本文 §6 验收表 |
| 下一个可模块化的模块 | 本文 §8 候选路线 |
| 为什么这样划分 | ADR-0015 |

## 2. 划分判据

1. **独立修整单元**：修改或新增一个功能，只动“相关模块的内部”；跨模块只经稳定契约，或触碰 §2.3 放行共享面。
2. **模块 = 领域能力**：一个模块拥有它的运行态/服务、配置面（设置子 VM/卡片）与领域数据语义；页面是聚合壳（§5 D6），不强行归单一模块。
3. **消费方归属**：共享件出现第二个消费方族才提升为共享模块；单一消费方的能力留在消费模块内部（ADR-0014 消费方判据的推广）。
4. **无“文档分组惯性”**：没有共享领域上下文、没有耦合、只因“都小/都横切”而并在一起的概念，不得并成一个模块（历史反例：本地化与消息，已拆）。

### 2.3 共享内核放行清单（不算“其它业务模块内部”）

下列改动按设计是共享面，扩展功能时允许触碰，不视为跨模块违规：

- `config.json` 模型加字段（带默认值、向后兼容，见 [config.md](config.md)）；
- i18n 文案键与四语言 resx（见 [localization.md](localization.md)）；
- `Composition.cs` / 导航（`MainViewModel` 导航项、`MainView.xaml` DataTemplate、[naming.md](naming.md) 映射表）登记一次；
- 「消息与通知」hub 新增消息/通知类型（Q16-A，ADR-0015 决策 7）；
- 共享视图基础设施（`Views/Converters/`、`Views/Controls/`、`Views/Styles/`，无业务归属，非模块）；
- 共享「图标资产」（S1）新增资产/能力（单一资产条目，不含业务逻辑）。

## 3. 模块地图（目标划分，12 个模块）

### 业务纵向模块（5）

#### M1 手势与动作
- **职责**：手势触发判定与执行全链、动作系统端到端、配置方案（Profile）编辑面。
- **关键内部**：`Services/Gestures/*`（MouseHook、GestureController、GestureEngine、WindowContext、WheelFactory）、`Services/Actions/*`（路由/执行/系统命令映射）、动作系统预设目录、`Models/ActionItem` 语义、触发与场景设置面（`BehaviorSettingsViewModel`/`TriggerSettingsPage`）、配置方案设置面（`ProfileListViewModel`/`SlotViewModel`/`GesturesSettingsPage`）。
- **对外契约**：经 `IWheelFactory` 装配 M2 瞬态轮盘（例外见 §5 D5）；消费 S2 配置模型、S3、S4、S6；向 M2 提供只读 `IProfilePreviewSource`（预览上下文，实现方为配置方案设置面 VM `ProfileListViewModel`，#69 已落地）。
- **扩展局部性**：新增动作类型（原型 D）、新增触发条件/场景规则 → M1 内部；新图标资产 → S1；新文案 → S3。

#### M2 轮盘与渲染
- **职责**：手势轮盘瞬态 VM、窗口呈现、样式渲染体系、外观配置面、轮盘配色解析、实时预览。
- **关键内部**：`ViewModels/Wheel/*`、`WheelAppearanceSettingsViewModel`、`Views/Wheel/RadialWindow`、`Views/Renderers/*`、`Models/WheelPalette/Catalog/Parser`、轮盘视觉几何（`Services/Wheel/WheelGeometry.cs`，R6 三分物理收编）。
- **对外契约**：由 M1 经 `IWheelFactory` 装配；动作图标渲染消费 S1；窗口主题应用消费 M4 的 `IThemeService`；预览 Profile 上下文经 M1 只读 `IProfilePreviewSource` 转发（#69 已落地）。
- **扩展局部性**：新增轮盘样式（原型 E）、改几何/配色/排版/预览 → M2 内部。

#### M3 程序扫描与目录
- **职责**：已安装程序扫描、目录合并/过滤、快捷方式目标解析（.lnk → 真实路径）。
- **关键内部**：`ProgramScanner`、`ProgramCatalog`、快捷方式解析（`ResolveShortcutTarget`）。
- **对外契约**：数据经注入/纯函数目录提供给 S6 的程序选择对话框，不反向依赖 S6。
- **扩展局部性**：新增程序来源/目录/过滤规则 → M3 内部。

#### M4 界面主题
- **职责**：窗口 UI 主题体系（AppTheme）——配置与解析、状态/切换/系统跟随、XAML 令牌集与整项替换、界面主题设置面、主题应用消息。
- **关键内部**：`Services/Shell/ThemeService`+`IThemeService`、根 `ThemePaletteManager.cs`、`Views/Styles/Themes/*.xaml`、`InterfaceThemeSettingsViewModel`、`AppThemeChangedMessage`；各窗口（MainView/对话框/RadialWindow）仅按 ADR-0009 白名单注入应用。
- **扩展局部性**：新增主题方案/令牌/跟随策略 → M4 内部 + S3 文案。

#### M5 壳层与系统集成
- **职责**：托盘与气泡、开机自启、内存整理、主窗口壳层行为、高级与关于设置面。
- **关键内部**：`TrayIconManager`、`AutostartRegistry`（R1）、`MemoryOptimizer`（R3）、`MainView.xaml.cs`（壳层行为，R4）、`GeneralSettingsViewModel`+`AdvancedSettingsPage`、`AboutViewModel`+`AboutSettingsPage`。
- **子职责目录**：见 §5 D2（防“系统集成”垃圾筐）。
- **扩展局部性**：新托盘菜单项/自启策略/内存策略/系统页设置项 → M5 内部。

### 共享与基础设施模块（6）

#### S1 图标资产
- **职责**：动作图标资产与文件图标提取——矢量图标清单、SVG 键目录/取值、自定义图标存储（列表/导入/删除/图像源）、文件/程序图标提取（`GetIcon`）。
- **关键内部**：`Services/Icons/IconAssets.cs`、`Services/Icons/VectorIconItem.cs`（R6 三分物理收编，T3a–T3d/#65–#68）；消费方：M1 动作编辑、M2 轮盘渲染、S6 图标选择器。
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
- **职责**：设置控制台页面切换、导航项状态、DataTemplate 页面映射、侧栏。
- **关键内部**：`NavigationStore`、`INavigationService<>`、`MainViewModel`（主归属；双职责登记见 §5 D3）、`NavigationItemViewModel`、`MainView.xaml`（R4）、`SidebarView`。
- **扩展局部性**：新增页面（原型 B）→ S5 登记导航项/模板一次 + 所属模块 + H1 注册一行（放行）。

#### S6 对话框
- **职责**：全部对话框唯一形态——`IDialogService`/`DialogService`、VM/Window 配对、结果 record、通用选择器（程序选择、图标选择、取色、文本/热键输入、屏幕取色）。
- **关键内部**：`Services/Dialogs/*`、`ViewModels/Dialogs/*`、`Views/Dialogs/*`。
- **对外契约**：领域数据经注入提供者获得（R7 接缝整理见 §8 B4），不直穿 M3/S1 内部。
- **扩展局部性**：新增对话框（原型 C）→ S6 内部 + 调用方一行。

### 宿主（1）

#### H1 宿主与组合根
- **职责**：进程生命周期（单实例、全局异常、启动/退出/隐藏协调）、DI 组合根注册与解析、宿主回调委托、开发实例。
- **关键内部**：`App`/`AppHost`/`Composition`/`AppHostDelegates`、`DevInstance`（R2）。
- **扩展局部性**：新服务/页面 VM 注册一行（放行）；不承载业务逻辑。

## 4. 归属裁定表（R1–R8）

| # | 项 | 归属 | 物理现状 | 迁移 |
|---|---|---|---|---|
| R1 | `AutostartRegistry` | M5 壳层 | `Services/Shell/` | 已落地（#70：物理迁至 M5 侧目录并同步命名空间） |
| R2 | `DevInstance` | H1 宿主 | `WinPieGestures/`（工程根） | 已落地（#70：物理迁至工程根并同步命名空间） |
| R3 | `MemoryOptimizer` | M5 壳层 | `Services/Shell/` | 已清零（B1/#64：host.md 组成摘除） |
| R4 | `MainView.xaml` / `MainView.xaml.cs` | xaml → S5；xaml.cs → M5 | `Views/Navigation/` | 已清零（B1/#64：navigation.md/shell.md 文件级登记） |
| R5 | `GesturePoint` | 共享内核值类型（目标迁 `Models`） | `Models/` | 已落地（#70：自 `GestureEngine.cs` 提取独立文件并迁入 `Models/`） |
| R6 | `IconHelper` | **三分**：图标资产 → S1；几何（`CreateAdvancedSectorGeometry`/`GetCoreIconGeometry`）→ M2；程序侧（`ResolveShortcutTarget`）→ M3 | 原 `Services/Programs/IconHelper.cs`（T3d/#68 已删）；收编结果：S1 `Services/Icons/IconAssets.cs`+`VectorIconItem.cs`、M2 `Services/Wheel/WheelGeometry.cs`、M3 `Services/Programs/ShortcutResolver.cs` | 已落地（B3/T3a–T3d/#65–#68：接线迁移 + 物理收编 + 叶子回填） |
| R7 | `ProgramPicker`/`IconPicker` | S6 对话框（通用选择器） | `ViewModels/Dialogs/`+`Views/Dialogs/` | 接缝整理候选 B4 |
| R8 | `Models` 语义归属 | `WheelProfile`/`ActionItem` → M1；`WheelPalette*`/`CustomColorPreset` → M2；物理均在 `Models/` 共享内核 | `Models/` | 已清零（B1/#64：gestures.md/wheel.md 语义登记） |

## 5. 登记表（子职责 / 双职责 / 装配点）

### D1 M1 内部子面
触发（MouseHook/Controller/Engine/Behavior 页）、动作执行（Actions）、配置方案编辑（ProfileList/Slot/Gestures 页）三个子面；共享上下文 = Gesture 语义含“松开执行动作”、Profile=扇区动作集合。**观察信号**：触发与动作各自膨胀成独立服务族、或 M1 出现第二个外部“动作执行”消费方时，再评估拆为两个模块。

### D2 M5 子职责目录与护栏
子职责：托盘 / 自启 / 内存 / 主窗口壳层行为 / 高级与关于设置面。护栏：新 OS 集成功能必须先对号入座；放不进任何现有子职责时，须先论证与壳层上下文的共享关系，否则不得并入 M5。

### D3 MainViewModel 类型级双职责
主归属 **S5 导航**（导航项/当前页/选中同步）；壳层职责成员（`WindowTitle`、`IsExiting`、`Save()`）登记为“借调 M5”，属类型级双职责例外（一个类内成员混装，无法按文件切分）。若壳层职责继续膨胀，再另行评估拆出独立壳层 VM。

（navigation.md 已按 B1/#64 登记；B5/#70 确认为非目标——壳层职责膨胀条件未触发，维持“主归属导航 + 双职责登记”。）

### D4 AppHost 语言字典投影
`AppHost.cs` 归 H1；其运行时语言字典投影与壳外文案刷新是 H1 消费 S3 的行为，不是双归属（防旧 localization.md 把 AppHost 列入“组成文件”造成的误解；随 B1 修订叶子表述）。

（B1/#64 已修订：localization.md 不再把 AppHost 列入组成文件，host.md 登记投影为 H1 对 S3 的消费。）

### D5 WheelFactory 装配点例外
`WheelFactory`（M1 文件）承担 M2 瞬态轮盘（VM+Window）装配，`layering.md` 已登记；M2 构造契约变更会波及该装配点，视为放行装配面。若装配职责迁入 M2 且 M1 只依赖工厂接口，需另行 ADR（当前不推动）。

### D6 页面壳
- Trigger/Gestures 设置页 = M1 的设置面（整页 VM 属 M1）；
- Appearance 设置页 = M4（界面主题卡）+ M2（轮盘外观卡）的聚合壳（#56 已实现）；
- Advanced/About 设置页 = M5 的设置面；
- 新增设置页按原型 B 走导航登记，不预设归属模块。

## 6. 扩展点验收表（“只动相关内部”）

| 原型/场景 | 示例 | 只动 | 放行共享面 |
|---|---|---|---|
| A 新增设置项 | 现有页加开关 | 所属模块 VM | S2 模型字段、S3 文案键 |
| B 新增设置页面 | 新导航页 | 新域/所属模块 | S5 导航登记、H1 Composition 注册、S3 文案 |
| C 新增对话框 | 新模态 | S6 内部 | 调用方模块一行（经 `IDialogService`） |
| D 新增动作类型 | 新 Launch/Folder/Hotkey/System 值 | M1 内部（路由/执行/预设/槽位编辑/图标键映射） | 新图标资产 → S1；S3 文案；config 兼容 |
| E 新增轮盘样式 | 新 Renderer | M2 内部（渲染器/工厂/配色目录/外观选项） | S3 文案 |
| F 新增后台服务/监听器 | 新 Hook/Service | 所属模块内部 | H1 注册一行 |
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
> 差异行随本批清零；MainViewModel 未拆分（D3 非目标登记）。下表剩余差异：gestures.md 的 B2 叶子补全
> 与 dialogs.md 的 B4 接缝整理（均需代码 + 叶子回填），已无「仅文档」待办。

| 现状叶子 | 目标归属 | 差异（待批次） |
|---|---|---|
| [dialogs.md](dialogs.md) | S6 | R7 接缝；B4 |
| [gestures.md](gestures.md) | M1 | ProfileList/Slot/Gestures 页组成补全（B2） |
| [localization.md](localization.md) | S3 | —（B1/#64 已清零） |
| [messages.md](messages.md)（B1 新叶） | S4 | —（B1/#64 已清零） |
| [navigation.md](navigation.md) | S5 | —（B1/#64 已清零：R4/D3） |
| [programs.md](programs.md) | M3 | —（B3/T3a–T3d/#65–#68 已清零：三分收口与叶子回填） |
| [shell.md](shell.md) | M5 | —（B1/#64 已清零） |
| [interface-theme.md](interface-theme.md)（B1 新叶） | M4 | —（B1/#64 已清零） |
| [wheel.md](wheel.md) | M2 | —（B3/T3a–T3d/#65–#68 已清零：几何收编与叶子回填） |

## 8. 候选路线（非规范，方向性）

> 每个批次：构建 + xUnit 绿；涉及可见文案时 e2e 绿；完成后回填对应叶子并从本表移除/降级。
>
> B1（#64，纯文档基线）已完成并回填（见 §7 注记）；B2 的 `IProfilePreviewSource` 只读接口化
> 已按 #69 落地（代码 + wheel.md/gestures.md/layering.md/host.md 回填）；B3（图标/几何/程序解析
> 三分收口）已按 #65–#68 落地（S1/M2/M3 出口、接线与物理收编，见 §7 注记）；B5（物理小件迁移）
> 已按 #70 落地（R1/R2/R5 收编与叶子回填，见 §7 注记），下表为剩余批次。

| 批次 | 内容 | 依据 |
|---|---|---|
| B2 | M1 配置方案设置面叶子补全（`ProfileList`/`Slot`/`Gestures` 页组成与 D1 细节；`IProfilePreviewSource` 接口化已按 #69 落地） | D1/#55 同款接口化模式 |
| B4 | S6 提供者接线整理：`DialogService` 与对话框 VM 不再直连 M3/S1 静态，改经注入提供者 | R7 |
| B6 | 页面壳子 VM 化：出现新页面级聚合需求时，按 #56 Appearance 先例拆子 VM | D6/原型 B |

## 参见 ADR

[0015](../adr/0015-module-map-and-ownership.md)（模块划分共识：12 模块地图、归属裁定与修整单元判据）。
