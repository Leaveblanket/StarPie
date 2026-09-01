# Views 文件夹与命名空间架构

延续 ADR-0006 的功能目录原则整理 `Views`。原来 `Views` 根目录同时承载对话框窗口、轮盘窗口和共享样式，命名空间 `WinPieGestures.Views` 无法表达这些窗口的角色；决定把根目录下的视图文件归入功能子目录，并同步命名空间，保持 `Navigation`、`Pages`、`Renderers` 现有目录不变。

## Status

accepted

## 结构

```text
Views/
  Dialogs/
  Navigation/
  Pages/
  Renderers/
  Styles/
  Wheel/
```

对话框窗口位于 `Views/Dialogs`（`ColorPickerWindow`、`IconPickerWindow`、`InputDialog`、`ProgramPickerWindow`），轮盘窗口位于 `Views/Wheel`，共享设置样式位于 `Views/Styles`。带代码后置的 XAML 使用对应命名空间，例如 `WinPieGestures.Views.Dialogs.ColorPickerWindow`、`WinPieGestures.Views.Wheel.RadialWindow`；`SettingsStyles.xaml` 是无代码类型的资源字典，只移动目录不引入命名空间。

## Considered Options

- 保留所有窗口与样式在 `Views` 根目录：否决，根目录仍会是新视图的默认落点，分类信息丢失。
- 把共享样式留在 `Pages` 或 `Navigation`：否决，样式被多个页面共享，应放在独立 `Styles` 目录。

## Consequences

- 新增视图按角色归属 `Dialogs`、`Wheel`、`Navigation`、`Pages` 或 `Renderers`；共享样式放 `Styles`。
- `x:Class` 与代码后置命名空间必须同步更新，避免 WPF partial class 失配。
- 资源引用路径更新为 `../Styles/SettingsStyles.xaml`。
- `GlobalUsings.cs` 导入 `WinPieGestures.Views.Dialogs`、`Navigation`、`Pages`、`Renderers`、`Wheel` 子命名空间。
