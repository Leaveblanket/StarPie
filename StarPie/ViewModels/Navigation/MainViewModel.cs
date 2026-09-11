using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    /// （模块注册器 <c>RegisterNavigation</c> 写入），点击与"选中态置真"都经
    /// <see cref="INavigationExecutor"/> 按槽位惰性解析页面 VM——本 VM 不硬编码页面 VM 类型。
    /// 选中态驱动导航（而非仅命令驱动）是为 UIA <c>SelectionItem.Select</c> 留出入口：
    /// 自动化/无障碍客户端只能置选中态、不产生鼠标输入，e2e 静默导航依赖这条路径。
    /// 壳层职责（WindowTitle/IsExiting/Save）
    /// 在 Host 的 ShellViewModel；主框架分区 DataContext：导航区绑本 VM、壳区绑壳层 VM。
    /// 本 VM 为容器单例并实现 <see cref="IDisposable"/>：与本地化事件成对退订，
    /// 由 AppHost.Dispose 调用（兼作测试拆卸）。
    /// </remarks>
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly NavigationStore _store;
        private readonly ILocalizationService _localization;

        /// <summary>导航项（按 NavigationCatalog 槽位 0–3 注册顺序，即侧边栏顺序）。</summary>
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

            // 导航项选中态 → 导航：点击（RadioButton Command）与 UIA SelectionItem.Select
            // 都会把 IsChecked(↔IsSelected) 置真，两条路径最终都落到目录执行缝且幂等
            // （同槽位导航命中同一页面 VM 单例，store 对同实例不重发变更）。
            foreach (NavigationItemViewModel item in NavigationItems)
            {
                item.PropertyChanged += OnNavigationItemPropertyChanged;
            }

            // 导航项标题属驻留文案：语言切换时即时重取，不随页面重建刷新。
            _localization.LanguageChanged += RefreshTitles;

            SyncSelection();
        }

        /// <summary>
        /// 选中态置真即导航（UIA <c>Select</c> 与点击共用的入口）；已停在目标页时跳过，
        /// 避免 <see cref="SyncSelection"/> 回灌选中态时自我导航。
        /// </summary>
        private void OnNavigationItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(NavigationItemViewModel.IsSelected) ||
                sender is not NavigationItemViewModel item ||
                !item.IsSelected ||
                item.TargetViewModelType == _store.CurrentViewModel?.GetType())
            {
                return;
            }

            item.NavigateCommand.Execute(null);
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
