# 输入栈重建:SharpHook 接管捕获,抑制/回放/自愈留在自研侧

**状态**:accepted

**修订**:看门狗的周期探针改由注入的 `TimeProvider` 驱动(生产系统时钟、测试假时钟),判定入口 `CheckOnce` 由公开面收回私有、`MouseInputHook.Watchdog` 句柄随之下线;判定语义(事件计数 + 系统光标位移比对、3s 周期、就地重注册)与其余条款不变(见 ADR-0053)。

轮盘交互输入侧按「捕获 / 抑制 / 注入 / 自愈」四件事重建,落地在 `StarPie.Ui/Services/Input/`,不新增 SDK 契约、不新增工程——`StarPie.Host` 仍只做纯决策(`WheelInteractionEngine` 的判据与结果语义不变)。决定:**捕获换 `SharpHook`(MIT)的 `SimpleGlobalHook`**。它是该库唯一支持事件抑制的实现(抑制必须与钩子同线程同步设置,`EventLoopGlobalHook`/`TaskPoolGlobalHook` 会忽略抑制),以 `GlobalHookType.Mouse` 只装鼠标钩子;**钩子独占专用线程**,线程上只做「喂坐标 + 拿抑制结论」,轮盘创建与更新、动作执行、点击回放一律异步卸载到 UI 线程——这是微软对低级钩子的官方建议,也是 `LowLevelHooksTimeout`(Windows 10 1709+ 上限 1000ms,超时后钩子被**静默移除且应用无从感知**)之下唯一的安全形态。**自注入识别**改用「回放窗口」:SharpHook 的 `UioHookEvent` 不暴露 `dwExtraInfo`,社区通行的注入戳记(Stroke 的 `0x7F`)在捕获侧不可移植;回放前置一次性旗标、以 `IsEventSimulated`(Windows 上即 `LLMHF_INJECTED`)辅助校验,回放期间到达的输入事件不参与轮盘交互(但**不按该标记整体过滤**——外部注入必须仍能触发轮盘交互,e2e 正是这么驱动的)。回放注入改走 SharpHook `EventSimulator`,顺带结清 ADR-0051 挂账的 `mouse_event`→`SendInput` 换代。**触发键在栈内参数化**(默认右键,不暴露配置面与 UI)。**看门狗保留**:事件计数 + 系统光标位移探针 + 周期 `Stop()`/重 `RunAsync()` 重注册(探针周期沿用 3s;「同实例 Stop 后可再 Run」有官方示例背书)。键盘侧不在本次范围:不装键盘钩子,`ActionExecutorService` 的键盘注入与委托接缝不动(动作执行是另一个域)。验收口径沿用「可观察行为零变化」,唯二记账是「钩子线程换人」与「回放注入换实现」;`-Full` e2e 为门禁。

## 考虑过的方案

