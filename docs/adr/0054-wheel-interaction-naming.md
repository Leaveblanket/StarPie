# 轮盘交互归位:代码符号随术语改名为 WheelInteraction

**状态**:accepted

**修订**:第二条鼠标输入线（轨迹识别）已决定移除，词表中原让给它的「手势」「轨迹」「图样」三条词条一并删除。**改名决策本身不变**——`WheelInteraction*` 对应词表词条「轮盘交互」，一对一映射依然成立；原稿曾把「手势」一词让给那条规划线，该安排随线移除作废，「手势」不再是本应用的任何概念的正式名。**另（2026-09-19）**：原「主题风格」词条已改名为「轮盘风格」（与「轮盘配色」并列时不再靠上下文分辨「主题」指哪一套），「长按呼出」词条已随该功能取消而删除。**再修订（2026-09-19，功能裁决重议后）**：词表词条「轮盘交互」改名为「轮盘手势」（Wheel Gesture），代码符号 `WheelInteraction*` 家族随之整体改名 `WheelGesture*`——目录、命名空间、页面/模板/设计时数据/测试文件与 e2e 同步，上文符号映射表保留 `WheelInteraction` 原名、只作当时那次改名的历史记录。同场重议维持既有功能裁决：第二条轨迹手势线维持取消、键盘捕获维持否决（ADR-0052）、长按呼出维持移除。「手势」一词随之以「轮盘手势」的完整形态重回正典，单说「手势」仍在 Avoid 之列。

代码里 `GestureEngine`/`GestureState`/`GesturesContributor`/`GesturesSettingsPage` 的语义一直是轮盘交互，而词表词条是「轮盘交互」——名字与术语对不上，任何按下表 grep 的人都会落错模块。决定:**代码侧 Gesture 家族符号照术语改名**（映射表见下），目录、命名空间、文件与类型一并改，不留「路径说 A、类型说 B」的中间态。**术语侧不新增概念**：改名的目的就是让名字与既有词条对齐，而不是为将来的线预留名字。UI 文案（四语 resx）、UIA AutomationId 与 e2e 一并改——只改显示文案会留下「文档说轮盘、UIA 说手势」的第三套命名。

## 考虑过的方案

- **保留 `Gesture*` 符号，只在文档层区分**：否决。改名目的就是让名字可被 grep；符号不改，代码与词表的漂移原地保留。
- **`RadialEngine`（对齐 `RadialWindow` 前缀）**：否决。`Radial` 在词表「轮盘」条目的 Avoid 之列（RadialMenu），采纳它等于把刚判死的词请回来。
- **`WheelController`**：否决。`Controller` 在本仓无先例，中文语境下与「设置控制台」易混。
- **只改类型名、保留目录与文件路径**：否决。`WheelInteractionSettingsPage`/`WheelInteractionContributor` 的语义若只改类型，会制造「路径说手势、类型说轮盘」的新漂移；本仓已有一次全量改名先例（`e51625c` 命名空间根改工程名）。
- **只改显示文案、保留 UIA AutomationId**（以减少 e2e 改动）：否决。保留 Id 会让自动化面继续把轮盘叫手势，第三套命名只是被藏进测试；e2e 改名的成本一次性且可控。

## 后果

**符号映射**（`Gesture*` 家族共 10 个符号；`WheelProfile`/`WheelPalette`/`WheelFactory` 等既有 `Wheel*` 名不动）：

| 现名 | 新名 |
|---|---|
| `StarPie.Host/Gestures/`（`StarPie.Host.Gestures`） | `StarPie.Host/WheelInteraction/`（`StarPie.Host.WheelInteraction`） |
| `GestureEngine` / `GestureState` / `GestureReleaseResult` | `WheelInteractionEngine` / `WheelInteractionState` / `WheelInteractionReleaseResult` |
| `GestureModifierKeys` | `HeldModifierKeys`（避免与 WPF `ModifierKeys` 同名） |
| `GesturePoint`（`StarPie.Sdk/Models`） | `ScreenPoint`（语义是屏幕坐标，与轮盘无关；输入栈、看门狗、光标探针都在用） |
| `StarPie.Ui/ViewModels/Gestures/` | `StarPie.Ui/ViewModels/WheelInteraction/` |
| `GesturesContributor` / `GesturesPageTemplates.xaml` / `GesturesSettingsPage` | `WheelInteractionContributor` / `WheelInteractionPageTemplates.xaml` / `WheelInteractionSettingsPage` |
| `GesturesSettingsDesignTimeData` 及其条目类型 | `WheelInteractionSettingsDesignTimeData` / `ProfileDesignTimeItem` / `SlotDesignTimeItem` / `ActionTypeDesignTimeOption` |
| `NavigationSlot.Gestures` | `NavigationSlot.WheelInteraction`（槽位序号不变，`NavPage2` 字符串不受影响） |
| `GestureEngineTests` / `tests/test_gesture_wheel.py` | `WheelInteractionEngineTests` / `tests/test_wheel_interaction.py` |

