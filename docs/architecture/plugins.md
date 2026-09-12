# 插件体系（目标态）

> **状态**：P1（三集物理形态 + 统一注册管线 + 静态加载）已落地；P2 首层（§3 的清单校验、发现顺序与
> 同 id 冲突、宿主状态字段、启动报告、开发者模式开关与准入四态）、§5/§6/§8 的 headless 运行时
> （collectible ALC 装载、能力注册与调用守卫、安全点卸载与回收判定）、§2 的首个随包 headless 插件
> 及其停用降级、隔离落盘与「下次启动不自动重试」亦已落地，其条款即 as-built；UI 托管（§7）、
> 最小插件管理面与诊断报告（§10 的列表/状态/启停/重试/诊断入口，含可定位残留清单）亦已落地；
> UI 托管（§7）、管理面剩余动作（重载/更新/彻底移除）与生态化（§11）为目标态规范，未落地条款在落地前 as-built 以
> [assemblies.md](assemblies.md) 与
> [modules.md](modules.md) 为准。
> **决策依据**：[ADR-0027](../adr/0027-plugin-architecture-and-host-sdk-ui-split.md)（三集形态、ALC 真卸载、SDK 单一引用面）、[ADR-0028](../adr/0028-plugin-ui-hosting-and-host-managed-lifecycle.md)（插件 UI 宿主化与宿主托管生命周期）、[ADR-0030](../adr/0030-ui-plugin-unload-semantics-downgrade.md)（UI 插件不承诺 ALC 真卸载，卸载语义降级为托管清理 + 隔离 + 重启生效）、[ADR-0034](../adr/0034-headless-unload-handover-and-hard-reclaim.md)（headless 卸载三条款）、[ADR-0035](../adr/0035-wpf-host-plugin-assembly-reclaim-downgrade.md)（回收判定按宿主环境分档：WPF 宿主降级为诊断）。
> **阅读方式**：本文只讲插件子系统的契约、生命周期、文件架构与迁移；宿主内核子域职责在 P1 后回填 `modules.md`。

## 术语（架构词正典在本文）

**宿主内核 (Host Kernel)**：`StarPie.Host` 承载的全部不可卸载运行时与服务。宿主内核不是插件。

**插件 (Plugin)**：可在运行期装载/卸载、只经 `StarPie.Sdk` 与宿主交互的扩展单元；可贡献 headless 能力与宿主托管的 UI。

**能力 (Capability)**：宿主声明、插件实现的无 UI 扩展点，用 id + ABI 号标识（如 `program-source@1`）。

**插件 UI 资产 (Plugin UI Asset)**：插件贡献的 WPF 对象——视图、窗口、资源字典、DataTemplate、命令/菜单、定时器/动画、事件订阅。

**资产登记表 (Asset Registry)**：`StarPie.Ui/PluginHosting` 中按 plugin id 记录全部 UI 资产的宿主侧清单；是卸载清理与验证的唯一依据。

**受支持特性白名单 (Certified Feature Set)**：P0 打样认证可可靠卸载的 WPF 特性集合；白名单外的特性不受支持。

**安全点 (Safe Point)**：无在途插件调用、配置已落盘、宿主侧引用可清的卸载时机。

**危险区 (Danger Zone)**：卸载链上"插件代码不得再运行"的一侧——服务作用域释放起，经 `ALC.Unload()`；进入后没有回头路。在途调用未归零一律中止于危险区之前（只摘能力条目，不释放作用域、不卸载 ALC）。

**泄漏隔离 (Leak Quarantine)**：清理或卸载验证失败后插件被停用、不再调用、提示重启的状态。

**宿主门面 (Host Facade)**：`IPluginContext`（headless 服务）与 `IPluginUiContext`（UI 资产注册）；插件可触达的全部宿主能力集合。

**宿主服务作用域 (Plugin Service Scope)**：每插件一个的子容器与句柄账本（`PluginServiceScope`）；订阅、回调、动作句柄全部登记于此，卸载时按 plugin id 整体释放。

**准入模式 (Admission Mode)**：插件被允许装载的依据——内置 / 审核清单 / 开发者模式；见 §11 与 ADR-0029。

**开发者模式 (Developer Mode)**：默认关闭的显式开关，开启后允许装载未命中审核清单的插件，并向用户展示进程内全信任风险披露。

**宿主状态 (Plugin State)**：宿主权威的插件运行状态（启用 / 停用 / 已装版本 / 路径 / 准入来源 / 隔离），存 `plugin-state.json`；与插件自己的配置 `plugins.<id>` 分离。

## 1. 边界与原则

1. **插件只引用 `StarPie.Sdk` 与 `StarPie.Sdk.Wpf`**；`StarPie.Host`/`StarPie.Ui` 的内部类型不在插件引用面内。
2. **宿主独占生命周期**：插件可创建 XAML/Window/ResourceDictionary/DataTemplate，但必须经宿主契约注册；宿主登记、跟踪、移除、验证，插件不得自行 merge 全局资源或长期持有 WPF 全局对象。
3. **宿主内核零 WPF**：`StarPie.Host` 不引用 WPF；一切 WPF 类型与清理在主进程 Ui 层 `PluginHosting` 执行，Host 经 `IPluginUiCoordinator` 端口协调。
4. **插件缺席是可运行态**：每个扩展点必须定义降级行为（无插件页时导航正常、无程序来源时选择器只剩内置来源）。
5. **跨 ALC 只共享 SDK 与框架程序集**：`StarPie.Sdk`/`StarPie.Sdk.Wpf` 一律从默认 ALC 解析，保证接口与 WPF 类型身份唯一。
6. **一切装卸发生在安全点**：更新 = 安全点卸载 + 装载新版本；不做无约束即时重载。
7. **白名单外即不支持**：不受支持的 WPF 特性不进入验收；发现泄漏按隔离流程处理，不降低验证标准。
8. **两组硬约束是落地判据**：`StarPie.Sdk.Wpf` 见 §5.1，HostServices 见 §6.1。违反不是"设计欠佳"，而是拒绝装载、拒绝合并或判定 `Quarantined`。

