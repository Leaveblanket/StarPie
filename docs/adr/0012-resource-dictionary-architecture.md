# 样式资源架构：主题令牌 XAML 化 + 宿主换入 + App 单点合并

设置界面样式资源长期以 `App.xaml` 与 `Views/Styles/SettingsStyles.xaml` 双文件维护：
同一批主题画刷在 App.xaml、SettingsStyles.xaml、`ThemeService`（C# 五套 hex）三处重复；
`SettingsStyles.xaml` 被主框架、侧栏与五个页面共 7 处合并，页面/侧栏因解析期
`StaticResource` 自足而各自携带静态 light 画刷，按 WPF 就近解析把页面内容钉死在浅色，
深色系主题在设置页内容区不生效。决定按四层模型重构：主题令牌 / 全局排版 / 控件样式 /
宿主装配，全部共享资源只由 `Application` 合并一次。

## Status

Accepted（grilling 共识 1C/2C/3B + T1/S1/U2；#35 起分批实施）。

> **修订（ADR-0013，#41 起）**：决策 2（`ApplyTheme` 把目标调色板写入 Application 直接资源，
> 直接键覆盖）已被 [ADR-0013](0013-localization-theme-overhaul.md) 第 4–5 条取代——改由
> `ThemePaletteManager` 整项替换 MergedDictionaries 活动主题槽 + `IThemeService.SetTheme`/
> `ThemeChanged` 单一入口（#46/#47）。其余（令牌 XAML 化、App 单点合并、五套同 key 集）保留；
> 令牌数随 #49 C2 增 `DangerBrush` 后为 27 个。

## Considered Options

- **最小收敛**（只删两文件重复、保留对话框本地样式拷贝）：被否——键控样式三份拷贝
  （IconPicker 复制、ProgramPicker 改名复制）与转换器双名（`BoolToVis`/
  `BoolToVisibility`）会继续漂移；页面去 merge 必须把键控资源上提到 Application 层，
  共享层既然存在就没有理由不让对话框使用。
- **主题画刷保留在 XAML 静态默认 + ThemeService 运行时写入（C# 双源）**：被否——light
  色板仍有两份（XAML/C#），继续存在漂移面；选择 XAML 化单一来源。
- **主题画刷完全删除静态默认、仅运行时注入**：被否——VS 设计器将无任何颜色；保留
  App.xaml 静态合并 Light 作为设计时/首帧默认，运行时宿主以 Application 直接资源覆盖。
- **ThemeService 自行加载 Views/Styles/Themes 的 XAML**：被否——违反 layering.md
  「Services → Views ✗」依赖矩阵；换入职责归宿主层 `AppHost`（宿主可引用 Views），
  ThemeService 经组合根回填的委托触发换入。
- **每窗口合并/持有自己的主题字典**：被否——全部画刷引用均为 DynamicResource，App 级
  单一资源即可全树生效；逐窗口注入是重复与 shadow 的根源。
- **控件样式继续分「设置壳专用」与「共享」两层字典**：被否——3B 已决整 App 统一现代
  外观，隐式样式全量全局化后「仅 MainView 合并」的 SettingsStyles 载体失去意义；键控
  样式按 key 显式取用，不构成隐式泄漏，统一放全局控件字典。

## Decision

1. **主题令牌**：`Views/Styles/Themes/{Light,Dark,MidnightNavy,RoyalViolet,TitaniumGray}.xaml`，
   每文件同一 key 集（27 个在用令牌；#49 C2 增 `DangerBrush`，删除无消费者的
   `PreviewGridLineBrush`）。`App.xaml`
   静态合并 `Light.xaml` 作设计时/首帧默认。
2. **宿主换入**：`AppHost` 是唯一加载/持有主题 XAML 的层；`ApplyTheme` 时把目标调色板
   画刷写入 `Application.Resources` 直接资源（覆盖静态 Light，沿用旧 SetBrush 的
   Application 写入语义，免去 MergedDictionaries 次序问题）。调色板按主题名缓存并冻结。
3. **ThemeService 瘦身**：只保留 `ResolveEffectiveTheme`（System/空值解析）、
   `CurrentEffectiveTheme` 状态、DWM 标题栏；删除五套 hex 与 `SetThemeBrushes`。
   `IThemeService.ApplyTheme(FrameworkElement?, string)` 签名保留（root 仅用于 DWM），
   5 个调用方（MainView + 4 对话框）不变。
4. **控件样式单点合并**：`Views/Styles/ModernControls.xaml` 承载全部隐式/键控样式与
   共享模板，仅由 `App.xaml` 合并；MainView/Sidebar/页面/对话框不再各自合并
   （#36 实施）。
