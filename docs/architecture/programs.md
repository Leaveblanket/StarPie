# 模块：程序扫描与目录

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及程序选择器数据来源与已安装
> 程序扫描时读本篇。

## 职责

程序选择器的候选来源扫描（**内置来源 + 插件来源**聚合）、跨源去重/过滤与快捷方式目标解析
（.lnk → 真实路径/图标位置）。
图标资产（矢量清单/SVG 键目录/文件图标提取）归 S1 共享「图标资产」出口，见
[layout.md](layout.md)；扫描候选是纯数据（零 WPF），图标由 UI 消费方按路径装配。

## 组成文件

- **契约（`StarPie.Sdk/`）**：`Services/Programs/IProgramScanner.cs`、`ProgramEntry.cs`、
  `ProgramCatalog.cs`（命名空间 `StarPie.Services.Programs`）+ `Services/Icons/IShortcutTargetResolver.cs`
  （.lnk SPI，命名空间 `StarPie.Services.Icons`）；`ProgramEntry` 为纯数据，不含图标字段。
- **实现（`StarPie.Host/Programs/`）**：`ProgramScanner.cs`（IO 扫描编排；实例实现契约
  `IProgramScanner`，构造注入 `IShortcutTargetResolver`）、`ProgramSourceCapability.cs`
  （`program-source@1` 能力契约 + 消费者侧守卫适配器）、`ProgramSourceAggregator.cs`
  （内置来源与插件来源的合并出口，消费者注入这一面）与 `ShortcutResolver.cs`
  （快捷方式解析出口；实例实现 SPI）。
- **插件实现（`plugins/src/StarPie.Plugin.Programs/`）**：首个随包 headless 插件，只引
  `StarPie.Sdk`；`ProgramSourcePlugin` 在 `StartAsync` 注册 `IProgramScanner` 能力，
  `InstalledProgramScanner` 提供深扫来源（默认启用、可停用）。
- **注册（组合根 `StarPie.Ui/Composition.cs`）**：直登记
  `IShortcutTargetResolver→ShortcutResolver`、能力表（声明程序来源契约 + 内置来源）与
  `IProgramScanner→ProgramSourceAggregator`；M3 无导航页故无 `RegisterNavigation`。
  扫描件标注 `[SupportedOSPlatform("windows")]`（宿主内核为平台中立 TFM，扫描来源依赖
  注册表/开始菜单等 Windows 设施）。

宿主内核只引用 `StarPie.Sdk`（跨集只经 SDK 契约）；消费方（Dialogs 程序选择器）经契约面注入，
不引用实现。程序集依赖方向见 [assemblies.md](assemblies.md) §3。

## 关键流程

1. `IProgramScanner.ScanInstalledPrograms()`（实例实现 `ProgramScanner`；集成性质、不单测）：
   内置来源 = 系统自带工具 + 开始菜单/桌面快捷方式（.lnk 解析经注入的 `IShortcutTargetResolver`）。
   插件来源（随包内置、可停用）= 用户 AppData、WindowsApps、注册表 App Paths 与 Uninstall、
   Program Files 顶层（`InstalledProgramScanner`）。
2. `ProgramSourceAggregator`（消费者面）按能力表顺序取全部来源（**内置在前**，其后按插件
   priority 与 plugin id），逐来源降级（单来源失败只跳过它）→ `ProgramCatalog.MergeSources`
   纯函数跨源去重与显示名升级 → 按显示名自然排序，返回纯数据 `ProgramEntry` 列表。
   文件存在性/扩展名/大小检查与垃圾过滤在各来源实现内完成。
3. `ProgramPickerViewModel`（`StarPie.Ui/ViewModels/Dialogs/`）构造注入 `IProgramScanner`、
   `IShortcutTargetResolver` 与 `IIconAssetService`：扫描与图标装配同处后台线程
   （`ProgramPickerItem` = 候选 + `IIconAssetService.GetIcon(路径)`），搜索过滤经
   `ProgramCatalog.MatchesFilter` 纯函数；手动浏览的 .lnk 解析走注入的解析契约实例。

## 扩展点

- 新程序来源 = 在 `StarPie.Plugin.Programs` 的 `InstalledProgramScanner` 加 `ScanXxx` 步骤并登记进
  `ScanInstalledPrograms`（可选来源一律走插件；宿主内置来源只保留插件缺席时也必须可用的
  系统工具与快捷方式）。第三方插件经同一 `program-source@1` 契约注册即可并列贡献。
- 新扫描/解析契约或跨模块协议 = 扩展 `StarPie.Sdk/Services/{Programs,Icons}/` 契约面
  （消费方驱动；破坏性变更按 SDK additive-only 政策）。
- 新图标资产/提取能力 → S1（宿主内核 `Icons/` 与 Ui 侧 `Services/Icons/`，见 [layout.md](layout.md)）。

## 参见 ADR

[0004](../adr/0004-dialog-service-design.md)（对话框服务/集成性质不测）、
[0015](../adr/0015-module-map-and-ownership.md)（M3/S1 三分归属 R6）、
[0016](../adr/0016-assembly-split-target-and-roadmap.md)（M3 独立模块程序集）、
[0023](../adr/0023-module-contracts-hard-boundary-and-core-narrowing.md)
（扫描/SPI 契约随 M3 下沉）、
[0027](../adr/0027-plugin-architecture-and-host-sdk-ui-split.md)（插件体系与三集形态）、
[0033](../adr/0033-plugin-service-scope-without-di-container.md)（能力表与作用域）。
