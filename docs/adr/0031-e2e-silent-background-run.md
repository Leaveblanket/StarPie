# e2e 静默后台化：`--background` 窗口形态 + 选中态驱动导航 + 运行器脚本

> Status: Active（窗口形态与托盘/截图口径被 [0032](0032-e2e-silent-visible-window.md) 修订）
>
> 关联：#134。窗口形态登记在 [host.md](../architecture/host.md)，导航语义登记在 [navigation.md](../architecture/navigation.md)。

## 动机

1. **原 e2e 把人赶出电脑**：`tests/test_settings.py`（19 例、每例冷启动）是提交门必跑项，但 27 处 `click_input()` 用 SendInput 真实移动物理光标；主窗口无条件 `Show()` 抢前台；Save 走系统 `MessageBox`——它按显示器居中（不跟随 owner），即使 owner 离屏也弹在屏幕中央并抢前台。
2. **可行性边界靠实测划定**（一次性探针，未入库）：静态推理给出的两条"无物理输入"路线都不可靠——
   - 投递鼠标消息（`PostMessage`/`SendMessage` 的 `WM_LBUTTONDOWN/UP`，激活/非激活、带/不带 `WM_MOUSEACTIVATE`）**一律不能驱动 WPF 点击**；
   - `SetFocus` + `PostMessage(VK_SPACE)` 能驱动导航（WPF 的 RadioButton 只认 Space，不认 Enter），但 `set_focus` 本身抢前台，`WS_EX_NOACTIVATE` 也压不住；
   - UIA 写调用（`RangeValue.SetValue`/`Toggle`/`Invoke`/`SelectionItem.Select`）在**可激活**窗口上会把窗口顶到前台；
   - 窗口挂 `WS_EX_NOACTIVATE` + 离屏后，上述写调用全部静默（`select` 重复 3 次零前台变化），启动也不抢前台；
   - 系统 `MessageBox` 不受后台窗口形态影响，仍弹在用户屏幕中央。
3. 因此"静默"不是某一个开关能解决的，需要窗口形态、导航入口、对话框可见性三处语义同时到位，再把测试侧物理输入去掉。

## Considered Options

- **鼠标消息注入点击**（测试侧合成 `WM_LBUTTON*`）→ 否。四条变体实测全灭，WPF 不把投递消息当点击。
- **`SetFocus` + Space 键盘注入** → 否。能导航但抢前台（NOACTIVATE 也挡不住），与"不打扰"直接冲突。
- **隐藏桌面 / 独立 Windows 会话 / VM** → 暂不。本机零干扰但基建与调试成本高，保留为"半静默实测仍不够"时的升级路径。
- **换自动化栈**（WinProbe/FlaUI/换 .NET UIA 客户端）→ 否。阻塞点在 app 的 UIA 表面（纯 UIA 无法触发 `RadioButton.Command`），换库不换墙；且 WinProbe 是单日一次性发布、不可依赖。
- **`--background` 窗口形态 + 选中态驱动导航 + 后台对话框策略 + 运行器脚本** → 采纳。

## Decision

