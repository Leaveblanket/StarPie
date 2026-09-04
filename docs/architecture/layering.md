# 分层与依赖规范

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；写代码前需要核对“谁可以引用谁、类型可见性、Model/Service/VM/View 各自边界”时读本篇。

## 分层总览

```text
App / Composition            # 装配与生命周期（唯一解析点）
      |
      v
ViewModels ---> Views        # 经 DataContext/DataTemplate；View 不反向引用 VM 之外
      |
      v
Services ---> Models
```

## 依赖矩阵

| 引用方 \ 被引用方 | App/Composition | Models | Services | ViewModels | Views | Messages |
|---|---|---|---|---|---|---|
| Models | ✗ | △（同层值类型互用） | ✗ | ✗ | ✗ | ✗ |
| Services | ✗ | ✅ | ✅（经接口，见下） | ✗ | ✗ | ✅ |
| ViewModels | ✗ | ✅ | ✅（接口/委托） | △（仅静态已知依赖，见下） | ✗ | ✅ |
| Views | ✗ | △（仅 WPF-free 值类型经绑定/转换器） | △（仅白名单服务构造注入，见下） | ✅（DataContext/DataTemplate） | △（同层控件/样式/转换器） | ✗ |
| App/Composition | — | ✅ | ✅ | ✅ | ✅ | ✅ |

### 必须遵守的例外与说明

1. **Views → Services 白名单**：View 构造可注入 `IThemeService` 仅用于窗口主题应用（[ADR-0009](../adr/0009-view-code-behind-whitelist.md) 第 5 条）；不得注入业务服务、配置服务或在 View 中调用服务方法。
2. **ViewModels 之间**：仅允许静态已知依赖构造注入（如 `AppearanceSettingsViewModel → ProfileListViewModel` 读方案列表）；动态/广播协调一律走 IMessenger；同页状态不得用 messenger 替代绑定。
3. **Services 内部依赖**：允许经接口构造注入（如 `SettingsSaveOrchestrator → IConfigService/ISaveDebouncer`、`GestureEngine → IConfigService/IWindowContext/IWheelFactory`）；**解析点只允许在 Composition**，唯一例外是 `NavigationService<T>` 持有 `IServiceProvider`（开放泛型注册，[ADR-0005](../adr/0005-di-container-for-navigation.md)）。
4. **ViewModels 不得引用任何 WPF 类型**（`Window`、`MessageBox`、`Color`、`Brush`、`ICommandSource` 等），颜色一律用 `RgbColor`/hex 字符串，边界由 View 转换器处理。
5. **Views 不得反向依赖 Composition、配置或业务服务**；页面无参构造、不经容器（ADR-0008/0009）。

## 命名空间与可见性

- **命名空间 = 物理目录**：`WinPieGestures.Services.Actions`、`WinPieGestures.ViewModels.Dialogs`、`WinPieGestures.Views.Navigation`；根级类型（`App`、`Composition`）在 `WinPieGestures`。
- **可见性**：
  - 需要被测试工程引用的类型显式 `public`：Models 值类型、Services 接口与实现、页面/对话框 VM、消息与结果 record、导航件。
  - 其余内部实现细节（私有嵌套、纯辅助类等）默认 `internal`。
  - **不引入 `InternalsVisibleTo`**（现状：测试工程直接引用 public 类型）。若日后要收紧可见性，先写 ADR。
  - `Composition` 为 `internal sealed class`，仅同程序集 `App` 使用；不对外暴露。
- 页面 View 无参构造、不注册容器，因此不需要 public 构造注入（`MainView`、对话框 Window 是仅有的、经组合根/服务显式 `new` 的窗口）。

## Models

- 只放**纯数据 POCO**与**WPF-free 领域值类型/纯函数**：
  - 配置 POCO：`AppConfig`、`WheelProfile`、`ActionItem`、`CustomColorPreset`。
  - 值类型：`RgbColor`（readonly struct）、`ColorMath`（纯颜色换算）。
- 不引用 WPF、Services、ViewModels、命令、消息或 IMessenger；不含 IO、注册表、P/Invoke。
- **新字段必须有默认值**；不得改变既有 `config.json` 字段语义（Hard Constraint：存量用户配置向后兼容）。
- 值类型可携带纯换算方法（如 `ToHex`/`TryParseHex`），但不得有副作用。

## Services

