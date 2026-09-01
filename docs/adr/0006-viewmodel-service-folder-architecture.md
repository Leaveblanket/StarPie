# ViewModels/Services 文件夹与命名空间架构

T19 后 `ViewModels` 与 `Services` 都是扁平目录，分别积累到 15 与 33 个文件，命名空间无法表达职责，新文件归属只能靠个人判断。决定按功能域划分子目录，命名空间与目录同步；服务接口与实现同目录存放，组合根仍集中在 `Composition`，本轮不拆多类型文件。

## Status

accepted

## 结构

```text
Services/
  Actions/
  Configuration/
  Dialogs/
  Gestures/
  Localization/
  Messages/
  Navigation/
  Programs/
  Shell/

ViewModels/
  Dialogs/
  Gestures/
  Navigation/
  Pages/
  Wheel/
```

`IXXXService` 与其实现放在同一功能目录，例如 `IActionExecutorService` / `ActionExecutorService` 位于 `Services/Actions`。页面 ViewModel 位于 `ViewModels/Pages`；导航、对话框、轮盘运行时与手势编辑项 ViewModel 分别位于 `Navigation`、`Dialogs`、`Wheel`、`Gestures`。

## Considered Options

- 按抽象层拆 `Abstractions` / `Implementations`：否决，接口与实现同功能域维护成本更低，且 Q2 已明确按功能拆分。
- 只移动文件、保留扁平命名空间：否决，目录与命名空间会长期背离。
- 本轮拆分多类型文件：延后，先稳定文件边界再单独重构。

## Consequences

- 新增类型按功能目录归属；服务接口与实现同目录。
- 新增功能时同步更新 `GlobalUsings.cs` 或显式 `using`，以引用子命名空间。
- XAML 的 VM `DataType` 映射使用对应 `ViewModels.Pages` / `ViewModels.Gestures` 等命名空间。
- 不改变 DI 注册语义；`Composition` 仍是唯一装配与解析点，延续 ADR-0005。
