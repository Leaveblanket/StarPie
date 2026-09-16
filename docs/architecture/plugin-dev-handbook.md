# StarPie 插件开发手册

> 面向插件开发者的路由手册。**正典分工**：插件子系统的机制与边界 = [plugins.md](plugins.md)；
> 可用面、白名单与硬约束清单 = [plugin-contracts.md](plugin-contracts.md)；决策理由 = [ADR-0029](../adr/0029-plugin-trust-model.md)。
> 本手册只回答"我要写插件，从哪开始、怎么部署、凭什么被允许装载"，不复制上述清单条目；条文与正典不一致时以正典为准，并请开 issue 指出漂移。
>
> 上手示例（可独立构建、部署、运行）见 [plugins/samples/](../../plugins/samples/README.md)。

## 从哪开始

1. 跑通最小示例：[plugins/samples/README.md](../../plugins/samples/README.md)——
   headless（`IPlugin` 生命周期 + 日志）与 UI（`IPluginUiModule` 导航页）两条路径各一个。
2. 读一遍 `StarPie.Sdk` / `StarPie.Sdk.Wpf` 的公共接口——插件的全部宿主可达面只有两个对象：
   - `IPluginContext`（headless 面，`StarPie.Sdk`）：日志、事件订阅、能力注册（plugins.md §4）；
   - `IPluginUiContext`（UI 面，`StarPie.Sdk.Wpf`，仅清单声明 ui 段的插件可拿）（plugins.md §7）。
3. 写清单 `plugin.json` 并按 §3 部署包目录（包目录名必须等于清单 id）。

## 按特性查入口

一切宿主交互只走上述两个契约对象；返回的 `IDisposable` 注册句柄进宿主资产登记表，
插件可自行 Dispose，卸载时宿主仍会强制清理。

| 特性 | 入口 | 契约 |
|---|---|---|
| 生命周期（启动/停止） | `IPlugin` | plugins.md §4 |
| 宿主日志（自动带 plugin id） | `IPluginContext.Log` | plugins.md §9 |
| 订阅宿主事件（卸载即断） | `IPluginContext.Events` / UI 侧 `Subscribe<TEvent>` | plugin-contracts.md §4 与 plugins.md §7.2 |
| 订阅托盘状态消息自清缓存（内存自治模式） | `Events.Subscribe<MinimizedToTrayMessage>` / `<RestoredFromTrayMessage>` | plugin-contracts.md §4；示范见随包 Programs 插件 |
| 注册能力（宿主须已声明契约；清单声明 capability） | `IPluginContext.RegisterCapability<T>` | plugins.md §6 |
| 导航页 / 设置区 / 插件窗口 / 托盘菜单 | `IPluginUiContext.Register*`（Descriptor 纯数据 + 工厂） | plugins.md §7.1/§7.2 |
| 打开自己注册的窗口 | `IPluginUiContext.ShowWindow(windowKey)` | plugins.md §7.2 |
| 资源字典（每插件一个资源根） | `IPluginUiContext.MergeResourceDictionary` | plugins.md §7.3 |
| 定时器 / 动画（宿主签发与中介） | `IPluginUiContext.CreateTimer` / `CreateAnimation` | plugins.md §7.2 |
| 设置表单 | 清单带 `settings.schema.json`（宿主渲染）或注册设置区块自绘 | plugins.md §10 |
| 持久化 | 插件配置段经 `IPluginConfig` 读写 `config.json` 的 `plugins.<id>`；插件数据目录由宿主代管 | plugins.md §9 |

**不能做什么**（不支持列表，命中即拒绝装载或隔离）、**受支持特性的卸载判据**，以及两组硬约束
（`StarPie.Sdk.Wpf` / HostServices），都在 [plugin-contracts.md](plugin-contracts.md) §2–§4——
本手册只列入口，不列判据。

## 装载凭什么被允许（准入）

按「内置清单 → 审核清单（含签名）→ 开发者模式 → 拒绝」顺序判定，未命中即 `Rejected`。
判定细则、签名路径与版本级撤销的完整契约见 plugin-contracts.md §5 与 ADR-0029；开发者视角速览：

- **内置**：随宿主分发并登记在 `PluginAdmissionPolicy.DefaultBuiltInPluginIds` 的第一方插件。
- **审核清单 + 签名**（第三方发布路径）：插件 (id, version) 列入首方签名的审核清单，
  且包入口程序集带可信签名（或发布者指纹被 pin 且内容未被篡改）。
- **开发者模式**（本地开发路径）：显式开关（默认关闭），开启须确认"进程内全信任"风险披露。
- 准入四态在插件管理页可见；拒绝原因在诊断面板可查。

## 部署与验证

- 用户插件目录：`%LOCALAPPDATA%\StarPie\plugins\<清单 id>\`（dev 实例随数据目录隔离）。
- 验证：启动 StarPie → 插件管理页看状态与准入文案；headless 写日志、UI 插件经
  `NavPlugin_<插件 id>` 出现在侧边栏。
- 诊断：管理页诊断面板（状态/准入/隔离原因/残留清单）+ 启动报告
  `%LOCALAPPDATA%\StarPie\plugin-startup-report.json`。
- 隔离后：管理页提供「重试」（修复包后重装）与「停用」两条出口；隔离原因不因停用被抹去。

## 审核清单维护（维护者视角）

- 私钥不入仓库：本机 `~/.starpie-keys/review-catalog-private.pem`
  （配对公钥 pin 在 `StarPie.Host` 的 `SignedPluginReviewCatalog.FirstPartyPublicKeyPem`）。
- 流程：编辑 `plugins/review-catalog.json`（加 (id, version) 或撤销条目）→
  `scripts/sign-review-catalog.ps1` 重签 → 替换安装目录 `plugins/` 下的清单文件对 → 重启宿主。
- 旋转密钥 = 新密钥对重签清单 + 更新宿主 pin（需发布宿主更新）。
- 随仓库清单自带 `starpie.catalog.selfcheck` 条目，xUnit（`ReviewCatalogPinTests`）
  验证 pin 与清单未漂移。**该 id 是保留标识，不对应真实插件，也不接受插件以它注册。**
