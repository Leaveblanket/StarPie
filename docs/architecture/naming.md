# 命名与映射

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；新增类型/页面/对话框前核对命名与映射表。

## 命名规则

| 类型 | 规则 | 示例 |
|---|---|---|
| 服务接口/实现 | `IXxxService` / `XxxService` | `IDialogService` / `DialogService` |
| 页面 VM | `{Domain}SettingsViewModel`（按设置域） | `BehaviorSettingsViewModel` |
| 页面 View | `{Page}Page`（按导航区块） | `TriggerSettingsPage` |
| 对话框 VM / Window | `{Dialog}ViewModel` / `{Dialog}Window`（**必须同名配对**） | `ColorPickerViewModel` + `ColorPickerWindow` |
| 对话框结果 | `{Dialog}Result`（可空 record，定义在接口文件） | `ColorPickResult` |
| IMessenger 消息 | `XxxRequestedMessage` / `XxxChangedMessage` / `XxxImportedMessage` / 单例空载体 | `DebouncedSaveRequestedMessage` |
| 跨层通知载体 | `NoticeKind` / `NoticeRequest`（`Notices.cs`） | — |
| 转换器 | `XxxToYyyConverter` | `HexToBrushConverter` |
| 渲染器 | `XxxRenderer`（样式） / `XxxRenderer`（预览） | `GlassmorphismRenderer`、`WheelPreviewRenderer` |
| 测试 | `{被测类型}Tests.cs`（平铺于测试工程根） | `GestureEngineTests.cs` |

## 页面映射表（正典）

| ViewModel（设置域命名） | View（导航区块命名） | 说明 |
|---|---|---|
| `BehaviorSettingsViewModel` | `TriggerSettingsPage` | 触发与场景 |
| `AppearanceSettingsViewModel` | `AppearanceSettingsPage` | 外观与形态 |
| `ProfileListViewModel` | `GesturesSettingsPage` | 手势与动作 |
| `GeneralSettingsViewModel` | `AdvancedSettingsPage` | 高级与系统 |
| `AboutViewModel` | `AboutSettingsPage` | 关于与更新 |

规则：VM 名与页面名**允许错位**（VM 按领域、View 按区块），但**新增页面必须在 `MainViewModel` 导航项 + `MainView.xaml` DataTemplate + 本表各登记一行**；映射表是唯一事实来源（接线流程见 [navigation.md](navigation.md)）。

## 对话框配对与例外

正典配对（同名）：`ColorPickerViewModel ↔ ColorPickerWindow`、`IconPickerViewModel ↔ IconPickerWindow`、`ProgramPickerViewModel ↔ ProgramPickerWindow`、`ScreenEyedropperViewModel ↔ ScreenEyedropperWindow`。唯一形态与实现流程见 [dialogs.md](dialogs.md)。

例外表（新代码不得新增同类）：

| 例外 | 说明 |
|---|---|
| `InputViewModel ↔ InputDialog` | 遗留窗口名未对齐；仅保留，不改名 |