5. **默认即现代 + 变体键控**：现代控件外观作隐式默认（含 Button，#37 实施）；变体
   （PrimaryButton/FlatComboBox/ToggleSwitch/NavTab…）键控显式取用；透明/无边框特例
   显式 `Style={x:Null}`。转换器统一实例（`BoolToVis`）。
6. **排版与几何令牌**：排版属性归一层；`CornerRadius` 等魔法数令牌化（#38 实施）。

## Appendix: U2 按钮触发器与模板去重（#37 spike 结论）

`ModernButtonStyle`/`PrimaryButtonStyle` 原本各持一份整段 `ControlTemplate`，
hover/pressed 视觉硬编码在模板触发器内，派生样式无法复用单模板。#37 spike
（net8.0-windows STA；派生 Style「同属性、同触发值」覆盖实验 + `XamlReader`
整段解析最终 XAML 形态）确认 WPF 派生样式语义：

- 派生 Style 中与基样式「同属性、同触发值」的 `Style.Triggers` 触发器会覆盖基
  样式触发器，条件退出后干净回落到派生 Setter，不残留基样式触发值；
- 基样式触发器优先级高于派生样式 Setter——派生若不复写触发器，hover 仍取基值。

据此决定按钮链：**Button 隐式样式（唯一完整模板）→ 键控 `ModernButtonStyle`
兼容别名（BasedOn 隐式）→ `PrimaryButtonStyle`（BasedOn 别名，仅覆盖颜色与
触发器）**。hover/pressed 触发器统一上移到 `Style.Triggers`，`ControlTemplate`
不再含触发器，三份按钮模板收敛为一份；透明/无边框特例（如 HotkeyRecorderBox
✕ 清除钮）显式 `Style={x:Null}` 复位，不落入隐式默认。

## Appendix: 排版与几何令牌归一（#38）

**实证发现**：WPF 资源查找「同字典本地项优先于 MergedDictionaries」。因此
#36 后 `App.xaml` 本地隐式 TextBox（仅排版）实际**遮蔽** ModernControls 合并
字典中的完整 TextBox 模板——现代 TextBox 外观全 App 失效，而非 issue 原描述
的「排版样式被模板顶掉」。#38 spike（STA + Window 实测）确认。

排版机制决定为**隐式样式单一来源**（覆盖所有窗口，不依赖窗口根继承）：

- `RenderOptions.ClearTypeHint` 不走 DP 继承，必须落在每个文本控件自身的隐式
  样式上；
- `TextOptions.*`、`SnapsToDevicePixels`、`UseLayoutRounding` 会沿可视树继承，
  但隐式样式 Setter 优先级高于继承值，作为统一来源同样成立；
- 故两份隐式排版样式移入 `ModernControls.xaml`：隐式 TextBlock 保留，
  TextBox 隐式样式把排版 setters 与完整模板合一（消除遮蔽）；
- `App.xaml` 不再定义任何隐式 TextBlock/TextBox；MainView 与五个页面根上的
  TextFormattingMode/TextRenderingMode/ClearTypeHint 冗余删除；MainView 根
  保留 `UseLayoutRounding`/`SnapsToDevicePixels` 作为非文本布局提示；
- 本地覆盖 TextBlock 隐式样式处（ProgramPicker 占位/状态文案）改为
  `BasedOn="{StaticResource {x:Type TextBlock}}"`，排版不被局部 Style 重置。

几何令牌：`ModernControls.xaml` 顶部新增 7 个 `CornerRadius` 令牌
（Control/Item/Card/NavTab/ToggleTrack/ScrollThumb/SliderTrack），全部模板
魔法数（6/5/8/11/4/2.5）逐一替换；本次仅令牌化 ModernControls 模板内取值，
页面/对话框卡片圆角与 ListView 观感不在 #38 范围。

## Consequences

- 页面/侧栏不再携带本地画刷，深色系主题在设置内容区恢复生效；资源字典由 7+ 处合并收敛
  为 App 级一份。
- 新增主题 = 新增一个同 key 集 XAML + `ThemeService`/配置无需改（校验 key 集齐全即可）。
- `ThemeServiceTests` 的 null/解析/状态测试语义保持不变（调色板应用属视图层，由 e2e
  覆盖，沿 T09 注释约定）。
- 架构叶子 `shell.md`（主题流程）、`layout.md`（Views/Styles 结构）、`layering.md`
  （App/AppHost 分层）随各期实施同步回填。
