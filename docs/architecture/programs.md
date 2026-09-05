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
>
> T3c（#67，编辑与对话框侧接线迁移）：`ProgramScanner` 补图标改走 `IconAssets.GetIcon`（S1）、
> 快捷方式解析改走 `ShortcutResolver`（M3 出口）；对话框侧程序选择器经组合根注入的扫描委托
> 取得候选，图标选择器/动作编辑改走 `IconAssets`（S1）。旧入口 `IconHelper` 现仅余定义本身、
> 委托一致性测试与轮盘渲染路径引用（轮盘侧由 #66/#68 收口，本票不改）。

## 关键流程

1. `ProgramScanner.ScanInstalledPrograms()`（静态、集成性质、不单测）：八个来源（系统自带工具、开始菜单/桌面快捷方式、用户 AppData、WindowsApps、注册表 App Paths/Uninstall、Program Files 顶层）→ 垃圾过滤/存在性检查 → `ProgramCatalog.MergeSources` 纯函数跨源去重与显示名升级 → `IconAssets.GetIcon` 补图标（S1 出口，T3c/#67）→ 自然排序返回 `ProgramEntry` 列表；.lnk 来源经 `ShortcutResolver.ResolveShortcutTarget`（M3 出口）解析。
2. `ProgramPickerViewModel` 构造时注入扫描函数委托（`ProgramScanner.ScanInstalledPrograms` 由组合根登记并注入 `DialogService` 后转发，T3c/#67），不直接依赖静态类（可测性；对话框见 [dialogs.md](dialogs.md)）；手动浏览的 .lnk 解析直接走 M3 `ShortcutResolver` 出口。

## 扩展点

新来源 = 在 `ProgramScanner` 加 `ScanXxx` 步骤并登记进 `ScanInstalledPrograms`。

## 参见 ADR

[0004](../adr/0004-dialog-service-design.md)（对话框服务/集成性质不测）。