- **SDK 面**：改名触及 `GesturePoint`（`IWheelFactory.Create` 的入参）与 `NavigationSlot.Gestures` 两处；核查确认 `plugins/` 与 `StarPie.Sdk.Wpf` 全树零引用 Gesture 家族符号，故不升插件 ABI 版本。若已有第三方插件引用 `GesturePoint`，该改名破坏其二进制兼容——当前无此消费方。
- **文案与自动化面**：四语 resx 中语义为轮盘交互的「手势」字样一并改写（`PageGestures`→`PageWheelActions`「⚡ 轮盘与动作」、`GesturesHeader`→`WheelActionsHeader`、`GesturesSubheader`→`WheelActionsSubheader`、`TrayGestures`→`TrayWheelActions`，`TrayPause`/`TrayResume` 取值改「暂停」/「恢复」；另有 `TriggerSubheader`、`SensitivityTitle`、`SensitivityDesc`、`FullScreenOption`、`ModifierPassTitle`、`SectorCountOptionDesc`、`OuterEscapeCheckbox`、`MsgConfirmReset`、`AdminAutoStartDesc` 等句内用法）；UIA AutomationId `GesturesPageSubheader`→`WheelActionsPageSubheader`、`TrayMenuGestures`→`TrayMenuWheelActions`，e2e 断言与 `tests/conftest.py`、`tests/catalogs.py` 同步。
- **既有文档**：`docs/architecture/assemblies.md`、`layering.md`、`plugins.md`、ADR-0052 正文中语义为轮盘交互的「手势」字样同步改写；历史决策内容不变，仅术语归位。
- **词表**：`CONTEXT.md` 按域分节并收敛粒度——新增「扇区形状」（`Shape` 与 `WheelStyle` 正交：扇区几何只由扇区形状决定，风格只决定排版与装饰，原「主题风格」释义中的「切削形状」归位）、「中心核」「高亮」「扇区槽位」「导航槽位」「场景隔离」「释放结果」「补发点击」「暂停」「置前退出」「单实例恢复」「常驻壳层」等词条；新增「系统动作」「缺省动作」以收窄「预设」；「层」「长按呼出」「白名单模式」三条仍标注（规划中）——它们属轮盘自身的后续能力，与被移除的第二条线无关。
- **方向标签**：扇区方向标签（「右 (E / 0°)」等）原为构造期固化的硬编码中文，随本批改为「本地化方位名 + 语言中立符号」（角度与 E/SE/S 缩写），四语 resx 各补一组方位名（4/8/12 共用一份方向名表），切语时随通知刷新；单测里锁定字面量的断言同步更新。
- **同名消歧**：界面主题与轮盘配色的「System」选项文案加域限定（轮盘配色侧改「跟随系统（轮盘深浅）」），避免同一页两卡同名。
- **动作类型取值**：`Hotkey / Launch / Folder / System` 四类，`OpenFolder` 为 `Folder` 的同义旧值；词表按四类收敛（界面文案：快捷热键 / 启动程序 / 打开文件夹 / 系统控制），别名不单列词条。
- **门禁**：改名批次以 xUnit 全绿 + e2e `-Full` 为门禁（`AGENTS.md` 的收尾口径）；`-Smoke` 档位按路径引用的文件不受改名影响。
- **落地顺序**：术语拆分 → 词表分节与本 ADR → 代码改名（含架构叶子、四语文案、e2e）。改名是照本 ADR 映射表的机械动作，故排在其后。
