# 测试接缝:设计缝优先,判定入口不开公开面,反射 / IVT / UnsafeAccessor 不入测试

**状态**:accepted

`layering.md`「命名空间与可见性」已裁定「被测类型显式 public、不引入 `InternalsVisibleTo`」,但该规则只管**类型**级。实际落地出现了成员级滑移:看门狗的判定入口 `CheckOnce` 曾以「测试直接驱动一次判定」为由公开,`MouseInputHook.Watchdog` 公开句柄的唯一用途也是测试——公开面随测试需求逐点扩散,离开「跨集消费 / 装配」判据。决定:**测试的驱动面一律走构造注入的设计缝**(委托 / 接口 / 时钟;生产带默认值,测试注入假体),**不为测试开公开成员**。首个落地:看门狗的周期探针改由注入的 `TimeProvider` 驱动(生产 `TimeProvider.System`,测试 `FakeTimeProvider` 推时钟),`CheckOnce` 收回 `private`,`MouseInputHook.Watchdog` 句柄下线——判定语义、探针周期与重注册行为零变化。判据:**测试要驱动的是一个成员的输入,不是成员本身**;内部逻辑若无法经公开面与注入面驱动,正确动作是把它提炼为可测的纯函数或有接缝的单元,而不是开洞。**红线**:不写测试专用方法 / 测试专用分支 / 为测试放宽的成员可见性;反射、`UnsafeAccessor`、`InternalsVisibleTo` 均不作为测试访问机制(IVT 维持 `layering.md` 既有禁令,要动先改本 ADR)。

## 考虑过的方案

- **维持「为测试 public 的成员」(现状)**:否决。成本不在单点(应用集的 public 无外部契约),而在扩散——每次「测试不好驱动」都往公开面加一格,`CheckOnce` / `Watchdog` 已有先例;判据被稀释后无法机械复核。
- **`InternalsVisibleTo`**:否决,维持 `layering.md` 现状。它把测试可见面放大到整个程序集,诱导直取实现细节;本仓库单测试工程 + 设计缝已足够,收益不抵判据膨胀。
- **反射 / MSTest `PrivateObject` 一类**:否决。编译期零约束(改名 / 改签名到运行时才炸)、报错隔层、与 `TreatWarningsAsErrors` 及分析器工具链脱钩,并把「测试依赖了这个成员」的意图藏起来。
- **`UnsafeAccessor`(.NET 8+)**:作为测试机制否决。它比反射文明(JIT 解析、声明即文档),但定位是框架 / 序列化器等加不了接缝的场景;本仓库有 `TimeProvider` 这类现成设计缝时没有它的位置。
- **设计缝(采纳)**:驱动输入而非暴露成员——时钟(`TimeProvider`)、光标探针、注入器工厂、钩子替身(`TestGlobalHook`)是同族既有先例;测试断言只经过公开面与注入面(`Start` / `Stop` + 探针 / 回调 / 假时钟)。

## 后果

- `HookWatchdog` 构造器带 `TimeProvider?`(默认 `TimeProvider.System`);`StarPie.Tests` 引 `Microsoft.Extensions.TimeProvider.Testing`(CPM 锁版本,仅测试工程消费),看门狗用例改由 `Advance(周期)` 驱动周期判定。
- 断言面收窄到公开面:光标探针按序吐坐标、`recover` 调用计数、假时钟推进;`CursorUnavailable` 一类用例补「探测确实发生」的存在性断言,避免「没触发所以没判死」的假绿。
- `layering.md` 的可见性条款不变(被测**类型**仍 public、IVT 仍不引入),补一条成员级判据指向本 ADR。
- 后续新增测试缝按本 ADR 判据走:先问「它的输入是什么、能否注入」,再考虑把逻辑提炼为纯函数;「必须开洞」的个案先改本 ADR。
- 与 ADR-0050 的分工:0050 管测什么(域裁剪),本 ADR 管怎么够到(访问面),两文叠加构成完整测试口径。
