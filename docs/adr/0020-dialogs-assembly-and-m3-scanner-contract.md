# 对话框实现程序集化与 M3 扫描契约收口（8 程序集目标态）

把 S6「对话框」实现从宿主 exe 抽为独立模块程序集 `StarPie.Dialogs`（契约 `IDialogService`
与结果 record 留共享内核 Core），并收口接合缝审查发现的组合根委托注入缝——`ProgramEntry`/
`ProgramCatalog`/新增 `IProgramScanner` 契约上提 Core、`ProgramScanner` 改实例实现、注册器
下放注册；同步移除无生产订阅者的 `IThemeService.ThemeChanged` 死事件通道。程序集目标态由
ADR-0016 的 7 程序集扩展为 8 程序集。

## Status

Accepted（2026-09-08 grill-with-docs 会话：接合缝审查 → Q1–Q6 裁决 → Q7–Q12 全部按推荐
认可；实施批次 #88 已落地：代码 + 叶子回填 + seams.md 编目同步完成）。

## 背景与动机

1. **S6 契约与实现跨程序集不对齐**：ADR-0016 的 7 程序集目标态把 S6 对话框"契约留 Core、
   实现留 Host"。接合缝审查（S6）指出：五个对话框（ProgramPicker/IconPicker/Input/
   ColorPicker/ScreenEyedropper）的 VM/Window 全部在 exe，消费方却遍布 M1/M2/M4/M5；
   文档称"新增对话框 = 只动 S6 内部"，实际是动 Host exe——概念模块 S6 没有自己的物理落点。
   审查裁决 Q3 = B：抽独立程序集，S6 与其它模块同形（模块程序集 + 注册器自治）。
2. **组合根委托注入 M3 静态扫描（S21）是唯一"越过注册器直调模块静态方法"的缝**：
   `DialogService` 构造注入 `Func<IReadOnlyList<ProgramEntry>>`，组合根以
   `() => ProgramScanner.ScanInstalledPrograms(...)` 登记。ADR-0019/#87 已收口 .lnk 契约，
   但扫描入口仍裸露；审查裁决 Q2 = B：收口为共享契约。`ProgramCatalog` 出现第二消费方族
   （M3 扫描 + 程序选择器过滤），按 ADR-0015 §2.3 消费方判据提升共享。
3. **主题变更存在双通道**：`AppThemeChangedMessage`（S4 消息，实际唯一通道）与
   `IThemeService.ThemeChanged`（接口事件，无生产订阅者）表达同一"主题已变更"事实；
   死事件留在接口会诱导未来实现走双通道造成双应用。审查裁决 Q5 = A：移除。
4. **程序集目标态表述需扩展**：ADR-0016 记载 7 程序集（Host/Core/Programs/Shell/Theme/Wheel/
   Gestures）"目标态达成、路线清零"；本决策新增第 8 个业务模块程序集 `StarPie.Dialogs`，
   需以 ADR 形式扩展目标态，避免未来读者误以为 S6 留 Host 是"未拆完的过渡"。

## Considered Options

### 1. S6 实现落点
- **A. 维持实现留 Host**：改动最小，但 S6 内部横跨 Core（契约）+ exe（实现/VM/Window），
  新增对话框须动 Host；Host 组合根直接 new 五个对话框 Window/VM → 否。
- **B. 抽 `StarPie.Dialogs` 独立程序集（采纳，Q3=B）**：与 M1/M2/M4/M5 同形——模块程序集 +
  `DialogsModuleRegistrar`（RegisterServices 下放）+ 归属收口测试；契约 `IDialogService`
  与结果 record 留 Core 不动；Host 仅保留 `SetOwner(MainView)` 装配面（public，同 B6/#79
  `TrayIconManager` 先例）。
- **C. 把实现并入共享内核 Core**：Core 是共享 WPF 内核，但承载五个对话框 Window/VM 会
  显著膨胀共享面且与"S6 是通用能力、非模块"的定位冲突；Core 不应成为 exe 功能的收容所
  → 否。

### 2. M3 扫描能力出口（C 类委托收口）
- **A. 维持组合根委托注入静态扫描**：改动最小，但保留唯一"组合根直调模块静态方法"缝，
  与 B4–B9 注册器样板不一致 → 否。
- **B. 契约 + 纯数据上提 Core（采纳，Q7=a）**：仿 ADR-0019 `IShortcutTargetResolver` 先例——
  `ProgramEntry`（纯数据 record）迁 `StarPie.Core/Services/Programs/`（命名空间不变）；
  新增 `IProgramScanner` 契约驻 Core；`ProgramScanner` 由 static 改实例实现契约（构造注入
  `IIconAssetService`/`IShortcutTargetResolver`）；`ProgramsModuleRegistrar` 注册
  `IProgramScanner→ProgramScanner`；`DialogService`/`ProgramPickerViewModel` 改经契约注入；
  组合根删除委托行（S21 归零）。
