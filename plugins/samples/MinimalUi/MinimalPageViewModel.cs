using System.ComponentModel;

namespace StarPie.Plugin.MinimalUi
{
    /// <summary>
    /// 插件页 VM：导航契约只要求 <see cref="INotifyPropertyChanged"/>，不引宿主 MVVM 框架。
    /// </summary>
    public sealed class MinimalPageViewModel : INotifyPropertyChanged
    {
        private string _message = "来自最小 UI 示例插件";

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>示例文本（视图双向绑定：编辑框回写、回显行实时更新）。</summary>
        public string Message
        {
            get => _message;
            set
            {
                if (_message == value)
                {
                    return;
                }

                _message = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
            }
        }
    }
}
