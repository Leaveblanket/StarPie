# 模块：对话框

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；实现/新增对话框时读本篇。

## 职责

VM 层零对话框类型引用的统一模态对话框入口。

## 组成文件

`Services/Dialogs/`（`IDialogService`/`DialogService` + 结果 record）、`ViewModels/Dialogs/`、`Views/Dialogs/`。

## 唯一形态（正典）

> 一个对话框 = `ViewModels/Dialogs/{Dialog}ViewModel.cs` + `Views/Dialogs/{Dialog}Window.xaml(.cs)` 一一配对；`IDialogService.ShowXxx` 内 `new {Dialog}Window(themeService, viewModel)` → `ShowDialog()` → `BuildResult()`；取消/无效返回 `null`。窗口属性差异（全屏、透明、无 Owner）是同一形态上的属性，不是第二种形态。

## 关键流程

1. `DialogService` 构造注入 `IThemeService`、`ILocalizationService` 与 M3 程序扫描委托（组合根以
   `ProgramScanner.ScanInstalledPrograms` 登记，T3c/#67）；`_owner` 由组合根在设置窗口创建后
   `SetOwner(MainView)` 惰性回填（[ADR-0004](../adr/0004-dialog-service-design.md)，化解服务↔窗口循环）。
2. `ShowXxx`：`new XxxViewModel(...)`（对话框 VM 每次新建、不注册容器）→ `new XxxWindow(theme, vm)` → `ShowDialog()` → `vm.BuildResult()`；结果 record 定义在 `IDialogService` 文件（如 `InputDialogResult`、`ColorPickResult`、`EyedropResult`、`FilePickResult`、`ProgramPickResult`、`IconPickResult`）。程序/图标选择器的领域数据经注入提供者获得：扫描候选由构造注入委托转发给 `ProgramPickerViewModel`，图标资产默认实现引用 S1 `IconAssets` 出口——对话框模块不直连业务模块静态内部（R6/R7，T3c/#67）。
3. 窗口 code-behind 只做：`DialogResult=true`（由 VM `IsCompleted` 驱动）与取消 `DialogResult=false`、主题应用、XAML 表达不了的标题拼接（[ADR-0010](../adr/0010-localization-copy-principles.md) 例外）；取色器的 Win32 取像素与放大镜摆放属 [ADR-0009](../adr/0009-view-code-behind-whitelist.md) 白名单。
4. `ScreenEyedropperWindow`（全屏置顶、无 Owner）是独立 XAML Window，与其它对话框同形态；Win32 取像素与放大镜摆放留在 code-behind（ADR-0009 白名单）。
5. 系统 `OpenFileDialog`/`SaveFileDialog`/`OpenFolderDialog` 只出现在 `DialogService` 实现内部；`MessageBox` 仅允许出现在：`IDialogService` 内、`ActionExecutorService` 错误提示默认实现、`App.OnStartup` 启动致命错误（VM 与页面 View 不得出现）。

## 扩展点

新对话框按 [extending.md](extending.md)（原型 C）清单；禁止在 `IDialogService` 之外 new 对话框或新增第二种形态。

## 参见 ADR

[0004](../adr/0004-dialog-service-design.md)、[0009](../adr/0009-view-code-behind-whitelist.md)、[0010](../adr/0010-localization-copy-principles.md)。
