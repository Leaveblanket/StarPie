# 插件体系

> **阅读方式与正典分工**:本文是插件子系统**机制与边界**的正典(物理形态、包与清单、生命周期、
> 装载 / 卸载管线与设置面);**可用面与硬约束**的正典在 `StarPie.Sdk.Wpf` 的契约类型(原 xUnit
> 机械断言已下线,测试范围见 `docs/adr/0050-test-scope.md`);**上手路径**见 `plugins/samples/README.md` 与各示例工程。
> 本文不复制可用面清单。**正文为纯 as-built**(标注「as-built:」的条款即现状,
> 未加标注的条款即现行规范)。

## 术语(架构词正典在本文)

**宿主内核 (Host Kernel)**:`StarPie.Host` 承载的全部不可卸载运行时与服务。宿主内核不是插件。

**插件 (Plugin)**:可在运行期装载 / 卸载、只经 `StarPie.Sdk` 与宿主交互的扩展单元;可贡献 headless 能力与宿主托管的 UI。

**能力 (Capability)**:宿主声明、插件实现的无 UI 扩展点,用 id + ABI 号标识(如 `program-source@1`)。

**插件 UI 资产 (Plugin UI Asset)**:插件贡献的 WPF 对象——视图、窗口、资源字典、DataTemplate、命令 / 菜单、定时器 / 动画、事件订阅。

**资产登记表 (Asset Registry)**:`StarPie.Ui/PluginHosting` 中按 plugin id 记录全部 UI 资产的宿主侧清单;是卸载清理与验证的唯一依据。

**受支持特性白名单 (Certified Feature Set)**:已认证可可靠卸载的 WPF 特性集合;白名单外的特性不受支持。

**安全点 (Safe Point)**:无在途插件调用、配置已落盘、宿主侧引用可清的卸载时机。

**危险区 (Danger Zone)**:卸载链上"插件代码不得再运行"的一侧——服务作用域释放起,经 `ALC.Unload()`;进入后没有回头路。在途调用未归零一律中止于危险区之前(只摘能力条目,不释放作用域、不卸载 ALC)。

**泄漏隔离 (Leak Quarantine)**:清理或卸载验证失败后插件被停用、不再调用、提示重启的状态。

**宿主门面 (Host Facade)**:`IPluginContext`(headless 服务)与 `IPluginUiContext`(UI 资产注册);插件可触达的全部宿主能力集合。

**宿主服务作用域 (Plugin Service Scope)**:每插件一个的子容器与句柄账本(`PluginServiceScope`);订阅、回调、动作句柄全部登记于此,卸载时按 plugin id 整体释放。

**准入模式 (Admission Mode)**:插件被允许装载的依据——内置 / 审核清单 / 开发者模式。

**开发者模式 (Developer Mode)**:默认关闭的显式开关,开启后允许装载未命中审核清单的插件,并向用户展示进程内全信任风险披露。

**宿主状态 (Plugin State)**:宿主权威的插件运行状态(启用 / 停用 / 已装版本 / 路径 / 准入来源 / 隔离),存 `plugin-state.json`;与插件自己的配置 `plugins.<id>` 分离。

## 1. 边界与原则

