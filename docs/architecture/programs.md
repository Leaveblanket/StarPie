# 模块：程序扫描与目录

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及程序选择器数据来源与已安装
> 程序扫描时读本篇。

## 职责

程序选择器的候选来源扫描、跨源去重/过滤与快捷方式目标解析（.lnk → 真实路径/图标位置）。
图标资产（矢量清单/SVG 键目录/文件图标提取）已拆归 S1 共享「图标资产」出口，见
[layout.md](layout.md)。

## 组成文件

`StarPie.Programs/Services/Programs/`（**B4/#77 起独立模块程序集 `StarPie.Programs`**）：
`ProgramScanner`（IO 扫描）、`ProgramCatalog`（纯合并/去重，含 `ProgramEntry` 不可变记录）、
`ShortcutResolver`（M3 快捷方式解析出口）。共享图标资产出口 `IconAssets` 与矢量条目
`VectorIconItem` 居 Core `Services/Icons/`（S1，R6 三分，T3a–T3d/#65–#68 收口完成），见
[layout.md](layout.md)。Host exe 与测试工程**显式** `ProjectReference` `StarPie.Programs`
（slnx 登记，不依赖传递引用）；M3 程序集**零共享内核(Core)/Host 依赖**（程序集依赖方向见
[assemblies.md](assemblies.md) §3）。

## 关键流程

1. `ProgramScanner.ScanInstalledPrograms(iconProvider)`（静态、集成性质、不单测）：八个来源（系统自带工具、
   开始菜单/桌面快捷方式、用户 AppData、WindowsApps、注册表 App Paths/Uninstall、Program Files
   顶层）→ 垃圾过滤/存在性检查 → `ProgramCatalog.MergeSources` 纯函数跨源去重与显示名升级 →
   经**注入的图标委托补图标**（组合根以 Core S1 `IconAssets.GetIcon` 传入；M3 不直引 Core）→
   自然排序返回 `ProgramEntry` 列表；.lnk 来源经
   `ShortcutResolver.ResolveShortcutTarget`（M3 出口）解析。
2. `ProgramPickerViewModel` 构造时注入扫描函数委托（组合根登记
   `() => ProgramScanner.ScanInstalledPrograms(IconAssets.GetIcon)` 并注入 `DialogService`
   后转发——消费方不直接依赖 M3 静态类/内部，可测性见 [dialogs.md](dialogs.md)）；手动浏览的
   .lnk 解析直接走 M3 `ShortcutResolver` 出口。S1 `IconAssets.ResolveShortcutTarget` 的 .lnk
   解析回填缝仍由组合根装配前以 M3 `ShortcutResolver.ResolveShortcutTarget` 回填
   （Host→Programs 显式引用，见 [host.md](host.md)）。

## 扩展点

- 新程序来源 = 在 `ProgramScanner` 加 `ScanXxx` 步骤并登记进 `ScanInstalledPrograms`。
- 新图标资产/提取能力 → S1（`Services/Icons/`，见 [layout.md](layout.md)）。

## 参见 ADR

[0004](../adr/0004-dialog-service-design.md)（对话框服务/集成性质不测）、
[0015](../adr/0015-module-map-and-ownership.md)（M3/S1 三分归属 R6）、
[0016](../adr/0016-assembly-split-target-and-roadmap.md)（B4：M3 独立模块程序集）。
