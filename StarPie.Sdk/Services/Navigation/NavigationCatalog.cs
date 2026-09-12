using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StarPie.Services.Navigation
{
    /// <summary>
    /// 导航槽位：全局槽位表 0–4，枚举顺序即侧边栏顺序正典（触发/外观/手势/高级/插件）。
    /// </summary>
    /// <remarks>
    /// AutomationId 由 <see cref="NavigationSlots.GetAutomationId"/> 固定为 NavPage{槽位}，0–3 是既有四页、
    /// e2e（pywinauto）依赖该标识；插件管理页是宿主追加的第五页。缺失/重复/未知槽位由共享内核收口测试拦截。
    /// </remarks>
    public enum NavigationSlot
    {
        Trigger = 0,
        Appearance = 1,
        Gestures = 2,
        Advanced = 3,
        Plugins = 4,
    }

    /// <summary>槽位表正典工具：全部槽位与 AutomationId 映射（NavPage{槽位}）。</summary>
    public static class NavigationSlots
    {
        /// <summary>全部槽位（按槽位升序）。</summary>
        public static IReadOnlyList<NavigationSlot> All { get; } = Enum.GetValues<NavigationSlot>();

        /// <summary>槽位对应的正典 UIA AutomationId（NavPage{槽位}；0–3 由 e2e 依赖）。</summary>
        public static string GetAutomationId(NavigationSlot slot)
            => "NavPage" + ((int)slot).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>导航目录注册项：槽位、AutomationId、标题键、图标数据与目标页面 VM 类型。</summary>
    /// <remarks>
    /// 固定页带 <see cref="Slot"/>（0–4，侧边栏前五页）；插件页的 <see cref="Slot"/> 为 null，
    /// 靠 <see cref="Identifier"/> 定位并由 <see cref="ViewModelFactory"/> 按需创建实例。
    /// 导航执行由宿主目录驱动执行入口惰性取目标页面 VM。
    /// </remarks>
    public sealed record NavigationPageRegistration(
        NavigationSlot? Slot,
        string AutomationId,
        string TitleKey,
        string IconData,
        Type ViewModelType)
    {
        /// <summary>目录内稳定标识：固定页为 AutomationId，插件页为宿主签发的 <c>NavPlugin_&lt;插件 id&gt;</c>。</summary>
        public string Identifier { get; init; } = AutomationId;

        /// <summary>注册该页的插件 id；固定页为 null。</summary>
        public string? PluginId { get; init; }

        /// <summary>
        /// 插件页的 VM 工厂（宿主在导航时调用，返回值须为 <see cref="ViewModelType"/> 的实例）；
        /// 固定页为 null，改经容器解析 <see cref="ViewModelType"/>。
        /// </summary>
        public Func<object>? ViewModelFactory { get; init; }

        /// <summary>是否为插件页（无固定槽位）。</summary>
        public bool IsPluginPage => Slot is null;
    }

    /// <summary>
    /// 导航目录：共享内核的页面注册契约与唯一性收口。
    /// </summary>
    /// <remarks>
    /// 未知/重复槽位与重复 AutomationId 在注册时拦截，缺失槽位由 <see cref="Validate"/> 收口。
    /// 槽位表是侧边栏顺序的唯一正典；各模块经注册器自治写入本目录，导航 VM 按目录驱动。
    /// </remarks>
    public sealed class NavigationCatalog
    {
        private readonly Dictionary<NavigationSlot, NavigationPageRegistration> _bySlot = new();
        private readonly Dictionary<string, NavigationPageRegistration> _byAutomationId =
            new(StringComparer.Ordinal);
        private readonly List<NavigationPageRegistration> _fixedEntries = new();
        private readonly List<NavigationPageRegistration> _pluginEntries = new();

        /// <summary>目录内容发生变化（插件页注册或摘除）后触发；固定页可空——目录本体不再是可变清单。</summary>
        public event Action? Changed;

        /// <summary>
        /// 注册一页：<typeparamref name="TViewModel"/> 为目标页面 VM 类型。
        /// 未知槽位抛 <see cref="ArgumentOutOfRangeException"/>；重复槽位/重复 AutomationId
        /// 抛 <see cref="InvalidOperationException"/>。
        /// </summary>
        public void RegisterPage<TViewModel>(
            NavigationSlot slot,
            string automationId,
            string titleKey,
            string iconData)
        {
            if (!Enum.IsDefined(slot))
            {
                throw new ArgumentOutOfRangeException(nameof(slot), $"未知导航槽位: {(int)slot}");
            }
            if (string.IsNullOrWhiteSpace(automationId))
            {
                throw new ArgumentException("AutomationId 不能为空", nameof(automationId));
            }
            if (string.IsNullOrWhiteSpace(titleKey))
            {
                throw new ArgumentException("标题键不能为空", nameof(titleKey));
            }
            if (_bySlot.ContainsKey(slot))
            {
                throw new InvalidOperationException($"导航槽位 {slot} 重复注册");
            }
            if (_byAutomationId.ContainsKey(automationId))
            {
                throw new InvalidOperationException($"AutomationId '{automationId}' 重复注册");
            }

            var entry = new NavigationPageRegistration(
                slot,
                automationId,
                titleKey,
                iconData,
                typeof(TViewModel));
            _bySlot[slot] = entry;
            _byAutomationId[automationId] = entry;
            _fixedEntries.Add(entry);
        }

        /// <summary>
        /// 注册一个插件页：追加在固定槽位之后，AutomationId 由宿主按 <c>NavPlugin_&lt;插件 id&gt;</c> 签发。
        /// </summary>
        /// <param name="pluginId">注册该页的插件 id（非空）。</param>
        /// <param name="identifier">目录内稳定标识（非空；与固定页共用唯一性空间，重复即拒绝）。</param>
        /// <param name="automationId">侧边栏 UIA AutomationId（非空；重复即拒绝）。</param>
        /// <param name="titleKey">页面标题的文案键（非空）。</param>
        /// <param name="iconData">侧边栏图标数据（几何路径串）。</param>
        /// <param name="viewModelType">页面 VM 类型（非 null；工厂返回值的运行时类型，用于导航项识别）。</param>
        /// <param name="viewModelFactory">页面 VM 工厂（非 null；宿主在导航时调用，禁止返回已构造实例的缓存）。</param>
        /// <returns>目录注册项；摘除经 <see cref="RemovePluginPage"/>。</returns>
        /// <remarks>
        /// 注册顺序即侧边栏在固定页之后的顺序；插件页摘除后同标识可重新注册。
        /// 注册期不调用工厂——创建时机一律由宿主在 UI 线程决定。
        /// </remarks>
        public NavigationPageRegistration RegisterPluginPage(
            string pluginId,
            string identifier,
            string automationId,
            string titleKey,
            string iconData,
            Type viewModelType,
            Func<object> viewModelFactory)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
            {
                throw new ArgumentException("插件 id 不能为空", nameof(pluginId));
            }
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("插件页标识不能为空", nameof(identifier));
            }
            if (string.IsNullOrWhiteSpace(automationId))
            {
                throw new ArgumentException("AutomationId 不能为空", nameof(automationId));
            }
            if (string.IsNullOrWhiteSpace(titleKey))
            {
                throw new ArgumentException("标题键不能为空", nameof(titleKey));
            }
            ArgumentNullException.ThrowIfNull(viewModelType);
            ArgumentNullException.ThrowIfNull(viewModelFactory);

            // 目录未收口（Validate 之前）或页面被摘除后，同标识槽位可被重新占用；
            // 重复占用在注册点拦截，避免导航执行时解析到两条同标识注册项。
            if (_byAutomationId.ContainsKey(identifier) || _byAutomationId.ContainsKey(automationId))
            {
                throw new InvalidOperationException($"导航标识重复注册：{identifier}");
            }

            var entry = new NavigationPageRegistration(
                null,
                automationId,
                titleKey,
                iconData,
                viewModelType)
            {
                Identifier = identifier,
                PluginId = pluginId,
                ViewModelFactory = viewModelFactory,
            };
            _byAutomationId[identifier] = entry;
            _byAutomationId[automationId] = entry;
            _pluginEntries.Add(entry);
            Changed?.Invoke();
            return entry;
        }

        /// <summary>摘除一个插件页（按注册标识或 AutomationId）；固定页与未知标识不在此入口。</summary>
        /// <param name="identifier">注册标识或 AutomationId。</param>
        /// <returns>摘除了注册项即为 true；标识不存在或指向固定页时为 false。</returns>
        public bool RemovePluginPage(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier) ||
                !_byAutomationId.TryGetValue(identifier, out NavigationPageRegistration? entry) ||
                !entry.IsPluginPage)
            {
                return false;
            }

            _byAutomationId.Remove(entry.Identifier);
            _byAutomationId.Remove(entry.AutomationId);
            _pluginEntries.Remove(entry);
            Changed?.Invoke();
            return true;
        }

        /// <summary>已注册页面：固定槽位按槽位升序在前，插件页按注册顺序追加在后。</summary>
        public IReadOnlyList<NavigationPageRegistration> Entries
        {
            get
            {
                var entries = new List<NavigationPageRegistration>(
                    _fixedEntries.Count + _pluginEntries.Count);
                entries.AddRange(_fixedEntries.OrderBy(item => (int)item.Slot!.Value));
                entries.AddRange(_pluginEntries);
                return entries;
            }
        }

        /// <summary>按槽位取已注册页面；未注册槽位抛 <see cref="InvalidOperationException"/>。</summary>
        /// <remarks>完整目录由 <see cref="Validate"/> 在装配时收口。</remarks>
        public NavigationPageRegistration GetEntry(NavigationSlot slot)
        {
            if (_bySlot.TryGetValue(slot, out var entry))
            {
                return entry;
            }
            throw new InvalidOperationException($"导航目录未注册槽位: {slot}");
        }

        /// <summary>按注册标识或 AutomationId 取已注册页面；不存在抛 <see cref="InvalidOperationException"/>。</summary>
        public NavigationPageRegistration GetEntry(string identifier)
        {
            if (!string.IsNullOrWhiteSpace(identifier) &&
                _byAutomationId.TryGetValue(identifier, out NavigationPageRegistration? entry))
            {
                return entry;
            }

            throw new InvalidOperationException($"导航目录未注册标识: {identifier}");
        }

        /// <summary>完整性收口：全部槽位必须注册，缺失即抛 <see cref="InvalidOperationException"/>。</summary>
        public void Validate()
        {
            var missing = NavigationSlots.All.Where(s => !_bySlot.ContainsKey(s)).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException($"导航目录缺失槽位: {string.Join(", ", missing)}");
            }
        }
    }
}
