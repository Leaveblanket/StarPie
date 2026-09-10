# 术语终态与全仓正名：不留历史别名，只留持久化迁移

> Status: Active（已实施；终态词表见 `CONTEXT.md` 与 `docs/architecture/naming.md`）

## 动机

1. **同一概念多套英文并存造成实际滞后**：轮盘配色在领域词/类型名中叫 Wheel Palette，在配置键、UI 文案、VM/XAML 与渲染器参数中叫 Wheel Theme / `Theme`；「界面主题」与「轮盘配色」在 `Theme` 一词上互相污染。文档层面的“兼容性注记”只会复制同一病灶（程序集地图曾在多处互抄并出现滞后实锤）。
2. **品牌名残留**：产物名/命名空间/AppData 目录早已是 `StarPie`，但工程目录、工程文件名、解决方案、测试工程、注册表自启值、注释与文档仍大面积使用旧品牌 `WinPieGestures`。
3. **页面（Page）与 Tab 冲突**：领域词正名为「页面」并明令 Avoid Tab，但 UIA 自动化 ID 与 resx 键仍是 `NavTab*` / `Tab*`。
4. **`config.json` 向后兼容是 Hard Constraint**：已发布版本存在存量用户配置，键名不能直接删除；但“长期双读双写”会让新代码继续携带旧名，违背单一终态。

## Considered Options

### 命名策略
- **单一名义 + 加载期迁移**：代码/UI/文档只存在终态名；`config.json` 加载时若新键缺失则回退读旧键一次，保存写新键。旧键不出现在新代码、新文档。
- **保留旧名并加「历史遗留」注记**：改动最小，但术语仍双轨、文档持续解释旧名，互抄滞后风险继续 → 否。
- **不兼容改名**：违反 `config.json` Hard Constraint，存量用户丢失设置 → 否。

→ 选 **单一名义 + 加载期迁移**。

### 终态词表（采纳）

| 领域 | 终态 | 旧名去向 |
|---|---|---|
| 轮盘配色（领域概念/类型） | Wheel Palette（`WheelPalette*` 类型不变） | — |
| 轮盘配色选择（config 键/C# 属性） | `WheelPalette` | 旧键 `Theme` 加载期迁移 |
| 轮盘配色 VM 表面 | `PaletteOptions` / `SelectedPalette` / `WheelPaletteComboBox` | `ThemeOptions` / `SelectedTheme` / `ThemeComboBox` |
| 轮盘配色 resx 键与 UI 英文 | `WheelPalette*` / “Wheel Palette” | `WheelTheme*` / “Wheel Theme” |
| 主题风格（config 键/C# 属性/XAML） | `WheelStyle` / “Wheel Style” | `UiStyle` 加载期迁移 / “(UiStyle)” |
| 渲染/解析输入 | 参数 `palette`（方案名） | `theme` 参数 |
| 界面主题整项替换管理器 | `AppThemePaletteManager` | `ThemePaletteManager` |
| 界面主题变更消息成员 | `AppThemeChangedMessage.AppTheme` | `Theme` 属性 |
| 页面导航（UIA/resx） | `NavPage0..3` / `PageTrigger..PageAdvanced`（`PageAbout`/`NavPage4` 随 #107 于 2026-09-10 下线移除） | `NavTab0..4` / `TabTrigger..TabAbout` |
| 品牌/工程 | `StarPie` | `WinPieGestures`（目录/工程/测试工程/自启值/注释/文档） |

### 壳层术语（伞形终态）

「壳层 (Shell)」是应用外壳职责的伞形术语，下分两个子词条（CONTEXT.md 已收录）：
- **壳窗口 (Shell Window)**：设置控制台主窗口的窗口职责（H1：MainView/ShellViewModel/关窗驻留/界面主题应用）；
- **系统集成 (System Integration)**：M5 模块（托盘、开机自启、内存整理、高级设置面；程序集名 `StarPie.Shell` 在伞形语义下自洽，不改名）。

## Decision

1. 按终态词表对全仓（`.cs`/`.xaml`/`.resx`/`.py`/`.csproj`/`.slnx`/CI/`.vscode`/`docs`）执行终态改名；不保留任何“旧名为历史遗留”注记。
2. `JsonConfigService` 加载/导入实现单次迁移：新键缺失时按旧键读取（`Theme`→`WheelPalette`、`UiStyle`→`WheelStyle`），保存一律写新键；xUnit 覆盖「旧文件读入 → 新键落盘」与「新文件直读」。
3. 开机自启注册表值名随工程正名写 `StarPie`，启动时清理旧值 `WinPieGestures`，避免旧装机双自启。
4. 不重命名已发布 config 值（`System`/`Dark`/`ClassicRing`/`MatchaForest` 等），不重排 `StarPie.*` 程序集/命名空间拓扑。

## Consequences

- 术语单一来源：CONTEXT.md（领域词）+ modules.md/assemblies.md（架构词）不再需要“旧名”说明段；新代码/新文案按终态名书写。
- `config.json` 旧键仅存在于迁移读取路径与迁移测试中，作为行为而非术语保留。
- e2e 随 AutomationId（`NavPage*`）与 XAML 名称同步更新。
- git 历史保留旧名，未来读者经本 ADR 反查。
