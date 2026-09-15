# 模块：壳层与系统集成

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及托盘、开机自启、内存整理、主窗口壳层行为
> 与高级设置面时读本篇；界面主题见 [interface-theme.md](interface-theme.md)。

## 职责

托盘与气泡、开机自启、内存整理、主窗口壳层行为、高级设置面；子职责目录与护栏（D2）见
[modules.md](modules.md) §5。

## 组成文件

M5 物理落位（P1.10/#119 归并：自启注册表与内存整理入宿主内核，托盘与高级设置面入 Ui 集）：

- `StarPie.Ui/Services/Shell/TrayIconManager.cs`（含 `TrayMenuEntry`；托盘类与菜单行为随归并入 Ui，
  由同集 `ShellHost.Run` 装配实例——见下方关键流程 1）。
- `StarPie.Host/Kernel/ShellIntegration/AutostartRegistry.cs`（R1；HKCU Run 与提权自启计划任务两种形态，
  两者互斥落位，另含提权自启任务的按需触发与「立即提权」入口的可见性决策——见关键流程 3；
  `[SupportedOSPlatform("windows")]`、public 装配面）、
  `StarPie.Host/Kernel/ShellIntegration/MemoryOptimizer.cs`（R3；零 WPF、纯托管）、
  `StarPie.Host/Kernel/ShellIntegration/TrayStateSignal.cs`（托盘状态信号纯决策：输入是**控制台开/关**）、
  `StarPie.Host/Kernel/ShellIntegration/ShellExitSequence.cs`（托盘退出固定顺序纯决策）、
  `StarPie.Host/Kernel/ShellIntegration/ProcessElevation.cs`（当前进程是否以管理员身份运行的探测）——
  命名空间均为 `StarPie.Kernel.ShellIntegration`（单实例闸门的判定与握手信道同驻此目录，
  见 [host.md](host.md) §单实例闸门）。
- `StarPie.Ui/ViewModels/Pages/GeneralSettingsViewModel.cs` 与
  `StarPie.Ui/Views/Pages/AdvancedSettingsPage.xaml(.cs)`
  （D6：M5 设置面；页面 XAML 根直承 `UserControl`——共享页面基类 `SettingsPageBase` 已删除）。
- `StarPie.Ui/Modules/ShellContributor.cs` + `ShellPageTemplates.xaml`（M5 贡献者与
  页面模板字典，自报导航项/模板并登记页面 VM 的 DI 注册；见 [navigation.md](navigation.md)）。
- SDK 同时登记宿主回调契约 `StarPie.Sdk/Services/AppHostDelegates.cs`（托盘气泡/退出自 M3 起由壳层直接
  呈现与执行，属性保留为契约面，P1.3/#112 收口；见 [host.md](host.md)）。

M4 的主题服务（`IThemeService` 实现 `ThemeService`）在 Ui 集 `StarPie.Ui/Services/Shell/`
（命名空间 `StarPie.Services.Shell`；主题引擎 `ThemeEngine` 在宿主内核，见
[interface-theme.md](interface-theme.md)），不在 M5；各业务目录不跨模块登记。

- `ViewModels/Navigation/ShellViewModel.cs`（D3：Host 壳窗口壳层 VM——`WindowTitle`/`Save()`；
  归 H1 留 Host，不随 M5，见 [assemblies.md](assemblies.md) §4。进程退出态归**壳层**
  （`ShellHost`），不寄居在本 VM：退出是壳层编排，设置台只是被关闭）。
- `Views/Navigation/MainView.xaml(.cs)`（R4/ADR-0016：Host 壳窗口（H1）；`MainView.xaml`
  为纯壳——页面 DataTemplate 在 App 级模块页面模板字典（M1/M5 随归并入 `StarPie.Ui/Modules/`，
  Host 外观聚合页同在 `StarPie.Ui/Modules/`，见 [navigation.md](navigation.md)），
  分区 DataContext 接线见下关键流程 4）。

## 关键流程

1. **托盘**：`TrayIconManager`（驻 `StarPie.Ui/Services/Shell/`，`ShellHost.Run` 创建）持 tooltip
   （暂停态实时文案）、双击直达、
   右键菜单（`ShellHost.BuildTrayMenuEntries` 每次打开重建，`ILocalizationService` 即时取词）、
   气泡通知（`ShowBalloonTip`）、`Dispose`；tooltip 在语言切换时由壳层
   `ShellHost.RefreshTrayTooltip` 按暂停态刷新（宿主编排见 [host.md](host.md)）。托盘菜单深色配色不直读 M4；Shell
   不反向引用 Host/M4，`ShellHost` 装配时注入 `Func<bool>` 深色探针
   （`ThemeService.IsWindowsInDarkTheme`；该服务驻 `StarPie.Ui`，Host 经 `IThemeService` 契约消费）。