## 2. 目标物理形态（三集 + SDK.Wpf + 一等插件）

```text
StarPie/
├── StarPie.slnx                          # 解决方案：登记三集 + 一等插件 + 测试，一条命令 build/测全套
├── Directory.Build.props                 # 共享构建属性（TFM/可空性/分析器级别）：四集与插件工程不各写一遍，避免漂移
├── Directory.Packages.props              # 中央包管理：让「SDK 零第三方包」「Host 零 WPF」两条约束可由构建机械拦截
├── StarPie.Sdk/                          # net10.0；零 WPF / 零第三方包；headless 唯一引用面（ADR-0027 决策 1）
│   ├── Abstractions/                     # IPlugin、IPluginContext、IPluginCapability、生命周期：插件眼里「宿主长什么样」的全部
│   ├── Capabilities/                     # 能力契约（首期 IProgramSource）：按能力分文件，破坏性变更=加文件而非改文件，additive-only 可机械审
│   ├── Models/                           # 稳定 DTO：ProgramEntry、IconRef、ActionDescriptor…；跨 ALC 传递的类型必须来自默认 ALC 的 SDK
│   ├── Settings/                         # 声明式设置 schema 模型：宿主渲染通用表单的前提（插件不自绘设置）
│   ├── Events/                           # 宿主事件契约（订阅返回 IDisposable）：卸载即断的实现基础
│   ├── Manifest/                         # plugin.json 纯数据模型（校验逻辑在 Host，SDK 不做 IO）
│   └── Compatibility/                    # SDK 版本 / 宿主最低版本 / 能力 ABI 常量：判定集中一处
├── StarPie.Sdk.Wpf/                      # WPF 类型契约；默认 ALC 统一加载；additive-only（ADR-0028 + §5.1 八条）
│   ├── Abstractions/IPluginUiModule.cs, IPluginUiContext.cs    # 插件唯一合法的 UI 注册入口（funnel）
│   ├── Descriptors/                      # PluginPageDescriptor/PluginWindowDescriptor/PluginMenuItemDescriptor…
│   │                                     # 只带纯数据 + 工厂 + 类型名 + pack URI，禁内嵌已构造实例（§5.1 约束 5）
│   ├── Resources/                        # 资源字典注册描述（pack URI / 工厂）
│   └── Compatibility/                    # Ui SDK ABI 常量（与 Sdk 分政策编号）
├── StarPie.Host/                         # 零 WPF 引用、零 XAML；可 headless 单测（ADR-0027 决策 1）
│   ├── Kernel/{Configuration,Localization,Messaging,Navigation,ShellIntegration}
│   │                                     # 配置/文案/消息/页注册表/注册表与进程级壳集成；NavigationCatalog 由封闭槽位改为可增删页注册
│   ├── Actions/  Gestures/  Wheel/  Icons/  Themes/   # 纯模型与逻辑（动作路由/手势内核/配色解析/资产目录/调色板计算）；几何构造与 WPF 亲和件归 Ui
│   ├── Ports/                            # Host→Ui 端口：IUiDispatcher/IThemeApplier/IWheelPresenter/IIconImageFactory/IPluginUiCoordinator
│   │                                     # 存在理由：零 WPF 的 Host 要「做 WPF 事」只能回抛接口，这是两集间唯一的反向缝（9 个污染点收口）
│   ├── HostServices/                     # 插件可见宿主服务实现：IPluginLog/IPluginConfig/IPluginEvents/…；每插件一个 PluginServiceScope（§6.1）
│   └── PluginRuntime/{Discovery,Manifest,Admission,State,Hosting,Loading,Unloading,Lifecycle,Registry,Config,Isolation,Diagnostics}
│                                         # 发现/清单校验/准入判定/宿主状态/启用装载与停用再启用/collectible ALC/安全点卸载/状态机/能力表/配置命名空间/隔离决策/诊断报告
├── StarPie.Ui/                           # WinExe，AssemblyName=StarPie；唯一含 XAML（ADR-0027 决策 1）
│   ├── App.xaml(.cs)  AppHost/  Composition/   # 应用资源树、启动退出编排、组合根（内置与插件贡献者共用一条注册管线）
│   ├── Adapters/                         # 实现 Host/Ports 的 WPF 适配器：零 WPF 的 Host 只能吃接口
│   ├── ViewModels/  Views/  Controls/  Styles/  Themes/   # 全部 VM/View/对话框/轮盘渲染/主题字典/共享 UI 基建
│   └── PluginHosting/                    # 插件 UI 资产生命周期（宿主托管，ADR-0028 决策 3/4）
│       ├── PluginUiAssetRegistry.cs      # plugin id → 资产清单；卸载枚举与清零断言的唯一依据
│       ├── PluginUiHost.cs               # IPluginUiCoordinator + IPluginUiContext 实现：Host 端口与插件 funnel 在此对接
│       ├── Extensions/                   # 固定扩展点：导航页/设置区/托盘菜单/窗口/内容容器（U1）
│       ├── Resources/PluginResourceRoot.cs    # 每插件资源根字典（一次 merge、一次摘除），DataTemplate/Style 随容器摘净
│       ├── Windows/PluginWindowRegistry.cs    # 窗口由宿主创建、关闭并等待 Closed
│       ├── Views/PluginViewHost.cs       # 视图进宿主容器并记账（容器 ↔ 视图 ↔ plugin id）
│       ├── Commands/PluginCommandRegistry.cs  # 命令与 InputBinding 的注册/注销
│       ├── Menus/PluginMenuRegistry.cs   # 托盘/页内菜单项注册与摘除
│       ├── Timers/PluginTimerRegistry.cs # 宿主签发的 DispatcherTimer/动画，可整体停止
│       ├── Cleanup/PluginUiCleanup.cs    # UI 线程上的有序清理（§8 步骤 4）
│       └── Verification/PluginUiLeakVerifier.cs   # 泄漏扫描 + WeakReference 判定；生产诊断与测试共用
├── plugins/
│   ├── src/StarPie.Plugin.Programs/      # 首个 headless 插件（只引 StarPie.Sdk；深扫程序来源）；构建时随包打包进产物 plugins/<id>/；默认启用、可停用（Q6 dogfooding）
│   └── src/StarPie.Plugin.SampleUi/      # 首个 UI 示例插件（P3，引 Sdk + Sdk.Wpf）
├── StarPie.Tests/                        # 平铺：Plugin*Tests.cs / AbiTests.cs / BoundaryTests.cs + STA harness
└── tests/                                # 维持现状（pywinauto），P1 不搬迁；Q6 起程序选择器用例分「启用/停用」两态
```

