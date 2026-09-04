# 样式资源架构：主题令牌 XAML 化 + 宿主换入 + App 单点合并

设置界面样式资源长期以 `App.xaml` 与 `Views/Styles/SettingsStyles.xaml` 双文件维护：
同一批主题画刷在 App.xaml、SettingsStyles.xaml、`ThemeService`（C# 五套 hex）三处重复；
`SettingsStyles.xaml` 被主框架、侧栏与五个页面共 7 处合并，页面/侧栏因解析期
`StaticResource` 自足而各自携带静态 light 画刷，按 WPF 就近解析把页面内容钉死在浅色，
深色系主题在设置页内容区不生效。决定按四层模型重构：主题令牌 / 全局排版 / 控件样式 /
宿主装配，全部共享资源只由 `Application` 合并一次。

## Status

Accepted（grilling 共识 1C/2C/3B + T1/S1/U2；#35 起分批实施）。

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
   每文件同一 key 集（26 个在用令牌；删除无消费者的 `PreviewGridLineBrush`）。`App.xaml`
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

## Consequences

- 页面/侧栏不再携带本地画刷，深色系主题在设置内容区恢复生效；资源字典由 7+ 处合并收敛
  为 App 级一份。
- 新增主题 = 新增一个同 key 集 XAML + `ThemeService`/配置无需改（校验 key 集齐全即可）。
- `ThemeServiceTests` 的 null/解析/状态测试语义保持不变（调色板应用属视图层，由 e2e
  覆盖，沿 T09 注释约定）。
- 架构叶子 `shell.md`（主题流程）、`layout.md`（Views/Styles 结构）、`layering.md`
  （App/AppHost 分层）随各期实施同步回填。