- **接口与实现同目录**：`IXxxService` / `XxxService`。
- **只由 Composition 注册**；View/ViewModel 不自行 `new` 服务、不使用服务定位器（`NavigationService<T>` 例外见上）。
- 服务负责可注入、可 mock 的副作用：文件 IO、注册表、进程启动、SendInput、MessageBox、托盘等。
- **系统调用接缝模式**：实现类构造注入委托/接口并带生产默认值（如 `ActionExecutorService` 注入 `startProcess`/`sendKeyStrokes`/`lockWorkStation` 等，`ThemeService` 注入系统深浅色探测委托），测试注入假体即可全量验证路由决策。
- **纯决策提炼为静态纯函数**：与 IO/系统调用分开（如 `ActionRouting`、`ProgramCatalog`），直接单测。
- Win32 静态工具仅限无状态、无需 mock 的调用，并注释记录原因；有状态系统互操作（注册表自启、程序扫描）收敛为服务/静态工具后**经组合根委托注入**给 VM。
- 服务注册以单例为主；页面 VM 单例、轮盘 VM 按手势瞬态创建（见 [gestures.md](gestures.md)/[wheel.md](wheel.md)）。

## ViewModels

- 使用 `ObservableObject`、`[ObservableProperty]`、`[RelayCommand]`。
- **生命周期注册**：页面 VM 容器单例（状态跨导航常驻）；轮盘 VM 按手势创建、不注册；对话框 VM 由 `DialogService` 每次 `Show*` 新建（不注册容器）。
- 仅暴露可观察状态、命令与必要消息；**不得暴露临时 `event Action`**。
- 状态传输：View 经 `DataContext`/`Binding` 读取；可编辑值 `Mode=TwoWay`；VM 用 `INotifyPropertyChanged`（本项目 `ObservableObject`）。
- 用户动作：一律 `ICommand`；Button 等 `ICommandSource` 绑 `Command`/`CommandParameter`；代码后置不得调用 `Vm.Command.Execute(...)`。
- 跨 VM/页面协调：不可变 `IMessenger` 消息；静态已知依赖可构造注入（见上文例外 2）；同页状态不得用 messenger 替代绑定。
- 副作用经注入服务或**组合根注入的委托**编排（托盘气泡、退出、自启、导入导出、打开文件：`GeneralSettingsViewModel`/`AboutViewModel` 模式）；VM 不直接持有 `Window`、`MessageBox`、文件对话框等 WPF 类型。
- 对话框 VM 完成语义：`IsCompleted` 可观察状态 + `BuildResult()` 返回可空结果 record；取消/无效输入返回 `null`（[ADR-0004](../adr/0004-dialog-service-design.md)）。
- 订阅 `I18n.LanguageChanged`/messenger 的 VM（壳层与驻留文案持有者）必须成对退订（`MainViewModel.Dispose` 模式）。

官方 API：

- [WPF data binding overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/data-binding-overview)
- [FrameworkElement.DataContext](https://learn.microsoft.com/en-us/dotnet/api/system.windows.frameworkelement.datacontext)
- [WPF commanding overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/commanding-overview)
- [ICommandSource](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.icommandsource)
- [RelayCommand generator](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/relaycommand)
- [ObservableProperty generator](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty)
- [MVVM Toolkit messenger](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/messenger)

## Views

- XAML/View 负责布局、控件树、样式、模板、资源、动画和可视状态；**不在 View 中编排业务、写配置、调用服务、处理文件/注册表或决定领域状态**。
- code-behind 只保留 [ADR-0009](../adr/0009-view-code-behind-whitelist.md) 白名单：生命周期接线、XAML 表达不了的位置本地化、纯视觉渲染（Canvas 绘制/坐标转发）、纯 UI 适配（取消、滚动、焦点）、壳层职责（窗口类：主题应用、托盘/窗口行为）。
- 页面经 `MainView.xaml` 中 DataTemplate 映射 VM（无参构造、不注册容器，见 [navigation.md](navigation.md)）；页面卸载时成对取消静态事件与 messenger 订阅（`RadialWindow`、`MainView` 模式）。
- WPF 事件允许保留，但只能处理纯 UI 细节；不得调用 VM 方法、服务或命令作为业务入口（参见 [Routed events overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/events/routed-events-overview)）。
- 没有 `Command` 属性的控件优先属性绑定；仅“无等价绑定且纯 UI 适配”时才用行为/附加属性（`SpectrumCanvasBehavior` 属 ADR-0009 输入适配）。

### `AdvancedSettingsPage` 绑定规范（页面级示例，所有页面同则）

- 导入、导出、提权、内存整理按钮绑定 VM 命令。
- `AutoStartCheckBox.IsChecked` 双向绑定 `AutoStartEnabled`；注册表写入与保存请求放在 VM 属性变更回调（经组合根注入的自启委托）。
- `LanguageComboBox` 设置 `SelectedValuePath="Tag"`，双向绑定可写 `LanguageCode`；语言切换与持久化放在 VM。
- `UacWarningCard.Visibility` 绑定 VM 布尔状态并使用转换器。
- `MessageBox` 仅可作为 View 显示适配；提示内容与副作用由 VM/服务决定。