## 3. 插件包与清单

```text
%LOCALAPPDATA%\StarPie\plugins\<plugin-id>\
├── plugin.json
├── StarPie.Plugin.Example.dll
├── <私有依赖>.dll
├── settings.schema.json        # 可选
└── strings\*.json              # 可选
```

```json
{
  "schemaVersion": 1,
  "id": "com.example.program-source",
  "name": "Example Program Source",
  "version": "1.0.0",
  "sdk": "1.0",
  "ui": { "sdk": "1.0", "entryType": "Example.UiModule" },
  "entryAssembly": "StarPie.Plugin.Example.dll",
  "entryType": "Example.Plugin",
  "priority": 0,
  "capabilities": [{ "id": "program-source", "abi": 1 }],
  "settingsSchema": "settings.schema.json"
}
```

- `id` 反向域名、发布后不可变；`version` SemVer。
- `ui` 段可选：声明插件含 UI，并给出 `IPluginUiModule` 入口类型；无 `ui` 段即为 headless 插件。
- 发现顺序：安装目录 `plugins/` 与用户目录；同 id 冲突禁用两者并提示处理。
- 清单校验在发现期收口（失败即 `Rejected` 并附原因）：`schemaVersion` 只接受 1；`id`/`name`/`version`/`sdk`/`entryAssembly`/`entryType` 必填；`id` 须等于包目录名；`sdk` 按 §11 的 ABI 政策判定；`entryAssembly`/`settingsSchema` 必须是包内裸文件名且文件存在于包内；`ui` 段只做纯格式校验（UI 侧 ABI 兼容判定归 Ui 侧插件托管层，见 §5.1 约束 7）。
- 宿主状态条目字段：启用/停用、已装版本、包路径、准入来源（四态）、隔离状态；每次启动扫描重算准入并刷新条目，用户的启用意图与隔离状态不被扫描覆盖。
- 启动扫描产物 `plugin-startup-report.json`：扫描目录、开发者模式开关、逐插件准入结果与四态计数——**准入四态在启动报告里直接可见**。
- 开发者模式开关存宿主状态（默认关闭）：开启须显式确认全信任风险披露（可读配置与插件数据 / 执行任意代码 / 使进程崩溃 / 卸载可能失败并被隔离），未确认不得置位。
- 内置认定以宿主内置 id 清单为准：随包第一方插件登记 id，安装目录位置本身不是信任依据；未登记的包按未审核处理。
- 包内**不得**出现 `StarPie.Sdk.dll`/`StarPie.Sdk.Wpf.dll`（共享契约不做随包分发）；命中即 `Rejected`（§5.1 约束 3）。
- `capabilities` 是数组：一个插件可声明多个能力，每条各自带 ABI；**卸载粒度仍是整个插件**，不能单摘一个能力（Q8）。
- `priority` 可选（默认 0）：只影响插件之间的能力列表顺序（数值小者靠前），内置条目永远最前；插件未声明 `priority` 时按 plugin id 稳定序。
- 首期**禁止插件间依赖**：插件只依赖 SDK 与框架程序集，包内私有依赖由该插件独占，不跨插件共享（Q8）。
- 宿主状态与插件配置分离：宿主状态存 `%LOCALAPPDATA%\StarPie\plugin-state.json`；插件经 `IPluginConfig` 只能读写 `config.json` 的 `plugins.<id>`（Q9b、ADR-0029）。

## 4. 生命周期

```text
Discovered → Validated → Loading → Starting → Active
                                     ↘ Quarantined（加载/启动/调用/卸载失败）
Active → Stopping → ReleasingUi → Unloading → Unloaded
                                 ↘ Quarantined（资产未清零 / 全局根有残留）
```

