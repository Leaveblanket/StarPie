# View code-behind 白名单与输入适配边界

> Status: Active
>
> **更新（ADR-0014）**：界面主题应用与预览重绘补充消息化驱动（`AppThemeChangedMessage`/`AppearancePreviewInvalidatedMessage`），见 [ADR-0014](./0014-wheel-palette-module-boundary-and-appearance-split.md) 决策 7。

承接 ViewModel/View 严格边界纪律（现行规范见 `docs/architecture/layering.md` Views 节），明确 View 的 `.cs` 里允许保留什么、哪些必须 Binding/ICommand 化。此前各页迁移后仍残留手写 VM 状态读写、空壳事件与 View→Composition 反向依赖，且无文档界定归属，未来 agent 无法判断某段 code-behind 该留该迁。

## 决策

- **View .cs 白名单（允许保留）**：
  1. 生命周期接线：`Loaded/Unloaded/Closed` 成对订阅退订语言变更事件与 messenger；窗口 `Closing` 的壳层行为（隐藏到托盘、淡出）。
  2. 本地化：XAML 表达不了的位置（窗口标题、动态生成项）用本地化服务即时取词回填。
  3. 纯视觉渲染：Canvas 绘制、位图缩略图加载为显示、动画、把鼠标坐标转发给 Renderer。
  4. 纯 UI 适配（无领域语义）：`DialogResult=false` 的取消、滚动容器、焦点/SelectAll、列表项高亮。
  5. 壳层职责（仅窗口类）：主题应用、系统深浅色探测、托盘/窗口行为。
- **状态流必须 Binding**：View 通过 `DataContext`/Binding 读写状态；可编辑值 `Mode=TwoWay`；动态下拉用 `ItemsSource`/`SelectedValue`；可见性用 Converter/DataTrigger。禁止 code-behind 手写回填可绑定控件、禁止在 View 中改写 VM 状态。
- **动作流必须 ICommand**：`Button` 等 ICommandSource 绑 `Command`/`CommandParameter`；键盘 Enter/Delete 用 `InputBinding`；双击用 `MouseBinding`；无 Command 属性的原始输入（Canvas 取点/拖拽）用附加行为翻译坐标后执行 VM 命令。事件只处理纯 UI 细节，不得调用 VM 方法/命令作为业务入口。
- **INPC 订阅边界**：View 可订阅 VM `PropertyChanged`，但仅用于驱动无法用 Binding 表达的纯视觉渲染/绘制参数（如 Canvas 定位、缩略图重绘）；禁止借 INPC 回填可绑定控件或改写 VM 状态。
- **对话框完成**：确认/完成经 VM 命令或可观察完成状态驱动，View 把它落成 `DialogResult=true`；取消无业务语义，保留 `Click→DialogResult=false` 白名单。
- **依赖方向**：View 不得反向依赖 `Composition`、服务或配置；壳层主题应用是白名单（页面无参构造、不经容器，VM 不持 `IThemeService`）。

## Consequences

- 新增或遗留 code-behind 按上表分类：白名单项保留；非白名单项（状态回填、VM 改写、事件当命令入口、反向依赖）迁到 Binding/命令/VM。
- 未来 agent 不得把白名单项“好心”迁进 ViewModel——它们是呈现层适配而非领域状态。
- 新输入控件先找 `Command`/`InputBinding`/`MouseBinding`/附加行为，再考虑事件。
