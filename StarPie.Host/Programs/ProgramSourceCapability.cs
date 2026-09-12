using StarPie.PluginRuntime.Registry;
using StarPie.Services.Programs;

namespace StarPie.Programs
{
    /// <summary>
    /// 宿主声明的能力契约「程序来源」（<c>program-source@1</c>）：契约实例与消费者侧守卫适配器。
    /// </summary>
    /// <remarks>
    /// 契约是宿主对扩展点的声明：插件只认 SDK 的 <see cref="IProgramScanner"/>，注册与调用都经能力表。
    /// 内置程序来源与插件程序来源走同一契约——内置条目先登记，永远排在插件条目之前。
    /// </remarks>
    public static class ProgramSourceCapability
    {
        /// <summary>程序来源能力契约（id + ABI + 接口 + 守卫适配器工厂）。</summary>
        public static CapabilityContract Contract { get; } = new(
            "program-source",
            1,
            typeof(IProgramScanner),
            (guard, resolve) => new GuardedProgramScanner(guard, () => (IProgramScanner)resolve()));
    }

    /// <summary>
    /// 程序来源的守卫适配器：只持守卫与"每次调用现取实例"的解析器，不缓存插件实例。
    /// </summary>
    internal sealed class GuardedProgramScanner : IProgramScanner
    {
        private const string CallTarget = "program-source";

        private readonly CapabilityGuard _guard;
        private readonly Func<IProgramScanner> _resolve;

        internal GuardedProgramScanner(CapabilityGuard guard, Func<IProgramScanner> resolve)
        {
            _guard = guard;
            _resolve = resolve;
        }

        /// <inheritdoc/>
        public IReadOnlyList<ProgramEntry> ScanInstalledPrograms()
            => _guard.Invoke(CallTarget, () => _resolve().ScanInstalledPrograms());
    }
}