- `ReleasingUi`：仅 UI 插件进入；在 UI 线程执行资产清理并跑泄漏验证。
- 只有 `Active` 状态允许执行插件代码；`Stopping` 起拒绝新调用，在途调用排空。
- 重载 = 走完整卸载流程（资产清理 + 服务作用域释放 + `ALC.Unload()`）后按新包重新装载；不保留任何插件对象。UI 插件的旧程序集留在进程内无法回收（ADR-0030），因此 UI 插件的「新版本生效」以重启为界。
- **触发方式（Q7）**：只在启动扫描 + 管理面手动启停/更新；headless 插件更新 = 安全点卸载 + 装载新版本，UI 插件更新 = 隔离旧版本 + 下次启动装载新版本。不做目录监视自动重载——卸载时机必须在安全点，监视重载会把用户正在用的插件页抽掉。
- **开发者模式例外（Q7b）**：`--dev` 实例可开「监视 + 自动重载」开关（默认关闭，仅开发实例生效）；重载仍走完整安全点卸载与 UI 清理，不绕过验证。
- **隔离是持久状态（Q9）**：进入 `Quarantined` 即写 `plugin-state.json`；下次启动**不自动重试**装载，必须用户显式「重试」或「停用」。

## 5. 装载管线（collectible ALC）

```csharp
public interface IPlugin
{
    Task StartAsync(IPluginContext context, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

public interface IPluginUiModule
{
    void RegisterUi(IPluginUiContext context);
}
```

1. 清单校验 → 建 `PluginLoadContext(isCollectible: true)`。
2. `Load` 覆写：SDK/Sdk.Wpf/框架程序集返回 `null` 回退默认 ALC；其余经 `AssemblyDependencyResolver` 私有加载。
3. 载入口程序集，找 `IPlugin`（不缓存 `Type`），`StartAsync`。
4. UI 插件由宿主在 UI 线程调 `IPluginUiModule.RegisterUi`；**只做注册，不在此创建窗口/合并资源**。
5. 注册能力 → `Active`。
6. **BAML/pack URI 解析（已认证，宿主包装方式固定）**：插件程序集在 collectible ALC 内时，XAML 视图/窗口与资源字典的 BAML 与 pack URI 解析正常，**宿主不得用 `AssemblyLoadContext.EnterContextualReflection` 包装 XAML 解析**——不需要，且会让 `XamlReader` 路径解析不到自身的 BAML 资源（`组件不具有由 URI 识别的资源`）。
   - 宿主合并插件资源字典必须用 `new ResourceDictionary { Source = packUri }`；`Application.LoadComponent(绝对 pack URI)` 在 .NET Core 抛「无法使用绝对 URI」。
   - 松散 XAML（`XamlReader` 解析含 `assembly=` 类型引用的文本）不在支持面：插件 XAML 一律走编译期 BAML。

### 5.1 `StarPie.Sdk.Wpf` 硬约束（8 条，P2/P3 落地判据）

| # | 约束 | 判据与落点 | 违反时行为 |
|---|---|---|---|
| 1 | 由默认 ALC 统一加载 | `Load` 覆写对 `StarPie.Sdk`/`StarPie.Sdk.Wpf`/框架程序集返回 `null` 回退默认 ALC；与 SDK 同一政策 | 类型身份不一致 → 装载失败 |
| 2 | 共享契约，不是可携带依赖 | 插件的 UI 私有依赖走自己的 ALC，但 SDK 程序集永不随包分发 | 见约束 3 |
| 3 | 插件不得携带私有契约副本 | SDK 包以 `ExcludeAssets="runtime"` 引用；`PluginRuntime/Discovery` 扫描包目录，出现 `StarPie.Sdk.dll`/`StarPie.Sdk.Wpf.dll` 即拒 | `Rejected`（发现期拦下，不进 ALC） |
| 4 | 插件不直接交出 UI 实例 | `IPluginUiContext` 的签名只接受 Descriptor / `Uri` / `TimeSpan` / 值类型；不存在接收 `FrameworkElement`/`Window`/`ResourceDictionary`/`ICommand` 实例的重载 | 契约层不可表达；越权路径由泄漏扫描兜底 → `Quarantined` |
| 5 | 插件只提交 Descriptor | Descriptor 只承载纯数据 + 工厂委托 + 类型名 + pack URI，禁止内嵌已构造实例（含 VM）；`RegisterUi` 只注册不创建（§5 步骤 4） | 评审 + 签名约束；命中即拒绝 |
| 6 | 宿主负责创建、记账、显示、清理 | 创建时机由宿主在 UI 线程决定，产物先入 `PluginUiAssetRegistry` 再进视觉树 | 未登记资产 = 泄漏残留 → `Quarantined` |
| 7 | `StarPie.Host` 不引用 `StarPie.Sdk.Wpf` | Host 工程引用白名单 + `BoundaryTests`；`ui.sdk`/`ui.entryType` 的 **UI ABI 校验归 `StarPie.Ui/PluginHosting`**，Host 侧只做纯字符串/数据校验 | 编译期/边界测试拦截；校验错位 = 装载管线缺陷 |
| 8 | UI SDK ABI additive-only | 与 `StarPie.Sdk` 同政策：接受同主版本、次版本不高于宿主；破坏性变更 = 新描述符/新接口 | 不匹配 → 拒绝装载 |

