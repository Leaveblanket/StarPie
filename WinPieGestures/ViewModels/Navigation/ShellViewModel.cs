using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace StarPie.ViewModels.Navigation
{
    /// <summary>
    /// 主框架壳层 ViewModel (B1/D3，ADR-0016 决策 7)：承接主窗口壳层职责——窗口标题
    /// （<see cref="WindowTitle"/>，随 I18n 语言广播刷新）、退出态（<see cref="IsExiting"/>，
    /// 主框架 Closing 据此放行真关窗）、保存（<c>Save</c>：立即落盘请求 + 成功提示）。
    /// <see cref="MainView"/> 按区域分区 DataContext：壳区绑本 VM、导航区绑
    /// <see cref="MainViewModel"/>；生命周期同主窗口（容器单例，<see cref="IDisposable"/>
    /// 成对退订 I18n 静态事件，由 AppHost.Dispose 调用）。
    /// </summary>
    public partial class ShellViewModel : ObservableObject, IDisposable
    {
        private readonly IMessenger _messenger;
        private readonly IDialogService _dialogs;
        private readonly ILocalizationService _localization;

        /// <summary>App 级退出进行中（AppHost 在托盘退出时置位）：主框架 Closing
        /// 据此放行真关窗而非隐藏到托盘（ADR-0003）。</summary>
        public bool IsExiting { get; set; }

        /// <summary>壳层窗口标题（ADR-0010）：WindowTitle 键 + DevInstance 标记；语言切换随本 VM 刷新。</summary>
        public string WindowTitle => _localization.GetString("WindowTitle") + DevInstance.Suffix;

        public ShellViewModel(
            IMessenger messenger,
            IDialogService dialogs,
            ILocalizationService localization)
        {
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));

            // ADR-0010 第 3 条：进程级 VM 配 IDisposable 成对退订（AppHost.Dispose 调用）。
            _localization.LanguageChanged += RefreshWindowTitle;
        }

        [RelayCommand]
        private void Save()
        {
            _messenger.Send(ImmediateSaveRequestedMessage.Instance);
            _dialogs.ShowInfo(_localization.GetString("Notice"), _localization.GetString("MsgSaveSuccess"));
        }

        /// <summary>语言切换后刷新壳层窗口标题（壳层静态文案为声明式 {DynamicResource}，无需在此回填）。</summary>
        private void RefreshWindowTitle()
        {
            OnPropertyChanged(nameof(WindowTitle));
        }

        /// <summary>退订 I18n 静态事件（ADR-0010 第 3 条：进程级 VM 也成对退订；AppHost.Dispose 调用）。</summary>
        public void Dispose()
        {
            _localization.LanguageChanged -= RefreshWindowTitle;
        }
    }
}
