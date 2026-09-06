using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WinPieGestures.Services.Navigation
{
    /// <summary>
    /// 导航槽位（S5，ADR-0016 决策 5，B2/#75）：全局槽位表 0–4，顺序即侧边栏顺序正典
    /// （触发/外观/手势/高级/关于）；AutomationId 键值 = <c>NavTab{槽位}</c>，e2e（pywinauto）
    /// 依赖该标识，随槽位稳定。槽位表由 Core 收口测试拦截缺失/重复/未知槽位。
    /// </summary>
    public enum NavigationSlot
    {
        Trigger = 0,
        Appearance = 1,
        Gestures = 2,
        Advanced = 3,
        About = 4
    }

    /// <summary>槽位表正典工具：全部槽位与 AutomationId 映射（NavTab0..4）。</summary>
    public static class NavigationSlots
    {
        /// <summary>全部槽位（0–4，按槽位升序）。</summary>
        public static IReadOnlyList<NavigationSlot> All { get; } = Enum.GetValues<NavigationSlot>();

        /// <summary>槽位对应的正典 UIA AutomationId（NavTab0..4；e2e 依赖）。</summary>
        public static string GetAutomationId(NavigationSlot slot)
            => "NavTab" + ((int)slot).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 导航目录注册项（S5，B2/#75）：槽位、AutomationId、标题键、图标数据与目标页面 VM 类型。
    /// 导航执行（navigate 委托）与本地化接线由消费方完成（B3 起 MainViewModel/模块注册器）。
    /// </summary>
    public sealed record NavigationPageRegistration(
        NavigationSlot Slot,
        string AutomationId,
        string TitleKey,
        string IconData,
        Type ViewModelType);

    /// <summary>
    /// 导航目录（S5，ADR-0016 决策 3/5，B2/#75）：共享内核的页面注册契约与唯一性收口——
    /// 未知/重复槽位与重复 AutomationId 在注册时拦截，缺失槽位由 <see cref="Validate"/> 收口。
    /// 槽位表是侧边栏顺序唯一正典；本目录是 B3 起模块自治注册与导航 VM 目录驱动的基础。
    /// </summary>
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
