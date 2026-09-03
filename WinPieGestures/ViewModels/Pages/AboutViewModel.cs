using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinPieGestures.Services;

namespace WinPieGestures.ViewModels.Pages
{
    /// <summary>关于与更新页面 ViewModel。外部文件打开经组合根注入委托，避免 View 处理文件和进程副作用。</summary>
    public partial class AboutViewModel : ObservableObject
    {
        private readonly IDialogService _dialogs;
        private readonly Func<bool> _openChangelog;

        public AboutViewModel(IDialogService dialogs, Func<bool> openChangelog)
        {
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _openChangelog = openChangelog ?? throw new ArgumentNullException(nameof(openChangelog));
        }

        [RelayCommand]
        private void OpenChangelog()
        {
            try
            {
                if (!_openChangelog())
                {
                    _dialogs.ShowInfo(I18n.T("Notice"), I18n.T("ChangelogNotFound"));
                }
            }
            catch (Exception ex)
            {
                _dialogs.ShowInfo(I18n.T("Error"), $"{I18n.T("ChangelogOpenFailed")}\n{ex.Message}");
            }
        }
    }
}
