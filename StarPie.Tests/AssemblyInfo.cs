using Xunit.Sdk;
using Xunit.v3;

// 串行执行的约束来自保留用例里的弱引用回收断言（VM / 服务作用域释放后的探针判定）：
// GC.Collect 与「对象已死」的判定经不起并发用例的分配与保活扰动。
[assembly: Parallelization(Mode = ParallelMode.None)]