1. **插件只引用 `StarPie.Sdk` 与 `StarPie.Sdk.Wpf`**;`StarPie.Host` / `StarPie.Ui` 的内部类型不在插件引用面内。
2. **宿主独占生命周期**:插件可创建 XAML / Window / ResourceDictionary / DataTemplate,但必须经宿主契约注册;宿主登记、跟踪、移除、验证,插件不得自行 merge 全局资源或长期持有 WPF 全局对象。
3. **宿主内核零 WPF**:`StarPie.Host` 不引用 WPF;一切 WPF 类型与清理在主进程 Ui 层 `PluginHosting` 执行,Host 经 `IPluginUiCoordinator` 端口协调。
4. **插件缺席是可运行态**:每个扩展点必须定义降级行为(无插件页时导航正常、无程序来源时选择器只有内置来源)。
5. **跨 ALC 只共享 SDK 与框架程序集**:`StarPie.Sdk` / `StarPie.Sdk.Wpf` 一律从默认 ALC 解析,保证接口与 WPF 类型身份唯一。
6. **一切装卸发生在安全点**:更新 = 安全点卸载 + 装载新版本;不做无约束即时重载。
7. **白名单外即不支持**:不受支持的 WPF 特性不进入验收;发现泄漏按隔离流程处理,不降低验证标准。
8. **两组硬约束是落地判据**:`StarPie.Sdk.Wpf` 九条(见 `Abstractions/Ui/` 与 `Compatibility/` 的契约类型)与 HostServices 八条(见 `StarPie.Host/HostServices/` 的实现);两组的机械断言已下线(测试范围见 `docs/adr/0050-test-scope.md`)。违反不是"设计欠佳",而是拒绝装载、拒绝合并或判定 `Quarantined`。

## 2. 物理形态(三集 + SDK.Wpf + 一等插件)

