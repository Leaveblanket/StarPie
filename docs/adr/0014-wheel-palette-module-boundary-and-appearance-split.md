# 轮盘配色模块归属与外观设置模块化拆分（界面主题模块边界 + 预设名语义）

2026-09-04 对 #51 完成 grilling-with-docs 共识（Q1–Q14）后裁决：为 #41/#50 重构中
范围外暂存的轮盘配色（`SelectedTheme`/自定义预设）定稿模块归属、外观 VM 拆分目标
形态与预设名显示语义。本 ADR 记录该共识；只涉及设计与文档，不推翻 ADR-0013 既有
条款（ADR-0013 决策 8 将轮盘配色划出范围，本 ADR 为其暂存方向收口）。

## Status

Accepted（grilling 共识 Q1–Q14/#51，2026-09-04；实施批次待 #51 backlog 排期，架构
叶子维持 as-built，随实施批次回填）。

## Considered Options

### 模块命名与候选类别
- **「主题配色模块」（伞形/归并主张）**：把壳层界面主题子系统改名为主题配色模块，
  或新建泛化配色体系把界面主题与轮盘配色统一收纳。否决理由：普通语义「主题配色」
  同时涵盖窗口主题与轮盘配色，会在命名层复吸已按消费方切开的模块；与 CONTEXT
  「界面主题」的 Avoid 词（主题/皮肤/配色方案）冲突；代码层更名纯 churn。
- **「界面主题模块」（文档级正名）**：现状壳层界面主题子系统在文档/概念层正式称为
  界面主题模块；代码标识符与 CONTEXT 领域术语不动。

→ 选 **界面主题模块，文档级正名，不建伞形**（Q5-C/Q6-A/Q7-B）。

### 轮盘配色机制归属
- **读法甲（令牌化进 `Themes/*.xaml`，复用 `ThemePaletteManager`）**：表面可复用
  #49 的同 key 一致性测试与整项替换机制。否决理由：自定义预设是运行时用户数据，
  不可能编译期 XAML 化，固定方案令牌化只会造成「静态字典 + 数据预设」双轨；轮盘
  是每手势瞬态实例，无「已开轮盘跟随切换」语义；污染 chrome 令牌 key 集；
  `IRadialStyleRenderer` 纯视觉契约（ADR-0009）会被资源机制反耦合；与 ADR-0013/#41
  范围外裁决一致。
- **读法乙（扩展 `IThemeService` 统一托管轮盘配色）**：`IThemeService` 语义已被
  #47/#50 锁死为窗口界面主题单一入口；塞入轮盘配色会让托盘/MainView/对话框等订阅方
  收到无关变更；轮盘配色无常驻运行时状态可广播。唯一接触点 `IsWindowsInDarkTheme()`
  只是 OS 深浅色探测，不构成共享配色数据。
- **读法丙（消费方原则归轮盘模块）**：轮盘配色唯一运行时消费者是轮盘渲染器与预览，
  数据与解析留在轮盘侧。

→ 选 **读法丙**；明确否决令牌化与 ThemeService 托管两条路（Q3′/Q4）。

### 轮盘配色模块粒度
- **独立顶层模块**：与界面主题模块对称。否决理由：只有一个消费者族；规模不足以撑
  起顶层叶子；与 Q4 消费方判据冲突。升级规则：出现第二个消费者时再提升。
- **模块级并入轮盘模块 + 类型级分离**：配色目录/解析器独立类型，渲染器工厂与各
  风格渲染器保持独立，二者由轮盘渲染管线组合。

→ 选 **并入轮盘模块、类型级分离**（Q8-A）。

### 轮盘外观配置面范围
- 仅 UiStyle 渲染模板 vs 整个外观配置面。

→ 选 **整个轮盘外观配置面**：UiStyle + 几何 + 排版 + 轮盘配色 + 光晕 + 核图标 +
  背景纹理（外观页去掉界面主题后的全部）同属轮盘模块配置面（Q9-B）。

### 外观 VM 拆分形态
- **形态 A（只拆界面主题）**：改动小，但轮盘侧仍整块驻留页面 VM，模块化不彻底。
- **形态 B（双拆 + 薄聚合页 VM）**：界面主题与轮盘外观各自拥有设置子 VM，页面 VM
  只做聚合、导入重挂编排与消息广播。
