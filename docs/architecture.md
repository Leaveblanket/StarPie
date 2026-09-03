# WinPieGestures 开发规范

## 1. 技术栈

- .NET 8、WPF、CommunityToolkit.Mvvm、Microsoft.Extensions.DependencyInjection。
- MVVM 仅使用 CommunityToolkit.Mvvm；DI 仅在 `Composition.cs` 组合。
- 页面 VM 单元测试直接构造并注入依赖；不从容器解析测试对象。
- `releases/` 为旧版本，不修改、不作为当前实现依据。

## 2. 目录

```text
WinPieGestures/
├── App.xaml / App.xaml.cs      # 宿主生命周期
├── Composition.cs              # DI 组合根
├── Models/                     # 纯配置模型
├── Services/                   # 服务与副作用
├── ViewModels/                 # 状态、命令、消息
└── Views/                      # XAML、样式、渲染器
```

依赖方向：

```text
App / Composition
      |
      v
ViewModels ---> Views
      |
      v
Services ---> Models
```

## 3. 分层规范

### 3.1 App 与 Composition

- `App.xaml.cs` 只处理单实例、异常、启动、退出和资源释放。
- `Composition.cs` 是唯一 DI 组合根；服务和页面 VM在此注册。
- 启动顺序：加载配置 → 启动鼠标钩子 → 解析 VM → 创建窗口/托盘 → 显示窗口。
- 退出前冲刷挂起保存；不得恢复 `StartupUri`。

### 3.2 Models

- 只放纯 POCO：`AppConfig`、`WheelProfile`、`ActionItem`、`CustomColorPreset`。
- 不引用 WPF、服务、命令或消息。
- 新字段必须有默认值；保持 `config.json` 向后兼容。

### 3.3 Services

- 服务负责可注入、可 mock 的副作用；接口与实现同目录。
- 服务只由 Composition 注册；View/ViewModel 不自行 `new` 服务或使用服务定位器。
- Win32 静态工具仅限无状态、无需 mock 的调用，并记录原因。

### 3.4 ViewModels

- 使用 `ObservableObject`、`[ObservableProperty]`、`[RelayCommand]`。
- 页面 VM 注册为单例；轮盘 VM 按手势创建，不注册为单例。
- 仅暴露可观察状态、命令和必要消息；不得引用 WPF 类型，不暴露临时 `event Action`。
- 状态传输：View 通过 `DataContext`/`Binding` 读取；可编辑值使用 `Mode=TwoWay`；VM 使用 `INotifyPropertyChanged`（本项目使用 `ObservableObject`）。
- 用户动作：使用 `ICommand`；`Button` 绑定 `Command`，不得在代码后置中调用 `Vm.Command.Execute(...)`。
- 跨 VM/页面协调：使用不可变 `IMessenger` 消息；同页状态不得用 messenger 替代绑定。
- 副作用经注入服务或委托编排；VM 不直接持有 `Window`、`MessageBox`、文件对话框等 WPF 类型。

官方 API：

