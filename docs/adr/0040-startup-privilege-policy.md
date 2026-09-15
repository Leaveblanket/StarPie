# 启动权限策略：坚持 asInvoker 与按需提权，强制管理员与偏好持久化记为条件备选

> Status: Active
>
> 本文确立进程启动权限级别与提权入口的长期策略。被否决形态不整体关闭，而是记为带触发条件的备选。
> 契约正典：`docs/architecture/shell.md`（高级设置面）、`docs/architecture/host.md`（单实例闸门）；
> 术语见 `CONTEXT.md`（提权 / 高权限窗口）。

## 动机

1. **提权只服务一个次要场景**：不改权限时，高权限窗口（任务管理器、注册表编辑器等）内唤不起手势——
   UIPI 按**完整性级别**划界（不是按 token 特权），低层鼠标钩子收不到指向更高完整性级别进程的输入，
   注入到该类窗口的合成输入也被拦下。核心手势价值在普通窗口内完整成立，不受影响。
2. **强制提权的代价对每个用户、每次启动都生效**：每次 UAC（拒绝即不启动）、开机自启从静默变为登录弹窗、
   子进程继承管理员（资源管理器拖拽失效、映射盘不可见、Chromium 系拒绝以管理员运行）、
   进程内插件随宿主升到机器级爆炸半径、本地 e2e 与开发须常驻管理员终端。
3. **代价不对称决定取舍**：按需提权能覆盖强制提权的全部收益（用户点一次托盘即得管理员），
   反之不成立——强制提权无法把"静默启动、以普通权限运行"还给不想提权的用户；可得体验集合严格更大。
4. **零碎特权不存在**：Windows 没有"只授予输入桥接这一项能力"的粒度——完整性级别只有 low/medium/high/system
   四档且不可部分提升，`SeXxxPrivilege` 与输入无关。"按需提权"在机制上也不成立：能力必须在被需要的
   那一刻之前就持有，未越界的进程连"用户在高权限窗口上按下拖动"这一事实都收不到。

## 取证

- **当前为 asInvoker**：全仓无 `app.manifest`、无 `requestedExecutionLevel`（唯一 `.manifest` 命中是
  `.vs` 下的测试日志），即 .NET 默认。提权是运行期动作：`ShellHost.ElevateAndRestart`
  （`Process.Start` + `Verb = "runas"` + 退出自身，失败或取消则气泡提示且不退出），入口为托盘项与
  高级页 UAC 卡片（`ShowUacWarning => !IsAdministrator`），页面只经 SDK 契约
  `AppHostDelegates.ElevateAndRestart` 转发。
- **提权收益的成文口径只有一处**：`ElevateDesc`「以管理员身份重启，可在任务管理器、系统设置等
  高权限窗口中正常唤起手势」。代码中除提权重启外没有任何需要管理员的能力。
- **自启只有 HKCU Run**（`AutostartRegistry`）：改强制提权即须改建任务计划程序（最高权限运行）并经一次
  提权引导创建，而分发形态是绿色单文件 exe、无安装器。
- **单实例握手跨完整性级别不可靠**：互斥体 `Global\StarPie_SingleInstance_Mutex_…` +
  `FindWindow`（按标题） + `RegisterWindowMessage` + `SendMessage`。UIPI 默认拦截**值大于 WM_USER**
  的窗口消息，而注册消息必大于 WM_USER，故提权实例持有托盘窗口时非提权实例的置前请求送不到；
  互斥体方向则因更高完整性级别对象带"不向上写"强制策略，打开请求含写访问即被拒。
- **动作层子进程继承**：`ActionRouting.BuildLaunchStartInfo` 用 `UseShellExecute = true` 直接拉目标
  （仅文件夹动作走 `explorer.exe`），提权态下用户从轮盘启动的每个程序都带管理员令牌。
- **插件随宿主提权**：插件为进程内加载、宿主不承诺沙箱与配额（[ADR-0029](0029-plugin-trust-model.md)），
  宿主提权即把第三方插件的爆炸半径从用户级抬到机器级。
- **e2e 不提权**：`tests/conftest.py` 直接 `subprocess.Popen` 被测 exe 并以 UIA 连接，无任何提权路径。
- **UIAccess 是唯一"不拿管理员令牌也能跨 UIPI"的机制**，但门槛与边界都已文档化：`uiAccess="true"`
  的定义即"绕过用户界面保护级别、向更高权限窗口驱动输入"；须 Authenticode 签名
  （无论"仅提升安全位置 UIAccess 应用"策略开关如何都强制校验）与安全位置安装
  （`%ProgramFiles%`、`%ProgramFiles(x86)%`、`%SystemRoot%\system32`）；文档明示"不应用于非助残技术应用"；
  文档同时写明"UIAccess 不足以跨越完整性级别边界"：带 UIAccess 但由**非管理员账号**启动的进程为
  "medium+" 级别、仍访问不了高完整性级别的 UI。
- **同类产品的交付形态**：PowerToys 面对同一道题（FancyZones 与键盘重映射在高权限窗口失效）给出的答案是
  "检测到提权进程 → 提示用户重启为管理员 / `Always run as administrator` 开关"，并明确"除非绝对必要
  不建议总是以管理员运行"；其子进程继承问题（#43749 / #32206）长期未修，UIAccess 提案（#15241，
  提出者已实测可行）自 2022 年悬置。VS Code 官方立场是不要提权运行（推荐 user setup；提权时自动更新被禁用）。
  Everything 用"提权服务 + 非提权客户端"拆分，使客户端可保持标准用户。