```text
StarPie/
├── StarPie.slnx                          # 解决方案:登记四集 + 随包 / 示例插件工程 + 测试,一条命令 build / 测全套
├── Directory.Build.props                 # 共享构建属性(TFM / 可空性 / 分析器级别 / 警告视为错误):四集与插件工程不各写一遍,避免漂移
├── Directory.Packages.props              # 中央包管理(包版本唯一集中处);「SDK 零第三方包」= SDK csproj 无 PackageReference,「Host 零 WPF」由 TFM 结构性保证(原机械断言已下线)
├── StarPie.Sdk/                          # net10.0;零 WPF / 零第三方包;headless 唯一引用面
│   ├── Abstractions/                     # IPlugin、IPluginContext、IPluginLog:插件眼里「宿主长什么样」的全部
│   ├── Models/                           # 稳定 DTO 与 WPF-free 值类型:AppConfig / WheelProfile / ActionItem / CustomColorPreset / ColorMath / ScreenPoint;跨 ALC 传递的类型必须来自默认 ALC 的 SDK
│   ├── Services/  ViewModels/            # 非插件面契约与模型(Messages / Navigation / Dialogs / Icons / Programs / Wheel / Themes 契约与界面契约)
│   ├── Events/                           # 宿主事件契约(订阅返回 IDisposable):卸载即断的实现基础
│   ├── Manifest/                         # plugin.json 纯数据模型(校验逻辑在 Host,SDK 不做 IO)
│   └── Compatibility/                    # SDK 版本 / 宿主最低版本 / 能力 ABI 常量:判定集中一处
├── StarPie.Sdk.Wpf/                      # WPF 类型契约;默认 ALC 统一加载;additive-only
│   ├── Abstractions/Ui/                  # 插件 UI 契约:IPluginUiModule / IPluginUiContext / IUiDispatcher + 五个注册描述符(页面 / 设置区 / 窗口 / 菜单项 / 命令)
│   ├── Services/                         # IIconAssetService 与 IThemeService 的 WPF 契约(条目类型在 SDK、目录在 Host)
│   └── Compatibility/                    # UiSdkAbi 与 DefaultAlcPolicy(与 Sdk 分政策编号)
├── StarPie.Host/                         # 零 WPF 引用、零 XAML;可 headless 单测
│   ├── Configuration/  Localization/  Programs/  SystemIntegration/  Icons/  Themes/  Wheel/  WheelInteraction/  Actions/
│   │                                     # 配置 / 文案 / 程序扫描 / 注册表与进程级系统集成 / 资产目录 / 调色板计算 / 配色解析 / 轮盘交互内核 / 动作路由(纯模型与逻辑;几何构造与 WPF 亲和件归 Ui)
│   ├── Ports/                            # Host→Ui 端口:IThemeApplier(Host 零 WPF 只能吃接口;其余端口随需求引入)
│   ├── HostServices/                     # 插件可见宿主服务实现:PluginLog / PluginEvents / PluginEventPump / PluginServiceScope(每插件一个作用域)
│   └── PluginRuntime/{Discovery,Manifest,Admission,State,Hosting,Loading,Unloading,Lifecycle,Registry,Diagnostics,Ui}
│                                         # 发现 / 清单校验 / 准入判定 / 宿主状态 / 启用装载与停用再启用 / collectible ALC / 安全点卸载 / 状态机 / 能力表 / 诊断报告 / 插件 UI 协调
├── StarPie.Ui/                           # WinExe,AssemblyName=StarPie;唯一含 XAML
│   ├── App.xaml(.cs)  ResidentShell.cs  SettingsConsole.cs  Composition.cs   # 应用资源树、常驻壳层、设置台租户、组合根(内置与插件贡献者共用一条注册管线)
│   ├── Adapters/                         # 实现 Host/Ports 的 WPF 适配器:零 WPF 的 Host 只能吃接口
│   ├── ViewModels/  Views/  Services/  Modules/  Themes/   # 全部 VM / View / 对话框 / 轮盘渲染 / 主题字典 / 共享 UI 基建与贡献者注册
│   └── PluginHosting/                    # 插件 UI 资产生命周期(宿主托管)
│       ├── PluginUiAssetRegistry.cs      # plugin id → 资产清单;卸载枚举与清零断言的唯一依据
│       ├── PluginUiHost.cs               # IPluginUiCoordinator + IPluginUiContext 实现:Host 端口与插件 funnel 在此对接
│       ├── Extensions/                   # 固定扩展点:导航页 PluginPage / 设置区 PluginSettingsSection / 托盘菜单项 PluginMenuItem 的注册与摘除(PluginExtensionRegistry)
│       ├── Resources/PluginResourceRoot.cs    # 每插件资源根字典(一次 merge、一次摘除),DataTemplate / Style 随容器摘净
│       ├── Windows/PluginWindowRegistry.cs    # 窗口由宿主创建、关闭并等待 Closed
│       ├── Views/PluginViewHost.cs       # 视图进宿主容器并记账(容器 ↔ 视图 ↔ plugin id)
│       ├── Commands/PluginCommandRegistry.cs  # 命令与 InputBinding 的注册 / 注销
│       ├── Timers/PluginTimerRegistry.cs # 宿主签发的 DispatcherTimer / 动画,可整体停止
│       ├── Cleanup/PluginUiCleanup.cs    # UI 线程上的有序清理(§8 步骤 4)
│       └── Verification/PluginUiLeakVerifier.cs   # 泄漏扫描 + WeakReference 判定;生产诊断与测试共用
├── plugins/
│   ├── review-catalog.json(.sig)         # 审核清单 + 分离签名(公钥 pin 在宿主侧,可离线校验)
│   ├── src/StarPie.Plugin.Programs/      # 首个 headless 插件(只引 StarPie.Sdk;深扫程序来源),默认启用、可停用
│   ├── src/StarPie.Plugin.SampleUi/      # 首个 UI 示例插件(引 Sdk + Sdk.Wpf;导航页 / 设置区 / 窗口 / 托盘菜单示例)
│   └── samples/{MinimalHeadless,MinimalUi}/  # 最小示例工程(开发者上手指引见各示例工程 README)
├── StarPie.Tests/                        # 平铺:纯规则 / VM 状态逻辑 / 插件纯契约;范围见 docs/adr/0050-test-scope.md
└── tests/                                # pywinauto e2e;程序选择器用例分「启用 / 停用」两态
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

- `id` 反向域名、发布后不可变;`version` SemVer。
- `ui` 段可选:声明插件含 UI,并给出 `IPluginUiModule` 入口类型;无 `ui` 段即为 headless 插件。
- 发现顺序:安装目录 `plugins/` 与用户目录;同 id 冲突禁用两者并提示处理。
- 清单校验在发现期收口(失败即 `Rejected` 并附原因):`schemaVersion` 只接受 1;`id` / `name` / `version` / `sdk` / `entryAssembly` / `entryType` 必填;`id` 须等于包目录名;`sdk` 按 ABI 政策判定;`entryAssembly` / `settingsSchema` 必须是包内裸文件名且文件存在于包内;`ui` 段只做纯格式校验(UI 侧 ABI 兼容判定归 Ui 侧插件托管层)。
- 宿主状态条目字段:启用 / 停用、已装版本、包路径、准入来源(四态)、隔离状态、挂起版本(界面插件更新后就位、待下次启动装载的那一份);每次启动扫描重算准入并刷新条目,用户的启用意图与隔离状态不被扫描覆盖。
- 启动扫描产物 `plugin-startup-report.json`:扫描目录、开发者模式开关、逐插件准入结果与四态计数——**准入四态在启动报告里直接可见**。
- 开发者模式开关存宿主状态(默认关闭):开启须显式确认全信任风险披露(可读配置与插件数据 / 执行任意代码 / 使进程崩溃 / 卸载可能失败并被隔离),未确认不得置位。
- 内置认定以宿主内置 id 清单为准:随包第一方插件登记 id,安装目录位置本身不是信任依据;未登记的包按未审核处理。
- 包内**不得**出现 `StarPie.Sdk.dll` / `StarPie.Sdk.Wpf.dll`(共享契约不做随包分发);命中即 `Rejected`。
- `capabilities` 是数组:一个插件可声明多个能力,每条各自带 ABI;**卸载粒度仍是整个插件**,不能单摘一个能力。
- `priority` 可选(默认 0):只影响插件之间的能力列表顺序(数值小者靠前),内置条目永远最前;插件未声明 `priority` 时按 plugin id 稳定序。
- **禁止插件间依赖**:插件只依赖 SDK 与框架程序集,包内私有依赖由该插件独占,不跨插件共享。
- 宿主状态与插件配置分离:宿主状态存 `%LOCALAPPDATA%\StarPie\plugin-state.json`;插件配置段归 `config.json` 的 `plugins.<id>`(§9)。

## 4. 生命周期

```text
Discovered → Validated → Loading → Starting → Active
                                     ↘ Quarantined(加载 / 启动 / 调用 / 卸载失败)
