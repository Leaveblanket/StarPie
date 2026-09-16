# 插件契约面（可用面 / 硬约束 / 准入判据）

> 本文是插件**可用面与硬约束的唯一正典**：插件作者与 ABI/准入评审按需查的清单与判据。
> 插件子系统的叙事、生命周期与落点见 [plugins.md](plugins.md)；开发者上手指引见 [plugin-dev-handbook.md](plugin-dev-handbook.md)；
> 决策依据 [ADR-0027](../adr/0027-plugin-architecture-and-host-sdk-ui-split.md)、[ADR-0028](../adr/0028-plugin-ui-hosting-and-host-managed-lifecycle.md)、
> [ADR-0029](../adr/0029-plugin-trust-model.md)、[ADR-0030](../adr/0030-ui-plugin-unload-semantics-downgrade.md)、[ADR-0034](../adr/0034-headless-unload-handover-and-hard-reclaim.md)、
> [ADR-0035](../adr/0035-wpf-host-plugin-assembly-reclaim-downgrade.md)、[ADR-0046](../adr/0046-plugin-surface-copy-source.md)、[ADR-0047](../adr/0047-plugin-reachable-surface.md)。

## 1 插件可达面


> **插件可达面**：插件在宿主的可达面 = 从 `IPluginContext` 与 `IPluginUiContext` 的成员签名出发、
> 可传递到达的宿主类型集合。程序集导出面只决定「哪些类型可被引用」，不决定「插件能拿到什么服务」——
> 涉及「插件能不能做什么」的判定一律以可达面为准（[ADR-0047](../adr/0047-plugin-reachable-surface.md)）。
> 据此，`StarPie.Sdk.Wpf` 导出面里的 `IThemeService` **不构成**插件可达面（没有注入边），插件侧的
> 深浅色需求由宿主注入的无状态探针满足；该判定由 `SdkWpfBoundaryTests` 的成员签名闭包断言守护。

## 2 `StarPie.Sdk.Wpf` 硬约束（9 条）


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
| 9 | 插件界面文案来源与失败可见 | 描述符文案成员按「`DisplayName` 字面量 → `TitleKey` 宿主 resx 键」单一优先级解析（解析点收成一个共用件），两者不可同时为空；解析不到时按字面量显示并在**注册期**告警（告警经 UI 装载结果回传宿主日志，自动带 plugin id） | 解析不到不静默；插件自持文案表与插件面取词属规划（见 §9、[ADR-0046](../adr/0046-plugin-surface-copy-source.md)） |

## 3 受支持特性白名单与不支持列表（卸载判据）


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

## 4 HostServices 硬约束（8 条）

HostServices = 插件可见的宿主服务（`IPluginLog`/`IPluginConfig`/`IPluginEvents`/…）。八条都是验收判据：

| # | 约束 | 判据与落点 |
|---|---|---|
| 1 | 接口在 `StarPie.Sdk` | 插件编译期只认 SDK 面；新增插件可见类型必须先进 SDK，Host 内部类型不得出现在签名里 |
| 2 | 实现在 `StarPie.Host/HostServices` | 实现类型 `internal`；插件拿到的永远是 SDK 接口，拿不到实现类型 |
| 3 | 插件只经 `IPluginContext` 取用 | 无静态单例、无服务定位器；`IPluginContext` 的属性即插件的全部可达面 |
| 4 | 服务按插件作用域隔离 | 每插件一个 `PluginServiceScope`（自持宿主服务实例 + 能力实例 + 句柄账本，不引入 MS.DI 容器，见 [ADR-0033](../adr/0033-plugin-service-scope-without-di-container.md)）；宿主根容器不含任何插件类型 |
| 5 | 订阅/回调/动作可按 plugin id 注销 | 每次注册返回 `IDisposable` 并登记进该插件的 scope 账本；卸载按 id 强制枚举清理，不依赖插件自觉 Dispose |
| 5a | 托盘状态消息对插件可见（headless 侧经 `PluginEventPump` 广播、UI 侧经 `PluginUiEvents` 直桥） | 插件经 `IPluginEvents` 订阅 `MinimizedToTrayMessage`（进托盘）/`RestoredFromTrayMessage`（恢复）即收投递——订阅方在宿主 Send 调用线程同步执行，推荐语义是"进托盘释放自身深扫缓存、恢复按需重建"（内存自治出账，示范见随包 Programs 插件；宿主后台静默形态下宿主侧出账禁用但消息照发） |
| 6 | 审计与日志不持有插件对象 | 只记 plugin id + 字符串/值类型字段；插件异常入日志前先转成"类型全名 + message + stack 字符串"的宿主 DTO——**`Exception` 实例与任何插件对象不得存进长生命周期结构（含日志 sink、诊断快照）** |
| 7 | 卸载前必须释放该插件的作用域 | `PluginServiceScope.Dispose()`（幂等）是 `ALC.Unload()` 的前置；scope 未释放或释放后仍有句柄残留 → `Quarantined`（残留是防御性检查：`Dispose` 先清账本再释放句柄，账本必为零；不为零即说明清账语义被改动） |

