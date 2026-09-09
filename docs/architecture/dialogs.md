# 模块：对话框

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；实现/新增对话框时读本篇。

## 职责

VM 层零对话框类型引用的统一模态对话框入口。

## 组成文件

- **S6 契约（`StarPie.Dialogs.Contracts/`，ADR-0023/#96 起独立成集、自 Core 迁出；纯 C#）**：
  `IDialogService` + 各 `ShowXxx` 的可空结果 record（`InputDialogResult`/`ColorPickResult`/
  `EyedropResult`/`FilePickResult`/`ProgramPickResult`/`IconPickResult`，命名空间
  `StarPie.Services.Dialogs` 不变）。
- **实现与界面（独立模块程序集 `StarPie.Dialogs/`，ADR-0020/#88 B11 已落地）**：
  `Services/Dialogs/DialogService.cs`、`ViewModels/Dialogs/`、`Views/Dialogs/` 与
  `Views/Controls/SpectrumCanvasBehavior.cs`（契约与实现跨程序集，接口稳定）。依赖方向：
  Dialogs → Dialogs.Contracts（自身契约）+ Programs.Contracts（程序扫描候选经 `IProgramScanner`
  注入，M3 注册器提供实现——不引用 `StarPie.Programs` runtime）+ Icons.Contracts（图标资产/
  .lnk SPI 消费）+ Core（S2/S3/S4 共享基建）+ StarPie.Theme 允许边（窗口主题应用消费
  M4 `IThemeService`）。

## 唯一形态（正典）

> 一个对话框 = `ViewModels/Dialogs/{Dialog}ViewModel.cs` + `Views/Dialogs/{Dialog}Window.xaml(.cs)` 一一配对；`IDialogService.ShowXxx` 内 `new {Dialog}Window(themeService, viewModel)` → `ShowDialog()` → `BuildResult()`；取消/无效返回 `null`。窗口属性差异（全屏、透明、无 Owner）是同一形态上的属性，不是第二种形态。

## 关键流程

1. `DialogService` 构造注入 `IThemeService`、`ILocalizationService`、共享图标资产实例服务
   `IIconAssetService`（Icons.Contracts）、.lnk 解析契约 `IShortcutTargetResolver` 与程序扫描
   契约 `IProgramScanner`（后两者驻 Programs.Contracts，实现与注册由 M3 `ProgramsModuleRegistrar`
   下放——ADR-0020/#88：S21 委托注入归零，组合根不再直调 M3 静态扫描；ADR-0023/#96：契约随
   实现方下沉，Dialogs→Programs 仅经契约边）；`_owner` 由 Host 在设置
   窗口创建后 `DialogService.SetOwner(MainView)` 惰性回填（[ADR-0004](../adr/0004-dialog-service-design.md)，
   化解服务↔窗口循环；SetOwner 为 public 装配面，不泄露进 `IDialogService`）。
2. `ShowXxx`：`new XxxViewModel(...)`（对话框 VM 每次新建、不注册容器）→ `new XxxWindow(theme, vm)` → `ShowDialog()` → `vm.BuildResult()`；结果 record 定义在 `StarPie.Dialogs.Contracts` 的 `IDialogService` 文件（如 `InputDialogResult`、`ColorPickResult`、`EyedropResult`、`FilePickResult`、`ProgramPickResult`、`IconPickResult`）。程序/图标选择器的领域数据经注入提供者获得：扫描候选经 `IProgramScanner` 注入 `ProgramPickerViewModel`（构造另注入 `IShortcutTargetResolver` 供手动浏览 .lnk 解析）；图标卡片渲染与存储副作用经共享图标资产实例服务 `IIconAssetService`（`DialogService` 注入后传给选择器 VM/Window）与静态纯目录 `IconCatalog`——对话框模块不直连业务模块 runtime 内部（R6/R7，T3c/#67；ADR-0019/#87 S1 双形拆分；ADR-0020/#88 扫描契约化；ADR-0023/#96 契约下沉）。
3. 窗口 code-behind 只做：`DialogResult=true`（由 VM `IsCompleted` 驱动）与取消 `DialogResult=false`、主题应用、XAML 表达不了的标题拼接（[ADR-0010](../adr/0010-localization-copy-principles.md) 例外）；取色器的 Win32 取像素与放大镜摆放属 [ADR-0009](../adr/0009-view-code-behind-whitelist.md) 白名单。
4. `ScreenEyedropperWindow`（全屏置顶、无 Owner）是独立 XAML Window，与其它对话框同形态；Win32 取像素与放大镜摆放留在 code-behind（ADR-0009 白名单）。
5. 系统 `OpenFileDialog`/`SaveFileDialog`/`OpenFolderDialog` 只出现在 `DialogService` 实现内部；`MessageBox` 仅允许出现在：`IDialogService` 内、`ActionExecutorService` 错误提示默认实现、`App.OnStartup` 启动致命错误（VM 与页面 View 不得出现）。

## 扩展点

新对话框按 [extending.md](extending.md)（原型 C）清单；禁止在 `IDialogService` 之外 new 对话框或新增第二种形态。新增对话框只动 `StarPie.Dialogs` 内部 + 调用方一行，不碰 Host；新增结果 record/对话框契约 = 扩展 `StarPie.Dialogs.Contracts`（消费方经契约边，模块 runtime 互引清零）。

## 参见 ADR

[0004](../adr/0004-dialog-service-design.md)、[0009](../adr/0009-view-code-behind-whitelist.md)、[0010](../adr/0010-localization-copy-principles.md)、
[0020](../adr/0020-dialogs-assembly-and-m3-scanner-contract.md)（S6 实现程序集化 + 扫描契约收口）、
[0023](../adr/0023-module-contracts-hard-boundary-and-core-narrowing.md)（#96：S6 契约随实现方下沉 Dialogs.Contracts）。
