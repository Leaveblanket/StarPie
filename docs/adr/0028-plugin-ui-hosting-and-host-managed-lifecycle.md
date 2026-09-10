# 插件 UI 宿主化：允许 XAML/Window/ResourceDictionary，宿主托管资产生命周期、强制登记、卸载验证与泄漏隔离

> Status: Active
>
> 本文修订 [ADR-0027](0027-plugin-architecture-and-host-sdk-ui-split.md) 决策 2 与决策 8 中「插件不提供 WPF 视图/资源字典」的条款；其余条款（三集形态、collectible ALC 真卸载、SDK 单一引用面、依赖自治）继续有效。目标态规范见 `docs/architecture/plugins.md`。

## 动机

1. **第三方生态需要界面**：只有 headless 能力（程序来源、图标来源）撑不起生态；插件必须能贡献设置页、窗口、托盘菜单等界面。
2. **WPF 的全局根让"插件自觉清理"不可信**：`Application.Current.Resources`/`Windows`、DataTemplate、`DependencyProperty`/`RoutedEvent` 注册、`DispatcherTimer`、`Storyboard`、静态事件与 `TypeDescriptor` 缓存都会 root 插件程序集；插件写得再规范，宿主也无法据此判断卸载是否真的成功。
3. **卸载必须可验证**：热卸载的判据只能是宿主侧的证据（资产登记表清零 + 全局根扫描 + `WeakReference` + GC），不能是插件的自我声明。

## Considered Options

- **禁止插件 UI（ADR-0027 原决策）**：热卸载最安全，但生态退化为"纯能力插件" → 被本 ADR 修订。
- **允许插件 UI，由插件自行清理**：无法验证、无法强制，卸载失败会变成幽灵插件 → 否。
- **允许插件 UI + 宿主托管资产 + 受支持特性白名单 + 验证/隔离** → 采纳。

## Decision

1. **插件可提供 XAML/Window/ResourceDictionary/DataTemplate**，但一切 UI 资产必须经宿主契约注册；宿主是这些对象生命周期的唯一所有者。插件绕过契约自建 WPF 全局对象属于不受支持行为，泄漏扫描发现即隔离。
2. **SDK 分层**：`StarPie.Sdk` 保持零 WPF；一切 WPF 类型契约进 `StarPie.Sdk.Wpf`，与 `StarPie.Sdk` 一样从默认 ALC 统一加载，additive-only。
3. **资产登记表**：宿主按 plugin id 记录全部 UI 资产——视图与宿主容器、窗口、资源字典、命令/菜单、宿主签发的定时器/动画、宿主中介的事件订阅；要求可枚举、可按 id 整体移除。
4. **卸载顺序固定**：安全点（无在途能力调用、配置已 flush）→ 插件 `StopAsync`/`Release` → **UI 线程**执行 WPF 清理（移出视觉树 → 关窗 → 摘资源字典 → 注销命令/菜单 → 停定时器/动画 → 清 DataContext/绑定/订阅）→ 宿主注销能力与子容器 → GC → `WeakReference` 验证 → `ALC.Unload()` → 再验证。任一环节失败 = `Quarantined` + 诊断报告 + 重启提示。
5. **受支持特性白名单**：首版仅认证「插件视图、插件窗口、插件资源字典/DataTemplate、宿主签发的 DispatcherTimer、宿主中介的简单动画与事件订阅」。**不支持**：插件自建 `DependencyProperty`/`RoutedEvent`、插件静态缓存、全局静态事件、绕过契约的 WPF 注册。
6. **承诺分级**：headless 插件承诺真卸载；UI 插件承诺"托管清理 + 可验证 + 泄漏隔离"，不承诺任意 WPF 代码必可卸载。
7. **信任前置**：进程内 UI 插件 = 全信任代码（可读配置、装钩子、使进程崩溃）；第三方 UI 生态必须配套签名/审核/权限披露（信任模型另行决策）。

## Consequences

- `StarPie.Ui` 新增 `PluginHosting` 层（登记表、资源根字典、视图/窗口/命令/菜单/定时器托管、清理与泄漏验证）；`StarPie.Host` 保持零 WPF，经 WPF-free 的 `IPluginUiCoordinator` 端口协调 UI 清理阶段。
- `NavigationCatalog` 的封闭槽位（0–3）必须改为可动态增删的页面注册，否则插件页无处挂载；`MainViewModel` 与页面 eager 解析清单相应改为动态。
- 卸载测试成为独立验证门：必须覆盖视图、窗口、资源字典、DataTemplate、定时器、动画、事件、绑定，并建立 STA 测试 harness。
- 插件开发手册必须写清"经宿主契约注册"是唯一合法路径；`StarPie.Sdk.Wpf` 的 ABI 与 `StarPie.Sdk` 同政策（同主版本 additive-only）。
- 若 P0 打样证明某类 WPF 特性无法可靠卸载，该特性进入"不支持列表"，而不是降低验证标准。