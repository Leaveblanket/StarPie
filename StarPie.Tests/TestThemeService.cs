using System;
using System.Windows;
using StarPie.Services.Shell;

namespace StarPie.Tests;

/// <summary>
/// <see cref="IThemeService"/> 的测试替身（工程约定：mock 直接 new，不使用 mocking 框架）：
/// 主题状态无操作，记录深浅色探测次数——供轮盘工厂预热与预热可回收性用例断言
/// "预热确实走完了构造窗口 → 探测深浅色的装配"。
/// </summary>
public sealed class TestThemeService : IThemeService
{
    /// <summary><see cref="IsWindowsInDarkTheme"/> 被调用次数。</summary>
    public int DarkModeProbeCalls { get; private set; }

    public string CurrentEffectiveTheme => "Light";

    public void SetTheme(string themeName) { }

    public void ApplyWindowTheme(FrameworkElement? rootElement) { }

    public string ResolveEffectiveTheme(string themeName)
        => string.IsNullOrEmpty(themeName) || themeName == "System" ? "Light" : themeName;

    public bool IsWindowsInDarkTheme()
    {
        DarkModeProbeCalls++;
        return false;
    }
}
