# 共享 UI 基建去共享化：转换器/样式字典归 Host、专用控件归模块、页面基类删除

把共享内核 `StarPie.Core` 中的可复用呈现实现件（共享 UI 基建：4 个通用转换器、
`ModernControls.xaml`、`HotkeyRecorderBox`、`SettingsPageBase`）移出内核：转换器与
全局样式字典归宿主 `WinPieGestures`（Host）、专用控件归唯一消费方 `StarPie.Gestures`、
共享页面基类删除。本决策是 #89（StarPie.Core 共享内核章程）会话的讨论前提落地，锚点
issue #90；决策记录 #93。

## Status

Accepted（2026-09-08/09 grill-with-docs 会话：Q1=B 局部处置、Q3 按推荐；决策先行——
本 ADR 落地后由实施票 #94 执行，叶子维持 as-built 随实施回填，ADR-0016/0021 同款纪律）。

## 背景与动机

1. **共享 UI 基建不是内核成员**：#89 Q1 已确认「内核成员 = 服务全局的能力/全局数据，
   且变更受治理」。`Views/Converters`、`Views/Controls`、`Views/Styles`、
   `Views/Pages` 是**可复用呈现实现件**，不属于全局机制/数据；社区 shared-kernel 通行
   规则明确「内核不收基础设施实现与 UI」（Milan Jovanović / DevIQ）。
2. **无跨模块编译期复用价值**：`HotkeyRecorderBox` 唯一编译期消费方是
   `GesturesSettingsPage.xaml`；转换器与 `ModernControls.xaml` 的键虽被多模块 XAML
   运行期引用，但全部经 `App.xaml` 单点资源合并以 `{StaticResource}` 解析——搬离 Core
   不产生模块编译期依赖，只动资源缝的 pack URI。
3. **模块 UI 自治已是常态**：B6/#79 起 M5 页面、B7/#80 起 M4 主题 XAML、B8/#81 起 M2
   轮盘 UI、B9/#82 起 M1 页面、B11/#88 起 S6 对话框均随各自模块程序集；Core 继续持有
   共享 UI 基建是 B5/#78 时代的「共享收容所」残留，与「模块保留自有 UI 面」的既定方向
   相悖（用户裁决 Q1=B：只消灭跨程序集共享/无归属的共享 UI 基建层，不以任一模块为样板
   把所有模块 UI 迁 Host）。
4. **与 ADR-0023 同向收窄**：模块契约硬边界（ADR-0023）后 Core 收窄为契约 + 全局机制/
   数据 + 共享基建；UI 实现件若留 Core 将成为「非内核成员却住在内核」的负债（#89 B 类
   平台件语义）。

## Considered Options

### Q1. 处置范式
- **A. 全局范式（以 Programs 样板把所有模块 UI 迁 Host）**：会推翻 ADR-0015 D6 /
  ADR-0016 B6–B9/B11 / ADR-0021 与 6 个 PlacementTests 的模块 UI 自治 → 否。
- **B. 局部处置（采纳）**：模块保留自有 UI 面；本次只消灭「跨程序集共享/无归属的共享
  UI 基建层」。VM 留模块（Q1=B 推论，Round-2 Q2 关闭）。

### Q3. 各件落点
- **3a. 4 个通用转换器 → Host（采纳）**：转换器无业务归属、被多模块经 App 级资源运行期
  消费；Host 是 App.xaml 唯一持有者，单点实例化 key 不变；Dialogs/Gestures 无编译期
  引用，可安全迁移。
- **3b. `ModernControls.xaml` → Host（采纳）**：全局控件样式字典，App.xaml 本地合并；
  先摘除 `HotkeyRecorderBox` 样式段（该段随控件下沉）。
- **3c. `HotkeyRecorderBox` → Gestures（采纳）**：唯一编译期消费方 = `GesturesSettingsPage`
  （`xmlns:controls="...assembly=StarPie.Core"`）。模块不可反引 Host，故不能进 Host；
  其样式段随控件入 `StarPie.Gestures`。备选「进 Host 供 Gestures 经 pack URI 引用」因
  Gestures 页需编译期 xmlns 引用 Host 程序集而违反模块不引用 Host 基线 → 否。
- **3d. `SettingsPageBase` → 删除（采纳）**：基类仅 18 行 Loaded/Unloaded 钩子 + 两个
  virtual；实际 override 仅 Trigger/Advanced/Appearance 3 页，Gestures/About 无 override。
  5 页 XAML 根改 `UserControl`，3 个 override 页在各自 code-behind 直接 `Loaded +=`/
  `Unloaded +=` 成对订阅（ADR-0009 白名单第 1 条，先例 `InputDialog.xaml.cs`/
  `RadialWindow.xaml.cs`）——保留基类无跨页共享收益，且维持「共享面最小化」。

