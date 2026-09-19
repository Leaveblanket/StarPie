using System;
using SharpHook.Data;
using StarPie.Ui;

namespace StarPie.Tests;

/// <summary>
/// 测试实例的触发键覆盖解析：只有声明了测试实例的进程受理 <c>--trigger-button=</c>，
/// 取值限于 Button1–Button5，其余一律返回 null（触发键交由运行态配置决定，
/// 见 <see cref="TestInstanceSwitches"/> 与 ADR-0056）。
/// </summary>
/// <remarks>
/// 解析是全字符串匹配 + 手工取数，边界（前缀大小写、缺等号、非数字、越界、
/// 非测试实例）都在这里钉住——覆盖值来自命令行，是进程启动期唯一的外部输入。
/// </remarks>
public sealed class TestInstanceSwitchesTests
{
    // --- 测试实例标记（与单实例闸门绕过、退出消息受理同一份判定） -------------

    [Theory]
    [InlineData("StarPie.exe --allow-multiple")]
    [InlineData("StarPie.exe --test-instance")]
    [InlineData("\"C:\\Program Files\\StarPie\\StarPie.exe\" --Allow-Multiple --trigger-button=4")]
    public void IsTestInstance_RecognisesBothSwitches_CaseInsensitively(string commandLine)
        => Assert.True(TestInstanceSwitches.IsTestInstance(commandLine));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("StarPie.exe")]
    [InlineData("StarPie.exe --trigger-button=4")]
    public void IsTestInstance_WithoutSwitch_IsFalse(string? commandLine)
        => Assert.False(TestInstanceSwitches.IsTestInstance(commandLine));

    // --- 触发键取值 --------------------------------------------------------

    [Theory]
    [InlineData(1, MouseButton.Button1)]
    [InlineData(2, MouseButton.Button2)]
    [InlineData(3, MouseButton.Button3)]
    [InlineData(4, MouseButton.Button4)]
    [InlineData(5, MouseButton.Button5)]
    public void Resolve_TestInstanceWithValidNumber_ReturnsThatButton(int number, MouseButton expected)
        => Assert.Equal(
            expected,
            TestInstanceSwitches.Resolve($"StarPie.exe --allow-multiple --trigger-button={number}"));

    [Fact]
    public void Resolve_SideButton_IsTheXButtonPair()
    {
        // 侧键身份：SharpHook Button4/Button5 ⟷ libuiohook MOUSE_BUTTON4/5 ⟷ Windows
        // XBUTTON1/XBUTTON2（捕获走 WM_XBUTTONDOWN，注入走 MOUSEEVENTF_XDOWN）。
        // e2e 的注入层按同一映射发侧键（tests/mouse_input.py 的 SIDE_BUTTON）。
        Assert.Equal(
            MouseButton.Button4,
            TestInstanceSwitches.Resolve("StarPie.exe --allow-multiple --trigger-button=4"));
        Assert.Equal(
            MouseButton.Button5,
            TestInstanceSwitches.Resolve("StarPie.exe --allow-multiple --trigger-button=5"));
    }

    [Fact]
    public void Resolve_NonTestInstance_IgnoresTheSwitch()
        => Assert.Null(TestInstanceSwitches.Resolve("StarPie.exe --trigger-button=4"));

    [Theory]
    [InlineData("StarPie.exe --allow-multiple")]
    [InlineData("StarPie.exe --allow-multiple --trigger-button=")]
    [InlineData("StarPie.exe --allow-multiple --trigger-button=abc")]
    [InlineData("StarPie.exe --allow-multiple --trigger-button=0")]
    [InlineData("StarPie.exe --allow-multiple --trigger-button=6")]
    [InlineData("StarPie.exe --allow-multiple --trigger-button=-1")]
    [InlineData("StarPie.exe --allow-multiple --trigger-button")]
    public void Resolve_MissingOrInvalidValue_YieldsNoOverride(string commandLine)
        => Assert.Null(TestInstanceSwitches.Resolve(commandLine));

    [Fact]
    public void Resolve_TestInstanceWithoutSwitch_YieldsNoOverride()
        => Assert.Null(TestInstanceSwitches.Resolve("StarPie.exe --allow-multiple"));

    [Fact]
    public void Resolve_TrailingSwitchAfterValue_DoesNotLeakIntoTheNumber()
    {
        // 取数只吃紧跟等号的连续数字，后续开关不被吞进取值。
        Assert.Equal(
            MouseButton.Button4,
            TestInstanceSwitches.Resolve("StarPie.exe --allow-multiple --trigger-button=4 --test-instance"));
    }
}
