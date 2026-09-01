using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinPieGestures.Services;

namespace WinPieGestures.ViewModels
{
    /// <summary>
    /// 主框架 ViewModel (T19)：设置控制台窗口 DataContext 的单一根源——
    /// <see cref="CurrentViewModel"/> 供 ContentControl 呈现当前页面（DataTemplate 按 VM 类型
    /// 映射页面 View），<see cref="NavigationItems"/> 供侧边栏数据驱动呈现。
    /// 导航项选中态随 NavigationStore 当前页同步；标题随 I18n 语言广播刷新（本 VM 容器单例，
    /// 与应用同生命周期，静态广播订阅无泄漏）。
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly NavigationStore _store;

        /// <summary>五个导航项（触发与场景/外观与形态/手势与动作/高级与系统/关于与更新），顺序即侧边栏顺序。</summary>
        public ObservableCollection<NavigationItemViewModel> NavigationItems { get; }

        /// <summary>当前页面 ViewModel（经 NavigationStore 转发；启动初始导航前为 null）。</summary>
        public ObservableObject? CurrentViewModel => _store.CurrentViewModel;

        public MainViewModel(
            NavigationStore store,
            INavigationService<BehaviorSettingsViewModel> navTrigger,
            INavigationService<AppearanceSettingsViewModel> navAppearance,
            INavigationService<ProfileListViewModel> navGestures,
            INavigationService<GeneralSettingsViewModel> navAdvanced,
            INavigationService<AboutViewModel> navAbout)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (navTrigger == null) throw new ArgumentNullException(nameof(navTrigger));
            if (navAppearance == null) throw new ArgumentNullException(nameof(navAppearance));
            if (navGestures == null) throw new ArgumentNullException(nameof(navGestures));
            if (navAdvanced == null) throw new ArgumentNullException(nameof(navAdvanced));
            if (navAbout == null) throw new ArgumentNullException(nameof(navAbout));
            _store = store;

            NavigationItems = new ObservableCollection<NavigationItemViewModel>
            {
                new("NavTab0", "TabTrigger", IconTrigger, typeof(BehaviorSettingsViewModel), navTrigger.Navigate),
                new("NavTab1", "TabAppearance", IconAppearance, typeof(AppearanceSettingsViewModel), navAppearance.Navigate),
                new("NavTab2", "TabGestures", IconGestures, typeof(ProfileListViewModel), navGestures.Navigate),
                new("NavTab3", "TabAdvanced", IconAdvanced, typeof(GeneralSettingsViewModel), navAdvanced.Navigate),
                new("NavTab4", "TabAbout", IconAbout, typeof(AboutViewModel), navAbout.Navigate)
            };

            store.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(NavigationStore.CurrentViewModel))
                {
                    OnPropertyChanged(nameof(CurrentViewModel));
                    SyncSelection();
                }
            };

            // 语言切换 → 导航项标题即时刷新（I18n 静态广播，ADR-0002 判据不变）
            I18n.LanguageChanged += RefreshTitles;

            SyncSelection();
        }

        /// <summary>随导航当前页同步各导航项选中态（目标类型比对，数据驱动不再依赖 Tag 数字索引）。</summary>
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
                item.Title = I18n.T(item.TitleKey);
            }
        }

        // 导航图标（迁移前 NavTab{0..4} 的 Path Data 原样搬迁）
        private const string IconTrigger =
            "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4M12,6A6,6 0 0,0 6,12A6,6 0 0,0 12,18A6,6 0 0,0 18,12A6,6 0 0,0 12,6M12,8A4,4 0 0,1 16,12A4,4 0 0,1 12,16A4,4 0 0,1 8,12A4,4 0 0,1 12,8Z";
        private const string IconAppearance =
            "M12,3C7.03,3 3,7.03 3,12C3,16.97 7.03,21 12,21C12.83,21 13.5,20.33 13.5,19.5C13.5,19.11 13.35,18.76 13.11,18.49C12.88,18.23 12.73,17.88 12.73,17.5A1.5,1.5 0 0,1 14.23,16H16C18.76,16 21,13.76 21,11C21,6.58 16.97,3 12,3M6.5,12A1.5,1.5 0 0,1 5,10.5A1.5,1.5 0 0,1 6.5,9A1.5,1.5 0 0,1 8,10.5A1.5,1.5 0 0,1 6.5,12M9.5,8A1.5,1.5 0 0,1 8,6.5A1.5,1.5 0 0,1 9.5,5A1.5,1.5 0 0,1 11,6.5A1.5,1.5 0 0,1 9.5,8M14.5,8A1.5,1.5 0 0,1 13,6.5A1.5,1.5 0 0,1 14.5,5A1.5,1.5 0 0,1 16,6.5A1.5,1.5 0 0,1 14.5,8M17.5,12A1.5,1.5 0 0,1 16,10.5A1.5,1.5 0 0,1 17.5,9A1.5,1.5 0 0,1 19,10.5A1.5,1.5 0 0,1 17.5,12Z";
        private const string IconGestures =
            "M4,6c0-1.1,0.9-2,2-2h12c1.1,0,2,0.9,2,2v12c0,1.1-0.9,2-2,2H6c-1.1,0-2-0.9-2-2V6z M6,8h12V6H6V8z M6,12h12v-2H6V12z M6,16h6v-2H6V16z";
        private const string IconAdvanced =
            "M12,15.5A3.5,3.5 0 0,1 8.5,12A3.5,3.5 0 0,1 12,8.5A3.5,3.5 0 0,1 15.5,12A3.5,3.5 0 0,1 12,15.5M19.43,12.97C19.47,12.65 19.5,12.33 19.5,12C19.5,11.67 19.47,11.34 19.43,11L21.54,9.37C21.73,9.22 21.78,8.95 21.66,8.73L19.66,5.27C19.54,5.05 19.27,4.96 19.05,5.05L16.56,6.05C16.04,5.66 15.5,5.32 14.87,5.07L14.5,2.42C14.46,2.18 14.25,2 14,2H10C9.75,2 9.54,2.18 9.5,2.42L9.13,5.07C8.5,5.32 7.96,5.66 7.44,6.05L4.95,5.05C4.73,4.96 4.46,5.05 4.34,5.27L2.34,8.73C2.21,8.95 2.27,9.22 2.46,9.37L4.57,11C4.53,11.34 4.5,11.67 4.5,12C4.5,12.33 4.53,12.65 4.57,12.97L2.46,14.63C2.27,14.78 2.21,15.05 2.34,15.27L4.34,18.73C4.46,18.95 4.73,19.03 4.95,18.95L7.44,17.94C7.96,18.34 8.5,18.68 9.13,18.93L9.5,21.58C9.54,21.82 9.75,22 10,22H14C14.25,22 14.46,21.82 14.5,21.58L14.87,18.93C15.5,18.67 16.04,18.34 16.56,17.94L19.05,18.95C19.27,19.03 19.54,18.95 19.66,18.73L21.66,15.27C21.78,15.05 21.73,14.78 21.54,14.63L19.43,12.97Z";
        private const string IconAbout =
            "M11,9H13V7H11M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M11,17H13V11H11V17Z";
    }
}