2. **内存（分层常驻）**：`MemoryOptimizer.CollectGarbage()`（驻
   `StarPie.Host/Kernel/ShellIntegration/`）是纯托管 GC 收敛——两轮全量压缩 + finalizer
   （保留 2 秒节流与防重入）；工作集裁剪（EmptyWorkingSet/SetProcessWorkingSetSize P/Invoke）
   已整体删除，设置页手动"内存整理"入口与四语言文案已移除（不留"留作诊断"死路径，
   需要时从 git 历史恢复）。自动触发点经 `TrayStateSignal` 有序决策编排（输入是设置台开/关）：App 启动兜底
   force（轮盘预热之后，#150）与进托盘（非后台）——进托盘固定顺序
   `FlushPendingSave → 导航视图出账（#152）→ 图标缓存出账（#153）→ 发 MinimizedToTrayMessage →
   CollectGarbage 后台执行`，恢复按最后导航槽位重放导航后发 `RestoredFromTrayMessage`；
   后台静默形态（e2e）出账动作禁用、消息照发。GC 堆预算由
   `StarPie.Ui/runtimeconfig.template.json` 的 `System.GC.HeapHardLimit`（256 MiB）约束，
   逼近上限时 GC 自行提升回收激进度；声明面与编译产物由 `MemoryResidencyTests` 双断言锁定，
   运行时生效值以 `GC.GetConfigurationVariables` 为口径经 `Debug.WriteLine` 记载——
   **只在 Debug 构建/附加调试器时可见**（`Debug` 日志调用在 Release 构建被编译期整体移除，
   实测该字面量在 Release 产物中不存在），正式版核对以产物 `StarPie.runtimeconfig.json`
   的 `configProperties` 为准。
   **收益口径**：这条路线换来的是可达性与释放时机的明确（会话作用域、出账、瞬态租户），不是字节数——
   关窗出账连同两轮压缩回收实测只释放个位数 MB 工作集，重开的会话再关一次近乎为零；进程内存主体是
   原生私有分配与 WPF 渲染栈的首启常驻（窗口类注册、渲染线程与 D3D 资源、字体/纹理缓存），
   不随出账归还、只有进程退出才释放。故对外报内存用**专用工作集**（任务管理器默认「内存」列），
   不用含共享代码页的总工作集；设置台「关闭即销毁」同样不拿省内存当理由——若将来以首次开台变冷或
   页内半输入状态丢失为由讨论回退，内存不构成理由。
3. **自启**：两种形态的读写都收敛于 `AutostartRegistry` 静态工具（与 VM 同驻
   `StarPie.Host/Kernel/ShellIntegration/`），经同集贡献者
   `ShellContributor.RegisterServices` 委托注入
   `GeneralSettingsViewModel`（`isAutoStartEnabled`/`applyAutoStart`/`isAdminAutoStartEnabled`），
   不进 VM/View。形态一 HKCU Run（路线 A：普通权限自启）；形态二 Windows 任务计划程序任务
   （路线 B：`/rl highest /sc onlogon /delay 0000:00`，提权由服务在触发时完成、**不弹 UAC**，见
   [ADR-0041](../adr/0041-admin-autostart-opt-in.md) 与 [ADR-0042](../adr/0042-privilege-routes-two-only.md)）。
   **两种形态互斥落位**：落位形态由纯决策 `AutostartRegistry.ResolvePlacement(enable, asAdmin)` 给出——
   提权形态下注册表 Run 键**缺位**（两条自启路径同在登录时触发，同时落位会让非提权实例与提权实例抢
   单实例闸门，非提权那个赢了就等于提权自启白开），普通形态下计划任务被删除；关总开关时两者一并删除。
   界面的总开关按"两种形态任一在运行"取值，提权形态的状态读自计划任务的实况（`schtasks /query` 退出码），
   `config.json` 的 `AutoStartAsAdmin` 只记录用户意图；落位失败（UAC 取消、账号无管理员凭据）
   由 VM 提示并把开关拨回实况，不静默。dev 实例的任务名带独立后缀，与配置目录同口径。
   **那颗任务也是即时提权的载体**（[ADR-0043](../adr/0043-elevated-instance-takeover.md)）：
   `AutostartRegistry.RunAdminTask` 以 `schtasks /run /tn <任务名>` 按需触发它，非提权进程即可静默得到
   一个 High 完整性级别、同一交互会话的实例，全程不弹 UAC（触发本身不需要提权，建/删任务才需要）；
   返回值只表示"任务被受理"，**不是就绪判据**——就绪只认"单实例互斥体已可取得"（见
   [host.md](host.md) §单实例闸门）。
