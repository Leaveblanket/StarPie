using System;
using System.Reflection;
using StarPie.Kernel.ShellIntegration;

namespace StarPie.Tests;

/// <summary>
/// 完整性级别探测的 P/Invoke 契约守护：这类系统调用的失败方式是**静默返回垃圾值**而不是抛异常，
/// 故这里直接核对读出的数值本身（自进程令牌的完整性 RID 必须落在合法的完整性级别集合内），
/// 而不是只断言"没抛异常"。判据是纯函数那一侧（<see cref="ElevatedWindowNotice"/>）的事。
/// </summary>
/// <remarks>
/// 用反射取私有探针是有意的：被守护的正是"从 SID 读子权威"这一段裸指针代码
/// （`GetSidSubAuthorityCount` 返回的是指针而非计数，取错就得到越界的垃圾 RID）。
/// 该方法改名即测试变红，属期望行为。
/// </remarks>
public sealed class ProcessElevationTests
{
    /// <summary>合法的完整性级别 RID：untrusted / low / medium / high / system。</summary>
    private static readonly uint[] KnownIntegrityLevels = { 0x0000, 0x1000, 0x2000, 0x3000, 0x4000 };

    [Fact]
    public void 自进程完整性级别_读出合法RID()
    {
        object? rid = OwnIntegrityRid();

        Assert.NotNull(rid);
        Assert.Contains((uint)rid!, KnownIntegrityLevels);
    }

    [Fact]
    public void 不存在的进程_按未知处理()
    {
        // 打不开的进程一律返回 null（未知），不得抛异常、不得编造级别。
        Assert.Null(ReadIntegrityRid(0xFFFFFFF0u));
    }

    [Fact]
    public void 探测结果_三态之内不抛异常()
    {
        // 前台窗口可以是任何东西（含无前台窗口的会话）：允许 true/false/null，
        // 但不允许抛出——探测失败必须落在"未知"上。
        bool? higher = ProcessElevation.IsForegroundWindowHigherIntegrity();

        Assert.True(higher is true || higher is false || higher is null);
    }

    private static object? OwnIntegrityRid()
        => Private("TryGetOwnIntegrityRid").Invoke(null, null);

    private static object? ReadIntegrityRid(uint processId)
        => Private("TryGetIntegrityRid").Invoke(null, new object[] { processId });

    private static MethodInfo Private(string name)
        => typeof(ProcessElevation).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
           ?? throw new InvalidOperationException($"ProcessElevation.{name} 不存在：探测实现已改名，本守护需同步。");
}