- **继续自维护 CsWin32 钩子**(ADR-0051 的试点产物):否决。管道自持成本(`HOOKPROC` 保活、消息分支、结构体解引用、声明清单条目)不低,而真正值钱的语义——抑制决策、回放窗口、看门狗——换库后仍由自研侧持有:换掉的是管道,不是语义。
- **H.Hooks 高层钩子库**:维持 ADR-0051 的否决(不提供健康检查重注册、点击回放与「只忽略自己一次注入」的粒度)。
- **SharpHook `EventLoopGlobalHook` / `TaskPoolGlobalHook`**:否决。抑制必须与钩子同线程同步设置,这两者会忽略抑制——而抑制是轮盘交互的地基。
- **SharpHook `UioHookProvider` 低级面自起消息循环**:否决。等于把管道收回自己手里,收益归零。
- **给自身注入打戳记**(社区 Stroke 的 `dwExtraInfo = 0x7F` 做法):不可行。捕获侧读不到 `dwExtraInfo`,只能退回「回放窗口 + `IsEventSimulated`」。
- **raw input**(微软对该场景的替代建议):否决。raw input 只能异步监视、不能抑制,与「吞掉触发键」的轮盘交互语义冲突。
- **引入键盘捕获**(对齐上游的键盘触发键、独占热键录制、ESC 取消、切层键):否决,键盘侧维持不在范围内。技术上可行却代价失衡:捕获必须与鼠标共用**同一个** `IGlobalHook` 实例(库的硬约束,见「后果」),而两库键码**不互通**——`KeyboardEventData.KeyCode` 是 libuiohook 编号(实测 `VcEscape=1`、`VcF12=13`、`VcLeftControl=146`),`RawCode` 才是 Windows VK(实测 `27`/`123`/`162`),但测试替身 `TestGlobalHook` 喂出的 `RawCode` 恒为 `1`——**「键身份」在捕获侧没有既对应测试又能对应生产的字段**,要么自建 VK 映射表,要么放弃替身覆盖。叠加抑制面扩大的风险(独占录制会拦下 `Win+D`/`Alt+Tab`,任一退出路径失效即让用户失去键盘),收益不抵成本。将来若重启该方向,先解决键身份字段与替身覆盖。
- **把输入栈抽进 `StarPie.Sdk` 或新建工程**:本次不做。插件与设置页没有输入消费方,而 SDK 面是 additive-only 的兼容负担;等出现第一个真实消费方再评估。

## 后果

- 新增依赖 `SharpHook`(版本在 CPM 锁定):`StarPie.Ui` 引生产包,`StarPie.Tests` 引 `SharpHook.Testing` 以 `TestGlobalHook` 作钩子替身(仍在 ADR-0050 域内:不碰 STA、真实窗口与真实 ALC);`StarPie.Sdk`/`StarPie.Sdk.Wpf`/`StarPie.Host`/插件工程不引入。
- 分发面新增 LGPL-3.0 的 libuiohook 原生库(`runtimes/win-*/native/uiohook.dll`,动态加载;单文件发布经自解压落地):随发布物附 SharpHook 与 libuiohook 的许可声明,动态加载形态满足可替换要求。
- 钩子线程由 UI 线程改为专用后台线程:`IWheelFactory`/`IWheelViewModel` 契约注释里「调用方可能位于钩子线程」由假设变成事实;轮盘侧的阻塞式 `Dispatcher.Invoke` 改为非阻塞派发,顺序由 Dispatcher 队列 FIFO 保证。
- 回放窗口内若有外部注入(含 e2e 注入)恰好到达,会被误吞——已知残差风险,由 e2e 覆盖正常路径、并在代码注释点名。
- **全进程只允许一个 `IGlobalHook` 实例**(库的硬约束:文档明写多个并发钩子会损坏 libuiohook 的内部全局状态)。故「鼠标 + 键盘」不是两个钩子实例,若将来扩展只能改为**同一实例**以 `GlobalHookType.All` 运行、在处理器内分流——这也会把两类事件压进同一条钩子线程与同一个看门狗,键身份字段问题(见「考虑过的方案」)随之成为前置。当前实现是单实例 `GlobalHookType.Mouse`,天然合规。
- 钩子专属的 CsWin32 声明(`SetWindowsHookEx`/`UnhookWindowsHookEx`/`CallNextHookEx`/`MSLLHOOKSTRUCT`/`WM_RBUTTONDOWN`/`WM_RBUTTONUP`/`WM_MOUSEMOVE`/`mouse_event`)随实现回收;`GetCursorPos` 因看门狗保留。ADR-0051 的其余条款(唯一声明面、白名单、防回流扫描)不变。
- 落地顺序:ADR 与术语 → 输入栈骨架(捕获适配 / 抑制决策 / 回放窗口 / 看门狗) → xUnit(`TestGlobalHook`) → e2e(`-Full`) → 架构叶子(`layering.md`/`assemblies.md` 的 M1 模块件清单)同步。
