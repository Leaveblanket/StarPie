# 模块：程序扫描与图标

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；涉及程序选择器数据来源时读本篇。

## 职责

程序选择器的候选来源扫描、去重与图标提取。

## 组成文件

`Services/Programs/`：`ProgramScanner`、`ProgramCatalog`、`ShortcutResolver`（M3 快捷方式解析
出口，T3a 扩展）、`IconHelper`（旧入口，T3a 起委托新出口）、`VectorIconItem`（S1 矢量图标条目，
T3a 过渡期仍居此命名空间）。共享图标资产出口 `IconAssets` 见 [layout.md](layout.md)（`Services/Icons/`）。

> T3a 扩展注记（#65，过渡态）：R6 三分的新出口已落地——图标资产 →
> `WinPieGestures.Services.Icons.IconAssets`（S1）、轮盘几何 →
> `WinPieGestures.Services.Wheel.WheelGeometry`（M2，见 [wheel.md](wheel.md)）、快捷方式解析 →
> `WinPieGestures.Services.Programs.ShortcutResolver`（M3）；`IconHelper` 保留 public 签名并全量
> 委托，现有调用方零改动。物理目录与模块归属的最终收口在 T3b–T3d，modules.md §7 差异届时清零。

## 关键流程

1. `ProgramScanner.ScanInstalledPrograms()`（静态、集成性质、不单测）：八个来源（系统自带工具、开始菜单/桌面快捷方式、用户 AppData、WindowsApps、注册表 App Paths/Uninstall、Program Files 顶层）→ 垃圾过滤/存在性检查 → `ProgramCatalog.MergeSources` 纯函数跨源去重与显示名升级 → `IconHelper.GetIcon` 补图标 → 自然排序返回 `ProgramEntry` 列表。
2. `ProgramPickerViewModel` 构造时注入扫描函数委托（`ProgramScanner.ScanInstalledPrograms`），不直接依赖静态类（可测性；对话框见 [dialogs.md](dialogs.md)）。

## 扩展点

新来源 = 在 `ProgramScanner` 加 `ScanXxx` 步骤并登记进 `ScanInstalledPrograms`。

## 参见 ADR

[0004](../adr/0004-dialog-service-design.md)（对话框服务/集成性质不测）。
