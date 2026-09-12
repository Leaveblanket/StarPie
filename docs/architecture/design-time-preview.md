# 设计时预览

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及 VS XAML 设计器预览问题、
> `Properties/DesignTimeResources.xaml`、设计期字符串字典、设计视口登记与 L1 样例数据时读本篇；
> 决策理由见 [ADR-0025](../adr/0025-design-time-preview.md)。

## 职责与范围

为全部 XAML 设计面提供“运行时真实视口的像素级还原”预览：

- **设计面清单**：4 设置页（Appearance / Advanced / Gestures / Trigger）、`MainView`、
  `SidebarView`、5 对话框（ColorPicker / IconPicker / Input / ProgramPicker / ScreenEyedropper）、
  `RadialWindow`。
- **目标**：声明式文案可见、配色与控件样式沿用 VS Host 上下文、尺寸锚定运行视口；**运行时零行为
  变更、模块依赖方向不变、资源单点合并不变**。
- **非目标**：非 100% 缩放 / 自定义标题栏下的完美等价（近似）；单画布看完整长页（超高内容走
  `ScrollViewer` + 设计器缩放）；`ScreenEyedropperWindow` / `RadialWindow` 运行时形态的像素等价
  （只做结构预览）。

## 资源锚

含 UI 工程在 `Properties/DesignTimeResources.xaml` 放 VS 设计期资源锚（固定路径 +
csproj `Page Update` 的 `ContainsDesignTimeResources` 元数据），内容为合并设计期字符串字典；
**仅设计期生效，运行时不会自动合并**。归并后只有 Ui 集一份锚（`StarPie.Ui/Properties/DesignTimeResources.xaml`，
其余工程不再持有资源锚）。

## 设计期字符串字典

- **单源文件**：`StarPie.Ui/Services/Localization/DesignTimeStrings.xaml`（Ui 集内、Page
  编译、zh-CN 值，**签入仓库**），由同目录生成脚本
  `StarPie.Ui/Services/Localization/GenerateDesignTimeStrings.ps1` 从运行时 resx
  `StarPie.Host/Kernel/Localization/Strings.resx` 派生；Ui 资源锚以 pack URI 合并
  （`pack://application:,,,/StarPie;component/Services/Localization/DesignTimeStrings.xaml`）。
  字典随 Ui 集（程序集名 `StarPie`）承载，是设计期投影而非运行时数据源：编译为惰性 BAML，
  运行时依赖一律经 `StarPie.Host`/`StarPie.Sdk`/`StarPie.Sdk.Wpf`；独立设计期投影壳 `StarPie.Core`
  已随 P1 收口删除。
- **选型说明**：原 (c) 方案（仓库根 `design/DesignTimeStrings.xaml` 松散单源 + 跨工程相对路径
  合并）spike 无法验证——本机无 VS 设计器、且无官方文档支撑跨工程父目录松散合并行为，按
  ADR-0025 契约回退本路径；字典是**设计期投影**而非运行时第二数据源：Page 编译为惰性 BAML，
  运行时永不自动合并（资源锚仅被 VS 设计器读取，见
  [ADR-0025](../adr/0025-design-time-preview.md)）。
- **同步护栏**：新增/修改文案键后必须重跑生成脚本
  （`powershell -ExecutionPolicy Bypass -File StarPie.Ui/Services/Localization/GenerateDesignTimeStrings.ps1`）；
  xUnit 一致性测试锁“键集一致 + zh-CN 值与 resx 一致”（resx 在宿主内核、字典在 Ui 集，
  测试两侧都断言文件在位：`DesignTimeStringsConsistencyTests`）。

## 设计视口

锚点 = `MainView` 1060×720 外尺寸 + Windows 标准标题栏 + 100% 缩放；登记值以运行探针实测为准。

| 面 | 设计尺寸 | 说明 |
|---|---|---|
| 5 设置页 | 780 × 569 | 宽 = 1060 − 230（侧栏）− 25×2（右区外边距）口径；高 = 运行实测页面容器高 |
| SidebarView | 230 × 681 | 高 = 运行实测客户区高（侧栏占满客户区） |
| MainView | 1060×720 | 等于外尺寸 |
| ColorPickerWindow | 510×610 | 等于现有 Width/Height |
| IconPickerWindow | 600×480 | 等于现有 Width/Height |
| ProgramPickerWindow | 440×540 | 等于现有 Width/Height |
| InputDialog | 400 × 262 | `SizeToContent="Height"`；262 = 运行典型实例实测外框高（客户区 ≈223） |
| ScreenEyedropperWindow | 1600×900 | 全屏覆盖层的结构预览画布，非视觉等价 |
| RadialWindow | 360×360 | 等于现有 Width/Height |

禁止以 `d:Height`/`d:Background` 等设计期属性伪造内容全高；超高内容依赖 `ScrollViewer` 与设计器
缩放。设计面根节点一律带与登记值一致的 `d:DesignWidth/Height`。

实测口径（MainView 1060×720 外尺寸 @100% 缩放 + 标准标题栏，DIP）：客户区 1044×681；右页面
容器实际宽 764（客户区 1044 − 230 − 25×2），页面 `d:DesignWidth` 按任务口径取 780（1060 外框 −
230 − 25×2）；页面容器高实测 569（y≈216→785，即客户区 681 − footer 区）。

## 样例数据（L1）

- **覆盖面（4 处）**：`SidebarView`（导航 5 项）、`GesturesSettingsPage`（Profiles / Slots）、
  `TriggerSettingsPage`（Blacklist）、`ProgramPickerWindow`（DisplayedPrograms）。
- **落位**：样例类型在各工程 `Views/DesignTime/`（命名空间 `StarPie.Views.DesignTime`，
  **无条件编译**、惰性），只被设计面根节点
  `d:DataContext="{d:DesignInstance ..., IsDesignTimeCreatable=True}"` 消费，运行时代码不得引用。
- **ProgramPickerWindow 无参构造**：运行时窗口为带参构造（DI 装配）；为让设计器能实例化根窗口
  展示 L1 样例，补充仅供设计器使用的无参构造（仅 `InitializeComponent`，`_vm` 置空、不装配）；
  `DialogService` 仍走带参构造，运行时不触碰无参构造。
- **未样例清单（留空是正典）**：Appearance 两下拉（AppThemeOptions / PaletteOptions）、
  Gestures `ActionTypes` 下拉（`SystemPresets` 为 `x:Static` 已可见除外）、IconPicker /
  ColorPicker / RadialWindow 的绑定区块、单值绑定（开关状态、命令、Visibility）。

## 已知限制

- 带 DI 构造的窗口（`MainView`、除 ProgramPickerWindow 外的对话框、`RadialWindow`）尚无
  设计期无参构造，VS 设计器无法实例化其根窗口做整窗预览；页面（UserControl）均为默认构造、
  ProgramPickerWindow 已补设计期无参构造（见上）。若需整窗设计预览，须按同款补设计期无参构造。
- 视觉资源不注入：设计期配色与控件样式依赖 VS 借用 Host `App.xaml` 上下文（Light +
  ModernControls + 模块模板字典）；若 VS 未来不再借用，模块页视觉退化——不预建跨工程视觉副本。
- 设计视口是 100% 缩放 + 标准标题栏下的近似；页面有 `ScrollViewer` 兜底，不追求任意 DPI 等价。