- **形态 C（不建新类型，仅文档声明）**：不实现「可独立修改」目标。

→ 选 **形态 B**（Q10-B）。

### View 对 VM 感知的收窄
- **白名单现状（保留事件 + DataContext 强转读状态）**：第 3/4 类现象仍在。
- **状态驱动 + 依赖倒置**：界面主题应用改消息驱动（VM 写配置后发消息，MainView
  订阅执行）；预览重绘消息补全（ShowCoreIcon 也发预览消息）；页面 code-behind 只剩
  DataContext 一次桥接与消息订阅，不读模块状态。
- **零 code-behind（设置区 UserControl 化 + 行为化）**：最彻底，改动面最大；与
  ADR-0009 白名单「允许少量 code-behind」的既有取舍重复，列为未来演进方向而非本次
  目标。

→ 选 **状态驱动 + 依赖倒置**（Q13-B）。

### 预览渲染器输入形态
- **直依赖 `WheelAppearanceSettingsViewModel`**：简单，但渲染器认具体 VM。
- **只读接口 `IWheelAppearanceState`**：依赖倒置，测试/替换友好。
- **不可变快照**：解耦最强，但 hover 交互需重取或缓存几何，复杂度上升。

→ 选 **只读接口 `IWheelAppearanceState`**（Q14-B）。

### 自定义预设名显示语义
- **名称本地化展示**：否决，预设名是用户数据（#49 已定）。
- **名称原样 + 周围文案键化**：下拉格式键 `WheelThemeCustomPreset` 固定，`{0}`
  原样插入用户数据；保存/重命名/删除的标题、提示、确认、成功信息、空名校验、默认名
  模板全部键化。
- **默认名建议**：本地化模板 + 时间戳，创建时语言定格，保存后为数据。
- **存储卫生**：保存/重命名 trim 后入库，空名拒绝。
- **重名/长度**：允许重名、不设长度上限，截断交 ComboBox。

→ 整包采纳（Q11/Q12）。

## Decision

1. **文档级模块正名**：壳层界面主题子系统在架构文档/讨论用语中称为「界面主题
   模块」；代码标识符（`IThemeService`/`ThemePaletteManager`/`Services/Shell`/
   `Views/Styles/Themes/*.xaml`）与 CONTEXT 领域术语不改；不建立「主题配色模块」
   伞形类别。
2. **归类判据**：模块归属 = 运行时效用/消费方；轮盘配色与界面主题各自独立，互不
   隶属（Q4）。
3. **轮盘配色归属**：属轮盘模块；模块级并入、类型级分离——配色目录（系统预设 hex
   数据）与解析器（System↔OS 深浅色、自定义预设匹配）独立成类型，渲染器工厂与风格
   渲染器保持独立；不向 `Themes/*.xaml` 添加轮盘 key；不扩展 `IThemeService` 托管
   轮盘配色状态。
4. **轮盘配色能力边界**：配置数据（`AppConfig.Theme`/`Custom*`/`CustomColorPresets`
   等）+ 解析 + 设置编排；画刷构建留在视图/渲染层，Models 保持 WPF-free，解析输出
   hex/`RgbColor` 值。
5. **轮盘外观配置面**：外观页去掉界面主题后的全部设置（UiStyle、几何、排版、轮盘
   配色、光晕、核图标、背景纹理）同属轮盘模块配置面。
6. **外观 VM 拆分目标形态（形态 B）**：
   - 新增 `InterfaceThemeSettingsViewModel`（界面主题模块设置子 VM）：独占 `AppTheme`
     透传与驻留主题选项目录（ItemsSource 化；语言切换重建；随容器 Dispose 退订）；
   - 新增 `WheelAppearanceSettingsViewModel`（轮盘模块设置子 VM）：承接全部轮盘外观
     状态与命令（含现有 `ProfileListViewModel` 预览依赖与预设 CRUD）；
   - `AppearanceSettingsViewModel` 收为薄聚合页 VM：暴露 `InterfaceTheme`/
     `WheelAppearance`、配置导入后重挂编排、`PageConfigReloaded` 广播与 Dispose 链；
   - 两个设置子 VM 物理落位 `ViewModels/Pages`（沿用页面 VM 命名/单例规范），不新增
     导航页；`ViewModels/Wheel` 仍是瞬态运行时 VM，不放单例设置 VM。
