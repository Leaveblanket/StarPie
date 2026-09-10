# 插件体系与三集物理形态：第三方能力插件、collectible ALC 真卸载、宿主独占呈现、Host/SDK/Ui 三集

> Status: Active（部分被 ADR-0028 修订）
>
> 修订：决策 2「插件 = 能力插件（headless）」与决策 8 中「不承诺插件提供 WPF 视图/XAML/资源字典」由 [ADR-0028](0028-plugin-ui-hosting-and-host-managed-lifecycle.md) 修订；三集形态、collectible ALC 真卸载、SDK 单一引用面、依赖自治继续有效。
>
> 目标态程序集与文件架构见 `docs/architecture/plugins.md`；P1 落地后 `docs/architecture/assemblies.md` 与 `modules.md` 按本决策回填。本 ADR 部分推翻 ADR-0023 的**对外**契约判据（内部模块边界判据保留）。

## 动机

1. **现状是编译期模块，不是插件**：Host 在 `Composition.cs` 静态调用全部模块注册器、在 `App.xaml` 静态合并模块页面模板与主题字典、`NavigationCatalog` 是封闭槽位 0–3 且启动时 `Validate()`、`CreateAppHost` eager 解析全部页面 VM。模块可以拆集，但没有任何运行期装卸入口。
2. **第三方生态需要单一 SDK 面**：现 15 程序中，外部作者要消费宿主能力需引用 Core + 6 个 `*.Contracts`（24–174 行的薄集）并理解内部模块编号；模块 runtime 以契约互引、Host 引用全部 runtime。这不是可发布、可版本化、可承诺兼容的 SDK。
3. **WPF 与可卸载性冲突**：`Application.Current.Resources`、DataTemplate、DependencyProperty、窗口句柄、OS 钩子都会 root 插件程序集；collectible ALC 只能回收"没有任何宿主强引用"的闭包。
4. **第三方 = 全信任**：ALC 是加载隔离，不是安全边界。第三方代码在进程内可读配置、装钩子、拖垮进程；该事实必须显式写进决策，而不是假装卸载机制解决了隔离。
5. **现有契约归属判据服务的是内部边界**：ADR-0023"契约随实现方下沉"让插件作者面对的是模块实现史的产物，而非稳定的对外 ABI。

## Considered Options

- **维持 15 集、增量加 SDK**：假边界与 Host 静态引用不消，插件仍能绕过 SDK 引内部实现，ABI 不可控 → 否。
- **沿用"契约随实现方下沉"作为插件契约判据**：契约按模块分散，对外引用面随内部改集漂移 → 否（该判据作为 Host 内部目录规则保留）。
- **进程外插件**：真隔离、可强杀卸载，但 UI/性能/调试/生态成本高，与"首期进程内真卸载"的决定冲突 → 首期否，保留为未来承载不可信插件的后端。
- **允许插件提供 WPF 视图/XAML**：需要把 Application 资源树、DataTemplate、导航目录全部动态化，且卸载可靠性无法在首期证明 → 否。
- **三集 + headless 能力插件** → 采纳。

## Decision

1. **物理形态三集**：`StarPie.Sdk`（唯一第三方引用面：零 WPF、零第三方包依赖）、`StarPie.Host`（宿主内核：全部非 XAML 运行时、服务与插件宿主）、`StarPie.Ui`（WinExe：App/AppHost/Composition + 全部 ViewModel/View/Window/XAML/主题字典）；发布产物仍名为 `StarPie.exe`。
2. **插件 = 能力插件（headless）**：插件只贡献数据与无 UI 的策略/服务；宿主独占全部呈现（窗口、页面、对话框、轮盘绘制）。插件设置以声明式 schema 声明，由宿主渲染通用表单。
3. **真卸载**：collectible `AssemblyLoadContext` + 每插件子 `ServiceProvider`；SDK 与框架程序集从默认 ALC 共享（保证类型身份）；卸载只在安全点执行——无在途能力调用、配置已 flush、宿主侧引用已清。
4. **卸载承诺分级**：只对"无 UI、无 OS 句柄、无宿主强引用"的插件承诺真卸载；检测到泄漏即 `Quarantined`（停用、提示重启），不得谎报卸载成功。
5. **能力契约只加不改**：能力接口带 ABI 号，SDK 同主版本 additive-only；破坏性变更 = 新能力 id + 新接口。宿主拒绝 ABI/主版本不匹配的插件。
6. **依赖自治**：插件携带私有依赖经 `AssemblyDependencyResolver` 解析；首期不允许插件间互相依赖，避免级联卸载与版本地狱。
7. **撤销清单**：15 集并入三集 + 插件（映射见 `plugins.md` §12）；`StarPie.Shell`/`StarPie.Theme`/`StarPie.Dialogs`/`StarPie.Icons`/`StarPie.Programs` 与各 `*.Contracts` 均不再作为独立程序集存在。
8. **明确不承诺**：进程内安全隔离；插件提供 WPF 视图/XAML/资源字典；插件间依赖；目录监视自动热重载；SDK 跨主版本并行支持。

## Consequences

- 第三方开发者引用面收敛为一个 SDK 程序集；宿主可对插件包做清单校验、ABI 校验与能力白名单。
- 能力调用必须经宿主代理（在途计数、超时、异常熔断、隔离），能力接口必须窄且不含 UI 类型；这是可卸载性的价格。
- 现有 `RuntimeNoCrossReferenceTests`/`*AssemblyPlacementTests` 与 `modules.md`/`assemblies.md` 的三集前口径全部失效，P1 需重写为"三集 + 插件 ABI/依赖"测试与文档。
- 宿主内核不再有独立业务程序集；M1–M5 从"程序集边界"退化为"宿主内核内的子域 + 能力插件边界"。
- ADR-0023 的 runtime 互引清零、契约不反向依赖等内部判据在 Host 内部以目录/命名空间规则保留；对外判据改为"插件只引 SDK"。
- 未来若要开放 UI 插件或不可信插件，须新增 ADR，且先通过 P0 的 WPF/ALC 实验或转向进程外后端。
