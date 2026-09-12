using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StarPie.Abstractions.Ui;
using StarPie.Services.Navigation;

namespace StarPie.PluginHosting.Extensions
{
    /// <summary>
    /// 固定扩展点托管：导航页（进宿主导航目录）、设置区（进宿主设置区块清单）、托盘菜单
    /// （进宿主菜单清单）的注册与出账。挂载与呈现由宿主在对应扩展点执行，本类只登记。
    /// </summary>
    /// <remarks>
    /// 每个注册项同时进 <see cref="PluginUiAssetRegistry"/>，卸载时随资产登记表整体摘除；
    /// 插件的 UI 上下文未接导航目录时（例如 headless 装配路径）页面注册只记账不进目录，
    /// 不产生空壳导航项。
    /// </remarks>
    internal sealed class PluginExtensionRegistry
    {
        /// <summary>插件页标识里允许的字符：与插件 id 共用 <c>NavPlugin_</c> 命名空间，禁止下划线以免歧义。</summary>
        private static readonly Regex NavigationKeyPattern = new(
            @"^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$",
            RegexOptions.Compiled);

        private readonly PluginUiAssetRegistry _assets;
        private readonly string _pluginId;
        private readonly NavigationCatalog? _navigationCatalog;
        private readonly List<PluginPage> _pages = new();
        private readonly List<PluginSettingsSection> _settingsSections = new();
        private readonly List<PluginMenuItem> _menuItems = new();

        internal PluginExtensionRegistry(
            PluginUiAssetRegistry assets,
            string pluginId,
            NavigationCatalog? navigationCatalog = null)
        {
            _assets = assets;
            _pluginId = pluginId;
            _navigationCatalog = navigationCatalog;
        }

        /// <summary>本插件当前注册的导航页（按注册顺序）。</summary>
        internal IReadOnlyList<PluginPage> Pages => _pages;

        /// <summary>本插件当前注册的设置区块（按注册顺序）。</summary>
        internal IReadOnlyList<PluginSettingsSection> SettingsSections => _settingsSections;

        /// <summary>本插件当前注册的托盘菜单项（按注册顺序）。</summary>
        internal IReadOnlyList<PluginMenuItem> MenuItems => _menuItems;

        /// <summary>登记导航页。</summary>
        internal IDisposable RegisterPage(PluginPageDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            if (string.IsNullOrWhiteSpace(descriptor.NavigationKey) ||
                !NavigationKeyPattern.IsMatch(descriptor.NavigationKey))
            {
                throw new ArgumentException(
                    $"插件页键必须是小写字母/数字/连字符：{descriptor.NavigationKey}",
                    nameof(descriptor));
            }
            if (string.IsNullOrWhiteSpace(descriptor.TitleKey))
            {
                throw new ArgumentException("插件页标题键不能为空", nameof(descriptor));
            }
            if (descriptor.ViewModelFactory is null)
            {
                throw new ArgumentException("插件页 VM 工厂不能为空", nameof(descriptor));
            }

            string automationId = AutomationIdFor(_pluginId);
            // 目录条目要携页面 VM 类型（导航项识别用），故这里让宿主按自己的时机取一次工厂的
            // 产物类型；导航时的实例仍由工厂现取，注册产物不做缓存。类型取值先于任何记账，
            // 插件工厂抛异常时目录与资产登记表都保持原样。
            Type viewModelType = descriptor.ViewModelFactory().GetType();
            var page = new PluginPage(
                _pluginId,
                automationId,
                descriptor.TitleKey,
                descriptor.IconData,
                descriptor.ViewModelFactory);

            // 宿主未提供导航目录时只记账：注册不失败，也不产生无处可挂的空壳导航项。
            // 目录注册先于本类记账：目录注册失败（重复标识）时不留下半登记状态。
            _navigationCatalog?.RegisterPluginPage(
                _pluginId,
                automationId,
                automationId,
                descriptor.TitleKey,
                descriptor.IconData,
                viewModelType,
                descriptor.ViewModelFactory);
            _pages.Add(page);

            return new PluginUiAssetHandle(
                _assets,
                _assets.Track(
                    _pluginId,
                    PluginUiAssetKind.Page,
                    $"导航页 {automationId}",
                    () =>
                    {
                        _navigationCatalog?.RemovePluginPage(automationId);
                        _pages.Remove(page);
                        return true;
                    }));
        }

        /// <summary>登记设置页区块。</summary>
        internal IDisposable RegisterSettingsSection(PluginSettingsSectionDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            if (string.IsNullOrWhiteSpace(descriptor.SectionKey))
            {
                throw new ArgumentException("设置区块键不能为空", nameof(descriptor));
            }
            if (descriptor.ViewModelFactory is null)
            {
                throw new ArgumentException("设置区块 VM 工厂不能为空", nameof(descriptor));
            }

            var section = new PluginSettingsSection(_pluginId, descriptor);
            _settingsSections.Add(section);

            return new PluginUiAssetHandle(
                _assets,
                _assets.Track(
                    _pluginId,
                    PluginUiAssetKind.SettingsSection,
                    $"设置区块 {descriptor.SectionKey}",
                    () =>
                    {
                        _settingsSections.Remove(section);
                        return true;
                    }));
        }

        /// <summary>登记托盘菜单项。</summary>
        internal IDisposable RegisterMenuItem(PluginMenuItemDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            if (string.IsNullOrWhiteSpace(descriptor.ItemKey))
            {
                throw new ArgumentException("菜单项键不能为空", nameof(descriptor));
            }
            if (string.IsNullOrWhiteSpace(descriptor.TitleKey))
            {
                throw new ArgumentException("菜单项标题键不能为空", nameof(descriptor));
            }

            var item = new PluginMenuItem(_pluginId, descriptor);
            _menuItems.Add(item);

            return new PluginUiAssetHandle(
                _assets,
                _assets.Track(
                    _pluginId,
                    PluginUiAssetKind.MenuItem,
                    $"菜单项 {descriptor.ItemKey}",
                    () =>
                    {
                        _menuItems.Remove(item);
                        return true;
                    }));
        }

        /// <summary>插件页的宿主签发标识：<c>NavPlugin_&lt;插件 id&gt;</c>（插件 id 已由清单校验为反向域名）。</summary>
        internal static string AutomationIdFor(string pluginId) => $"NavPlugin_{pluginId}";
    }
}
