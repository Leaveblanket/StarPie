using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StarPie.ViewModels.Navigation
{
    /// <summary>
    /// 侧边栏导航项 ViewModel：数据驱动——图标、标题、目标页面类型与选中态。
    /// </summary>
    /// <remarks>
    /// 点击经 <see cref="NavigateCommand"/> 走目录执行缝（构造注入的 navigate 委托，
    /// 由主框架 VM 用 <see cref="INavigationExecutor"/> 按槽位接线）。AutomationId 固定为
    /// NavTab{0..4}（e2e 依赖）；标题属驻留文案，随语言切换由主框架 VM 刷新。
    /// </remarks>
    public partial class NavigationItemViewModel : ObservableObject
    {
        /// <summary>UIA 自动化标识（NavTab{槽位}，e2e 依赖）。</summary>
        public string AutomationId { get; }

        /// <summary>标题的本地化键（语言切换经主框架 VM 重设 <see cref="Title"/>）。</summary>
        public string TitleKey { get; }

        /// <summary>导航项标题（已本地化）。</summary>
        [ObservableProperty]
        private string _title;

        /// <summary>导航图标矢量路径数据。</summary>
        public string IconData { get; }

        /// <summary>目标页面 ViewModel 类型（选中态判定与新页面注册的依据）。</summary>
        public Type TargetViewModelType { get; }

        /// <summary>当前导航是否停在本项目标页（随 NavigationStore 同步，驱动 RadioButton 选中态）。</summary>
        [ObservableProperty]
        private bool _isSelected;

        public IRelayCommand NavigateCommand { get; }

        public NavigationItemViewModel(
            string automationId,
            string titleKey,
            string iconData,
            Type targetViewModelType,
            Action navigate,
            ILocalizationService localization)
        {
            AutomationId = automationId ?? throw new ArgumentNullException(nameof(automationId));
            TitleKey = titleKey ?? throw new ArgumentNullException(nameof(titleKey));
            _title = localization.GetString(titleKey);
            IconData = iconData ?? throw new ArgumentNullException(nameof(iconData));
            TargetViewModelType = targetViewModelType ?? throw new ArgumentNullException(nameof(targetViewModelType));

            if (navigate == null) throw new ArgumentNullException(nameof(navigate));
            NavigateCommand = new RelayCommand(navigate);
        }
    }
}
