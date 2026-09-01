using System;

namespace WinPieGestures.Services.Dialogs
{
    /// <summary>程序选择器的确认结果；取消与无效统一由服务返回 null，不再向调用方暴露。</summary>
    public sealed record ProgramPickResult(string Name, string Path);

    /// <summary>输入框的确认结果（已去除首尾空白的输入文本）。</summary>
    public sealed record InputDialogResult(string Text);

    /// <summary>图标选择的确认结果；IconKey 为 null 表示清除自定义图标。</summary>
    public sealed record IconPickResult(string? IconKey);

    /// <summary>颜色选择的确认结果（#AARRGGBB 十六进制串）。</summary>
    public sealed record ColorPickResult(string HexColor);

    /// <summary>屏上取色的确认结果。</summary>
    public sealed record EyedropResult(string HexColor);

    /// <summary>系统文件对话框的确认结果。</summary>
    public sealed record FilePickResult(string Path);

    /// <summary>
    /// 对话框服务 (T06, ADR-0004)：每类对话框一个具名方法、同步签名、可空结果对象——
    /// 取消与无效统一返回 <c>null</c>，调用方只判一次 null。验证回调作为参数由调用方传入。
    /// Owner 单一归属设置窗口，由组合根惰性回填（ADR-0002），不泄露进接口签名；
    /// 迁移期实现内部允许暂用旧 code-behind 窗口，接口稳定不变。
    /// </summary>
    public interface IDialogService
    {
        /// <summary>程序选择器。返回 null 表示取消或未选出有效程序。</summary>
        ProgramPickResult? ShowProgramPicker();

        /// <summary>输入框。返回 null 表示取消；文本有效性由 validator 判定（无效则留在框内）。</summary>
        InputDialogResult? ShowInputDialog(
            string title,
            string prompt,
            string defaultText = "",
            Func<string, (bool IsValid, string ErrorMessage)>? validator = null);

        /// <summary>图标选择器。返回 null 表示取消。</summary>
        IconPickResult? ShowIconPicker(string? currentIconKey);

        /// <summary>颜色选择器。返回 null 表示取消。</summary>
        ColorPickResult? ShowColorPicker(string initialHex);

        /// <summary>屏上取色器（全屏置顶工具，不用 Owner）。返回 null 表示取消。</summary>
        EyedropResult? ShowEyedropper();

        /// <summary>系统打开文件对话框（BCL 抽象一并入服务，保持边界完整）。返回 null 表示取消。</summary>
        FilePickResult? ShowOpenFileDialog(string filter, string? title = null);

        /// <summary>系统保存文件对话框（BCL 抽象一并入服务，保持边界完整）。返回 null 表示取消。</summary>
        FilePickResult? ShowSaveFileDialog(string filter, string? fileName = null, string? title = null);

        /// <summary>系统文件夹选择对话框（T12，OpenFolderDialog 同属 BCL 抽象）。返回 null 表示取消。</summary>
        FilePickResult? ShowFolderDialog(string? initialDirectory = null, string? title = null);

        /// <summary>是/否确认框 (T17)。返回 true 表示用户选择"是"。</summary>
        bool Confirm(string title, string message);

        /// <summary>信息提示框 (T17)：单按钮确认，无返回值。</summary>
        void ShowInfo(string title, string message);
    }
}
