# StarPie 插件开发手册

> 面向插件开发者的路由手册。**约束性契约的正典是 [docs/architecture/plugins.md](architecture/plugins.md)
> 与 [docs/adr/0029-plugin-trust-model.md](adr/0029-plugin-trust-model.md)**——本手册不复制契约细节，
> 只回答"我要写插件，从哪开始、能做什么、不能做什么"。条文与正典不一致时以正典为准，并请开 issue 指出漂移。
>
> 上手示例（可独立构建、部署、运行）见 [plugins/samples/](../plugins/samples/README.md)。

## 从哪开始

1. 跑通最小示例：[plugins/samples/README.md](../plugins/samples/README.md)——
   headless（`IPlugin` 生命周期 + 日志）与 UI（`IPluginUiModule` 导航页）两条路径各一个。
2. 读一遍 `StarPie.Sdk` / `StarPie.Sdk.Wpf` 的公共接口——插件的全部宿主可达面只有两个对象：
   - `IPluginContext`（headless 面，`StarPie.Sdk`）：日志、事件订阅、能力注册（plugins.md §4）；
   - `IPluginUiContext`（UI 面，`StarPie.Sdk.Wpf`，仅清单声明 ui 段的插件可拿）（plugins.md §7）。
3. 写清单 `plugin.json` 并按 §3 部署包目录（包目录名必须等于清单 id）。

## 受支持特性白名单

一切宿主交互只走上述两个契约对象；返回的 `IDisposable` 注册句柄进宿主资产登记表，
插件可自行 Dispose，卸载时宿主仍会强制清理。

| 特性 | 入口 | 契约 |
|---|---|---|
| 生命周期（启动/停止） | `IPlugin` | plugins.md §4 |
| 宿主日志（自动带 plugin id） | `IPluginContext.Log` | plugins.md §9 |
| 订阅宿主事件（卸载即断） | `IPluginContext.Events` / UI 侧 `Subscribe<TEvent>` | plugins.md §6.1/§7.2 |
| 注册能力（宿主须已声明契约；清单声明 capability） | `IPluginContext.RegisterCapability<T>` | plugins.md §6 |
| 导航页 / 设置区 / 插件窗口 / 托盘菜单 | `IPluginUiContext.Register*`（Descriptor 纯数据 + 工厂） | plugins.md §7.1/§7.2 |
| 打开自己注册的窗口 | `IPluginUiContext.ShowWindow(windowKey)` | plugins.md §7.2 |
| 资源字典（每插件一个资源根，卸载整根摘除） | `IPluginUiContext.MergeResourceDictionary` | plugins.md §7.3 |
| 定时器 / 动画（宿主签发与中介） | `IPluginUiContext.CreateTimer` / `CreateAnimation` | plugins.md §7.2 |
| 设置表单 | 清单带 `settings.schema.json`（宿主渲染）或注册设置区块自绘 | plugins.md §10 |
| 持久化 | 插件配置段经 `IPluginConfig` 读写 `config.json` 的 `plugins.<id>`；插件数据目录由宿主代管 | plugins.md §9 |

## 不支持列表（命中即拒绝装载或隔离）

- **绕过 UI 契约自建 WPF 全局对象**：自建 `Window`、直接 merge `Application.Current.Resources`、
  自建静态事件/缓存、自建定时器/动画——泄漏扫描命中即隔离（plugins.md §7.2/§7.4）。
- **Descriptor 内嵌已构造实例**（视图/VM/命令）：只允许纯数据 + 工厂 + 类型名 + pack URI（plugins.md §7.2）。
- **包内分发宿主/SDK 程序集**：`StarPie.Sdk.dll`、`StarPie.Sdk.Wpf.dll`、`StarPie.Host.dll`、`StarPie.dll`
  出现在包内即拒绝——共享契约由宿主默认 ALC 统一提供（plugins.md §3）。
- **清单/包校验违规**：`schemaVersion` 不受支持、字段缺失、id ≠ 包目录名、入口程序集缺席、
  SDK ABI 不兼容（plugins.md §3/§11）。
- **松散 WPF 资产**：`Popup`/`ContextMenu`/`ToolTip` 不在 `Application.Current.Windows`，
  必须经宿主契约创建或显式登记；禁止自建 `DependencyProperty`/`RoutedEvent`（plugins.md §5.2/§7.4）。
- **插件之间互调**：首期不支持，事件由宿主发布、插件只订阅（ADR-0029）。
- **沙箱承诺**：进程内插件与宿主同权限——可读配置、可执行任意代码、可使进程崩溃。
  宿主不承诺权限限制或资源配额；不可信插件需进程外后端（P5，另立 ADR）（ADR-0029）。
- **WPF 宿主内 ALC 真卸载**：宿主框架缓存使插件程序集留到重启释放；卸载语义 =
  托管清理 + 资产清零 + 泄漏隔离（ADR-0030/0035，plugins.md §5.2/§8）。

## 装载凭什么被允许（准入）

按「内置清单 → 审核清单（含签名）→ 开发者模式 → 拒绝」顺序判定，未命中即 `Rejected`。
判定细则、签名路径与版本级撤销的完整契约见 plugins.md §11 与 ADR-0029；开发者视角速览：

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