7. **View 收窄（状态驱动 + 依赖倒置）**：
   - 界面主题应用：`InterfaceThemeSettingsViewModel.AppTheme` 写配置后发布
     `AppThemeChangedMessage`，由 `MainView`（壳层 code-behind 白名单）订阅执行
     `ApplyAppTheme`；页面删除 `AppThemeComboBox_SelectionChanged`；
   - 预览重绘：`ShowCoreIcon` setter 补发 `AppearancePreviewInvalidatedMessage`，
     删除 `ShowCoreIconCheckBox_Changed` 事件处理器；
   - 页面 code-behind 仅保留 DataContext 一次桥接与消息订阅（ADR-0009 白名单），
     不读取任何模块状态。
8. **预览输入**：定义只读接口 `IWheelAppearanceState`（轮盘模块命名空间），由
   `WheelAppearanceSettingsViewModel` 实现；`WheelPreviewRenderer` 只依赖该接口。
9. **预设名语义定稿**：下拉固定格式键 `WheelThemeCustomPreset`（`{0}` = 用户数据
   原样插入、永不翻译、不随语言切换）；预设名周围文案全部键化（新增约 9–10 键 ×
   四语言，同步 i18n 盘点）；默认名建议用本地化模板 + 时间戳；保存/重命名 trim 后
   入库、空名拒绝（键化报错）；允许重名、不设长度上限。
10. **渲染器画刷数据流审计**：作为拆分实施时的同步检查项（hex 目录自
    `BaseStyleRenderer.Initialize` 收拢进轮盘配色解析器），不在本 ADR 展开设计。

## Consequences

- ADR-0013 决策 8「轮盘配色另立 #51 暂存」由本 ADR 收口为可实施的目标态；其余
  ADR-0013/0012/0010 条款不受影响。
- 架构叶子（`shell.md`/`wheel.md`/`layout.md`/`naming.md`/`architecture.md`）维持
  as-built，不先行描述未实现结构；随 #51 实施批次逐批回填（与 #42–#50 的 S1–S9
  分批回填惯例一致）。
- 本 ADR 不做代码改动；实施批次建议顺序：轮盘配色解析器收拢 + 画刷审计 →
  `InterfaceThemeSettingsViewModel` + 选项目录 + `AppThemeChangedMessage` →
  `WheelAppearanceSettingsViewModel` 抽取 + 聚合页收薄 → XAML DataContext 与事件
  清理 → `IWheelAppearanceState` 接口化 → 预设名对话框文案键化；每批构建 + xUnit
  绿，涉及可见文案时 e2e 绿。
- CONTEXT 无需修订：现有术语（界面主题/轮盘配色/主题风格）已覆盖；「界面主题模块」
  是架构模块名而非领域术语，按共识不进词汇表。

## Appendix：参考事实（2026-09-04 快照）

- 轮盘配色消费链：`RadialWindow`/`WheelPreviewRenderer` → `StyleRendererFactory` →
  `IRadialStyleRenderer.Initialize(theme, config, windowsInDarkMode)`；方案 hex 表现
  硬编码于 `Views/Renderers/BaseStyleRenderer.cs`（MatchaForest/GlacialIce/
  MorandiMuted/自定义预设/Custom 分支）。
- 外观页卡片：Card 0 界面主题（`AppThemeComboBox`，静态 ComboBoxItem + 事件应用）；
  Theme & Style Preset 卡（`UiStyleComboBox` + `ThemeComboBox` ItemsSource 化）；
  自定义高级配色扩展器（五色微调 + 预设 CRUD）；光晕/几何/排版/核图标卡。
- 预设 CRUD 硬编码中文残留（`AppearanceSettingsViewModel.cs`）：保存默认名/标题/提示
  （680）、保存成功提示（705）、空名校验（722）、删除确认（742）、删除成功提示
  （759）。
- 界面主题令牌：`Views/Styles/Themes/*.xaml` 27 key × 五套同 key 集（#49 一致性测试
  保护）；`IThemeService` 注入面：设置窗口、对话框、轮盘工厂、托盘（仅 OS 深浅色
  探测为轮盘侧唯一接触点）。
