namespace StarPie.PluginRuntime.Lifecycle
{
    /// <summary>
    /// 单个插件的生命周期状态机：只接受规范允许的相邻转移，失败统一经
    /// <see cref="Quarantine"/> 进入隔离终态并记录原因。
    /// </summary>
    /// <remarks>
    /// 装载与卸载管线共用同一实例；<see cref="Transitions"/> 保留已完成的转移，供诊断与测试核对过程。
    /// 隔离态只能经 <see cref="Quarantine"/> 进入（必须带原因），与已卸载态一样是终态，不再接受任何转移。
    /// 被准入拒绝的包不在此处留痕：拒绝是准入结果，不是生命周期阶段。
    /// </remarks>
    public sealed class PluginLifecycleStateMachine
    {
        /// <summary>规范允许的转移表：装载链、卸载链（headless 直连卸载，UI 插件先释放 UI）。</summary>
        private static readonly Dictionary<PluginLifecycleState, PluginLifecycleState[]> AllowedTransitions = new()
        {
            [PluginLifecycleState.Discovered] = new[] { PluginLifecycleState.Validated },
            [PluginLifecycleState.Validated] = new[] { PluginLifecycleState.Loading },
            [PluginLifecycleState.Loading] = new[] { PluginLifecycleState.Starting },
            [PluginLifecycleState.Starting] = new[] { PluginLifecycleState.Active },
            [PluginLifecycleState.Active] = new[] { PluginLifecycleState.Stopping },
            [PluginLifecycleState.Stopping] = new[] { PluginLifecycleState.ReleasingUi, PluginLifecycleState.Unloading },
            [PluginLifecycleState.ReleasingUi] = new[] { PluginLifecycleState.Unloading },
            [PluginLifecycleState.Unloading] = new[] { PluginLifecycleState.Unloaded },
            [PluginLifecycleState.Unloaded] = Array.Empty<PluginLifecycleState>(),
            [PluginLifecycleState.Quarantined] = Array.Empty<PluginLifecycleState>(),
        };

        private readonly List<PluginLifecycleTransition> _transitions = new();

        /// <summary>当前状态；新状态机停在已发现。</summary>
        public PluginLifecycleState Current { get; private set; } = PluginLifecycleState.Discovered;

        /// <summary>隔离原因；未隔离时为 null。</summary>
        public string? QuarantineReason { get; private set; }

        /// <summary>已完成的转移，按发生顺序。</summary>
        public IReadOnlyList<PluginLifecycleTransition> Transitions => _transitions;

        /// <summary>是否处于终态（已卸载或已隔离）——终态不再接受任何转移。</summary>
        public bool IsTerminal
            => Current is PluginLifecycleState.Unloaded or PluginLifecycleState.Quarantined;

        /// <summary>
        /// 转移到相邻状态；跳级、回退、终态出走都抛 <see cref="InvalidOperationException"/>，
        /// 状态与转移记录保持不变。
        /// </summary>
        /// <param name="next">目标状态；必须是当前状态的规范相邻状态。</param>
        /// <exception cref="InvalidOperationException">
        /// 目标为隔离态（须经 <see cref="Quarantine"/>）、当前为终态或目标不是规范允许的相邻状态。
        /// </exception>
        public void Transition(PluginLifecycleState next)
        {
            if (next == PluginLifecycleState.Quarantined)
            {
                throw new InvalidOperationException("隔离必须经 Quarantine(reason) 进入并携带原因");
            }

            if (IsTerminal)
            {
                throw new InvalidOperationException($"生命周期已处于终态 {Current}，不再接受转移（目标 {next}）");
            }

            if (!AllowedTransitions[Current].Contains(next))
            {
                throw new InvalidOperationException($"非法生命周期转移：{Current} → {next}");
            }

            _transitions.Add(new PluginLifecycleTransition(Current, next));
            Current = next;
        }

        /// <summary>
        /// 从任一非终态进入隔离并记录原因；重复隔离或卸载后隔离抛
        /// <see cref="InvalidOperationException"/>。
        /// </summary>
        /// <param name="reason">隔离原因（诊断与测试可读文本）。</param>
        /// <exception cref="ArgumentException">原因为空或全为空白。</exception>
        public void Quarantine(string reason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            if (IsTerminal)
            {
                throw new InvalidOperationException($"生命周期已处于终态 {Current}，不能再次隔离");
            }

            _transitions.Add(new PluginLifecycleTransition(Current, PluginLifecycleState.Quarantined));
            QuarantineReason = reason;
            Current = PluginLifecycleState.Quarantined;
        }
    }
}
