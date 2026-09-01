using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WinPieGestures.ViewModels
{
    /// <summary>
    /// 侧边栏导航项 ViewModel (T19)：数据驱动——图标/标题/目标页面类型/选中态；
    /// 点击经 <see cref="NavigateCommand"/> 走类型化导航服务（构造注入的 navigate 委托，
    /// 由主框架 VM 用泛型导航服务接线）。AutomationId 沿用迁移前 NavTab{0..4}，
    /// e2e（pywinauto）依赖该标识。标题随语言广播由主框架 VM 刷新。
    /// </summary>
    public partial class NavigationItemViewModel : ObservableObject
    {
        /// <summary>UIA 自动化标识（沿用迁移前 NavTab{0..4}）。</summary>
        public string AutomationId { get; }

        /// <summary>标题的 I18n 键（语言切换经主框架 VM 重设 <see cref="Title"/>）。</summary>
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
            Action navigate)
        {
            AutomationId = automationId ?? throw new ArgumentNullException(nameof(automationId));
            TitleKey = titleKey ?? throw new ArgumentNullException(nameof(titleKey));
            _title = I18n.T(titleKey);
            IconData = iconData ?? throw new ArgumentNullException(nameof(iconData));
            TargetViewModelType = targetViewModelType ?? throw new ArgumentNullException(nameof(targetViewModelType));

            if (navigate == null) throw new ArgumentNullException(nameof(navigate));
            NavigateCommand = new RelayCommand(navigate);
        }
    }
}
