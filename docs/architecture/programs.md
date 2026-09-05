# 模块：程序扫描与目录

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及程序选择器数据来源与已安装
> 程序扫描时读本篇。

## 职责

程序选择器的候选来源扫描、跨源去重/过滤与快捷方式目标解析（.lnk → 真实路径/图标位置）。
图标资产（矢量清单/SVG 键目录/文件图标提取）已拆归 S1 共享「图标资产」出口，见
[layout.md](layout.md)。

## 组成文件

`Services/Programs/`：`ProgramScanner`（IO 扫描）、`ProgramCatalog`（纯合并/去重）、
`ShortcutResolver`（M3 快捷方式解析出口）。共享图标资产出口 `IconAssets` 与矢量条目
`VectorIconItem` 居 `Services/Icons/`（S1，R6 三分，T3a–T3d/#65–#68 收口完成），见
[layout.md](layout.md)。

## 关键流程

1. `ProgramScanner.ScanInstalledPrograms()`（静态、集成性质、不单测）：八个来源（系统自带工具、
   开始菜单/桌面快捷方式、用户 AppData、WindowsApps、注册表 App Paths/Uninstall、Program Files
   顶层）→ 垃圾过滤/存在性检查 → `ProgramCatalog.MergeSources` 纯函数跨源去重与显示名升级 →
   `IconAssets.GetIcon` 补图标（S1 出口）→ 自然排序返回 `ProgramEntry` 列表；.lnk 来源经
   `ShortcutResolver.ResolveShortcutTarget`（M3 出口）解析。
2. `ProgramPickerViewModel` 构造时注入扫描函数委托（`ProgramScanner.ScanInstalledPrograms` 由
   组合根登记并注入 `DialogService` 后转发，不直接依赖静态类；可测性，对话框见
   [dialogs.md](dialogs.md)）；手动浏览的 .lnk 解析直接走 M3 `ShortcutResolver` 出口。

## 扩展点

- 新程序来源 = 在 `ProgramScanner` 加 `ScanXxx` 步骤并登记进 `ScanInstalledPrograms`。
- 新图标资产/提取能力 → S1（`Services/Icons/`，见 [layout.md](layout.md)）。

## 参见 ADR

[0004](../adr/0004-dialog-service-design.md)（对话框服务/集成性质不测）、
[0015](../adr/0015-module-map-and-ownership.md)（M3/S1 三分归属 R6）。
