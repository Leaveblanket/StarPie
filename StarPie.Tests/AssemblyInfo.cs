using Xunit.Sdk;
using Xunit.v3;

// 串行执行的约束来自 StaTestHarness 持有的进程内唯一 WPF Application 与 STA 线程：
// 多个用例读写 Application 的窗口集合与资源字典，带弱引用回收断言的类也经不起并发扰动。
[assembly: Parallelization(Mode = ParallelMode.None)]
