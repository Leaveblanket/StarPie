# 模块：手势与动作

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及手势管线、动作执行/路由与配置方案（Profile）设置面时读本篇。

## 职责

鼠标按下→拖动→选择→松开 的完整手势判定（纯逻辑）与动作副作用（系统调用）分离。

## 组成文件

- `Services/Gestures/`：`MouseHook`、`GestureController`、`GestureEngine`（+ `GestureState`/`GestureReleaseResult`）、`IWindowContext`/`WindowContext`、`IWheelFactory`/`WheelFactory`。
- `Services/Actions/`：`IActionExecutorService`/`ActionExecutorService`、`ActionRouting`（+ `ActionRoute`/`KeyStroke`/`SystemCommand`）。
- `Models/ActionItem.cs`、`Models/WheelProfile.cs`（R8：动作项与配置方案 Profile 的语义归 M1；物理均居
  `Models/` 共享内核，见 [modules.md](modules.md) §4 R8）。
- `Models/GesturePoint.cs`（R5：手势坐标点归共享内核（Models 语义）；物理已随 #70 收编 `Models/`，
  见 [modules.md](modules.md) §4 R5）。
- M1 动作编辑的图标取值（`SlotViewModel.VectorIconPathData` 等）消费 S1 共享图标资产出口
  `IconAssets`（R6 三分，T3c/#67 起接线）。

## 配置方案设置面的对外只读契约（#69）

`ViewModels/Pages/IProfilePreviewSource.cs`：M1 对外只读「预览 Profile 来源」契约——实现方为同目录
配置方案设置面 VM `ProfileListViewModel`（选中/首项回落语义，见 [modules.md](modules.md) §3 M1），
被 M2 轮盘外观设置面消费（`WheelAppearanceSettingsViewModel` 构造注入本接口并转发给
`IWheelAppearanceState.PreviewProfile`，见 [wheel.md](wheel.md)）；接口只读，轮盘侧不引用具体
方案列表 VM 类型。

## 配置方案设置面（D1 子面组成，B2/#71 补全）

> 配置方案设置面是 M1 内部三子面之一（触发与场景 / 动作执行 / 配置方案编辑，见
> [modules.md](modules.md) §5 D1）。页面壳原则见 [modules.md](modules.md) §5 D6；本页导航登记见
> [naming.md](naming.md) 页面映射表。

### 页面与 VM 组成

- `ViewModels/Pages/ProfileListViewModel.cs`（「手势与动作」导航页 `GesturesSettingsPage` 的
  DataContext；同文件嵌套 `ProfileItemViewModel` 作单条方案展示包装）。方案列表侧职责全部收编于此
  （T19/T21/T24 起页面 code-behind 无业务）：`Profiles`/`SelectedProfile` 选中态与首项回落
  （`PreviewProfile` = 选中 ?? 首项，实现 `IProfilePreviewSource`）、方案增删改/重命名/导入的
  对话框编排（经 `IDialogService`）、导入后订阅 `ConfigImportedMessage` 自行重挂、扇区数切换
  （`ApplySectorCount`，按 4/8/12 规范化）与方向槽位集合重建（`Slots`/`RebuildSlots`）。直持运行态
  配置 `Profiles` 引用 live-apply（与 `WheelViewModel` 持有运行态配置同先例）；落盘请求经
  `IMessenger` 发送保存消息（见 [config.md](config.md)）。
- `ViewModels/Gestures/SlotViewModel.cs`：方向槽位 VM（+ 同文件 `SystemPresetItem`/
  `ActionTypeOption`），包装扇区绑定的 `ActionItem` 提供编辑绑定——名称直写模型（无额外验证）、
  类型切换、热键录制（`Parameter` 绑定）与参数/图标文本派生；动作编辑闭环（程序/文件夹选择、
  图标设置）经 `IDialogService` 完成，图标取值消费 S1 共享图标资产出口 `IconAssets`
  （R6 三分，T3c/#67 起接线，见 [modules.md](modules.md) §4 R6）；编辑提交的落盘请求经 `IMessenger`
  发送保存消息上报（如 `ImmediateSaveRequestedMessage`，见 [config.md](config.md)）。
- `Views/Pages/GesturesSettingsPage.xaml(.cs)`：聚合壳页面（D6），卡片式承载 Profile 选择/增删改、
  扇区数切换与方向槽位编辑；code-behind 无业务。

### 扩展点

- 新增动作类型 = 新增系统预设条目与槽位编辑 UI 选项（清单见 [extending.md](extending.md) 原型 D）；
  新图标资产走 S1（放行共享面，见 [modules.md](modules.md) §2.3）。

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