- [WPF data binding overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/data-binding-overview)
- [FrameworkElement.DataContext](https://learn.microsoft.com/en-us/dotnet/api/system.windows.frameworkelement.datacontext)
- [WPF commanding overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/commanding-overview)
- [ICommandSource](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.icommandsource)
- [RelayCommand generator](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/relaycommand)
- [ObservableProperty generator](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty)
- [MVVM Toolkit messenger](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/messenger)

### 3.5 Views

- XAML/View 负责布局、控件树、样式、模板、资源、动画和可视状态。
- 代码后置仅处理本地化、主题、Canvas 绘制、鼠标坐标、生命周期等纯 View 效果。
- 不在 View 中编排业务、写配置、调用服务、处理文件/注册表或决定领域状态。
- 页面通过 DataTemplate 映射 VM，必须无参构造，不注册进容器。
- 页面卸载时成对取消静态事件和 messenger 订阅。
- WPF 事件允许保留，但只能处理纯 UI 细节；不得调用 VM 方法、服务或命令作为业务入口。参见 [Routed events overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/events/routed-events-overview)。
- 没有 `Command` 属性的控件优先使用属性绑定；只有无等价绑定且仍属纯 UI 适配时才使用行为/附加属性。

`AdvancedSettingsPage` 绑定规范：

- 导入、导出、提权、内存整理按钮绑定 VM 命令；当前提权和内存整理动作需先在 VM 中生成命令。
- `AutoStartCheckBox.IsChecked` 双向绑定 `AutoStartEnabled`；注册表写入和保存请求放在 VM 属性变更回调。
- `LanguageComboBox` 设置 `SelectedValuePath="Tag"`，双向绑定可写的 `LanguageCode`；语言切换和持久化放在 VM。
- `UacWarningCard.Visibility` 绑定 VM 布尔状态并使用转换器。
- `MessageBox` 仅可作为 View 显示适配；副作用和提示内容由 VM/服务决定。

## 4. 目录与命名

```text
Services/{Feature}/
ViewModels/Pages/{领域}SettingsViewModel.cs
ViewModels/Dialogs/{Dialog}ViewModel.cs
ViewModels/Navigation/
ViewModels/Gestures/
ViewModels/Wheel/
Views/Pages/{页面}Page.xaml
Views/Dialogs/{Dialog}Window.xaml
Views/Controls/
Views/Converters/
Views/Renderers/
Views/Styles/
```

- 服务：`IXxxService` / `XxxService`。
- 页面 VM：`XxxSettingsViewModel`；页面 View：`XxxSettingsPage`。
- 消息：`XxxRequestedMessage`、`XxxChangedMessage`、`XxxImportedMessage`。
- 转换器：`XxxToYyyConverter`；渲染器：`XxxRenderer`。
- 消息放 `Services/Messages/Messages.cs`；带数据消息使用不可变 record/class。

页面映射：

| ViewModel | View |
|---|---|
| `BehaviorSettingsViewModel` | `TriggerSettingsPage` |
| `AppearanceSettingsViewModel` | `AppearanceSettingsPage` |
| `ProfileListViewModel` | `GesturesSettingsPage` |
| `GeneralSettingsViewModel` | `AdvancedSettingsPage` |
| `AboutViewModel` | `AboutSettingsPage` |

## 5. 关键流程

### 5.1 导航

1. `MainViewModel` 创建导航项和目标 VM 类型。
2. `NavigationService<T>` 从容器解析页面 VM。
3. 写入 `NavigationStore.CurrentViewModel`。
4. `MainView` 的 `ContentControl` 通过 DataTemplate 显示 View。

### 5.2 保存

1. VM 修改运行态 `AppConfig`。
2. 发送 `DebouncedSaveRequestedMessage.Instance`。
3. `SettingsSaveOrchestrator` 通过 `ISaveDebouncer` 延迟保存。
4. 显式保存、隐藏、退出和导入前调用 `FlushPendingSave()`。

### 5.3 对话框

1. VM 仅依赖 `IDialogService`。
2. `DialogService` 创建 View、设置 Owner 并返回可空结果 record。
3. 取消或无效输入返回 `null`。
4. VM 不暴露 Window、MessageBox 或文件对话框类型。

### 5.4 手势与动作

1. `MouseHook` → `GestureController` → `GestureEngine`。
2. `GestureEngine` 只负责纯手势判定并返回 `GestureReleaseResult`。
3. `GestureController` 通过 `IActionExecutorService` 执行副作用。
4. `WheelFactory` 在 UI 线程创建 `WheelViewModel` 和 `RadialWindow`。

### 5.5 渲染

- `RadialWindow` 观察 `WheelViewModel` 状态并绘制。
- `IRadialStyleRenderer` 和 `StyleRendererFactory` 负责轮盘样式。
- `WheelPreviewRenderer` 负责外观页 Canvas 预览。

## 6. 禁止事项

- 不得在 ViewModel 使用 WPF 表现类型或暴露临时事件。
- 不得在 View 中写业务、配置、服务调用或副作用。
- 不得在使用处创建 ServiceCollection、服务实例或静态服务定位器。
- 不得改变现有 `config.json` 字段语义。
- 不得修改 `releases/` 旧版本代码。

## 7. 新功能检查

1. 确认功能域、模型和配置兼容性。
2. 纯逻辑放 Services；副作用放可注入服务。
3. VM 只含状态、命令、消息；View 只含布局和纯 UI 效果。
4. 在 Composition 注册服务/VM；配置页同步 DataTemplate 和导航项。
5. 跨页协调使用消息；本地状态使用绑定。
6. 用户可见文本使用 `I18n.T(key)`。
