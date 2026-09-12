using StarPie.Kernel.Localization;
using StarPie.PluginRuntime.State;

namespace StarPie.PluginRuntime.Admission
{
    /// <summary>
    /// 开发者模式开关：默认关闭，开启须显式确认全信任风险披露。
    /// </summary>
    /// <remarks>
    /// 开关状态存宿主状态文件（<c>plugin-state.json</c>），开启/关闭立即落盘，跨重启保持。
    /// 披露文案随当前语言取词；确认动作由调用方（管理面）在展示披露后回传，服务不代为确认。
    /// </remarks>
    public sealed class PluginDeveloperModeService
    {
        private readonly PluginStateStore _stateStore;
        private readonly ILocalizationService _localization;

        /// <summary>构造开关服务：状态存宿主状态文件，披露文案取本地化服务。</summary>
        public PluginDeveloperModeService(PluginStateStore stateStore, ILocalizationService localization)
        {
            _stateStore = stateStore;
            _localization = localization;
        }

        /// <summary>开发者模式是否已开启（默认关闭）。</summary>
        public bool IsEnabled => _stateStore.Current.DeveloperModeEnabled;

        /// <summary>风险披露标题（用户可见文案，随当前语言）。</summary>
        public string DisclosureTitle => _localization.GetString("PluginDevModeDisclosureTitle");

        /// <summary>
        /// 全信任风险披露条目：进程内插件与宿主同权限的四条后果，逐条展示后由用户确认。
        /// </summary>
        public IReadOnlyList<string> GetDisclosurePoints() => new[]
        {
            _localization.GetString("PluginDevModeDisclosureData"),
            _localization.GetString("PluginDevModeDisclosureCode"),
            _localization.GetString("PluginDevModeDisclosureCrash"),
            _localization.GetString("PluginDevModeDisclosureUnload"),
        };

        /// <summary>
        /// 开启开发者模式：未确认披露时返回 false 且不改变任何状态；确认后立即落盘。
        /// </summary>
        public bool Enable(bool disclosureAcknowledged)
        {
            if (!disclosureAcknowledged)
            {
                return false;
            }

            if (!_stateStore.Current.DeveloperModeEnabled)
            {
                _stateStore.Current.DeveloperModeEnabled = true;
                _stateStore.Save();
            }

            return true;
        }

        /// <summary>关闭开发者模式并立即落盘；插件包与状态条目保持不动。</summary>
        public void Disable()
        {
            if (!_stateStore.Current.DeveloperModeEnabled)
            {
                return;
            }

            _stateStore.Current.DeveloperModeEnabled = false;
            _stateStore.Save();
        }
    }
}