1. **`--background` 后台模式**（`App.OnStartup` 解析 → `Composition.CreateAppHost(background)` → `AppHost`）：窗口 `ShowActivated=false` + `ShowInTaskbar=false` + 离屏 `-32000,-32000` + `SourceInitialized` 时挂 `WS_EX_NOACTIVATE`；不建托盘、不启全局鼠标钩子（否则用户操作鼠标时轮盘会弹到屏幕上）。仅影响窗口呈现/激活与这些副作用，导航、配置与渲染语义不变。
2. **后台模式的对话框策略**：`DialogService` 回填后台模式后，`ShowInfo` 不呈现、`Confirm` 取"是"（无人应答场景）；自定义对话框（程序/图标/颜色选择器、输入框）离屏 `-32000,-32000` + `ShowActivated=false` + `WS_EX_NOACTIVATE`（与主窗口同配方），真实打开但不占可见屏幕、不抢前台。对话框↔VM 的接线由 xUnit（`TestDialogService` 断言 `InfoCalls`/`ConfirmCalls`）覆盖；e2e 以程序选择器打开/关闭用例断言离屏与干净关闭（#135），系统 `MessageBox` 本身仍不在 e2e 覆盖内。
3. **导航由"选中态置真"驱动**：`MainViewModel` 订阅各导航项 `IsSelected`，置真且目标页不是当前页时执行导航；点击（`RadioButton.Command`）与 UIA `SelectionItem.Select` 成为等价入口（后者是 e2e 静默导航与无障碍客户端的可用路径）。两条路径幂等——同槽位命中同一页面 VM 单例，`NavigationStore` 对同实例不重发变更；`SyncSelection` 回灌的选中态因指向已停驻页面而短路，不产生回环。
4. **e2e 侧去物理输入 + 运行器**：`tests/conftest.py` 默认以 `--background` 启动被测应用（`STARPIE_E2E_ONSCREEN=1` 时可见，供调试）；27 处导航 `click_input()` 改为 `select()`；新增 `scripts/run-e2e.ps1` 作为唯一入口——命名 Mutex 串行化（防两个 e2e 互抢桌面对话框/沙盒）、日志与 junitxml 落 `artifacts/e2e/`、`-OnScreen`/`-NoWait`/`-Status`。`docs/agents/git-commits.md` 与 `CONTRIBUTING.md` 的 e2e 命令随之改指向脚本。

## Consequences

- **覆盖代价**：e2e 不再经过"物理鼠标输入 → WPF 命中测试"这一段（改由 UIA 模式调用驱动），键盘注入路径不测。真实鼠标点击路径不再有自动化覆盖，靠人工验收与 `-OnScreen` 调试形态兜。
- 系统 `MessageBox` 提示框呈现不在 e2e 覆盖内；`-OnScreen` 模式下仍走真对话框（用例里的对话框关闭分支此时生效）。自定义对话框经程序选择器用例覆盖打开/取消路径。
- 失败截图只覆盖 `-OnScreen` 形态：后台离屏窗口不被 DWM 合成客户区，系统级截图（含 `PrintWindow`）只能得到黑图/标题栏空壳，故后台形态由 `status.json` 的 `screenshotAvailable=false` + `screenshotNote` 显式标注不可用，失败取证由 `conftest` 的窗口 dump 承担；`-OnScreen` 形态用 `PrintWindow(PW_RENDERFULLCONTENT)` 抓真实内容（依赖 `tests/requirements.txt` 的 pillow，缺件时 `conftest` 告警）。
- **已知残留（可接受）**：打开 ComboBox 下拉时，WPF 的 Popup 会被"约束回可见工作区"而出现在屏幕左上角 `(0,0)`（实测：一轮全量 e2e 共 15 次、均为小尺寸弹层；主窗口 19 次全部在 `-32000` 离屏）。不抢焦点、不动物理光标，用户确认为可接受；要消掉需定制弹层定位，列为可选优化。
- 副产品验证：后台模式实例经 `TB_BUTTONCOUNT` 对照确认不创建托盘图标（后台实例 +0，普通实例 +1）；全局鼠标钩子未启（否则用户操作鼠标会弹出轮盘）。
- 静默能力对**交互控件选型**提出约束：优先选带 UIA `Invoke`/`Value` 模式的控件；依赖 `OnClick` 的交互会让静默化退步（导航项正因 `RadioButton` 不暴露 `Invoke`、`Select` 又不触发 `Command`，才改成选中态驱动）。
- 正式运行语义零变化：不带 `--background` 时三处行为全部与改造前一致；`--background` 只由 e2e 运行器使用。
- 未覆盖的场景：锁屏/无交互桌面下跑 e2e、点击路径的自动化验证——将来需要时走隐藏桌面（`CreateDesktop`）或独立会话/VM，另行立项。