Active → Stopping → ReleasingUi → Unloading → Unloaded
                                 ↘ Quarantined(资产未清零 / 全局根有残留)
```

- `ReleasingUi`:仅 UI 插件进入;在 UI 线程执行资产清理并跑泄漏验证。
- 只有 `Active` 状态允许执行插件代码;`Stopping` 起拒绝新调用,在途调用排空。
- 重载 = 走完整卸载流程(资产清理 + 服务作用域释放 + `ALC.Unload()`)后按新包重新装载;不保留任何插件对象。UI 插件的旧程序集留在进程内无法回收(因 WPF 宿主中 collectible ALC 回收有降级),因此 UI 插件的「新版本生效」以重启为界。
- **触发方式**:只在启动扫描 + 管理面手动启停 / 更新;headless 插件更新 = 安全点卸载 + 装载新版本,UI 插件更新 = 隔离旧版本 + 下次启动装载新版本。不做目录监视自动重载——卸载时机必须在安全点,监视重载会把用户正在用的插件页抽掉。
- **开发者模式例外**:dev 实例(Debug 构建)可开「监视 + 自动重载」开关(默认关闭,仅开发实例生效);重载仍走完整安全点卸载与 UI 清理,不绕过验证。
- **隔离是持久状态**:进入 `Quarantined` 即写 `plugin-state.json`;下次启动**不自动重试**装载,必须用户显式「重试」或「停用」。

## 5. 装载管线(collectible ALC)

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
2. `Load` 覆写:SDK / Sdk.Wpf / 框架程序集返回 `null` 回退默认 ALC;其余经 `AssemblyDependencyResolver` 私有加载。
3. 载入口程序集,找 `IPlugin`(不缓存 `Type`),`StartAsync`。
4. UI 插件由宿主在 UI 线程调 `IPluginUiModule.RegisterUi`;**只做注册,不在此创建窗口 / 合并资源**。
5. 注册能力 → `Active`。
6. **BAML / pack URI 解析(已认证,宿主包装方式固定)**:插件程序集在 collectible ALC 内时,XAML 视图 / 窗口与资源字典的 BAML 与 pack URI 解析正常,**宿主不得用 `AssemblyLoadContext.EnterContextualReflection` 包装 XAML 解析**——不需要,且会让 `XamlReader` 路径解析不到自身的 BAML 资源(`组件不具有由 URI 识别的资源`)。
   - 宿主合并插件资源字典必须用 `new ResourceDictionary { Source = packUri }`;`Application.LoadComponent(绝对 pack URI)` 在 .NET Core 抛「无法使用绝对 URI」。
   - 松散 XAML(`XamlReader` 解析含 `assembly=` 类型引用的文本)不在支持面:插件 XAML 一律走编译期 BAML。

> 硬约束(`StarPie.Sdk.Wpf` 九条)与「插件可达面」定义见 `StarPie.Sdk.Wpf/Abstractions/Ui/` 的契约类型;
> 受支持特性白名单与不支持列表见 §3。

## 6. 能力注册与调用代理(headless)

- 宿主声明 `CapabilityId + Abi + 接口`;消费者经 `CapabilityRegistry.GetAll<T>()` 取实例。
- 插件在 `StartAsync` 内经 `IPluginContext.RegisterCapability<T>` 注册。
- 所有能力调用经宿主 `CapabilityGuard`:状态检查、在途计数、超时、异常捕获、连续失败熔断与隔离;**手写适配器,不用 `DispatchProxy` / 运行时代码生成**。
- **能力–插件归属**:能力实例活在该插件的 `PluginServiceScope` 内;消费者经 `CapabilityGuard` 短租用,宿主单例不得缓存能力实例。
- **顺序语义**:`CapabilityRegistry.GetAll<T>()` 返回顺序 = 内置优先(内置不可被插件覆盖)→ 插件清单 `priority` → plugin id 稳定序;用户可调的顺序覆盖存 `plugin-state.json`,避免列表顺序随装载顺序抖动。

> HostServices 八条硬约束见 `StarPie.Host/HostServices/` 的契约与实现。

## 7. 插件 UI 宿主化(宿主托管)

### 7.1 固定扩展点,不做通用 Region 框架

扩展点由宿主定义并与现有 UI 结构对齐:**导航页、设置区、托盘菜单、插件窗口、内容容器宿主**(可按需增加轮盘样式扩展点)。不引入 Prism 式通用 Region / 模块系统。

插件页追加在固定五页之后;固定页 AutomationId 仍是 `NavPage0..4`(e2e 依赖),插件页用 `NavPlugin_<plugin-id>`——该规则是插件页接入的前置。

扩展点的宿主落点(as-built):导航页进 `NavigationCatalog`(运行期可增删,摘除随资产出账);
设置区由 `PluginUiCoordinator.SettingsSections` 汇总、在插件管理页的设置区按区块呈现;托盘菜单
由 `TrayMenuComposer` 在内置条目之后追加并路由到插件命令;插件窗口与内容容器分别经
`PluginWindowRegistry` 与 `PluginViewHost` 托管。**无插件时每个扩展点都必须为空而非空壳**:
目录只有固定页、托盘菜单不追加分隔线、设置区整块隐藏、未注册 UI 资产的插件释放直接成功。

### 7.2 契约funnel:插件只能经 `IPluginUiContext` 注册

```csharp
public interface IPluginUiContext
{
    string PluginId { get; }