- **降权启动有官方路径**：微软官方示例 Execute In Explorer 的文档原话即"从提权进程启动非提权进程时有用"，
  实现为 `ShellWindows` + `IShellDispatch2::ShellExecute`（**带 `Args` 与 `Directory` 参数**，故"explorer
  中转必丢参数"只成立于裸命令行写法）；Raymond Chen 明确反对自制降权令牌（"很难把令牌的提权性质正确地
  剥掉"）而推荐交 Explorer 代劳；微软自家 nodejstools 用同一技术。该法的主要缺陷（子进程挂 Explorer 名下、
  拿不到 PID、不能等待、无控制台继承）恰好不落在 StarPie 的动作语义上——动作层本就是点火即忘。

## Considered Options

- **manifest `requireAdministrator`（硬强制）** → 否。每次 UAC、拒绝即不启动；自启破裂；子进程与插件提权；
  标准用户账号每次启动都需管理员凭据。
- **manifest `highestAvailable`** → 否。对管理员账号等同硬强制，对标准用户账号静默降级到非提权，
  反而稳定制造混合权限状态。
- **启动自检 + `runas` 自重启（软强制，拒绝则降级运行）** → 否。交互效果同为每次弹窗，且混合完整性级别
  成为常态，放大跨级别问题的暴露面。
- **现状 + "始终以管理员身份启动"偏好持久化** → 否。开关一旦打开即把硬强制的全部代价搬回，而相对现状的
  唯一收益是省掉用户点一次托盘。
- **UIAccess（asInvoker + `uiAccess="true"`）** → 否。必须签名并安装到安全位置——签名与绿色分发并不冲突，
  冲突的是安全位置要求，即须把"拷贝即用"改成安装版；机制被文档定位为助残技术专用；非管理员账号仍填不满。
  PowerToys 具备全部前置条件（有签名、有安装器、装在 Program Files）仍长期未采纳。
- **提权辅助进程（双进程）** → 否。钩子仍必须在交互会话内的提权进程中运行，故既不省 UAC 也不省自启改造，
  只降低爆炸半径；且 [ADR-0039](0039-resident-shell-and-transient-settings-console.md) 已否决双进程形态。
- **asInvoker + 按需提权（现状），并补齐跨完整性级别握手** → 采纳。

## Decision

1. **不写 `requestedExecutionLevel`**，保持 .NET 默认 asInvoker 与绿色单文件分发形态；提权维持运行期用户动作
   （托盘项 + 高级页卡片 + `AppHostDelegates.ElevateAndRestart` 转发），不引入清单声明。
2. **不提供"始终以管理员身份启动"的偏好持久化**，也不改写自启项为提权形态。
3. **被否决形态记为条件备选，带明确触发条件**：若"覆盖高权限窗口"上升为产品核心承诺，先重估 **UIAccess**
   （它严格优于强制提权：不拿管理员令牌、不继承提权子进程、插件不提权），且仅在发行形态已具备代码签名与
   安全位置安装时成立；强制提权只在该前提之外才进入讨论。
4. **约束（排除强制路线的依据之一）**：静默开机自启是既有承诺，不与"每次启动弹 UAC"共存。
5. **提权态与未提权态的边界行为归位**：
   - 单实例恢复消息的**接收端放行**本进程自有的注册消息（`ChangeWindowMessageFilterEx`，按窗口、仅在提权态执行），
     使非提权实例的置前请求能送进提权实例；
   - 互斥体**打开失败（`UnauthorizedAccessException`）按"已有实例"处理**并走恢复消息路径，
     不再退回新实例——退回会得到两个托盘图标与两条全局鼠标钩子；
   - 托盘提权入口**只在非提权态出现**，与高级页提权卡片同口径；
   - 提权态探测收敛为共享内核单一份实现（`ProcessElevation`），壳层与高级页同源。
6. **本次不改变的两个已知影响**（记录而非修复）：提权态下"启动程序"动作的子进程继承管理员；
   提权态下进程内插件随宿主获得机器级权限。

## Consequences

- 高权限窗口内唤不起手势仍是默认行为，只能由用户主动提权获得；本 ADR 不引入任何运行时告知机制。
- 强制路线的四笔代价（UAC 常税、自启破裂、子进程继承、插件提权）不落地；提权失败/取消的既有语义
  （气泡提示且不退出）保持不变。
- **已定但未落地的行为**（各自独立为后续工作项，不阻塞本 ADR）：
  - 未提权态检测到前台窗口属于更高完整性级别时，报**一次**托盘气泡（每个安装一次，需 `config.json`
    新增标记字段，缺字段按未提示处理）；
  - 提权态下在插件管理页显示一行警示（当前插件以管理员身份运行）；
  - ~~提权态下"启动程序"动作改走 Explorer 中介降权启动~~ 已落地（#163：
    `ExplorerShellLaunch` + `ActionRouting.ResolveLaunchMode` + 动作项显式的"以管理员身份启动"选项；
    见 [shell.md](../architecture/shell.md)、[gestures.md](../architecture/gestures.md)）。
- **不可自动化验证的边界**：跨完整性级别行为（消息放行与互斥体分支）无法被不提权的 xUnit/e2e 环境复现，
  其验收只能由一次真实的提权实例 + 非提权双击手动完成；本 ADR 不为此设自动判据。
- **叶子回填**：`host.md` 单实例段补跨级别行为与失败分类；`shell.md` 高级设置面段补托盘提权入口的可见性口径；
  `CONTEXT.md` 增「提权」「高权限窗口」两词。
- **边界守护**：新增 public 内核类型 `ProcessElevation` 已登记进 `HostBoundaryTests` 的内核清单
  （该表为"导出面 = 内核清单"的守护，新增 public 类型须同步）；后续落地的
  `ExplorerShellLaunch` 与 `LaunchMode` 同此登记。
