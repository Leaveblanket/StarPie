using System;
using WinPieGestures.Services.Dialogs;

namespace WinPieGestures.Tests;

/// <summary>
/// 输入对话框 ViewModel 的行为覆盖 (T07, ADR-0004)：确认逻辑与迁移前 InputDialog code-behind 一致——
/// 去除首尾空白、空输入拦截（固定文案）、验证回调只收到去空白文本且拒绝时保留错误信息；
/// 取消与无效输入不产生结果（BuildResult 为 null）。无 WPF 依赖，validator 直接传委托。
/// T20 起完成经 <see cref="InputViewModel.IsCompleted"/> 可观察状态驱动，无效提示经 IDialogService。
/// </summary>
public sealed class InputViewModelTests
{
    private static InputViewModel Create(
        TestDialogService dialogs,
        string defaultText = "",
        Func<string, (bool IsValid, string ErrorMessage)>? validator = null)
        => new("测试标题", "测试提示", defaultText, validator, dialogs);

    // --- 构造状态 -----------------------------------------------------------------

    [Fact]
    public void Constructor_CarriesTitlePromptAndDefaultText()
    {
        var vm = Create(new TestDialogService(), defaultText: "默认名");

        Assert.Equal("测试标题", vm.Title);
        Assert.Equal("测试提示", vm.Prompt);
        Assert.Equal("默认名", vm.InputText);
        Assert.Null(vm.BuildResult()); // 尚未确认，无结果
        Assert.False(vm.IsCompleted);
    }

    // --- 确认有效 -----------------------------------------------------------------

    [Fact]
    public void Confirm_NonEmptyText_TrimsAndCompletesWithResult()
    {
        var vm = Create(new TestDialogService(), defaultText: "  游戏模式  ");

        vm.ConfirmCommand.Execute(null);

        Assert.True(vm.IsCompleted);
        Assert.Equal("游戏模式", vm.BuildResult()!.Text); // 确认文本去首尾空白，与迁移前一致
    }

    [Fact]
    public void Confirm_WithoutValidator_AcceptsAnyNonEmptyText()
    {
        var vm = Create(new TestDialogService(), defaultText: "任意文本");

        vm.ConfirmCommand.Execute(null);

        Assert.True(vm.IsCompleted);
        Assert.Equal("任意文本", vm.BuildResult()!.Text);
    }

    [Fact]
    public void Confirm_ValidatorAccepts_CompletesWithResult()
    {
        var vm = Create(new TestDialogService(), defaultText: "myapp.exe", validator: _ => (true, ""));

        vm.ConfirmCommand.Execute(null);

        Assert.True(vm.IsCompleted);
        Assert.Equal("myapp.exe", vm.BuildResult()!.Text);
    }

    [Fact]
    public void Confirm_ValidatorReceivesTrimmedInput()
    {
        string? seen = null;
        var vm = Create(new TestDialogService(), defaultText: "  spaced  ", validator: input => { seen = input; return (true, ""); });

        vm.ConfirmCommand.Execute(null);

        Assert.Equal("spaced", seen); // validator 只见去空白文本，与迁移前一致
    }

    // --- 空输入 -------------------------------------------------------------------

    [Fact]
    public void Confirm_EmptyText_ShowsNoticeAndDoesNotComplete()
    {
        var dialogs = new TestDialogService();
        var vm = Create(dialogs, defaultText: "");

        vm.ConfirmCommand.Execute(null);

        // 空输入固定文案经对话框服务提示，窗口保持打开、无结果
        var call = Assert.Single(dialogs.InfoCalls);
        Assert.Equal(I18n.T("Notice"), call.Title);
        Assert.Equal(I18n.T("InputDialogEmpty"), call.Message);
        Assert.False(vm.IsCompleted);
        Assert.Null(vm.RejectedText); // 空输入不携带被拒文本（区别于 validator 拒绝）
        Assert.Null(vm.BuildResult());
    }

    [Fact]
    public void Confirm_WhitespaceOnlyText_IsRejectedAsEmpty()
    {
        var dialogs = new TestDialogService();
        var vm = Create(dialogs, defaultText: "   ");

        vm.ConfirmCommand.Execute(null);

        var call = Assert.Single(dialogs.InfoCalls);
        Assert.Equal(I18n.T("InputDialogEmpty"), call.Message);
        Assert.False(vm.IsCompleted);
        Assert.Null(vm.RejectedText);
        Assert.Null(vm.BuildResult());
    }

    // --- 验证回调拒绝 ----------------------------------------------------------------

    [Fact]
    public void Confirm_ValidatorRejects_ShowsValidatorMessageAndDoesNotComplete()
    {
        var dialogs = new TestDialogService();
        var vm = Create(
            dialogs,
            defaultText: "已占用",
            validator: _ => (false, "已存在同名的配置方案，请换一个名称！"));

        vm.ConfirmCommand.Execute(null);

        // 提示文案来自 validator；被拒文本保留供视图全选；窗口保持打开
        var call = Assert.Single(dialogs.InfoCalls);
        Assert.Equal("已存在同名的配置方案，请换一个名称！", call.Message);
        Assert.Equal("已占用", vm.RejectedText);
        Assert.False(vm.IsCompleted);
        Assert.Null(vm.BuildResult());
    }

    [Fact]
    public void Confirm_AfterRejectedAttempt_SucceedsOnRetry()
    {
        var calls = 0;
        var vm = Create(
            new TestDialogService(),
            defaultText: "第一次",
            validator: _ => ++calls <= 1 ? (false, "第一次拒绝") : (true, ""));

        vm.ConfirmCommand.Execute(null);
        Assert.False(vm.IsCompleted); // 第一次被拒

        vm.ConfirmCommand.Execute(null);
        Assert.True(vm.IsCompleted); // 重试通过，可再次确认
        Assert.Equal("第一次", vm.BuildResult()!.Text);
    }
}
