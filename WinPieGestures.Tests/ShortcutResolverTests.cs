using System;

namespace WinPieGestures.Tests;

/// <summary>
/// 程序快捷方式解析出口（M3）纯路径覆盖（T3a/#65，R6/ADR-0015）：空/空白与不存在的 .lnk
/// 在触达 COM 前按 <c>File.Exists</c> 早退并复位 out 参数（测试不触 COM）。
/// 真实 .lnk COM 解析属集成面，按仓库惯例不在此单测。
/// </summary>
public sealed class ShortcutResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveShortcutTarget_NullOrWhitespace_ReturnsFalseWithDefaults(string? lnkPath)
    {
        bool resolved = ShortcutResolver.ResolveShortcutTarget(
            lnkPath!, out string targetPath, out string iconPath, out int iconIndex);

        Assert.False(resolved);
        Assert.Equal("", targetPath);
        Assert.Equal("", iconPath);
        Assert.Equal(0, iconIndex);
    }

    [Fact]
    public void ResolveShortcutTarget_MissingLnkFile_ReturnsFalseWithDefaults()
    {
        string missingPath = @"C:\StarPie-Tests\missing-" + Guid.NewGuid().ToString("N") + ".lnk";

        bool resolved = ShortcutResolver.ResolveShortcutTarget(
            missingPath, out string targetPath, out string iconPath, out int iconIndex);

        Assert.False(resolved);
        Assert.Equal("", targetPath);
        Assert.Equal("", iconPath);
        Assert.Equal(0, iconIndex);
    }
}