4. **关窗即销毁、托盘驻留、重开重建**（[ADR-0039](../adr/0039-resident-shell-and-transient-settings-console.md)）：
   设置台是瞬态租户——关窗销毁窗口与 VM 树（`Window_Closing` 不再取消），托盘驻留由常驻壳层
   （`ShellHost` + 托盘消息窗口）承担，托盘直达/单实例恢复经 `ShellHost.ShowSettingsConsole` 重建。关窗收尾
   走 `Services/Shell/TransientWindowTeardown.cs` 的唯一实现（清动画 → 丢弃内容与 DataContext → `Close()`
   → 排空 Dispatcher → `Application.MainWindow` 回退常驻锚窗口），`SettingsConsole` 另解绑对话框 Owner。
   `App.xaml` 因此设 `ShutdownMode="OnExplicitShutdown"`（关窗不等于退出进程）。
   `MainView` 壳层 code-behind 只剩淡入`ShowAndActivate`、主题应用与深色探测（ADR-0009 白名单第 3/5 条）；
   壳层成员（`WindowTitle`/`Save()`）在 `ShellViewModel`（D3：Host 壳窗口 VM，H1，随设置台会话生灭），
   `MainView` 分区 DataContext——壳区（窗口标题/底部操作区）绑 `ShellViewModel`、导航区（侧栏/页面）绑
   `MainViewModel`（见 [navigation.md](navigation.md)）；`CloseButton_Click` 纯 UI 取消语义。
5. **高级设置面**：导入/导出与两个自启开关在贡献者接线（本模块静态行为）；**托盘气泡归壳层**
   ——气泡由壳层在进托盘时报出（贡献者只依赖 SDK，壳层回填实现，见 [host.md](host.md)）；
   页面绑定规范见 [layering.md](layering.md)（`AdvancedSettingsPage` 示例）。
   进程权限级别由 [ADR-0040](../adr/0040-startup-privilege-policy.md) 与
   [ADR-0042](../adr/0042-privilege-routes-two-only.md) 固定为**两条互斥路线**：普通权限启动
   （asInvoker，不写清单声明）与管理员权限静默启动（即提权自启，见下方关键流程 3）。
   **「立即提权」是路线 B 的即时触发形态**（[ADR-0043](../adr/0043-elevated-instance-takeover.md)），
   不是第三条路线：托盘菜单项与设置页按钮是同一件事，共用纯决策
   `AutostartRegistry.ResolveAdminRestartEntry(elevated, adminTaskExists)` 的口径——已是管理员权限运行时
   整块不出现（提权入口只在非提权态出现）；提权自启关闭、任务不存在时不可点并写明原因（需先开启该开关），
   不给一个点了没反应的按钮。入口挂在「以管理员身份开机自启」卡片内，不单起一张卡片；
   点击触发的就是关键流程 3 那颗任务（复用路线 B，不给应用内入口开专用通道），
   页面 VM 经 `AppHostDelegates.RestartElevated` 转发请求、不反向依赖宿主类。
   接管没成时由既有实例经气泡通道告知「提权未生效」（见 [host.md](host.md) §单实例闸门）。

## 扩展点

- 新壳层行为（如开机自启策略变化）：改 M5 内部（`StarPie.Ui` 的 `TrayIconManager`、
  `StarPie.Host/Kernel/ShellIntegration/` 的 `AutostartRegistry` 等）并保持委托注入边界；
  新增 M5 设置页只动 Ui 内部（贡献者 + 模板字典 +
   VM 注册，见 [navigation.md](navigation.md)），不碰 Host。
- 新托盘菜单项：在 `ShellHost.BuildTrayMenuEntries` 登记（宿主接线见 [host.md](host.md)）。
- 新 OS 集成功能按 D2 护栏先对号入座（[modules.md](modules.md) §5 D2）。

## 参见 ADR

[0003](../adr/0003-application-host-restructure.md)（宿主重构）、
[0009](../adr/0009-view-code-behind-whitelist.md)（壳层 code-behind 白名单）、
[0015](../adr/0015-module-map-and-ownership.md)（12 模块地图：M5 与 R1/R3/R4/D2/D3）、
[0041](../adr/0041-admin-autostart-opt-in.md)（提权自启）、
[0042](../adr/0042-privilege-routes-two-only.md)（两条权限路线）、
[0043](../adr/0043-elevated-instance-takeover.md)（即时提权与接管协议）。
