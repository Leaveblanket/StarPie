using System;
using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// 输入对话框 ViewModel 的行为覆盖 (T07, ADR-0004)：确认逻辑与迁移前 InputDialog code-behind 一致——
/// 去除首尾空白、空输入拦截（固定文案）、验证回调只收到去空白文本且拒绝时保留错误信息；
/// 取消与无效输入不产生结果（BuildResult 为 null）。无 WPF 依赖，validator 直接传委托。
/// </summary>
public sealed class InputViewModelTests
{
    private static InputViewModel Create(
        string defaultText = "",
        Func<string, (bool IsValid, string ErrorMessage)>? validator = null)
        => new("测试标题", "测试提示", defaultText, validator);

    // --- 构造状态 -----------------------------------------------------------------

    [Fact]
    public void Constructor_CarriesTitlePromptAndDefaultText()
    {
        var vm = Create(defaultText: "默认名");

        Assert.Equal("测试标题", vm.Title);
        Assert.Equal("测试提示", vm.Prompt);
        Assert.Equal("默认名", vm.InputText);
        Assert.Null(vm.BuildResult()); // 尚未确认，无结果
    }

    // --- 确认有效 -----------------------------------------------------------------

    [Fact]
    public void Confirm_NonEmptyText_TrimsAndRaisesCloseRequestedWithResult()
    {
        var vm = Create(defaultText: "  游戏模式  ");
        InputDialogResult? received = null;
        vm.CloseRequested += r => received = r;

        vm.ConfirmCommand.Execute(null);

        Assert.NotNull(received);
        Assert.Equal("游戏模式", received!.Text); // 确认文本去首尾空白，与迁移前一致
        Assert.Equal("游戏模式", vm.BuildResult()!.Text);
    }

    [Fact]
    public void Confirm_WithoutValidator_AcceptsAnyNonEmptyText()
    {
        var vm = Create(defaultText: "任意文本");
        InputDialogResult? received = null;
        vm.CloseRequested += r => received = r;

        vm.ConfirmCommand.Execute(null);

        Assert.NotNull(received);
        Assert.Equal("任意文本", received!.Text);
    }

    [Fact]
    public void Confirm_ValidatorAccepts_RaisesCloseRequestedWithResult()
    {
        var vm = Create(defaultText: "myapp.exe", validator: _ => (true, ""));
        InputDialogResult? received = null;
        vm.CloseRequested += r => received = r;

        vm.ConfirmCommand.Execute(null);

        Assert.NotNull(received);
        Assert.Equal("myapp.exe", received!.Text);
    }

    [Fact]
    public void Confirm_ValidatorReceivesTrimmedInput()
    {
        string? seen = null;
        var vm = Create(defaultText: "  spaced  ", validator: input => { seen = input; return (true, ""); });

        vm.ConfirmCommand.Execute(null);

        Assert.Equal("spaced", seen); // validator 只见去空白文本，与迁移前一致
    }

    // --- 空输入 -------------------------------------------------------------------

    [Fact]
    public void Confirm_EmptyText_RaisesValidationFailedWithFixedMessage_WithoutCloseRequest()
    {
        var vm = Create(defaultText: "");
        InputDialogResult? closed = new("sentinel");
        (string Message, string? Rejected)? failed = null;
        vm.CloseRequested += r => closed = r;
        vm.ValidationFailed += (message, rejected) => failed = (message, rejected);

        vm.ConfirmCommand.Execute(null);

        Assert.NotNull(failed);
        Assert.Equal(I18n.T("InputDialogEmpty"), failed!.Value.Message); // 空输入固定文案
        Assert.Null(failed.Value.Rejected);
        Assert.Equal(new("sentinel"), closed); // 未请求关闭
        Assert.Null(vm.BuildResult()); // 无效输入不产生结果
    }

    [Fact]
    public void Confirm_WhitespaceOnlyText_IsRejectedAsEmpty()
    {
        var vm = Create(defaultText: "   ");
        (string, string?)? failed = null;
        vm.ValidationFailed += (message, rejected) => failed = (message, rejected);

        vm.ConfirmCommand.Execute(null);

        Assert.NotNull(failed);
        Assert.Equal(I18n.T("InputDialogEmpty"), failed!.Value.Item1);
        Assert.Null(failed.Value.Item2);
    }

    // --- 验证回调拒绝 ----------------------------------------------------------------

    [Fact]
    public void Confirm_ValidatorRejects_RaisesValidationFailedWithValidatorMessage_WithoutCloseRequest()
    {
        var vm = Create(
            defaultText: "已占用",
            validator: _ => (false, "已存在同名的配置方案，请换一个名称！"));
        InputDialogResult? closed = null;
        (string Message, string? Rejected)? failed = null;
        vm.CloseRequested += r => closed = r;
        vm.ValidationFailed += (message, rejected) => failed = (message, rejected);

        vm.ConfirmCommand.Execute(null);

        Assert.NotNull(failed);
        Assert.Equal("已存在同名的配置方案，请换一个名称！", failed!.Value.Message); // 提示文案来自 validator
        Assert.Equal("已占用", failed.Value.Rejected); // validator 无效时携带被拒文本（视图据此全选）
        Assert.Null(closed); // 窗口保持打开
        Assert.Null(vm.BuildResult()); // 无效输入不产生结果
    }

    [Fact]
    public void Confirm_AfterRejectedAttempt_SucceedsOnRetry()
    {
        var calls = 0;
        var vm = Create(
            defaultText: "第一次",
            validator: _ => ++calls <= 1 ? (false, "第一次拒绝") : (true, ""));
        InputDialogResult? received = null;
        vm.CloseRequested += r => received = r;

        vm.ConfirmCommand.Execute(null);
        Assert.Null(received); // 第一次被拒

        vm.ConfirmCommand.Execute(null);
        Assert.NotNull(received); // 重试通过，可再次确认
        Assert.Equal("第一次", received!.Text);
    }
}
