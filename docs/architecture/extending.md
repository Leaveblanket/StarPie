# 新功能添加规范

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；加新功能前先读“通用底线”，再按功能原型读对应清单。

## 通用底线（所有新功能）

1. 先确认功能域、模型与 `config.json` 兼容性（新字段带默认值，不改旧字段语义）。
2. 纯逻辑放 Services 纯函数/引擎；副作用放可注入服务或组合根注入的委托。
3. VM 只含状态、命令、消息；View 只含布局与纯 UI 效果；引用遵守 [layering.md](layering.md)（依赖矩阵）。
4. 服务/页面 VM 注册：M5 在 `StarPie.Shell` 的 `ShellModuleRegistrar`、M1 在 `StarPie.Gestures`
   的 `GesturesModuleRegistrar` 内注册（RegisterServices），Host 外观聚合页仍在 `Composition.cs`
   注册（见 [host.md](host.md)）；导航项经所属模块注册器
   `RegisterNavigation`、页面 DataTemplate 收进所属模块页面模板字典（M5 在
   `StarPie.Shell/Modules/ShellPageTemplates.xaml`、M1 在 `StarPie.Gestures/Modules/GesturesPageTemplates.xaml`，
   见 [navigation.md](navigation.md)），映射表（[naming.md](naming.md)）同步登记。
5. 跨页协调用消息；静态已知依赖构造注入；本地状态用绑定，不用 messenger 替代。
6. 用户可见文本一律用 i18n 文案键 + 四语言值（新增/修改流程见 [localization.md](localization.md)）。
7. 新增单测：`StarPie.Tests/{被测类型}Tests.cs`，直接构造 + 手写替身。
8. 目录/注册/映射变化后同步对应叶子文档；满足 ADR 三条件时新增 ADR。

## 原型 A：新增设置项（在现有页面加开关/滑块/输入）

1. **模型**：`AppConfig`（或对应子模型）加字段，带默认值；确认 `config.json` 向后兼容。
2. **VM**：
   - 只读展示/可编辑值 → `[ObservableProperty]`；属性变更回调内 live-apply（写运行态配置、发保存消息或调注入服务）。
   - 按钮类动作 → `[RelayCommand]`（如需要参数用 `CommandParameter`）。
3. **View**：控件绑定（可编辑值 `Mode=TwoWay`；`SelectedValuePath="Tag"` 模式同 `LanguageComboBox`）；可见性用布尔状态 + 转换器；禁止 code-behind 回填。
4. **保存**：变更后发 `DebouncedSaveRequestedMessage.Instance`（或按语义 `ImmediateSaveRequestedMessage`）；不直接写文件（见 [config.md](config.md)）。
5. **i18n**：标签/ToolTip 用键 + `DynamicResource`；键补四语言（见 [localization.md](localization.md)）。
6. **测试**：VM 单测覆盖默认值、变更回调、边界输入；View 不单测。

## 原型 B：新增设置页面

1. **VM**：M5 页面在 `StarPie.Shell/ViewModels/Pages/`、M1 页面在 `StarPie.Gestures/ViewModels/Pages/`、
   Host 页面在 exe `ViewModels/Pages/`（`ObservableObject`；按需注入 `IConfigService`/`IDialogService`/`IMessenger`
   或组合根/模块注册器委托；单例注册）。
2. **View**：M5 页面在 `StarPie.Shell/Views/Pages/`、M1 页面在 `StarPie.Gestures/Views/Pages/`、
   Host 页面在 exe `Views/Pages/`，无参构造；仅布局与 ADR-0009 白名单 code-behind（页面 XAML 根直承 `UserControl`）。
3. **注册与接线（目录驱动；M5/M1 均已跨程序集自治）**：页面 VM 注册——M5 由
   `ShellModuleRegistrar.RegisterServices`、M1 由 `GesturesModuleRegistrar.RegisterServices` 下放
   模块程序集，Host 页仍在 `Composition.ConfigureServices` → 所属模块注册器
   `RegisterNavigation(NavigationCatalog)` 加一行（槽位/AutomationId/TitleKey/IconData；M5 为
   `StarPie.Shell` 的 ShellModuleRegistrar、M1 为 `StarPie.Gestures` 的 GesturesModuleRegistrar、
   Host 为 exe 内 HostModuleRegistrar）→ 所属模块页面模板字典加 DataTemplate → [naming.md](naming.md) 页面映射表登记。
   eager 启动解析与侧栏导航项由目录自动纳入，无需再改组合根清单。
4. **i18n**：导航标题/壳层文案键 + 四语言（见 [localization.md](localization.md)）。
5. **测试**：页面 VM 单测；`NavigationTests` 如涉及导航项列表需同步。