### 5.2 受支持特性白名单与不支持列表（卸载判据）

白名单逐项只按「宿主清理后资产登记表清零 + 插件对象与插件委托的 `WeakReference` 全部死亡 + 全局根扫描无残留」判定；**WPF 宿主不判 ALC/程序集回收**（[ADR-0030](../adr/0030-ui-plugin-unload-semantics-downgrade.md) + [ADR-0035](../adr/0035-wpf-host-plugin-assembly-reclaim-downgrade.md)：宿主框架必然强引用插件程序集，存活只记诊断）。

| 特性 | 资产可清零 | 摘除方式 |
|---|---|---|
| 插件视图（XAML UserControl） | 是 | 清宿主容器 `Content`/`DataContext`，断绑定 |
| 插件窗口（XAML Window） | 是 | `Close()` 并等待 `Closed`，清 `Owner`/`DataContext` |
| 插件资源字典 | 是 | 并入插件资源根，卸载时整根摘除 |
| 插件 DataTemplate | 是 | 随资源根摘除；宿主容器清 `ContentTemplate` |
| 宿主签发 `DispatcherTimer` | 是 | `Stop()` + 摘除 Tick 回调，句柄账本清零 |
| 宿主中介动画 | 是 | **必须 `Storyboard.Remove(元素)`**；只 `Stop()` 会残留时钟与时间线 |
| 宿主中介事件订阅 | 是 | 宿主吊销即断，订阅计数归零 |
| 插件 Binding | 是 | `BindingOperations.ClearBinding` + 清 `DataContext` |

**不支持列表**：

- 插件的热卸载与热更新（UI 插件程序集在进程内不可回收；更新 = 隔离旧版本 + 下次启动装载新版本）。
- 插件自建 `DependencyProperty`/`RoutedEvent`、插件静态缓存、全局静态事件、绕过宿主契约的 WPF 注册。
- 松散 XAML；`Application.LoadComponent(绝对 pack URI)`；`EnterContextualReflection` 包裹 XAML 解析。
- `Popup`/`ContextMenu`/`ToolTip` 不经宿主契约自建：它们不在 `Application.Current.Windows` 中，工具提示类资产只能经宿主契约创建或显式登记。

**兜底与可判定的边界**：全局根扫描覆盖 `Application.Current.Resources`（含 `MergedDictionaries` 递归）与 `Application.Current.Windows`（含 `Owner`/`DataContext`），命中即按 plugin id 摘除并记 `Quarantined`；插件内部静态缓存这类根扫描不到，只有 `WeakReference` 判定能兜住，因此两者不可互相替代。

## 6. 能力注册与调用代理（headless）

- 宿主声明 `CapabilityId + Abi + 接口`；消费者经 `CapabilityRegistry.GetAll<T>()` 取实例。
- 插件在 `StartAsync` 内经 `IPluginContext.RegisterCapability<T>` 注册。
- 所有能力调用经宿主 `CapabilityGuard`：状态检查、在途计数、超时、异常捕获、连续失败熔断与隔离；**手写适配器，不用 `DispatchProxy`/运行时代码生成**。
- **能力–插件归属（Q8）**：能力实例活在该插件的 `PluginServiceScope` 内；消费者经 `CapabilityGuard` 短租用，宿主单例不得缓存能力实例。
- **顺序语义（Q10）**：`CapabilityRegistry.GetAll<T>()` 返回顺序 = 内置优先（内置不可被插件覆盖）→ 插件清单 `priority` → plugin id 稳定序；用户可调的顺序覆盖存 `plugin-state.json`，避免列表顺序随装载顺序抖动。

### 6.1 HostServices 硬约束（7 条，P2 落地判据）

HostServices = 插件可见的宿主服务（`IPluginLog`/`IPluginConfig`/`IPluginEvents`/…）。七条都是验收判据：

| # | 约束 | 判据与落点 |
|---|---|---|
| 1 | 接口在 `StarPie.Sdk` | 插件编译期只认 SDK 面；新增插件可见类型必须先进 SDK，Host 内部类型不得出现在签名里 |
| 2 | 实现在 `StarPie.Host/HostServices` | 实现类型 `internal`；插件拿到的永远是 SDK 接口，拿不到实现类型 |
| 3 | 插件只经 `IPluginContext` 取用 | 无静态单例、无服务定位器；`IPluginContext` 的属性即插件的全部可达面 |
| 4 | 服务按插件作用域隔离 | 每插件一个 `PluginServiceScope`（自持宿主服务实例 + 能力实例 + 句柄账本，不引入 MS.DI 容器，见 [ADR-0033](../adr/0033-plugin-service-scope-without-di-container.md)）；宿主根容器不含任何插件类型 |
| 5 | 订阅/回调/动作可按 plugin id 注销 | 每次注册返回 `IDisposable` 并登记进该插件的 scope 账本；卸载按 id 强制枚举清理，不依赖插件自觉 Dispose |
| 6 | 审计与日志不持有插件对象 | 只记 plugin id + 字符串/值类型字段；插件异常入日志前先转成"类型全名 + message + stack 字符串"的宿主 DTO——**`Exception` 实例与任何插件对象不得存进长生命周期结构（含日志 sink、诊断快照）** |
| 7 | 卸载前必须释放该插件的作用域 | `PluginServiceScope.Dispose()`（幂等）是 `ALC.Unload()` 的前置；scope 未释放或释放后仍有句柄残留 → `Quarantined`（残留是防御性检查：`Dispose` 先清账本再释放句柄，账本必为零；不为零即说明清账语义被改动） |

