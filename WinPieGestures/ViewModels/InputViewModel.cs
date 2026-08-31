using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WinPieGestures.ViewModels
{
    /// <summary>
    /// 输入对话框 ViewModel (T07, ADR-0001/0004)：接管迁移前 InputDialog code-behind 的全部确认逻辑——
    /// 去除首尾空白、空输入拦截（固定文案）、验证回调判定。有效性规则收在 VM：确认无效时经
    /// <see cref="ValidationFailed"/> 携带提示文案交视图层弹窗（窗口保持打开）；确认有效时经
    /// <see cref="CloseRequested"/> 携带结果请求关窗；取消不经过事件，由视图直接关窗。
    /// 结果遵循可空结果对象约定（ADR-0004）：取消与无效输入不产生结果，<see cref="BuildResult"/> 为 null。
    /// </summary>
    public partial class InputViewModel : ObservableObject
    {
        private readonly Func<string, (bool IsValid, string ErrorMessage)>? _validator;
        private InputDialogResult? _result;

        /// <summary>对话框标题（窗口标题与头部文案共用）。</summary>
        public string Title { get; }

        /// <summary>输入提示文案。</summary>
        public string Prompt { get; }

        [ObservableProperty]
        private string _inputText;

        /// <summary>确认有效后请求窗口关闭，携带已去空白的输入结果；确认无效时不触发（无效走 <see cref="ValidationFailed"/>）。</summary>
        public event Action<InputDialogResult?>? CloseRequested;

        /// <summary>
        /// 确认无效：message 为提示文案（空输入固定文案或 validator 错误信息）；
        /// rejectedText 为被拒的已去空白文本，空输入时为 null（视图据此复刻迁移前焦点行为——仅 validator 无效时全选）。
        /// </summary>
        public event Action<string, string?>? ValidationFailed;

        public InputViewModel(
            string title,
            string prompt,
            string defaultText = "",
            Func<string, (bool IsValid, string ErrorMessage)>? validator = null)
        {
            Title = title;
            Prompt = prompt;
            _inputText = defaultText;
            _validator = validator;
        }

        /// <summary>确认结果：仅在确认有效后非空；取消与无效输入为 null。</summary>
        public InputDialogResult? BuildResult() => _result;

        [RelayCommand]
        private void Confirm()
        {
            string trimmed = (InputText ?? "").Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                ValidationFailed?.Invoke(I18n.T("InputDialogEmpty"), null);
                return;
            }

            if (_validator != null)
            {
                var (isValid, errorMessage) = _validator(trimmed);
                if (!isValid)
                {
                    ValidationFailed?.Invoke(errorMessage, trimmed);
                    return;
                }
            }

            _result = new InputDialogResult(trimmed);
            CloseRequested?.Invoke(_result);
        }
    }
}