## 5 ABI、版本与信任

- **ABI**：`StarPie.Sdk` 与 `StarPie.Sdk.Wpf` 同政策：主.次版本；宿主接受同主版本且次版本不高于宿主的插件；接口 additive-only，破坏性变更 = 新接口 + 新能力 id/新描述符。
- **准入（ADR-0029）**：`as-built：` 仅第一方随包插件与**开发者模式**插件放行（默认关闭的显式开关 + 全信任风险披露）；`规划：` 以签名（Authenticode 或受 pin 的发布者证书）+ 审核清单（可离线校验）作准入判据，未命中即 `Rejected`；不做默认侧载放行。
  as-built：`WinTrustSignatureVerifier`（WinVerifyTrust）对生效候选包的**入口程序集**做校验——可信链 → 可信；有签名但链不可信 → 提取签名主体与发布者指纹（证书 SHA-256），供「受 pin 的发布者证书」路径判定；无签名/不可解析 → Unsigned；**内容摘要与签名不符（篡改）一律不可信，pin 不救**。WinVerifyTrust 的证书级吊销检查（CRL）按 WTD_REVOKE_NONE 关闭——证书吊销不在撤销通道内，撤销走清单（下条）。内置插件不走签名闸（开发构建无签名）。
- **审核清单（as-built）**：安装目录 `plugins/` 子目录下 `review-catalog.json` + 分离 RSA-SHA256 签名（`review-catalog.json.sig`，base64），公钥 pin 在宿主侧（`SignedPluginReviewCatalog`），文件可离线校验。白名单按 **(pluginId, version) 精确命中**——清单未列入的新版本不因旧版本已审核而放行。清单被篡改、验签失败、文件对缺失或公钥 pin 为空一律**降级为空清单**：保守拒绝，宁可拒绝不误放行（ADR-0029 降级决策）。更新通道 = 首方私钥重签（`scripts/sign-review-catalog.ps1`，私钥不入仓库）后替换文件对并重启；随仓库清单自带 selfcheck 条目由 xUnit 防漂移。**该清单通道已 as-built 落地**（首期仍无第三方条目——第三方插件一律 `Rejected`，开发者模式为唯一例外）；开发者示例与部署路径见 [plugin-dev-handbook.md](plugin-dev-handbook.md) 与 `plugins/samples/`。
- **撤销**：审核清单支持版本级黑名单；每次启动扫描按当前清单重新判定，命中即拒绝装载（管理面显示 `Rejected` 与撤销原因）。用户启停意图不被翻转——撤销解除后插件自动回到可装载；显式「停用」是用户另做的独立决策。
- **准入结果四态**：内置 / 已审核 / 开发者模式 / 拒绝（附原因）；启动报告与插件管理面都要能看出当前处于哪一态。「内置」= 命中宿主内置 id 清单的第一方随包插件，见 §3。签名主体随扫描进宿主状态与启动报告（`SignatureSubject`），供诊断与撤销取证。
- **ALC 不是安全边界**：进程内插件（含 UI 插件）与宿主同权限——可读配置与插件数据、可执行任意代码、可使进程崩溃。宿主不承诺沙箱、权限限制或资源配额；不可信插件只能走进程外后端（另起 ADR）。**ALC 也不是 WPF 宿主内任何插件的卸载边界**：宿主框架缓存使程序集留在进程内不可回收，卸载语义见 ADR-0030、ADR-0035 与本文 §3。

## 参见

[plugins.md](plugins.md)、[plugin-dev-handbook.md](plugin-dev-handbook.md)、[ADR-0029](../adr/0029-plugin-trust-model.md)、[ADR-0047](../adr/0047-plugin-reachable-surface.md)。