## 7. 插件 UI 宿主化（宿主托管）

### 7.1 固定扩展点，不做通用 Region 框架

扩展点由宿主定义并与现有 UI 结构对齐：**导航页、设置区、托盘菜单、插件窗口、内容容器宿主**（P3 可按需增加轮盘样式扩展点）。不引入 Prism 式通用 Region/模块系统（ADR-0016 已否决 Prism）。

插件页追加在固定五页之后；固定页 AutomationId 仍是 `NavPage0..4`（e2e 依赖），插件页用 `NavPlugin_<plugin-id>`——该规则是 P3 前置（Q10）。

### 7.2 契约funnel：插件只能经 `IPluginUiContext` 注册

```csharp
public interface IPluginUiContext
{
    string PluginId { get; }

    IDisposable RegisterPage(PluginPageDescriptor descriptor);       // 导航页（纯数据 + 模板与 VM 工厂；宿主调用工厂创建）
    IDisposable RegisterSettingsSection(PluginSettingsSectionDescriptor descriptor);
    IDisposable RegisterWindow(PluginWindowDescriptor descriptor);   // 工厂，宿主创建并跟踪实例
    IDisposable RegisterMenuItem(PluginMenuItemDescriptor descriptor);
    IDisposable RegisterCommand(PluginCommandDescriptor descriptor);
    IDisposable MergeResourceDictionary(Uri packUri);                // 宿主并入插件资源根
    IDisposable CreateTimer(TimeSpan interval, Action tick);         // 宿主签发，可整体停止
    IDisposable Subscribe<TEvent>(Action<TEvent> handler);           // 宿主中介，卸载即断
}
```

- 每个返回值进资产登记表；插件可直接 Dispose，卸载时宿主仍会强制清理。
- **Descriptor 只描述、不承载实例**：允许纯数据 + 工厂委托 + 类型名 + pack URI，禁止内嵌已构造的视图/VM/命令实例——创建时机必须由宿主在 UI 线程决定（§5.1 约束 4/5）。
- 插件若绕过契约（自建 `Window`、直接 merge `Application.Current.Resources`、自建静态事件/缓存）→ 不受支持；泄漏扫描命中即隔离。

### 7.3 资源字典：每插件一个资源根

- 插件资源不逐条 merge，而是并入宿主为插件创建的 `PluginResourceRoot`（一个 `ResourceDictionary` 容器）；卸载 = 从 `Application.Current.Resources.MergedDictionaries` 移除该根容器，DataTemplate/Style/Theme 随容器一次摘净。
- 插件页/窗口的局部资源同样挂在宿主分配的容器上，便于定位与清理。

### 7.4 视图与窗口

- 视图：宿主把插件视图放进宿主提供的容器（导航页内容宿主/设置区宿主）；登记表记录"容器 ↔ 视图 ↔ plugin id"；卸载先清容器 `Content`，再断开 DataContext/Binding。
- 窗口：插件只注册工厂，宿主创建、显示、跟踪 `Window`；卸载时在 UI 线程 `Close()`，等待 `Closed`，清 `Owner`、`DataContext`、事件处理器。
- `Popup`/`ContextMenu` 不在 `Application.Current.Windows` 中，必须经宿主契约创建或显式登记，否则泄漏扫描命中即隔离。

### 7.5 线程模型

- 一切 UI 注册、创建、清理都在 UI 线程；插件后台线程不得直接触碰 WPF 对象，须经宿主 `IUiDispatcher` 端口。
- 卸载编排：Host（后台/任意线程）→ `IPluginUiCoordinator.ReleaseAsync(pluginId)` → Ui 封送到 UI 线程执行清理 → 返回验证结果。

## 8. 卸载管线与验证

```text
安全点
 1. 配置 FlushPendingSave()
 2. 能力摘除 + 在途调用归零
 3. IPlugin.StopAsync()（插件的 Shutdown/Release）
 4. IPluginUiCoordinator.ReleaseAsync(pluginId)        ← UI 线程
      a. 视图清出视觉树、DataContext/Binding 清空
      b. Close 全部插件窗口并等待 Closed
      c. 从 MergedDictionaries 摘除插件资源根
      d. 注销命令/InputBinding/菜单
      e. 停止并摘除全部宿主签发定时器与动画（动画须 `Storyboard.Remove(元素)`）
      f. 断开宿主中介订阅
      g. 断言资产登记表清零
 5. 释放该插件的服务作用域 `PluginServiceScope.Dispose()`（§6.1 约束 7，幂等）；宿主侧缓存/Type/委托清空，含 `IPlugin`/`IPluginUiModule` 入口对象本身
 6. GC.Collect → WaitForPendingFinalizers → GC.Collect
 7. `WeakReference` 判定（可判定项）：资产对象与插件委托必须全部死亡
 8. ALC.Unload()
9. 再 GC + 二次判定（`PluginReclaimPolicy` 分档）：纯 headless 宿主硬判 ALC 与程序集回收；WPF 宿主降级——
   只硬判插件自有对象（入口实例/作用域句柄/在途调用），ALC 与程序集存活记诊断，不判隔离（ADR-0035）
任一步失败（配置未落盘 / 能力摘除失败 / 在途未归零 / 停用失败 / 作用域未释放或句柄残留 / 回收判定未过 /
资产未清零 / 全局根有残留） → Quarantined + 诊断（含残留清单）+ 重启提示；不得谎报成功
```

