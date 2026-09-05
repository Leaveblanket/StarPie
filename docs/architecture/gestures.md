# 模块：手势与动作

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及手势管线、动作执行/路由时读本篇。

## 职责

鼠标按下→拖动→选择→松开 的完整手势判定（纯逻辑）与动作副作用（系统调用）分离。

## 组成文件

- `Services/Gestures/`：`MouseHook`、`GestureController`、`GestureEngine`（+ `GesturePoint`/`GestureState`/`GestureReleaseResult`）、`IWindowContext`/`WindowContext`、`IWheelFactory`/`WheelFactory`。
- `Services/Actions/`：`IActionExecutorService`/`ActionExecutorService`、`ActionRouting`（+ `ActionRoute`/`KeyStroke`/`SystemCommand`）。
- `Models/ActionItem.cs`、`Models/WheelProfile.cs`（R8：动作项与配置方案 Profile 的语义归 M1；物理均居
  `Models/` 共享内核，见 [modules.md](modules.md) §4 R8）。

## 配置方案设置面的对外只读契约（#69，B2）

`ViewModels/Pages/IProfilePreviewSource.cs`：M1 对外只读「预览 Profile 来源」契约——实现方为同目录
配置方案设置面 VM `ProfileListViewModel`（选中/首项回落语义，见 [modules.md](modules.md) §3 M1），
被 M2 轮盘外观设置面消费（`WheelAppearanceSettingsViewModel` 构造注入本接口并转发给
`IWheelAppearanceState.PreviewProfile`，见 [wheel.md](wheel.md)）；接口只读，轮盘侧不引用具体
方案列表 VM 类型。

## 关键流程

1. `MouseHook`（Win32 钩子线程）产生 `OnRightButtonDown/Up`、`OnMouseMove` 事件 → `GestureController` 订阅并喂给 `GestureEngine`。
2. `GestureEngine`（无 WPF/Win32 引用，纯状态机）：
   - `OnTriggerDown`：隔离检查（黑名单进程、修饰键禁用、全屏禁用，经 `IWindowContext` 实时取前台进程/修饰键/全屏态；配置实时读取）→ 通过则进入 `WaitingThreshold` 并吃掉事件。
   - `OnTriggerMove`：超过 `DragThreshold` → `Active` → 前台进程查 Profile（`IConfigService.GetProfileForProcess`，Global 兜底）→ `IWheelFactory.Create` 创建并 `Show` 轮盘 → 按角度高亮扇区；中心死区/外甩脱离取消由引擎决策。
   - `OnTriggerUp`：`WaitingThreshold` → `ReplayClick`（补发被吞的右键）；`Active` → 取选中扇区动作 `Execute(action)` 或 `Cancel`；其余 `PassThrough`。
3. `GestureController`（App 侧适配器，负责副作用且必须在 UI 线程）：按 `GestureReleaseResult` 分发——补发点击用 `Dispatcher.BeginInvoke`，执行动作用 `Dispatcher.Invoke` 调 `IActionExecutorService.Execute`。
4. `ActionExecutorService`（系统调用层，全部经构造注入接缝）：`ActionRouting.ResolveRoute`（大小写敏感：`Launch`/`Folder`/`Hotkey`/`System`）→ 进程启动、文件夹打开（环境变量展开、文件/目录探测）、`SendInput` 键序注入、`LockWorkStation`、系统命令映射（`ActionRouting.ResolveSystemCommand`：窗口管理/工具类发键序或启动，失败可降级发热键；未知静默）。错误提示经注入的 `MessageBox` 委托。
5. `WheelFactory`：`Create` 在 UI 线程 `Dispatcher.Invoke` 中创建 `WheelViewModel` + `RadialWindow`，返回 `DispatchedWheelViewModel` 包装（所有轮盘交互封送回 UI 线程；窗口字段作 GC 根防未显示即回收；轮盘 VM/窗口见 [wheel.md](wheel.md)）。

## 扩展点

- 新增动作类型 = `ActionItem` 新类型值 + `ActionRouting.ResolveRoute` + `ActionExecutorService` 分支 + 动作选择 UI 选项 + i18n + config 兼容（清单见 [extending.md](extending.md) 原型 D）。
- 调整手感常量/死区比例/外甩倍数：改 `GestureEngine` 常量（`CenterDeadzoneFractionOfThreshold`、`OuterEscapeFractionOfRadius`）。

## 参见 ADR

[0002](../adr/0002-manual-composition-root.md)（引擎纯函数/副作用接缝）。
