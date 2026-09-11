using System.Collections.Generic;

namespace StarPie.Tests;

/// <summary>
/// IThemeApplier 的测试替身（工程约定：mock 直接 new，不使用 mocking 框架）：
/// 记录内核主题引擎每次应用的有效主题，供状态机用例断言换肤调用。
/// </summary>
public sealed class TestThemeApplier : IThemeApplier
{
    /// <summary>按调用顺序记录的有效主题名。</summary>
    public List<string> Themes { get; } = new();

    public void ApplyTheme(string effectiveTheme) => Themes.Add(effectiveTheme);
}
