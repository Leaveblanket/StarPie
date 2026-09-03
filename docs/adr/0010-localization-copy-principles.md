# 本地化目标态原则：四类文案与生命周期契约

T24（#29）已把 5 个设置页的 code-behind 回填改为“应用级语言字典 + `{DynamicResource}`”。剩余对话框/壳层/托盘/VM 的 `I18n.T()` 消费需要统一的**目标态原则**，否则每张迁移票都会自造一套机制。本 ADR 按“文案本质”而非“代码层”立法，承接 ADR-0002/0005/0008/0009。

> **状态**：accepted

## 术语（已落档 CONTEXT.md）

- **声明式文案 (Declarative Copy)**：界面静态展示、语言切换后自动随当前语言更新的文案，呈现者无需逐项回填。
- **驻留文案 (Resident Copy)**：长期存活于视图模型、语言切换时必须同步更新的展示文案（如导航标题、动作类型选项）。
- **即时取词 (Point-of-use Lookup)**：仅在内容呈现或事件发生那一刻读取当前语言、此后不再跟随切换的文案（通知、输入框标题、错误消息）。
- **壳外文案 (Shell-external Copy)**：由操作系统级界面（托盘菜单、悬浮提示）而非设置控制台界面元素树呈现的文案，在每次展示时读取当前语言。

## 决策

1. **语言变更源与介质不变量**
   - `I18n` 静态 + `LanguageChanged` 是唯一语言变更源（ADR-0002 判据不动）；
   - T24 应用级语言字典只是“声明式文案”的介质，不是给 VM/托盘用的 API；
   - 不引入每语言 XAML 资产文件，不引入 `ILocalizationService`。

2. **文案分类与各自机制**
   - **声明式文案**：页面（T24 已迁）+ 壳层（`MainView`/`SidebarView`）+ 对话框（Color/Icon/Program/Input）的静态文本一律 `{DynamicResource}`；例外只保留 XAML 表达不了的位置——`Window.Title` 拼接、托盘 tooltip、View 动态生成项（注释指向本 ADR / ADR-0009）。`Window.Title` 收进壳层 VM 属性（`MainViewModel.WindowTitle`）后不再是例外。
   - **驻留文案**：只允许三种合格机制，不引入 LocalizedText 包装或绑定级语言订阅器——
     ① 容器统一刷新（`MainViewModel.RefreshTitles` 范本，子项不各自订阅）；
     ② 瞬态自订阅 + `IDisposable` 成对退订（`SlotViewModel` 经 `ProfileListViewModel` 持有者 Dispose）；
     ③ 实时计算属性（getter 内 `I18n.T()`）在切语时由对象内 `OnPropertyChanged` 刷新。
   - **即时取词**：调用时/呈现时 `I18n.T()` 是**目标态而非技术债**；VM/服务中出现属正常，不得为“零 `I18n.T()`”硬塞进绑定或字典。
   - **壳外文案**：托盘是进程级壳，不属于 `MainView` 生命周期。品牌/版本名（`StarPie v1.4.1` + `DevInstance.Suffix`）锁死、永不翻译；菜单项每次打开重建（已是目标态）；tooltip 在语言切换时由组合根按暂停态刷新。

3. **VM 生命周期契约**
   - 页面 VM 维持容器单例（ADR-0005），状态跨导航常驻；**不引入“导航驱动激活/失活”**（#29 挂起项正式关闭）。生命周期治理只针对瞬态 VM 与订阅清理。
   - 强静态事件（`I18n.LanguageChanged`）订阅者必须成对退订：瞬态 VM 实现 `IDisposable` 并由持有者 Dispose（`SlotViewModel` → `ProfileListViewModel`；对话框 VM 若未来订阅强事件，由 `DialogService.Show*` 收尾）；进程级单例（`MainViewModel`）也实现 `IDisposable`，由 DI 随 `Composition.Dispose` 自动调用（兼作测试拆卸）。
   - `WeakReferenceMessenger` 订阅为弱引用，无需 Dispose。

4. **内容与机制分离**：未本地化硬编码文案（页面副标题、About 里程碑、屏上取色提示等）另立“文案缺口盘点”backlog，机制迁移票不新增翻译键（`I18n.Translations` 数据在 T24 后维持不动）。

5. **UI 自动化定位**：声明式化删除“纯回填用 x:Name”时，凡自动化（pywinauto e2e）或辅助功能需要定位的控件保留显式 `AutomationProperties.AutomationId`（不生成 code-behind 字段）；e2e 不得按语言化文本定位控件。

## Consequences

- ADR-0009 白名单第 2 条（本地化回填）收窄为“XAML 表达不了的位置”，其余 View 静态文案一律声明式化。
- 拆票：T25 壳层 + 托盘收尾；T26 对话框声明式化；T27 瞬态 VM 生命周期治理；T28 文案缺口盘点（backlog）。各票互不阻塞。
- 对话框等瞬态窗口每次 `Show*` 新建，打开即取当前语言；迁移不改变任何语言行为，只换消费形态。
