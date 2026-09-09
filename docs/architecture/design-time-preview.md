# 设计时预览

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及 VS XAML 设计器预览问题、
> `Properties/DesignTimeResources.xaml`、设计期字符串字典、设计视口登记与 L1 样例数据时读本篇；
> 决策理由见 [ADR-0025](../adr/0025-design-time-preview.md)。

## 职责与范围

为全部 XAML 设计面提供“运行时真实视口的像素级还原”预览：

- **设计面清单**：5 设置页（Appearance / Advanced / About / Gestures / Trigger）、`MainView`、
  `SidebarView`、5 对话框（ColorPicker / IconPicker / Input / ProgramPicker / ScreenEyedropper）、
  `RadialWindow`。
- **目标**：声明式文案可见、配色与控件样式沿用 VS Host 上下文、尺寸锚定运行视口；**运行时零行为
  变更、模块依赖方向不变、资源单点合并不变**。
- **非目标**：非 100% 缩放 / 自定义标题栏下的完美等价（近似）；单画布看完整长页（超高内容走
  `ScrollViewer` + 设计器缩放）；`ScreenEyedropperWindow` / `RadialWindow` 运行时形态的像素等价
  （只做结构预览）。

## 资源锚

每个含 UI 工程在 `Properties/DesignTimeResources.xaml` 放 VS 设计期资源锚（固定路径 +
csproj `Page Update` 的 `ContainsDesignTimeResources` 元数据），内容为合并设计期字符串字典；
**仅设计期生效，运行时不会自动合并**。覆盖工程：StarPie / StarPie.Shell / StarPie.Gestures /
StarPie.Dialogs / StarPie.Wheel。

## 设计期字符串字典

- **单源文件**：`design/DesignTimeStrings.xaml`（仓库根、松散、zh-CN 值），由
  `StarPie.Core/Services/Localization/Strings.resx` 经 `design/` 内生成脚本派生，**签入仓库**；
  各工程资源锚以相对路径合并。
- **回退路径**：若设计器不支持跨工程松散相对合并（spike 判定），字典改落
  `StarPie.Core/Services/Localization/DesignTimeStrings.xaml`（Page 编译、pack URI 合并），并同步
  修订本叶子与 `layout.md` 登记。
- **同步护栏**：新增/修改文案键后必须重跑生成脚本；xUnit 一致性测试锁“键集一致 + zh-CN 值与
  resx 一致”。

## 设计视口

锚点 = `MainView` 1060×720 外尺寸 + Windows 标准标题栏 + 100% 缩放；登记值以运行探针实测为准。

| 面 | 设计尺寸 | 说明 |
|---|---|---|
| 5 设置页 | 780 × 实测高 | 780 = 1060 − 230（侧栏）− 25×2（右区外边距）；高度待探针回填 |
| SidebarView | 230 × 实测高 | 高度待探针回填 |
| MainView | 1060×720 | 等于外尺寸 |
| ColorPickerWindow | 510×610 | 等于现有 Width/Height |
| IconPickerWindow | 600×480 | 等于现有 Width/Height |
| ProgramPickerWindow | 440×540 | 等于现有 Width/Height |
| InputDialog | 400 × 代表高 | `SizeToContent="Height"`，高度取运行典型实例实测值 |
| ScreenEyedropperWindow | 1600×900 | 全屏覆盖层的结构预览画布，非视觉等价 |
| RadialWindow | 360×360 | 等于现有 Width/Height |

禁止以 `d:Height`/`d:Background` 等设计期属性伪造内容全高；超高内容依赖 `ScrollViewer` 与设计器
缩放。设计面根节点一律带与登记值一致的 `d:DesignWidth/Height`。

## 样例数据（L1）

- **覆盖面（4 处）**：`SidebarView`（导航 5 项）、`GesturesSettingsPage`（Profiles / Slots）、
  `TriggerSettingsPage`（Blacklist）、`ProgramPickerWindow`（DisplayedPrograms）。
- **落位**：样例类型在各工程 `Views/DesignTime/`（命名空间 `StarPie.Views.DesignTime`，
  **无条件编译**、惰性），只被设计面根节点
  `d:DataContext="{d:DesignInstance ..., IsDesignTimeCreatable=True}"` 消费，运行时代码不得引用。
- **未样例清单（留空是正典）**：Appearance 两下拉（AppThemeOptions / PaletteOptions）、
  Gestures `ActionTypes` 下拉（`SystemPresets` 为 `x:Static` 已可见除外）、IconPicker /
  ColorPicker / RadialWindow 的绑定区块、单值绑定（开关状态、命令、Visibility）。

## 已知限制

- 视觉资源不注入：设计期配色与控件样式依赖 VS 借用 Host `App.xaml` 上下文（Light +
  ModernControls + 模块模板字典）；若 VS 未来不再借用，模块页视觉退化——不预建跨工程视觉副本。
- 设计视口是 100% 缩放 + 标准标题栏下的近似；页面有 `ScrollViewer` 兜底，不追求任意 DPI 等价。
