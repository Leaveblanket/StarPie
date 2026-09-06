namespace WinPieGestures.Services.Messages
{
    /// <summary>弹窗级别（VM 层不引用 WPF 类型，窗口据此映射 MessageBoxImage）。</summary>
    public enum NoticeKind { Info, Warning, Error }

    /// <summary>弹窗请求：标题、正文与级别。</summary>
    public sealed record NoticeRequest(string Title, string Message, NoticeKind Kind);
}