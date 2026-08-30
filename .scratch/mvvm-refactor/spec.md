
## Problem Statement

StarPie 约 1.35 万行代码全部采用 WPF code-behind 模式：UI 逻辑、业务逻辑、系统调用混在窗口类里，10 个静态类作为全局状态被各层直接引用，全仓零单元测试。维护者改一处设置逻辑无法在不启动完整应用的情况下验证；AI agent 难以安全导航与修改；每次改动都依赖手工全量回归。

## Solution

引入 CommunityToolkit.Mvvm，将全部 UI 迁移到 MVVM：视图状态进 ViewModel、绘制留在 View 层、静态设施按边界服务化（或刻意保持静态）、组合根手动装配。手势控制拆为无 UI 依赖的纯逻辑状态机。分票渐进交付，每票合并后应用可运行；ViewModel 与纯逻辑层获得 xUnit 单元测试。**用户可见行为完全不变。**

## User Stories

1. As a 终端用户, I want 所有设置改动立即生效, so that 我不用寻找保存按钮
2. As a 终端用户, I want 关闭设置窗口后应用驻留托盘、笔势继续可用, so that 关窗不等于退出
3. As a 终端用户, I want 只有托盘菜单的退出才真正结束应用, so that 不会被误退出
4. As a 终端用户, I want 按住右键拖出轮盘、拖动选择、松开执行扇区动作的手感与重构前一致, so that 肌肉记忆不作废
5. As a 终端用户, I want 旧版 config.json 升级后无需迁移直接可用, so that 升级零成本
6. As a 终端用户, I want 四种轮盘皮肤的外观与切换行为不变, so that 个性化设置不丢
7. As a 终端用户, I want 程序选择器的扫描、搜索、图标显示行为不变, so that 配置体验不回退
8. As a 终端用户, I want 切换语言后全部界面文本立即刷新, so that 不用重启
9. As a 终端用户, I want 深浅主题切换行为不变, so that 视觉偏好即时生效
10. As a 维护者, I want 设置逻辑住在 ViewModel 而非窗口 code-behind, so that 不启动应用即可验证业务规则
11. As a 维护者, I want 手势状态机无 WPF 依赖, so that 能在纯测试进程里驱动它
12. As a 维护者, I want 配置读写经接口注入, so that 测试可替换为内存配置
13. As a 维护者, I want 对话框打开经服务抽象, so that 含对话框的流程可以 mock 测试
14. As a 维护者, I want 服务装配集中在一个组合类, so that 依赖关系一眼可查、构造签名漂移在编译期暴露
15. As a 维护者, I want 每张迁移票合并后应用可运行可发布, so that 任何时刻都能出包
16. As a 维护者, I want ViewModel 与纯逻辑有单元测试, so that 回归在合并前被拦截
17. As a AI agent, I want 分层文件夹结构（Models / ViewModels / Views / Services / Controls）, so that 不用全文搜索就能定位代码
18. As a AI agent, I want 刻意保持静态的工具类有文档记录, so that 不会有人再"好心"包装一遍
19. As a AI agent, I want ADR 记录架构决策与理由, so that 不会推翻刻意决策（如手动组合根）
20. As a AI agent, I want 领域术语表约束命名与沟通, so that 代码与文档说同一种语言

## Implementation Decisions

- **全量 MVVM + CommunityToolkit.Mvvm**（ObservableObject / RelayCommand），作为唯一 MVVM 基建依赖。见 ADR-0001。
- **手动组合根**：装配集中在独立组合类，不引 DI 容器；重新评估触发器已登记。见 ADR-0002。
- **静态类处置**：配置、动作执行、前台窗口上下文、主题四处服务化（IConfigService / IActionExecutorService / IWindowContext / IThemeService）；I18n（带切换广播）、图标工具、内存整理、渲染器工厂、开发实例常量五处刻意保持静态。见 ADR-0002。
- **应用宿主重组**：移除 StartupUri，托盘上移应用层，真退出走 Application.Shutdown；"关窗隐藏到托盘、托盘退出才真退"的行为语义不变。见 ADR-0003。
- **手势链路**：鼠标钩子作为适配层输出自定义事件参数（无 UI 框架类型）→ 纯逻辑状态机 GestureEngine → 轮盘 ViewModel；轮盘窗口经工厂瞬态创建（每次手势新建）。见 ADR-0001/0002。
- **轮盘呈现**：选中扇区、转义状态、扇区集合等视图状态进 ViewModel；四个渲染器实现从 VM 读状态，绘制代码留在 View 层。见 ADR-0001。
- **设置域**：按分区拆子 ViewModel 聚合于根 ViewModel（沿用项目已有的扇区槽位 ViewModel 先例）；保持立即生效语义；既有绑定保留表达式结构、只换 DataContext。
- **对话框服务**：具名方法、同步签名、可空结果对象（取消与无效统一为 null）、单 Owner 惰性回填、含系统文件对话框与屏上取色器；迁移期实现内部可暂用旧窗口。见 ADR-0004。
- **结构**：按层分文件夹；热键录制框等 Control 子类归控件层，依赖属性绑定，不做 VM 化。
- **交付**：分票渐进，每票可运行；第一票 = 基建（工具库 + 文件夹重组 + 组合根 + 宿主重组 + 服务缝）+ 程序选择器打样验证管线。
- **硬约束**：Model 层保持纯 POCO；config.json 格式向后兼容。

## Testing Decisions

- **好测试的标准**：只测外部行为——状态机迁移、VM 公开命令与属性、服务返回值；不测实现细节。
- **缝**：零新增测试缝，全部复用架构既定缝。入口：GestureEngine 从鼠标事件参数流进（mock IWindowContext、轮盘工厂）；设置子 VM 从命令与属性变更进（mock IConfigService、IDialogService、IThemeService）；纯函数直接调用（手势阈值/方向判定、垃圾可执行过滤、扫描去重与显示名升级、配置校验）。
- **明确不测**：鼠标钩子适配层、四个渲染器、托盘服务、组合根装配、轮盘窗口 XAML——由现有 Python 端到端测试兜底启动级行为。
- **测试项目**：新建 WinPieGestures.Tests（xUnit），与现有 Python 测试并存互不影响。
- **先例**：仓库无 .NET 测试先例，本 spec 建立第一条。

## Out of Scope

- 任何用户可见行为变化（功能冻结，纯架构迁移）
- config.json 格式变更
- 插件系统（登记为未来独立议题，触发容器与插件架构的重新评估）
- 迁移或改写现有 Python 端到端测试
- 重写既有绑定表达式以追求"更规范"
- 为程序扫描 IO 开测试缝
- 引入 DI 容器

## Further Notes

- 全部决策已纸面化：CONTEXT.md（术语表 + 硬约束）、docs/adr/0001-0004。
- 迁移期 ViewModel 与 code-behind 共存是预期状态，非技术债。
- 本 spec 确认后以 /to-tickets 拆分 tracer-bullet 票，票间依赖以 blocking 边声明。