    IUiDispatcher Dispatcher { get; }                                // 后台线程触碰 UI 的唯一入口(§7.5)

    IDisposable RegisterPage(PluginPageDescriptor descriptor);       // 导航页(纯数据 + 模板与 VM 工厂;宿主调用工厂创建)
    IDisposable RegisterSettingsSection(PluginSettingsSectionDescriptor descriptor);
    IDisposable RegisterWindow(PluginWindowDescriptor descriptor);   // 工厂,宿主创建并跟踪实例
    IDisposable ShowWindow(string windowKey);                        // 打开已注册窗口:宿主创建、显示并记账,返回关闭句柄
    IDisposable RegisterMenuItem(PluginMenuItemDescriptor descriptor);
    IDisposable RegisterCommand(PluginCommandDescriptor descriptor);
    IDisposable MergeResourceDictionary(Uri packUri);                // 宿主并入插件资源根
    IDisposable CreateTimer(TimeSpan interval, Action tick);         // 宿主签发,可整体停止
    IDisposable CreateAnimation(FrameworkElement target, Storyboard storyboard); // 宿主中介;摘除用 Remove 而非 Stop
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;  // 宿主中介,卸载即断
}
```

- 每个返回值进资产登记表;插件可直接 Dispose,卸载时宿主仍会强制清理。
- **Descriptor 只描述、不承载实例**:允许纯数据 + 工厂委托 + 类型名 + pack URI,禁止内嵌已构造的视图 / VM / 命令实例——创建时机必须由宿主在 UI 线程决定。
- 插件若绕过契约(自建 `Window`、直接 merge `Application.Current.Resources`、自建静态事件 / 缓存)→ 不受支持;泄漏扫描命中即隔离。

### 7.3 资源字典:每插件一个资源根

- 插件资源不逐条 merge,而是并入宿主为插件创建的 `PluginResourceRoot`(一个 `ResourceDictionary` 容器);卸载 = 从 `Application.Current.Resources.MergedDictionaries` 移除该根容器,DataTemplate / Style / Theme 随容器一次摘净。
- 插件页 / 窗口的局部资源同样挂在宿主分配的容器上,便于定位与清理。

### 7.4 视图与窗口

- 视图:宿主把插件视图放进宿主提供的容器(导航页内容宿主 / 设置区宿主);登记表记录"容器 ↔ 视图 ↔ plugin id";卸载先清容器 `Content`,再断开 DataContext / Binding。
- 窗口:插件只注册工厂,宿主创建、显示、跟踪 `Window`;卸载时在 UI 线程 `Close()`,等待 `Closed`,清 `Owner`、`DataContext`、事件处理器。
- `Popup` / `ContextMenu` 不在 `Application.Current.Windows` 中,必须经宿主契约创建或显式登记,否则泄漏扫描命中即隔离。

### 7.5 线程模型

- 一切 UI 注册、创建、清理都在 UI 线程;插件后台线程不得直接触碰 WPF 对象,须经宿主 `IUiDispatcher` 端口。
- 卸载编排:Host(后台 / 任意线程)→ `IPluginUiCoordinator.ReleaseAsync(pluginId)` → Ui 封送到 UI 线程执行清理 → 返回验证结果。

## 8. 卸载管线与验证

```text
安全点
 1. 配置 FlushPendingSave()
 2. 能力摘除 + 在途调用归零
 3. IPlugin.StopAsync()(插件的 Shutdown / Release)
 4. IPluginUiCoordinator.ReleaseAsync(pluginId)        ← UI 线程
      a. 视图清出视觉树、DataContext / Binding 清空
      b. Close 全部插件窗口并等待 Closed
      c. 从 MergedDictionaries 摘除插件资源根
      d. 注销命令 / InputBinding / 菜单
      e. 停止并摘除全部宿主签发定时器与动画(动画须 `Storyboard.Remove(元素)`)
      f. 断开宿主中介订阅
      g. 断言资产登记表清零
 5. 释放该插件的服务作用域 `PluginServiceScope.Dispose()`(幂等);宿主侧缓存 / Type / 委托清空,含 `IPlugin` / `IPluginUiModule` 入口对象本身
 6. GC.Collect → WaitForPendingFinalizers → GC.Collect
 7. `WeakReference` 判定(可判定项):资产对象与插件委托必须全部死亡
 8. ALC.Unload()
