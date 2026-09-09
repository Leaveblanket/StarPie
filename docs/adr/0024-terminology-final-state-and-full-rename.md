# 术语终态与全仓正名：不留历史别名，只留持久化迁移

> Status: Active

领域/架构术语经 grill-with-docs 会话（issue #98）审查后定为**单一名义终态**，代码、
UI 文案、resx 键、config.json、UIA 自动化 ID、e2e、注释与文档全部对齐；不再使用
「旧名为历史遗留、仅加注不扩散」的双轨措辞。唯一例外是 `config.json` 的**持久化
兼容**：旧键只做加载期单次迁移读取，保存一律写新键，不构成代码/文档中的别名。

## Status

Accepted（2026-09-09 grill-with-docs 会话，issue #98；决策先行，同一 issue 实施）。

## 背景与动机

1. **同一概念多套英文并存的漂移已实际造成滞后**：轮盘配色在 CONTEXT/类型名中叫
   Wheel Palette（`WheelPalette*`），在配置键、UI 文案、VM/XAML 与渲染器参数中叫
   Wheel Theme / `Theme`；「界面主题」与「轮盘配色」在 `Theme` 一词上互相污染。
   程序集地图曾在五处叶子互抄并已出现滞后实锤（layering.md 漏
   `Shell → Dialogs.Contracts`）——文档层面的「兼容性注记」只会复制同一病灶。
2. **品牌名残留**：产物名/命名空间/AppData 目录早已是 `StarPie`（v1.3.8 起），但
   工程目录、工程文件名、解决方案、测试工程、注册表自启值、注释与文档仍大面积使用
   旧品牌 `WinPieGestures`（git 跟踪文件 97 行 / 28 文件）。
3. **页面（Page）与 Tab 的冲突**：CONTEXT 正名「页面」并明令 Avoid Tab，但 UIA
   自动化 ID 与 resx 键仍是 `NavTab*` / `Tab*`，e2e 依赖其存在。
4. **`config.json` 向后兼容为 Hard Constraint**：已发布版本存在存量用户配置，键名
   不能直接删除；但「长期双读双写」会让新代码继续携带旧名，违背单一终态。

## Considered Options

### 1. 命名策略
- **A. 单一名义 + 加载期迁移（采纳）**：代码/UI/文档只存在终态名；`config.json`
  加载时若新键缺失则回退读旧键一次，保存写新键。旧键不出现在新代码、新文档。
- **B. 保留旧名并加「历史遗留」注记**：改动最小，但术语仍双轨、文档持续解释旧名，
  且五处互抄的滞后风险继续存在 → 否（用户明确否决「携带历史遗留的妥协修正」）。
- **C. 不兼容改名**：违反 `config.json` Hard Constraint，存量用户丢失轮盘配色/风格
  设置 → 否。

### 2. 终态词表（采纳）
| 领域 | 终态 | 旧名去向 |
|---|---|---|
| 轮盘配色（领域概念/类型） | Wheel Palette（`WheelPalette` 等类型不变） | — |
| 轮盘配色选择（config 键/C# 属性） | `WheelPalette` | 旧键 `Theme` 加载期迁移 |
| 轮盘配色 VM 表面 | `PaletteOptions` / `SelectedPalette` / `WheelPaletteComboBox` | `ThemeOptions` / `SelectedTheme` / `ThemeComboBox` |
| 轮盘配色 resx 键与 UI 英文 | `WheelPalette*` / “Wheel Palette” | `WheelTheme*` / “Wheel Theme” |
| 主题风格（config 键/C# 属性/XAML） | `WheelStyle` / “Wheel Style” | `UiStyle` 加载期迁移 / “(UiStyle)” |
| 渲染/解析输入 | 参数 `palette`（方案名） | `theme` 参数 |
| 界面主题整项替换管理器 | `AppThemePaletteManager` | `ThemePaletteManager` |
| 界面主题变更消息成员 | `AppThemeChangedMessage.AppTheme` | `Theme` 属性 |
| 页面导航（UIA/resx） | `NavPage0..4` / `PageTrigger..PageAbout` | `NavTab0..4` / `TabTrigger..TabAbout` |
| 品牌/工程 | `StarPie` | `WinPieGestures`（目录/工程/slnx/测试工程/自启值/注释/文档） |

### 3. 壳层术语（伞形终态）
「壳层 (Shell)」是应用外壳职责的伞形术语，下分两个子词条：
- **壳窗口 (Shell Window)**：设置控制台主窗口的窗口职责（H1：MainView/
  ShellViewModel/关窗驻留/界面主题应用）；
- **系统集成 (System Integration)**：M5 模块（托盘、开机自启、内存整理、高级与关于
  设置面；程序集名 `StarPie.Shell` 在伞形语义下自洽，不改名）。
`StarPie.Services.Shell` 共享命名空间保留。

## Decision

1. 按上表对全仓（`.cs`/`.xaml`/`.resx`/`.py`/`.csproj`/`.slnx`/CI/`.vscode`/`docs`）
   执行终态改名；不保留任何「旧名为历史遗留」注记。
2. `JsonConfigService` 加载/导入时实现单次迁移：新键缺失时按旧键读取
   （`Theme`→`WheelPalette`、`UiStyle`→`WheelStyle`），保存一律写新键；xUnit 覆盖
   「旧文件读入 → 新键落盘」与「新文件直读」。
3. 开机自启注册表值名随工程正名写 `StarPie`，启动时清理旧值 `WinPieGestures`，
   避免旧装机双自启。
4. 新增 ADR 前不做：不重命名已发布 config 值（`System`/`Dark`/`ClassicRing`/
   `MatchaForest` 等），不重排 `StarPie.*` 程序集/命名空间拓扑。

## Consequences

- 术语单一来源：CONTEXT.md（领域词）+ modules.md/assemblies.md（架构词）不再需要
  「旧名」说明段；新代码/新文案按终态名书写。
- `config.json` 旧键仅存在于迁移读取路径与迁移测试中，作为行为而非术语保留。
- e2e 需随 AutomationId（`NavPage*`）与 XAML 名称同步更新并全量回归。
- git 历史保留旧名，未来读者经本 ADR 反查。
