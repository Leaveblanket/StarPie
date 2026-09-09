# 样式资源架构：主题令牌 XAML 化 + App 单点合并

> Status: Active（原决策 2 被 0013 修订）
>
> 原“宿主换入”（`ApplyTheme` 把调色板写入 Application 直接资源、直接键覆盖）已被 [ADR-0013](./0013-localization-theme-overhaul.md) 第 4–5 条取代——改为整项替换 MergedDictionaries 活动主题槽 + 主题服务单一入口（现实现为 `AppThemePaletteManager`）。其余决策（令牌 XAML 化、App 单点合并、五套同 key 集）仍然有效；令牌与键集现状见 `docs/architecture/interface-theme.md`。

## 动机

设置界面样式资源长期以 `App.xaml` 与 `Views/Styles/SettingsStyles.xaml` 双文件维护：同一批主题画刷在 App.xaml、SettingsStyles.xaml、主题服务（C# 五套 hex）三处重复；`SettingsStyles.xaml` 被主框架、侧栏与五个页面多处合并，页面/侧栏因解析期 `StaticResource` 自足而各自携带静态 light 画刷，按 WPF 就近解析把页面内容钉死在浅色，深色系主题在设置页内容区不生效。

## Considered Options

- **最小收敛**（只删两文件重复、保留对话框本地样式拷贝）：被否——键控样式拷贝（IconPicker 复制、ProgramPicker 改名复制）与转换器双名（`BoolToVis`/`BoolToVisibility`）会继续漂移；共享层既然存在就没有理由不让对话框使用。
- **主题画刷 XAML 静态默认 + 服务运行时写入（C# 双源）**：被否——light 色板仍有两份（XAML/C#），漂移面依旧；选 XAML 化单一来源。
- **完全删除静态默认、仅运行时注入**：被否——VS 设计器将无任何颜色；保留静态合并 Light 作设计时/首帧默认。
- **主题服务自行加载主题 XAML**：被否——违反 layering.md「Services → Views ✗」依赖矩阵；换入职责归宿主层（宿主可引用 Views）。
- **每窗口合并/持有自己的主题字典**：被否——画刷引用均为 DynamicResource，App 级单一资源即可全树生效；逐窗口注入是重复与 shadow 的根源。
- **控件样式分「设置壳专用」与「共享」两层字典**：被否——整 App 统一现代外观后隐式样式全量全局化，「仅主视图合并」的载体失去意义；键控样式按 key 显式取用，统一放全局控件字典。

## Decision

1. **主题令牌**：`Views/Styles/Themes/{Light,Dark,MidnightNavy,RoyalViolet,TitaniumGray}.xaml`，每文件同一 key 集；`App.xaml` 静态合并 Light 作设计时/首帧默认。
2. **主题服务瘦身**：只保留有效主题解析、当前主题状态与 DWM 标题栏应用；删除 C# 五套 hex 与写刷子逻辑（色板数据移入 XAML 令牌）。当前接口形态（`SetTheme`/`ThemeChanged` 单一入口）见 ADR-0013 与 `interface-theme.md`。
3. **控件样式单点合并**：`Views/Styles/ModernControls.xaml` 承载全部隐式/键控样式与共享模板，仅由 `App.xaml` 合并；主视图/侧栏/页面/对话框不再各自合并。
4. **默认即现代 + 变体键控**：现代控件外观作隐式默认（含 Button）；变体（PrimaryButton/FlatComboBox/ToggleSwitch/NavTab…）键控显式取用；透明/无边框特例显式 `Style={x:Null}`。转换器统一实例。
5. **排版与几何令牌**：排版属性归一层；`CornerRadius` 等魔法数令牌化。

## Consequences

- 页面/侧栏不再携带本地画刷，深色系主题在设置内容区恢复生效；资源字典由多处合并收敛为 App 级一份。
- 新增主题 = 新增一个同 key 集 XAML + 键集校验（校验 key 集齐全即可），主题服务与配置无需改。
- 调色板应用属视图层，由 e2e 覆盖，不进服务单测。
