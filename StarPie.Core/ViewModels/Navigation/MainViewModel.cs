using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StarPie.ViewModels.Navigation
{
    /// <summary>
    /// 主框架导航区 ViewModel：设置控制台窗口导航区（导航栏与页面内容区）的 DataContext。
    /// </summary>
    /// <remarks>
    /// <see cref="CurrentViewModel"/> 供页面 ContentControl 呈现当前页面（DataTemplate 按 VM 类型
    /// 映射页面 View），<see cref="NavigationItems"/> 供侧边栏数据驱动呈现。导航项（顺序/标识/
    /// 标题键/图标/目标类型/导航动作）全部来自 <see cref="NavigationCatalog"/> 注册
    /// （模块注册器 <c>RegisterNavigation</c> 写入），点击经 <see cref="INavigationExecutor"/>
    /// 按槽位惰性解析页面 VM——本 VM 不硬编码页面 VM 类型。壳层职责（WindowTitle/IsExiting/Save）
    /// 在 Host 的 ShellViewModel；主框架分区 DataContext：导航区绑本 VM、壳区绑壳层 VM。
    /// 本 VM 为容器单例并实现 <see cref="IDisposable"/>：与本地化事件成对退订，
    /// 由 AppHost.Dispose 调用（兼作测试拆卸）。
    /// </remarks>
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly NavigationStore _store;
        private readonly ILocalizationService _localization;

        /// <summary>导航项（按 NavigationCatalog 槽位 0–4 注册顺序，即侧边栏顺序）。</summary>
        public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

        /// <summary>当前页面 ViewModel（经 NavigationStore 转发；启动初始导航前为 null）。</summary>
        public ObservableObject? CurrentViewModel => _store.CurrentViewModel;

        public MainViewModel(
            NavigationStore store,
            NavigationCatalog catalog,
            INavigationExecutor navigation,
            ILocalizationService localization)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (navigation == null) throw new ArgumentNullException(nameof(navigation));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _store = store;

            NavigationItems = new ObservableCollection<NavigationItemViewModel>(
                catalog.Entries.Select(entry => new NavigationItemViewModel(
                    entry.AutomationId,
                    entry.TitleKey,
                    entry.IconData,
                    entry.ViewModelType,
                    () => navigation.Navigate(entry.Slot),
                    _localization)));

            store.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(NavigationStore.CurrentViewModel))
                {
                    OnPropertyChanged(nameof(CurrentViewModel));
                    SyncSelection();
                }
            };

            // 导航项标题属驻留文案：语言切换时即时重取，不随页面重建刷新。
            _localization.LanguageChanged += RefreshTitles;

            SyncSelection();
        }

        /// <summary>随导航当前页同步各导航项选中态（按目录注册的目标类型比对，数据驱动不依赖 Tag 数字索引）。</summary>
        private void SyncSelection()
        {
            Type? currentType = _store.CurrentViewModel?.GetType();
            foreach (NavigationItemViewModel item in NavigationItems)
            {
                item.IsSelected = item.TargetViewModelType == currentType;
            }
        }

        /// <summary>语言切换后按导航项的标题键重取本地化文本。</summary>
        private void RefreshTitles()
        {
            foreach (NavigationItemViewModel item in NavigationItems)
            {
                item.Title = _localization.GetString(item.TitleKey);
            }
        }

        /// <summary>退订本地化事件（容器单例成对退订；由 AppHost.Dispose 调用）。</summary>
        public void Dispose()
        {
            _localization.LanguageChanged -= RefreshTitles;
        }
    }
}
