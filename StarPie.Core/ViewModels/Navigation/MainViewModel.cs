using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StarPie.ViewModels.Navigation
{
    /// <summary>
    /// 主框架导航区 ViewModel（S5，B3/#76 目录驱动后迁入 Core）：设置控制台窗口分区 DataContext
    /// 的导航区——<see cref="CurrentViewModel"/> 供页面 ContentControl 呈现当前页面（DataTemplate
    /// 按 VM 类型映射页面 View），<see cref="NavigationItems"/> 供侧边栏数据驱动呈现。
    /// B3 起为目录驱动：导航项（顺序/标识/标题键/图标/目标类型/导航动作）全部来自
    /// <see cref="NavigationCatalog"/> 注册（模块注册器 <c>RegisterNavigation</c> 写入），
    /// 本 VM 不再硬编码任何页面 VM 类型；点击经 <see cref="INavigationExecutor"/>（目录执行缝）
    /// 按槽位惰性解析页面 VM。壳层职责（WindowTitle/IsExiting/Save）已拆至 Host 的
    /// ShellViewModel（B1/D3，ADR-0016 决策 7）；主框架分区 DataContext：
    /// 导航区绑本 VM、壳区绑壳层 VM。
    /// 生命周期（ADR-0010 第 3 条）：本 VM 为容器单例，同样实现 IDisposable 配对退订静态事件，
    /// 由 AppHost.Dispose 调用（兼作测试拆卸）。
    /// </summary>
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

            // B3/#76：目录驱动——AutomationId/TitleKey/IconData/TargetViewModelType 与槽位顺序
            // 全部来自 NavigationCatalog 注册；导航动作经 INavigationExecutor 按槽位惰性解析。
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

            // 语言切换 → 导航项标题即时刷新（I18n 静态广播，ADR-0002 判据不变；
            // ADR-0010 第 3 条：进程级 VM 配 IDisposable 成对退订）。
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

        /// <summary>退订 I18n 静态事件（ADR-0010 第 3 条：进程级 VM 也成对退订；AppHost.Dispose 调用）。</summary>
        public void Dispose()
        {
            _localization.LanguageChanged -= RefreshTitles;
        }
    }
}