## 原型 C：新增对话框

1. **VM**（在 `StarPie.Dialogs/ViewModels/Dialogs/`）：`{Dialog}ViewModel.cs`：构造注入所需服务/契约；完成语义 = `IsCompleted` + `BuildResult()` 返回可空结果；取消/无效 = `null`；不引用 WPF 类型。
2. **结果 record**：`{Dialog}Result` 定义在 `IDialogService.cs`（可空返回）。
3. **View**：`Views/Dialogs/{Dialog}Window.xaml(.cs)`，构造 `(IThemeService, {Dialog}ViewModel)`；code-behind 仅 ADR-0009 白名单（`IsCompleted→DialogResult=true`、取消、主题、标题拼接例外）。
4. **服务**：`IDialogService` 加 `Show{Dialog}(...)` 具名方法（同步、返回可空结果）；`DialogService` 实现 = new VM → new Window → `ShowDialog` → `BuildResult`。
5. **调用方**：VM 只依赖 `IDialogService`；不在 View/其它服务直接 new 对话框。
6. **i18n**：标题/按钮文案键；`InputDialog` 遗留命名不得复制。
7. **测试**：对话框 VM 单测 + `TestDialogService`（手写替身）扩展方法；窗口不单测。
8. **文档**：更新 [naming.md](naming.md) 配对表；若为宿主内覆盖层形态须注释宿主窗口。完整形态见 [dialogs.md](dialogs.md)。

## 原型 D：新增动作类型

1. **模型**：确认 `ActionItem.Type` 新值（`config.json` 存量兼容：老类型语义不变；新类型仅对新配置生效）。
2. **纯逻辑**：`ActionRouting.ResolveRoute` 加分支；若属系统命令，`ResolveSystemCommand` 加预设映射（大小写敏感/不敏感按既有规则）。
3. **副作用**：`ActionExecutorService.Execute` 加 case；系统调用一律走构造注入接缝。
4. **UI**：动作类型选项/参数编辑（涉及页面 VM 与 View）同步；图标/标签用键。
5. **测试**：`ActionRoutingTests`（路由纯函数）与 `ActionExecutorServiceTests`（注入假体断言外部行为）。
6. **文档**：更新 [gestures.md](gestures.md) 动作种类说明。

## 原型 E：新增轮盘渲染样式

1. **渲染器**：`StarPie.Ui/Views/Renderers/{Xxx}Renderer.cs`（P1.7/#116 随 M2 归并入 Ui），继承 `BaseStyleRenderer`，实现/覆写 `RenderDecorations`、`GetDefaultColors`、`PostInitialize`、高亮/外甩效果；保持纯视觉、不订阅事件、不反向依赖。
2. **工厂**：`StarPie.Ui/Views/Renderers/` 内 `StyleRendererFactory.CreateRenderer` 加样式名分支（空/未知 → `ClassicRingRenderer`）；配色目录 `WheelPaletteCatalog` 在 `StarPie.Host/Wheel/`。
3. **配置/UI**：样式名作为 `AppConfig.WheelStyle` 新值（默认值兜底）；外观页风格下拉加选项（键 + 四语言）。
4. **预览**：确认 `WheelPreviewRenderer`（`StarPie.Ui/Views/Renderers/`）走同一渲染契约后自动覆盖预览。
5. **测试**：渲染器纯函数可测部分（如颜色/几何推导）按需单测；视觉效果人工验收。

## 原型 F：新增后台服务/监听器

1. **接口与实现**：`Services/{Feature}/IXxxService.cs` + `XxxService.cs`（同目录）；副作用经构造注入接缝。
2. **注册**：所属模块注册器 `RegisterServices` 注册（默认单例；M1 为 `GesturesModuleRegistrar`，
   Host 侧件仍 `Composition.ConfigureServices`）；若需启动保活
   （订阅事件/消息），仿 `GestureController`（M1 注册器登记、`CreateAppHost` 经容器 eager 解析）/
   `SettingsSaveOrchestrator` 模式在组合根或注册器接线。
3. **线程边界**：钩子/后台线程事件不得直接改 VM/UI；经 Dispatcher 封送（`WheelFactory.DispatchedWheelViewModel` 模式——工厂驻 `StarPie.Ui/Services/Wheel/`，M1 只经 `IWheelFactory` 接口）或由 UI 线程组件消费。
4. **生命周期**：实现 `IDisposable` 并在 `Composition.Dispose`/`AppHost.Dispose` 停止/退订
   （`MouseHook.Stop`、I18n 退订模式）。
5. **测试**：决策逻辑纯函数/引擎单测；系统调用经注入假体验证；集成性质不测（如 `ProgramScanner`、注册表）。
