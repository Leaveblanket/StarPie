# 模块：程序扫描与目录

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及程序选择器数据来源与已安装
> 程序扫描时读本篇。

## 职责

程序选择器的候选来源扫描、跨源去重/过滤与快捷方式目标解析（.lnk → 真实路径/图标位置）。
图标资产（矢量清单/SVG 键目录/文件图标提取）已拆归 S1 共享「图标资产」出口，见
[layout.md](layout.md)。

## 组成文件

`StarPie.Programs/Services/Programs/`（**B4/#77 起独立模块程序集 `StarPie.Programs`**）：
`ProgramScanner`（IO 扫描编排；ADR-0020/#88 起实例实现 Core 契约 `IProgramScanner`，构造注入
扫描所需 Core 契约）、`ShortcutResolver`（M3 快捷方式解析出口；ADR-0019/#87 起实例实现 Core
契约 `IShortcutTargetResolver`）与模块注册器 `StarPie.Programs/Modules/ProgramsModuleRegistrar.cs`
（`RegisterServices`，注册 `IShortcutTargetResolver→ShortcutResolver` 与
`IProgramScanner→ProgramScanner`；M3 无导航页故无 `RegisterNavigation`）。纯规则目录
`ProgramCatalog` 与纯数据 `ProgramEntry` 已上提共享内核 `StarPie.Core/Services/Programs/`
（ADR-0020/#88：第二消费方族判据——M3 扫描与 S6 程序选择器过滤共用，命名空间
`StarPie.Services.Programs` 不变）。共享图标资产居 Core
`Services/Icons/`（S1，R6 三分，T3a–T3d/#65–#68 收口；ADR-0019/#87 双形拆为静态
`IconCatalog` + 实例 `IIconAssetService`/`IconAssetService`，`.lnk` 契约
`IShortcutTargetResolver` 亦驻此），见 [layout.md](layout.md)。Host exe 与测试工程**显式**
`ProjectReference` `StarPie.Programs`（slnx 登记，不依赖传递引用）；**M3 → Core 单向**
（ADR-0019/#87 + ADR-0020/#88 边界收口；不引用 Host/其它业务模块，程序集依赖方向见
[assemblies.md](assemblies.md) §3）。

## 关键流程

1. `IProgramScanner.ScanInstalledPrograms()`（实例实现 `ProgramScanner`，构造注入
   `IIconAssetService`/`IShortcutTargetResolver`；集成性质、不单测）：八个来源（系统自带工具、
   开始菜单/桌面快捷方式、用户 AppData、WindowsApps、注册表 App Paths/Uninstall、Program Files
   顶层）→ 垃圾过滤/存在性检查 → `ProgramCatalog.MergeSources` 纯函数（Core）跨源去重与
   显示名升级 → 经**注入的 Core 契约补图标与解析 .lnk**（`IIconAssetService.GetIcon` /
   `IShortcutTargetResolver`，M3 → Core 单向，ADR-0019/#87 + ADR-0020/#88）→ 自然排序返回
   `ProgramEntry` 列表。
2. `ProgramPickerViewModel`（`StarPie.Dialogs`，ADR-0020/#88）构造时注入 `IProgramScanner` 与
   `IShortcutTargetResolver`（契约驻 Core、实现与注册由 M3 `ProgramsModuleRegistrar` 下放——
   消费方不直接依赖 M3 静态类/内部，可测性见 [dialogs.md](dialogs.md)）；手动浏览的 .lnk
   解析走注入的解析契约实例（`ShortcutResolver` 实现 Core 契约，ADR-0019/#87；
   `IconAssets.ResolveShortcutTarget` 静态回填缝已删除，见 [host.md](host.md)）。

## 扩展点

- 新程序来源 = 在 `ProgramScanner` 加 `ScanXxx` 步骤并登记进 `ScanInstalledPrograms`。
- 新图标资产/提取能力 → S1（`Services/Icons/`，见 [layout.md](layout.md)）。

## 参见 ADR

[0004](../adr/0004-dialog-service-design.md)（对话框服务/集成性质不测）、
[0015](../adr/0015-module-map-and-ownership.md)（M3/S1 三分归属 R6）、
[0016](../adr/0016-assembly-split-target-and-roadmap.md)（B4：M3 独立模块程序集）、
[0020](../adr/0020-dialogs-assembly-and-m3-scanner-contract.md)（扫描契约收口与 ProgramEntry/
ProgramCatalog 上提）。
