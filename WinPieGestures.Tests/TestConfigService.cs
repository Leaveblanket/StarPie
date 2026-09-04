using System;

namespace WinPieGestures.Tests;

/// <summary>
/// IConfigService 的测试替身（工程约定：mock 直接 new，不使用 mocking 框架）：
/// 内存持有 <see cref="AppConfig"/>，记录 Save 调用，profile 查找回落当前配置首个内置档。
/// 行为显著特化（如按进程注入多档）的场景请在用例内保留局部替身，不要在本类堆特例。
/// </summary>
public sealed class TestConfigService : IConfigService
{
    public AppConfig Current { get; set; } = new();

    /// <summary>Save 调用次数。</summary>
    public int SaveCalls;

    public void Load() { }

    public void Save() => SaveCalls++;

    public WheelProfile GetProfileForProcess(string processName) => Current.Profiles[0];

    public WheelProfile GetGlobalProfile() => Current.Profiles[0];
}
