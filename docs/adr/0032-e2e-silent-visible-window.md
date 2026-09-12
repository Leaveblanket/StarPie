# e2e 静默形态改屏内左上角：点击穿透 + 托盘可见 + 失败截图可用

> Status: Active
>
> 关联：#139。修订 [ADR-0031](0031-e2e-silent-background-run.md) 的窗口形态与托盘/截图口径；其导航选中态、对话框策略、物理输入剥离决策不变。

## 动机

ADR-0031 把 `--background` 窗口放到 `-32000,-32000`：对用户完全不可见，但离屏窗口的客户区不被 DWM 合成——`PrintWindow(PW_RENDERFULLCONTENT)` 只能得到标题栏空壳、`capture_as_image` 为纯黑。实测：离屏窗口在 0/2/5/10/20s、切页交互、`RedrawWindow`、`DwmFlush` 后截取结果 SHA 完全一致（客户区 4 色，即空白）；同一进程把窗口移入屏内后立刻可截到真实内容（客户区 1527 色）。失败截图因此长期不可用，取证退化为窗口 dump。

把窗口放到屏幕左上角可同时满足"可截图"与"不打扰"：不可激活 + 点击穿透让用户键鼠不受影响，托盘保留人工观察与退出入口。

## Considered Options

- **保持离屏 + 失败时临时移入屏内截图** → 否。失败瞬间会在用户屏幕上闪出窗口，破坏静默承诺。
- **Windows Graphics Capture / windows-capture 等离屏捕获** → 否。同为系统截图路径，离屏窗口无合成表面可抓；且引入重依赖。
- **隐藏桌面 / 独立 Windows 会话** → 否。基建成本高，ADR-0031 已评估过，与本决策重复。
- **屏内左上角 + 不可激活 + 点击穿透 + 托盘保留** → 采纳。

## Decision

1. **窗口形态**：`--background` 主窗口定位 `(0,0)`（真实可见、被 DWM 合成）；`ShowActivated=false` + `ShowInTaskbar=false` + `WS_EX_NOACTIVATE`；额外 `WS_EX_TRANSPARENT`，并在 `AppHost` 的 HWND hook 中对 `WM_NCHITTEST` 返回 `HTTRANSPARENT`——鼠标点击穿透到下层窗口，键鼠不被打扰。
2. **托盘**：静默形态照常创建 `TrayIconManager`（通知区可见、可退出）；全局鼠标钩子仍不启动（用户真实手势不触发轮盘）。
3. **对话框**：程序/图标/颜色选择器与输入框保持离屏 + 不可激活（ADR-0031 决策 2 不变）；提示框不呈现、确认框取"是"不变。
4. **失败截图**：静默形态即可用——`conftest` 用 `PrintWindow(PW_RENDERFULLCONTENT)` 抓真实内容，客户区单色视为未取到内容；仅 pillow 缺件时 `status.json` 记 `screenshotAvailable=false` + `screenshotNote`。

## Consequences

- 静默形态运行期间，用户屏幕左上角会出现被测窗口（1060×720）；因点击穿透，覆盖区域的点击仍落到用户自己的窗口；窗口不抢焦点、不进任务栏/Alt+Tab。
- WPF 下拉弹层在屏内窗口上正常展开，ADR-0031 记录的"弹层被约束到 `(0,0)`"残留随之消失。
- 失败截图恢复真实内容，`-NoWait` 后台形态同样可截；`-Status` 的 `screenshotAvailable` 只反映 pillow 缺件。
- 静默能力边界不变：不注入物理输入、不抢前台、不启全局钩子、对话框不入屏。