### Q4. 程序集数量
- 不因本决策新增程序集：UI 件落入现有 Host/Gestures；「8 程序集目标态」的扩展由
  ADR-0023（模块契约硬边界）另行裁决，两决策独立。

## Decision

1. **4 个通用转换器 → `WinPieGestures`**：`HexToBrushConverter`/
   `StringToGeometryConverter`/`IntEqualsConverter`/`FilePathToImageConverter` 自
   `StarPie.Core/Views/Converters` 迁入 Host；`App.xaml` 仍单点实例化，资源 key 不变；
   模块 XAML 消费方（`{StaticResource}` 运行期解析）零改动。
2. **`ModernControls.xaml` → `WinPieGestures`**：App.xaml 由跨程序集 pack URI 合并改为
   本地合并；先摘除 `HotkeyRecorderBox` 样式段。
3. **`HotkeyRecorderBox`（控件 + 样式段）→ `StarPie.Gestures`**：
   `GesturesSettingsPage.xaml` 的 `xmlns:controls` 改 `assembly=StarPie.Gestures`。
4. **`SettingsPageBase` 删除**：Trigger/Gestures/Advanced/Appearance/About 5 页 XAML 根
   改 `UserControl`（`x:Class` 不变）；Trigger/Advanced/Appearance 3 页 code-behind 改为
   `Loaded`/`Unloaded` 成对订阅；Gestures/About 仅换根。
5. **Core `Views/` 清空**：`Converters/`、`Controls/`、`Styles/`、`Pages/` 目录移除后，
   Core 不再含呈现实现件（其余收窄随 ADR-0023/#95–#97）。
6. **测试与文档收口随实施票 #94**：`SharedUiAssemblyPlacementTests`/
   `ShellAssemblyPlacementTests` 断言更新或删除；必要时新增 Gestures 侧断言；叶子回填见
   Consequences。
7. **e2e 判定**：页面 XAML 根与资源字典有改动，命中 ADR-0018「页面/窗口 XAML」必跑面；
   实施按免跑判定核对，拿不准跑全量。

## Consequences

- **编译器边界**：模块不再编译期引用 Core 的 Views 命名空间类型（`SettingsPageBase`、
  `HotkeyRecorderBox` 除外——后者改引 `StarPie.Gestures`）；UI 消费全部退化为 App 级
  运行期资源解析。
- **资源缝**：`App.xaml` 合并源由 `/StarPie.Core;component/...` 改为本地（Host）与
  `/StarPie.Gestures;component/...`（HotkeyRecorderBox 样式段）；key 集不变。
- **页面代码**：5 页 XAML 根类型变化（无可见行为变化）；3 页 code-behind 订阅从基类
  virtual 钩子改为自订阅，退订义务随页（与 RadiaWindow/MainView 同款成对纪律）。
- **回填清单（随 #94 落地，维持 as-built 纪律）**：
  - `modules.md`：§2.3 共享放行清单「共享视图基础设施」行改写（落点 Host/Gestures/
    删除）；
  - `assemblies.md`：§2 Core 行（去掉 Views 件）、§9 B5/B6 历史行加注；
  - `layering.md`：程序集层与 Views 节（页面根基类、跨集控件引用表述）；
  - `layout.md`：Core/Host 目录表；
  - 相关叶子：`interface-theme.md`/`dialogs.md`/`gestures.md`/`wheel.md` 中引用 Core
    Views 类型或 `assembly=StarPie.Core` 的段落；
  - `architecture.md`：§3 Core 描述（ADR 索引行随本批）。
- **与 ADR-0023 的关系**：本决策先行移除 UI 实现件，ADR-0023 随后移除模块契约与 S1，
  两批共同把 Core 收窄为「全局机制/数据 + 共享基建」。

## 参考事实（2026-09-09 快照）

- `StarPie.Core/Views/` 共 7 件：`Converters/`（`HexToBrushConverter`/
  `StringToGeometryConverter`/`IntEqualsConverter`/`FilePathToImageConverter`）、
  `Controls/HotkeyRecorderBox.cs`、`Styles/ModernControls.xaml`、
  `Pages/SettingsPageBase.cs`（B5/#78、B6/#79 落位）。
- `App.xaml` 现状：合并 Theme Light / Core ModernControls / Gestures+Shell 模板字典 /
  HostPageTemplates；实例化 4 个 Core 转换器 + 2 个 Wheel 转换器。
- 编译期消费：`HotkeyRecorderBox` 仅 `GesturesSettingsPage.xaml`；`SettingsPageBase`
  为 Trigger/Gestures/Advanced/About/Appearance 5 页基类（override 3 页）。
- 测试：`WinPieGestures.Tests/SharedUiAssemblyPlacementTests.cs` 与
  `ShellAssemblyPlacementTests.cs` 断言上述件现驻 StarPie.Core。
