# 模块：程序扫描与目录

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及程序选择器数据来源与已安装
> 程序扫描时读本篇。

## 职责

程序选择器的候选来源扫描、跨源去重/过滤与快捷方式目标解析（.lnk → 真实路径/图标位置）。
图标资产（矢量清单/SVG 键目录/文件图标提取）已拆归 S1 共享「图标资产」出口，见
[layout.md](layout.md)。

## 组成文件

- **契约（`StarPie.Programs.Contracts/`，ADR-0023/#96 起独立成集，自 Core 迁出）**：
  `Services/Programs/IProgramScanner.cs`、`ProgramEntry.cs`、`ProgramCatalog.cs`（命名空间
  `StarPie.Services.Programs` 不变）+ `Services/Icons/IShortcutTargetResolver.cs`（SPI 随实现方
  M3 下沉，命名空间 `StarPie.Services.Icons` 不变）。契约程序集按需引用 WPF（`ProgramEntry.IconSource`
  = `ImageSource`），零 ProjectReference。
- **实现与注册（`StarPie.Programs/`，B4/#77 起独立模块程序集）**：
  `Services/Programs/ProgramScanner.cs`（IO 扫描编排；ADR-0020/#88 起实例实现契约
  `IProgramScanner`，构造注入 Icons.Contracts 的 `IIconAssetService` 与 Programs.Contracts 的
  `IShortcutTargetResolver`）、`Services/Programs/ShortcutResolver.cs`（M3 快捷方式解析出口；
  ADR-0019/#87 起实例实现 SPI `IShortcutTargetResolver`）与
  `Modules/ProgramsModuleRegistrar.cs`（`RegisterServices`，注册 `IShortcutTargetResolver→ShortcutResolver`
  与 `IProgramScanner→ProgramScanner`；M3 无导航页故无 `RegisterNavigation`）。

Host exe、测试工程与消费方（Dialogs runtime 程序扫描注入、Icons runtime SPI 消费）**显式**
`ProjectReference` `StarPie.Programs.Contracts`；Host/Tests 另显式引用 `StarPie.Programs`
（slnx 登记，不依赖传递引用）。**M3 → Programs.Contracts + Icons.Contracts 单向**（ADR-0019/#87 +
ADR-0020/#88 契约化 + ADR-0023/#96 契约下沉后，M3 runtime 不再引用共享内核 Core，也不引用
Host/其它业务模块 runtime；程序集依赖方向见 [assemblies.md](assemblies.md) §3）。

## 关键流程

1. `IProgramScanner.ScanInstalledPrograms()`（实例实现 `ProgramScanner`，构造注入
   `IIconAssetService`/`IShortcutTargetResolver`；集成性质、不单测）：八个来源（系统自带工具、
   开始菜单/桌面快捷方式、用户 AppData、WindowsApps、注册表 App Paths/Uninstall、Program Files
   顶层）→ 垃圾过滤/存在性检查 → `ProgramCatalog.MergeSources` 纯函数（Programs.Contracts）
   跨源去重与显示名升级 → 经**注入的契约补图标与解析 .lnk**（`IIconAssetService.GetIcon` /
   `IShortcutTargetResolver`，Icons.Contracts / Programs.Contracts 契约边）→ 自然排序返回
   `ProgramEntry` 列表。
2. `ProgramPickerViewModel`（`StarPie.Dialogs`，ADR-0020/#88）构造时注入 `IProgramScanner` 与
   `IShortcutTargetResolver`（契约驻 Programs.Contracts、实现与注册由 M3 `ProgramsModuleRegistrar`
   下放——Dialogs → Programs 仅经契约边，不依赖 M3 runtime 内部，可测性见 [dialogs.md](dialogs.md)）；
   手动浏览的 .lnk 解析走注入的解析契约实例（`ShortcutResolver` 实现 SPI，ADR-0019/#87；
   `IconAssets.ResolveShortcutTarget` 静态回填缝已删除，见 [host.md](host.md)）。

## 扩展点

- 新程序来源 = 在 `ProgramScanner` 加 `ScanXxx` 步骤并登记进 `ScanInstalledPrograms`。
- 新扫描/解析契约或跨模块协议 = 扩展 `StarPie.Programs.Contracts`（消费方驱动，模块 runtime 互引清零）。
- 新图标资产/提取能力 → S1（`Services/Icons/`，见 [layout.md](layout.md)）。

## 参见 ADR

[0004](../adr/0004-dialog-service-design.md)（对话框服务/集成性质不测）、
[0015](../adr/0015-module-map-and-ownership.md)（M3/S1 三分归属 R6）、
[0016](../adr/0016-assembly-split-target-and-roadmap.md)（B4：M3 独立模块程序集）、
[0020](../adr/0020-dialogs-assembly-and-m3-scanner-contract.md)（扫描契约收口与 ProgramEntry/
ProgramCatalog 上提）、[0023](../adr/0023-module-contracts-hard-boundary-and-core-narrowing.md)
（#96：扫描/SPI 契约随 M3 下沉 Programs.Contracts）。
