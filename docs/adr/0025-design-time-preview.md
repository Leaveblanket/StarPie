# 设计时预览协议：设计期资源注入与运行视口锚定

> Status: Active

## 动机

1. **全量 XAML 声明式文案在设计器为空**：声明式文案经 `{DynamicResource}` 键取自
   `AppHost.Run` 运行时代码注入的 `LanguageDictionary`（resx 267 键 ×4 语言）；VS 设计器不执行
   应用代码，且 WPF `DynamicResource` 无 FallbackValue——键缺失即文本为空。
2. **设计尺寸与运行视口脱节**：页面 `d:Design*`（760×600 等）是近似值；曾以 `d:Height="900"`、
   `d:Background="Yellow"` 等设计期属性伪造内容全高，造成显示不全/截断。
3. **类库无 App.xaml**：含 UI 的模块工程（Shell/Gestures/Dialogs/Wheel）没有 Application
   资源上下文；主题画刷与控件样式经 VS 借用 Host `App.xaml`（Light + ModernControls +
   模块模板字典）在设计期可用，但语言字典仍不可用。

## Considered Options

### 字符串注入载体
- **每工程 `Properties/DesignTimeResources.xaml` 资源锚**：VS 设计器按固定路径识别并仅设计期合并
  （csproj `ContainsDesignTimeResources`），运行时不会自动合并 → 选此，运行时零行为变更。
- 自定义 MarkupExtension 替代 `{DynamicResource}`：触碰运行时语言机制与全仓文案改写 → 否。

### 字典单源落位
- **(c) 仓库根松散单源 + 相对合并**：`design/DesignTimeStrings.xaml` 一份，各工程资源锚以
  `../../design/...` 合并。零编译产物、零 Core 污染；依赖设计器对松散文件的相对路径合并行为 →
  **先试**；spike 失败回退 (a)。
- **(a) Core 编译惰性字典 + pack URI**：`StarPie.Core/Services/Localization/DesignTimeStrings.xaml`
  编入 Core、pack URI 合并。机制文档化、稳；代价是 Core 出现首份 XAML 与“运行时永不合并”的
  惰性 BAML，需与“去共享化 / 不得另建文案字典”划清界限（定位为设计期投影，非运行时第二数据源）。
- 每工程生成副本：N 份 267 键副本与同步护栏，维护负担高 → 否。

### 设计视口口径
- **运行时真实视口锚定**：MainView 1060×720 外尺寸 + 标准标题栏 + 100% 缩放下的实测几何；
  超高内容走 ScrollViewer + 设计器缩放 → 选此。
- 内容全高伪造（d:Height 撑高整页）：与运行形态永远不一致 → 否。

### 样例数据保真级别
- **L1 关键区块样例**：仅 Sidebar（导航 5 项）、GesturesSettingsPage（Profiles/Slots）、
  TriggerSettingsPage（Blacklist）、ProgramPickerWindow（DisplayedPrograms）四面；样例类型各工程
  `Views/DesignTime/`、无条件编译 → 选此。
- L0 静态层（列表全空）不足以“预览全貌”；L2 全量样例 VM 成本高且与 DI 构造现实冲突 → 否。

### 视觉资源副本
- 不注入视觉副本：模块反向引用 Theme/Host 撞依赖墙，且推翻资源单点合并哲学 → 否，
  作为已知限制记录。

## Decision

1. 每个含 UI 工程（StarPie / StarPie.Shell / StarPie.Gestures / StarPie.Dialogs /
   StarPie.Wheel）建 `Properties/DesignTimeResources.xaml` 资源锚，仅设计期合并单源设计期字符串
   字典（zh-CN，派生自 `Strings.resx`）。
2. 字典单源先试仓库根 `design/DesignTimeStrings.xaml` 松散相对合并；spike 验证失败则回退
   Core 编译字典 + pack URI 合并（位置随回退修订并登记）。
3. 字典为签入生成物：生成脚本从 resx 派生，新增 xUnit 一致性测试锁键集与值；新增文案键后必须
   再生成。
4. 设计期尺寸 = 运行时真实视口锚点的登记值；不伪造内容全高；清理既有 `d:Height`/`d:Background`
   实验属性。
5. L1 样例仅上述 4 区块；样例类型各工程 `Views/DesignTime/`（命名空间 `StarPie.Views.DesignTime`、
   无条件编译），只被根节点 `d:DataContext` 消费；其余绑定区列入“未样例清单”。
6. 视觉资源不另注入：沿用 VS 借用 Host 上下文的现状，作为已知限制记录；模块依赖方向与资源
   单点合并不变，运行时零行为变更。

## Consequences

- 各 UI 工程新增 `Properties/DesignTimeResources.xaml`（含 csproj `Page` 元数据）；仓库根新增
  `design/`（单源字典 + 生成脚本）；Host/Gestures/Dialogs 新增 `Views/DesignTime/`——目录与文件
  在 `layout.md` 登记，协议细则在 `docs/architecture/design-time-preview.md`。
- Core 仅在回退路径含惰性 XAML 字典（作为唯一例外登记）。
- 设计期文本与视口可还原；对非 100% 缩放 / 自定义标题栏为近似；若 VS 不再借用 Host 上下文，
  视觉会退化（不预建副本）。
- 全仓 XAML 批量改动属 feature 变更，按提交纪律走任务分支与验证门。
