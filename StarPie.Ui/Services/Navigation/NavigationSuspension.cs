using System;
using System.ComponentModel;
using System.Linq;
using StarPie.Sdk.Services.Navigation;
using StarPie.Ui.Services.Navigation;

namespace StarPie.Ui.Services.Navigation
{
    /// <summary>
    /// 导航视图出账与恢复重放：进托盘时把当前页视图出容器（当前页 VM 置空 →
    /// ContentControl 释放视图树；固定页 VM 容器单例保留，插件页 VM 工厂新建型随出账
    /// 真实回收），并记录最后导航槽位；恢复时按记录重放导航重建视图，选中态随
    /// <see cref="NavigationStore"/> 变更由导航区回灌。
    /// </summary>
    /// <remarks>
    /// 出账幂等：当前页已置空时重复调用 no-op，不覆盖已记录的槽位；lastSlot 为空
    /// （从未出账，如启动首次显示）重放 no-op。
    /// </remarks>
    public sealed class NavigationSuspension
    {
        private readonly NavigationStore _store;
        private readonly NavigationCatalog _catalog;
        private readonly INavigationExecutor _navigation;
        private string? _lastIdentifier;

        public NavigationSuspension(
            NavigationStore store,
            NavigationCatalog catalog,
            INavigationExecutor navigation)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        }

        /// <summary>当前已记录、待恢复的导航标识（诊断/测试口径；null 即无待重放）。</summary>
        public string? PendingIdentifier => _lastIdentifier;

        /// <summary>导航视图出账：记录当前页目录标识并置空当前页 VM；已出账（当前页为空）时 no-op 返回 false。</summary>
        public bool Release()
        {
            INotifyPropertyChanged? current = _store.CurrentViewModel;
            if (current is null)
            {
                return false;
            }

            Type currentType = current.GetType();
            _lastIdentifier = _catalog.Entries
                .FirstOrDefault(entry => entry.ViewModelType == currentType)?
                .Identifier;
            _store.CurrentViewModel = null;
            return true;
        }

        /// <summary>恢复重放：按最后导航槽位重放导航重建视图；无记录（从未出账）时 no-op 返回 false。</summary>
        public bool Restore()
        {
            if (_lastIdentifier is not { } identifier)
            {
                return false;
            }

            _lastIdentifier = null;
            _navigation.Navigate(identifier);
            return true;
        }
    }
}
