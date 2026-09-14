using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace StarPie.ViewModels.Navigation
{
    /// <summary>
    /// 主框架壳层 ViewModel：承接主窗口壳层职责——窗口标题（<see cref="WindowTitle"/>，
    /// 随 I18n 语言广播刷新）与保存（<c>Save</c>：立即落盘请求 + 成功提示）。
    /// </summary>
    /// <remarks>
    /// <see cref="MainView"/> 按区域分区 DataContext：壳区绑本 VM、导航区绑
    /// <see cref="MainViewModel"/>；生命周期同设置台会话（一个会话一份，
    /// <see cref="IDisposable"/> 成对退订本地化静态事件，随会话作用域释放）。
    /// 进程退出态不在这里：它归常驻壳层（<see cref="ShellHost"/>）——退出是壳层的编排，
    /// 设置台只是被关闭。
    /// </remarks>
    public partial class ShellViewModel : ObservableObject, IDisposable
    {
        private readonly IMessenger _messenger;
        private readonly IDialogService _dialogs;
        private readonly ILocalizationService _localization;

        /// <summary>壳层窗口标题：WindowTitle 键 + DevInstance 标记；语言切换随本 VM 刷新。</summary>
        public string WindowTitle => _localization.GetString("WindowTitle") + DevInstance.Suffix;

        public ShellViewModel(
            IMessenger messenger,
            IDialogService dialogs,
            ILocalizationService localization)
        {
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));

            // 进程级 VM 配 IDisposable 成对退订（AppHost.Dispose 调用）。
            _localization.LanguageChanged += RefreshWindowTitle;
        }

        [RelayCommand]
        private void Save()
        {
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
            _dialogs.ShowInfo(_localization.GetString("Notice"), _localization.GetString("MsgSaveSuccess"));
        }

        /// <summary>语言切换后刷新壳层窗口标题；壳层静态文案为声明式 {DynamicResource}，
        /// 不在此处理。</summary>
        private void RefreshWindowTitle()
        {
            OnPropertyChanged(nameof(WindowTitle));
        }

        /// <summary>退订本地化静态事件（进程级 VM 成对退订；由 AppHost.Dispose 调用）。</summary>
        public void Dispose()
        {
            _localization.LanguageChanged -= RefreshWindowTitle;
        }
    }
}