**卸载输入是交接对象**：装载结果的入口实例、ALC 与服务作用域经 `PluginUnloadRequest.FromLoaded` 交接给卸载管线，交接即清空装载结果持有的三条强引用——调用方无从再经装载结果持有插件对象；回收判定在请求清空强引用、且 `ALC.Unload()` 的调用帧退出后再做。在途调用未归零时中止于危险区之前：不释放作用域、不卸载 ALC，直接按隔离收口，要收口只能等重启或显式重载时再走一次安全点。已隔离的插件可经同一管线回收资源：结论仍是隔离、不改写既有隔离原因，也不谎报已卸载。这三条款对全部 headless 插件成立；回收判定本身按**宿主环境**分档：`Hard`（纯 headless 宿主，管线缺省）硬判 ALC 与程序集回收，`Diagnostic`（WPF 宿主，Ui 组合根）只硬判插件自有对象、ALC 与程序集存活记诊断且不判隔离——规范判决见 [ADR-0034](../adr/0034-headless-unload-handover-and-hard-reclaim.md) 与 [ADR-0035](../adr/0035-wpf-host-plugin-assembly-reclaim-downgrade.md)。UI 插件的资产清理与泄漏扫描另按 [ADR-0030](../adr/0030-ui-plugin-unload-semantics-downgrade.md) 执行。

**泄漏扫描**（`PluginUiLeakVerifier`，生产诊断 + 测试共用）至少覆盖：

- `Application.Current.Windows`（含 Owner/DataContext/editables）
- `Application.Current.Resources` 与各窗口资源中的 `MergedDictionaries`（插件来源）
- 资产登记表残留（视图/窗口/命令/菜单/定时器/订阅）
- 全局绑定与命令（`CommandManager`、`InputBindings`）
- 插件服务作用域残留（订阅/回调/动作句柄），以及日志/审计 sink 中的插件对象引用（§6.1 约束 6）
- 泄漏对象的程序集归属，输出"哪个插件、哪类资产、哪条引用"

**测试矩阵**（`StarPie.Tests`，STA harness）：视图、窗口、资源字典、DataTemplate、定时器、动画、事件、绑定逐项覆盖；每项断言探针对象的 `WeakReference` 均死、资产登记表清零、全局根扫描无残留（ALC/程序集存活记诊断，不作为 UI 插件的失败判据）。动画一项额外断言用 `Storyboard.Remove(元素)` 摘除后才可清零。另需覆盖 HostServices 侧两项：`PluginServiceScope` 释放后句柄账本清零；插件自定义异常/自定义类型经日志与诊断报告后不 root 插件集（对自定义异常实例做 `WeakReference` 判定）。

## 9. 配置、数据、文案、日志

