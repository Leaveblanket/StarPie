# 全量 MVVM 架构与 CommunityToolkit.Mvvm

StarPie 现状是约 13.5k 行的 code-behind 单层结构、10 个静态类、零单元测试。决定将全部 UI 重构为 MVVM：视图状态进 ViewModel（CommunityToolkit.Mvvm 的 ObservableObject / RelayCommand），绘制代码留在 View 层；手势控制拆为无 WPF 依赖的纯逻辑状态机加轮盘 ViewModel。引入 CommunityToolkit.Mvvm 作为唯一的 MVVM 基建依赖。

## Considered Options

- 部分 MVVM（自绘的轮盘窗口保持 code-behind）：被否——目标是全项目统一架构，且轮盘状态联动（选中扇区、转义状态）需要可测。
- Microsoft.Extensions.DependencyInjection 容器：被否，见 ADR-0002。

## Consequences

- Model 层（AppConfig / WheelProfile / ActionItem）保持纯 POCO；`config.json` 格式向后兼容——已发布版本存在存量用户配置，这是代码里看不见的硬约束。
- 设置保持"立即生效"语义，不改为编辑副本模式，避免扩大重构的验证面。
- 分票渐进迁移：迁移期 ViewModel 与 code-behind 共存，每票合并后应用可运行可发布。
- ViewModel 与纯逻辑层补 xUnit 单元测试；现有 `tests/` 是 Python 端到端测试，不受影响。钩子、渲染、托盘不纳入单测。