- **C. `ProgramCatalog` 落点**：纯规则目录出现第二消费方族（M3 扫描 + Dialogs 过滤），按
  ADR-0015 §2.3 提升共享——随 `ProgramEntry` 上提 Core（采纳）；或保留 M3 令 Dialogs →
  Programs（违背 Q8 依赖方向）→ 否。

### 3. 依赖方向与装配面
- **A. Dialogs → Core + Theme（允许边）（采纳）**：窗口主题应用消费 M4 `IThemeService`，
  同 M2→M4 先例；不引用 Programs/Host/其它业务模块——扫描经 Core 契约注入。
- **B. `DialogService.SetOwner(MainView)` 回填仍由 Host 执行（采纳，Q9=a）**：接口不含
  SetOwner（Owner 是实现内部自由，不泄露进契约，ADR-0004）；`DialogService` 裁决 public
  作为宿主装配面。

### 4. 主题通道
- **A. 移除 `IThemeService.ThemeChanged`（采纳，Q11=a）**：接口/实现/注释删除，测试改经
  `AttachPaletteApplier` 回调计数断言应用/短路径/no-op（与事件同点触发）；主题应用唯一
  通道 = `AppThemeChangedMessage` → `MainView.ApplyAppTheme`。
- **B. 保留并文档化**：为不存在的订阅方保留公共事件，属死缝 → 否（真有需求时加回并配收口
  测试）。

## Decision

1. **新增 `StarPie.Dialogs` 程序集（S6 实现）**：DialogService + 五对对话框 VM/Window +
  `SpectrumCanvasBehavior` 随迁（命名空间沿用 `StarPie.*` 树不变）；新增
  `StarPie.Dialogs/Modules/DialogsModuleRegistrar.cs`（`RegisterServices` 下放
  `IDialogService→DialogService`，无 RegisterNavigation/模板字典）；slnx 登记，Host/Tests
  显式 `ProjectReference`；新增 `DialogsAssemblyPlacementTests` 6 例收口
  归属/依赖/BAML/契约/构造签名/注册器。
2. **依赖方向**：`StarPie.Dialogs → StarPie.Core` 单向 + `StarPie.Dialogs →
   StarPie.Theme` 允许边（`IThemeService`）；不引用 Host/Programs/其它业务模块。
3. **M3 扫描契约收口**：`ProgramEntry`、`ProgramCatalog`（纯规则，第二消费方族）与新增
   `IProgramScanner` 契约上提 `StarPie.Core/Services/Programs/`（命名空间
   `StarPie.Services.Programs` 不变，跨程序集共享命名空间树）；`ProgramScanner` 改
   sealed class 实例实现契约（构造注入 Core 契约）；`ProgramsModuleRegistrar` 增注册
   `IProgramScanner`；组合根删除 `() => ProgramScanner.ScanInstalledPrograms(...)` 委托行
   （S21 归零）。
4. **装配面**：`DialogService` 裁决 public；`DialogService.SetOwner(MainView)` 由 Host
   AppHost 建窗后回填（Host → Dialogs public 装配面，登记 seams.md）。
5. **移除主题死事件**：删除 `IThemeService.ThemeChanged` 接口成员与 `ThemeService` 实现/
   触发；`ThemeServiceTests` 改经 `AttachPaletteApplier` 回调计数；叶子表述回填。
6. **文档沉淀**：新增 `docs/architecture/seams.md` 活缝编目（规范内/需关注/残留三档 +
   各缝裁决链接），architecture.md 路由表登记；导航槽位容量约束登记 navigation.md
   （Q4=a：槽位表 = Core `NavigationSlot` 固定 0–4，新增页面需改 Core 枚举 + 收口测试，
   属放行共享面）。
7. **程序集目标态 = 8 程序集**：Host/Core/Dialogs/Programs/Shell/Theme/Wheel/Gestures；
   assemblies.md/modules.md/layering.md/layout.md/host.md/dialogs.md/programs.md 同步
   as-built 回填；ADR-0016 保持历史记录不回溯修改。

## Consequences

- S6 从"横跨 Core+exe"变为与其它模块同形：新增对话框 = `StarPie.Dialogs` 内部（VM/Window
  配对 + 注册器不变）+ 调用方一行；Host 组合根不再直接 new 对话框实现。
- 组合根直调模块静态方法的缝清零：M3 与 Dialogs 之间只有 Core 契约（`IProgramScanner`/
  `IShortcutTargetResolver`/`IIconAssetService`/`ProgramEntry`/`ProgramCatalog`）。
- 新增程序集成本：slnx/引用/收口测试各一次（放行共享面）；打包体量增加一个程序集
  （DialogService + 五个对话框，WPF 类库）。
- `ThemeChanged` 移除是 M4 内部 breaking（无外部订阅者，收口测试同步改写）；主题应用
  通道唯一化为 `AppThemeChangedMessage`。
- 触发条件（何时值得重开评估）：若对话框实现要按宿主/品牌换肤整体替换、或新增对话框
  频繁且需复用自绘控件，可再评估把对话框行为下沉 Core 共享 UI 基建；当前无此消费方。
