using System;
using System.Collections.Generic;
using StarPie.Kernel.Localization;
using StarPie.PluginHosting;
using StarPie.PluginHosting.Extensions;

namespace StarPie.Services.Shell
{
    /// <summary>
    /// 托盘菜单合成：内置条目之后追加插件菜单项（权重升序、插件 id 稳定序），
    /// 点击路由到插件注册的命令。
    /// </summary>
    /// <remarks>
    /// 插件菜单每次打开时按当前注册表重建，故插件装载/停用后菜单即时反映；
    /// 无插件菜单项时不追加分隔线——空壳分隔线不作为降级表现。
    /// </remarks>
    public static class TrayMenuComposer
    {
        /// <summary>合成宿主托盘菜单。</summary>
        /// <param name="builtIn">宿主内置条目（非 null）。</param>
        /// <param name="coordinator">插件 UI 托管门面（非 null）；其菜单项为空时原样返回内置条目。</param>
        /// <param name="localization">文案服务（非 null），按菜单项标题键取词。</param>
        public static IReadOnlyList<TrayMenuEntry> Compose(
            IReadOnlyList<TrayMenuEntry> builtIn,
            PluginUiCoordinator coordinator,
            ILocalizationService localization)
        {
            ArgumentNullException.ThrowIfNull(builtIn);
            ArgumentNullException.ThrowIfNull(coordinator);
            ArgumentNullException.ThrowIfNull(localization);

            IReadOnlyList<PluginMenuItem> pluginItems = coordinator.MenuItems;
            if (pluginItems.Count == 0)
            {
                return builtIn;
            }

            var entries = new List<TrayMenuEntry>(builtIn.Count + pluginItems.Count + 1);
            entries.AddRange(builtIn);
            entries.Add(TrayMenuEntry.Separator());
            foreach (PluginMenuItem pluginItem in pluginItems)
            {
                string commandId = pluginItem.Descriptor.CommandId;
                entries.Add(TrayMenuEntry.Item(
                    localization.GetString(pluginItem.Descriptor.TitleKey),
                    () => coordinator.ExecuteCommand(commandId)));
            }

            return entries;
        }
    }
}
