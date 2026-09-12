# 模块：对话框

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；实现/新增对话框时读本篇。

## 职责

VM 层零对话框类型引用的统一模态对话框入口。

## 组成文件

- **S6 契约（`StarPie.Sdk/Services/Dialogs/`，纯 C#，ADR-0023）**：
  `IDialogService` + 各 `ShowXxx` 的可空结果 record（`InputDialogResult`/`ColorPickResult`/
  `EyedropResult`/`FilePickResult`/`ProgramPickResult`/`IconPickResult`，命名空间
  `StarPie.Services.Dialogs` 不变）。
- **实现与界面（P1.10/#119 归并入 Ui 集，端口只在 Ui 内部）**：
  `StarPie.Ui/Services/Dialogs/DialogService.cs`、`StarPie.Ui/ViewModels/Dialogs/`、
  `StarPie.Ui/Views/Dialogs/` 与 `StarPie.Ui/Views/Controls/SpectrumCanvasBehavior.cs`
  （契约驻 `StarPie.Sdk`、实现同集，接口稳定）。依赖方向：实现消费 Sdk 契约
  （IDialogService、程序扫描/.lnk 契约与图标条目）+ Sdk.Wpf 契约面（`IIconAssetService`、
  M4 `IThemeService`）+ Host 实现（S2/S3 与图标目录/程序扫描）；注册在 Ui 组合根。

## 唯一形态（正典）

> 一个对话框 = `ViewModels/Dialogs/{Dialog}ViewModel.cs` + `Views/Dialogs/{Dialog}Window.xaml(.cs)` 一一配对；`IDialogService.ShowXxx` 内 `new {Dialog}Window(themeService, viewModel)` → `ShowDialog()` → `BuildResult()`；取消/无效返回 `null`。窗口属性差异（全屏、透明、无 Owner）是同一形态上的属性，不是第二种形态。

## 关键流程

1. `DialogService` 构造注入 `IThemeService`、`ILocalizationService`、图标资产实例服务
   `IIconAssetService`（契约驻 StarPie.Sdk.Wpf）、.lnk 解析契约 `IShortcutTargetResolver` 与程序扫描
   契约 `IProgramScanner`（后两者契约驻 StarPie.Sdk，实现与注册在组合根——ADR-0023，
   组合根不再直调静态扫描，Dialogs→扫描实现仅经契约边）；`_owner` 由 Host 在设置
   窗口创建后 `DialogService.SetOwner(MainView)` 惰性回填（[ADR-0004](../adr/0004-dialog-service-design.md)，
   化解服务↔窗口循环；SetOwner 为 public 装配面，不泄露进 `IDialogService`）。
2. `ShowXxx`：`new XxxViewModel(...)`（对话框 VM 每次新建、不注册容器）→ `new XxxWindow(theme, vm)` → `ShowDialog()` → `vm.BuildResult()`；结果 record 定义在 `StarPie.Sdk` 的 `IDialogService` 文件（如 `InputDialogResult`、`ColorPickResult`、`EyedropResult`、`FilePickResult`、`ProgramPickResult`、`IconPickResult`）。程序/图标选择器的领域数据经注入提供者获得：扫描候选经 `IProgramScanner` 注入 `ProgramPickerViewModel`（构造另注入 `IShortcutTargetResolver` 供手动浏览 .lnk 解析、注入 `IIconAssetService` 在后台线程按路径装配图标，列表项为 `ProgramPickerItem`）；图标卡片渲染与存储副作用经共享图标资产实例服务 `IIconAssetService`（`DialogService` 注入后传给选择器 VM/Window）与静态纯目录 `IconCatalog`——对话框模块不直连 Icons/Programs runtime 内部（R6/R7：经注入提供者接 S1/M3 出口；ADR-0023）。
3. 窗口 code-behind 只做：`DialogResult=true`（由 VM `IsCompleted` 驱动）与取消 `DialogResult=false`、主题应用、XAML 表达不了的标题拼接；取色器的 Win32 取像素与放大镜摆放属 [ADR-0009](../adr/0009-view-code-behind-whitelist.md) 白名单。
4. `ScreenEyedropperWindow`（全屏置顶、无 Owner）是独立 XAML Window，与其它对话框同形态；Win32 取像素与放大镜摆放留在 code-behind（ADR-0009 白名单）。
5. 系统 `OpenFileDialog`/`SaveFileDialog`/`OpenFolderDialog` 只出现在 `DialogService` 实现内部；`MessageBox` 仅允许出现在：`IDialogService` 内、`ActionExecutorService` 错误提示默认实现、`App.OnStartup` 启动致命错误（VM 与页面 View 不得出现）。

## 扩展点

新对话框按 [extending.md](extending.md)（原型 C）清单；禁止在 `IDialogService` 之外 new 对话框或新增第二种形态。新增对话框只动 `StarPie.Ui/Services|ViewModels|Views/Dialogs` 内部 + 调用方一行，不碰 Host；新增结果 record/对话框契约 = 扩展 `StarPie.Sdk` 的 Dialogs 契约（P1.3/#112 收口；实现方与消费方同在 Ui 集，端口不外泄）。

## 参见 ADR

[0004](../adr/0004-dialog-service-design.md)、[0009](../adr/0009-view-code-behind-whitelist.md)、
[0023](../adr/0023-module-contracts-hard-boundary-and-core-narrowing.md)（契约随实现方
下沉独立成集、P1.3/#112 收口入 `StarPie.Sdk`、WPF 契约件 P1.4/#113 收口入 `StarPie.Sdk.Wpf`；
Dialogs→Theme runtime 允许边清零，改经 Sdk.Wpf 契约边）。