- **配置**：`config.json` 新增 `plugins: { "<id>": { … } }`；旧配置无该段照常加载。该段归插件所有（经 `IPluginConfig` 读写），宿主状态不写这里（Q9b）。
- **数据**：`%LOCALAPPDATA%\StarPie\plugin-data\<id>\`；卸载默认保留，管理面提供"彻底移除"。
- **宿主状态**：`%LOCALAPPDATA%\StarPie\plugin-state.json`——启用/停用、已装版本、路径、准入来源（内置/审核清单/开发者模式）、隔离状态；宿主唯一权威，插件不可读写（Q9、Q9b）。
- **三个动作要分清（Q9）**：**停用** = 安全点卸载（停用插件代码、摘除能力、释放服务作用域与插件对象；WPF 宿主里插件程序集留到重启释放，见 ADR-0035）、保留包与状态；**移除包** = 停用后删插件目录、状态条目保留；**彻底移除** = 删 `plugins.<id>` 配置段 + `plugin-data\<id>` + `plugin-state.json` 条目，再 `FlushPendingSave()`。
- **文案**：随包 `strings\<culture>.json` + `IPluginContext.Localization`；宿主渲染的设置表单用插件自报文案。
- **日志**：插件只经 `IPluginContext.Log` 写宿主日志（自动带 plugin id）。

## 10. 设置面与插件管理面

- 插件不自绘设置表单时，用 `settings.schema.json` 由宿主渲染（bool/数字/字符串/枚举/路径/路径列表）；写了 UI 的插件也可以注册 `PluginSettingsSectionDescriptor` 自绘设置区。
- 宿主必须有 `PluginManagerPage`：列表、状态（Active / 已停用 / Quarantined）、启用/停用、重载/更新、隔离后的**重试**、诊断报告、"彻底移除"；页面常显当前**准入模式**（ADR-0029）。隔离态同时提供**重试**与**停用**两条出口：重试 = 回收续做 + 重新装载，停用 = 落停用意图并尽力回收（隔离原因保持可见，隔离不因停用被抹去）。
- 首次开启开发者模式必须走确认流程并展示全信任风险披露：可读配置与插件数据、可执行任意代码、可使进程崩溃、卸载可能失败并被隔离。

## 11. ABI、版本与信任

- **ABI**：`StarPie.Sdk` 与 `StarPie.Sdk.Wpf` 同政策：主.次版本；宿主接受同主版本且次版本不高于宿主的插件；接口 additive-only，破坏性变更 = 新接口 + 新能力 id/新描述符。
- **准入（ADR-0029）**：目标态 = 签名（Authenticode 或受 pin 的发布者证书）+ 审核清单（可离线校验），未命中即 `Rejected`；首期 = 仅第一方随包插件与**开发者模式**插件（默认关闭的显式开关 + 全信任风险披露）；不做默认侧载放行。
- **撤销**：审核清单支持版本级黑名单；每次启动扫描按当前清单重新判定，命中即停用。
- **准入结果四态**：内置 / 已审核 / 开发者模式 / 拒绝（附原因）；启动报告与插件管理面都要能看出当前处于哪一态。「内置」= 命中宿主内置 id 清单的第一方随包插件，见 §3。
- **ALC 不是安全边界**：进程内插件（含 UI 插件）与宿主同权限——可读配置与插件数据、可执行任意代码、可使进程崩溃。宿主不承诺沙箱、权限限制或资源配额；不可信插件只能走进程外后端（P5，另起 ADR）。**ALC 也不是 WPF 宿主内任何插件的卸载边界**：宿主框架缓存使程序集留在进程内不可回收，卸载语义见 ADR-0030、ADR-0035 与 §5.2。

## 12. 迁移映射（15 集 → 三集 + 插件）

| 现程序集 | 目标归属 |
|---|---|
| `StarPie`（App/AppHost/Composition/MainView/导航运行时/外观页/共享 UI 基建） | `StarPie.Ui` |
| `StarPie.Core`（Models/Messages/NavigationCatalog/AppHostDelegates） | 契约/模型 → `StarPie.Sdk`；运行时 → `StarPie.Host` |
| `StarPie.Core`（Configuration/Localization 实现） | `StarPie.Host.Kernel` |
| `StarPie.Gestures` | 可 headless 内核（`GestureEngine`/`WindowContext`/`ActionRouting`）→ `StarPie.Host`；WPF 亲和件（`MouseHook`/`GestureController`/`ActionExecutorService`）与 VM/View → `StarPie.Ui` |
| `StarPie.Wheel` | 配色目录与解析（`WheelPalette*`）→ `StarPie.Host`；几何构造（`WheelGeometry`）/`WheelFactory`/VM/Renderer/RadialWindow → `StarPie.Ui` |
| `StarPie.Programs` + `StarPie.Programs.Contracts` | 首个 headless 插件（能力契约入 SDK） |
| `StarPie.Theme` + `StarPie.Theme.Contracts` | 引擎 → `StarPie.Host`；字典/设置 VM → `StarPie.Ui` |
| `StarPie.Shell` | 自启/内存整理 → `StarPie.Host.Kernel.ShellIntegration`；托盘（`TrayIconManager`）/高级页 → `StarPie.Ui` |
| `StarPie.Dialogs` | `StarPie.Ui`（宿主对话框）；端口只在 Ui 内部 |
| `StarPie.Icons` + `StarPie.Icons.Contracts` | 资产目录/降级服务 → `StarPie.Host`；`IconRef` → SDK；图像构造 → Ui |

> **P1 归并口径（2026-09-11 路线审查）**：上表按 `StarPie.Host` 零 WPF 硬约束细化——直接构造 WPF
> 类型（如 `WheelGeometry` 的 `Geometry`）、持有 `Application.Current.Dispatcher` 或默认 `MessageBox`
> 的 WPF 亲和件一律留 `StarPie.Ui`；端口化推迟到出现真实 headless 需求时再引入（`Ports/` 新增项随需求走）。

## 13. 演进阶段

| 阶段 | 内容 | 验证门 |
|---|---|---|
| P0 | ALC+WPF 打样：XAML 视图/窗口/资源字典/DataTemplate/定时器/动画/事件/绑定逐项认证；确定 BAML/pack URI 宿主包装方式与卸载判据 | 打样报告 + 受支持特性白名单（§5.2）；未过项列入不支持列表 |
| P1 | 三集物理重构：建 SDK/Sdk.Wpf/Host/Ui；15 集撤销；现有模块改走统一注册管线但静态加载 | build + 全量 xUnit + 全量 e2e，行为零变化 |
| P2 | headless 插件运行时 + M3 首个插件：清单/发现/ALC/子容器/能力/安全点卸载/隔离；准入走开发者模式 | 卸载回收（含 §6.1 作用域释放与日志不 root；WPF 宿主按 ADR-0035 降级判定）、隔离持久化、停用态降级（程序选择器）、准入关闭时第三方被拒；全量门 |
| P3 | 插件 UI 宿主：`PluginHosting` 全层 + 导航动态注册（`NavPlugin_<id>`）+ 首个 UI 示例插件 + 卸载测试矩阵 | STA 卸载矩阵全绿（资产清零 + 泄漏隔离，ALC 存活不上判）；全量门 |
| P4 | 生态化：签名校验 + 审核清单与撤销通道、SDK 文档/示例仓库、插件管理面完善、第三方准入开启 | 发布前评审 |
| P5 | 视需要评估进程外后端（不可信插件） | 新 ADR |

## 参见

[ADR-0027](../adr/0027-plugin-architecture-and-host-sdk-ui-split.md)、[ADR-0028](../adr/0028-plugin-ui-hosting-and-host-managed-lifecycle.md)、[ADR-0029](../adr/0029-plugin-trust-model.md)、[ADR-0030](../adr/0030-ui-plugin-unload-semantics-downgrade.md)、[assemblies.md](assemblies.md)（P1 前 as-built）、[modules.md](modules.md)（P1 前 as-built）。
