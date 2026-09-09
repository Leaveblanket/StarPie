using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StarPie.Services.Navigation
{
    /// <summary>
    /// 导航槽位：全局槽位表 0–4，枚举顺序即侧边栏顺序正典（触发/外观/手势/高级/关于）。
    /// </summary>
    /// <remarks>
    /// AutomationId 由 <see cref="NavigationSlots.GetAutomationId"/> 固定为 NavPage{槽位}，
    /// e2e（pywinauto）依赖该标识；缺失/重复/未知槽位由共享内核收口测试拦截。
    /// </remarks>
    public enum NavigationSlot
    {
        Trigger = 0,
        Appearance = 1,
        Gestures = 2,
        Advanced = 3,
        About = 4
    }

    /// <summary>槽位表正典工具：全部槽位与 AutomationId 映射（NavPage0..4）。</summary>
    public static class NavigationSlots
    {
        /// <summary>全部槽位（0–4，按槽位升序）。</summary>
        public static IReadOnlyList<NavigationSlot> All { get; } = Enum.GetValues<NavigationSlot>();

        /// <summary>槽位对应的正典 UIA AutomationId（NavPage0..4；e2e 依赖）。</summary>
        public static string GetAutomationId(NavigationSlot slot)
            => "NavPage" + ((int)slot).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>导航目录注册项：槽位、AutomationId、标题键、图标数据与目标页面 VM 类型。</summary>
    /// <remarks>导航执行由 Host 目录驱动执行入口按槽位惰性解析目标页面 VM（ADR-0021/#92）。</remarks>
    public sealed record NavigationPageRegistration(
        NavigationSlot Slot,
        string AutomationId,
        string TitleKey,
        string IconData,
        Type ViewModelType);

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
        private readonly List<NavigationPageRegistration> _entries = new();

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
            _entries.Add(entry);
        }

        /// <summary>已注册页面（按槽位升序，即侧边栏顺序）。</summary>
        public IReadOnlyList<NavigationPageRegistration> Entries
            => _entries.OrderBy(e => (int)e.Slot).ToList();

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

        /// <summary>完整性收口：0–4 五个槽位必须全部注册，缺失即抛 <see cref="InvalidOperationException"/>。</summary>
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