9. 再 GC + 二次判定(`PluginReclaimPolicy` 分档):纯 headless 宿主硬判 ALC 与程序集回收;WPF 宿主降级——
   只硬判插件自有对象(入口实例 / 作用域句柄 / 在途调用),ALC 与程序集存活记诊断,不判隔离
任一步失败(配置未落盘 / 能力摘除失败 / 在途未归零 / 停用失败 / 作用域未释放或句柄残留 / 回收判定未过 /
资产未清零 / 全局根有残留) → Quarantined + 诊断(含残留清单) + 重启提示;不得谎报成功
```

**卸载输入是交接对象**:装载结果的入口实例、ALC 与服务作用域经 `PluginUnloadRequest.FromLoaded` 交接给卸载管线,交接即清空装载结果持有的三条强引用——调用方无从再经装载结果持有插件对象;回收判定在请求清空强引用、且 `ALC.Unload()` 的调用帧退出后再做。在途调用未归零时中止于危险区之前:不释放作用域、不卸载 ALC,直接按隔离收口,要收口只能等重启或显式重载时再走一次安全点。已隔离的插件可经同一管线回收资源:结论仍是隔离、不改写既有隔离原因,也不谎报已卸载。这三条款对全部 headless 插件成立;回收判定本身按**宿主环境**分档:`Hard`(纯 headless 宿主,管线缺省)硬判 ALC 与程序集回收,`Diagnostic`(WPF 宿主,Ui 组合根)只硬判插件自有对象、ALC 与程序集存活记诊断且不判隔离——硬判 / 降级判据在 `PluginReclaimPolicy` 与源码注释。UI 插件的资产清理与泄漏扫描另按 §7.2 / §7.3 / §7.4 执行。

**泄漏扫描**(`PluginUiLeakVerifier`,生产诊断 + 测试共用)至少覆盖:

- `Application.Current.Windows`(含 Owner / DataContext / editables)
- `Application.Current.Resources` 与各窗口资源中的 `MergedDictionaries`(插件来源)
- 资产登记表残留(视图 / 窗口 / 命令 / 菜单 / 定时器 / 订阅)
- 全局绑定与命令(`CommandManager`、`InputBindings`)
- 插件服务作用域残留(订阅 / 回调 / 动作句柄),以及日志 / 审计 sink 中的插件对象引用
- 泄漏对象的程序集归属,输出"哪个插件、哪类资产、哪条引用"

**自动化覆盖现状**:UI 插件卸载矩阵的逐项覆盖(视图、窗口、资源字典、DataTemplate、定时器、动画、事件、绑定)已随 `docs/adr/0050-test-scope.md` 下线——真实窗口与 STA 消息循环的复现不在 xUnit 内,该面由 e2e 与人工验收承担;`PluginUiLeakVerifier` 仍是生产诊断的判定入口。HostServices 侧两项仍在 xUnit:`PluginServiceScope` 释放后句柄账本清零;插件自定义异常 / 自定义类型经日志与诊断报告后不 root 插件集(对自定义异常实例做 `WeakReference` 判定)。

## 9. 配置、数据、文案、日志

- **配置**:`config.json` 的 `plugins: { "<id>": { … } }` 段(`AppConfig.Plugins`)归插件所有,缺失该段照常加载;宿主状态不写这里,插件侧读写面见 §11。
- **数据**:`%LOCALAPPDATA%\StarPie\plugin-data\<id>\`;卸载默认保留,管理面提供"彻底移除"。
- **宿主状态**:`%LOCALAPPDATA%\StarPie\plugin-state.json`——启用 / 停用、已装版本、路径、准入来源(内置 / 审核清单 / 开发者模式)、隔离状态、挂起版本;宿主唯一权威,插件不可读写。
- **三个动作要分清**:**停用** = 安全点卸载(停用插件代码、摘除能力、释放服务作用域与插件对象;WPF 宿主里插件程序集留到重启释放),保留包与状态;**移除包** = 停用后删插件目录、状态条目保留;**彻底移除** = 删 `plugins.<id>` 配置段 + `plugin-data\<id>` + `plugin-state.json` 条目,再 `FlushPendingSave()`。

  as-built:管理面按插件形态给两条更新路径——无界面插件就地「安全点卸载 + 按新包装载」;界面插件只隔离旧版本,
  把新版本登记为**挂起版本**(宿主状态 `PendingVersion`),页面显式提示「下次启动生效」,重启装载成功后挂起标记清除。
- **文案**:宿主渲染的三处标题(导航页 / 设置区块 / 托盘菜单项)按「`DisplayName` 字面量 →
  `TitleKey` 宿主 resx 键」解析;解析不到时按字面量显示并在注册期告警。
  窗口与命令描述符的 `TitleKey` 不由宿主渲染(窗口标题由插件自己的窗口设置、命令标题只作 id 路由),保留为兼容面;插件自持文案与插件面取词见 §11。
- **日志**:插件只经 `IPluginContext.Log` 写宿主日志(自动带 plugin id)。

## 10. 设置面与插件管理面

- 插件设置面:as-built 经 `PluginSettingsSectionDescriptor` 注册自绘设置区;清单可声明 `settingsSchema`(发现期校验为包内文件),宿主的 schema 表单渲染尚未落地(§11)。
- 宿主必须有 `PluginManagerPage`:列表、状态(Active / 已停用 / Quarantined)、启用 / 停用、重载 / 更新、隔离后的**重试**、诊断报告、"彻底移除";页面常显当前**准入模式**。隔离态同时提供**重试**与**停用**两条出口:重试 = 回收续做 + 重新装载,停用 = 落停用意图并尽力回收(隔离原因保持可见,隔离不因停用被抹去)。
- as-built:列表另有**待重启**态(界面插件的挂起版本);启停 / 重载 / 更新 / 重试 / 诊断 / 彻底移除各有稳定
  `PluginManager*_<plugin-id>` AutomationId;彻底移除前必须经用户确认,未清干净时如实报残余。
- as-built:**提权态**页首另有一行警示(`PluginManagerElevatedNotice`)——插件为进程内加载、宿主不承诺
  沙箱与配额,宿主提权即把第三方插件的爆炸半径从用户级抬到机器级;非提权态整行不出现。
  该行是"用户可见文案必须写清插件与 StarPie 同权限"的披露落点(另一处在提权自启开关的
  说明文案),不是混合权限态的适配。
  提权态不拒绝加载插件:准入决策已交给用户,不因权限级别被二次否决。
- 首次开启开发者模式必须走确认流程并展示全信任风险披露:可读配置与插件数据、可执行任意代码、可使进程崩溃、卸载可能失败并被隔离。

## 11. 目标态与差距

- `规划:` **`StarPie.Sdk/Capabilities/`**——能力契约按能力分文件(首个能力 `IProgramSource`):破坏性变更 = 加文件而非改文件,additive-only 可机械审。
- `规划:` **`StarPie.Sdk/Settings/`**——声明式设置 schema 模型与宿主通用表单渲染(插件不自绘设置时按 `settings.schema.json` 渲染 bool / 数字 / 字符串 / 枚举 / 路径 / 路径列表)。
- `规划:` **`StarPie.Host/Ports/` 其余端口**——`IWheelPresenter` / `IIconImageFactory` 等随真实 headless 需求引入(现仅 `IThemeApplier`)。
- `规划:` **插件侧配置读写面 `IPluginConfig`**——落地后只读写 `config.json` 的 `plugins.<id>`。
- `规划:` **插件自持文案与插件面取词**——随包 `strings\<culture>.json` + `IPluginContext.Localization`,声明式设置表单用插件自报文案;落地需同步打包链路与 ALC 卫星资源政策。

## 参见

程序集物理面见 [assemblies.md](assemblies.md);插件可用面 / 硬约束见 `StarPie.Sdk.Wpf/` 对应契约类型。