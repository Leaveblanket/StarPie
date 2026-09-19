using SharpHook.Data;
using StarPie.Sdk.Models;
using StarPie.Ui.Services.Input;

namespace StarPie.Tests;

/// <summary>
/// 配置键名 → SharpHook 鼠标键的解析：五个上游同名值域全部可达、大小写与首尾空白容忍，
/// 缺键 / 未知值一律回退默认右键（解析在钩子线程的按下 / 抬起事件上运行，绝不抛异常）。
/// </summary>
public sealed class TriggerButtonParserTests
{
    [Theory]
    [InlineData("LeftButton", MouseButton.Button1)]
    [InlineData("RightButton", MouseButton.Button2)]
    [InlineData("MiddleButton", MouseButton.Button3)]
    [InlineData("XButton1", MouseButton.Button4)]
    [InlineData("XButton2", MouseButton.Button5)]
    public void ParseOrDefault_CanonicalNames_MapToSharpHookButtons(string name, MouseButton expected)
        => Assert.Equal(expected, TriggerButtonParser.ParseOrDefault(name));

    [Theory]
    [InlineData("rightbutton", MouseButton.Button2)]
    [InlineData("  XButton2  ", MouseButton.Button5)]
    [InlineData("LEFTBUTTON", MouseButton.Button1)]
    public void ParseOrDefault_ToleratesCaseAndWhitespace(string name, MouseButton expected)
        => Assert.Equal(expected, TriggerButtonParser.ParseOrDefault(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("PinkyButton")]
    [InlineData("Button1")]
    [InlineData("2")]
    public void ParseOrDefault_UnknownValue_FallsBackToRightButton(string? name)
        => Assert.Equal(MouseButton.Button2, TriggerButtonParser.ParseOrDefault(name));

    [Fact]
    public void IsKnown_AcceptsExactlyTheFiveCanonicalNames()
    {
        Assert.True(TriggerButtonNames.IsKnown("RightButton"));
        Assert.True(TriggerButtonNames.IsKnown(" leftbutton "));
        foreach (string name in TriggerButtonNames.All)
        {
            Assert.True(TriggerButtonNames.IsKnown(name));
        }

        Assert.False(TriggerButtonNames.IsKnown(null));
        Assert.False(TriggerButtonNames.IsKnown("PinkyButton"));
        Assert.Equal(5, TriggerButtonNames.All.Count);
    }

    [Fact]
    public void Default_IsRightButton_AndIsPartOfTheKnownSet()
    {
        Assert.Equal("RightButton", TriggerButtonNames.Default);
        Assert.Contains(TriggerButtonNames.Default, TriggerButtonNames.All);
    }
}
