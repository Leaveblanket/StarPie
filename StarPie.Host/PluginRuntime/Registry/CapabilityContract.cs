using System;

namespace StarPie.PluginRuntime.Registry
{
    /// <summary>
    /// 宿主声明的能力契约：能力 id + 接口 ABI 号 + 接口类型 + 手写守卫适配器工厂。
    /// </summary>
    /// <remarks>
    /// 契约由宿主声明（<see cref="CapabilityRegistry.DeclareContract"/>），插件只能按 id/ABI 注册实现。
    /// 适配器是手写的（不用 DispatchProxy/运行时代码生成）：每个能力接口一个窄适配器，
    /// 把调用转成 <see cref="CapabilityGuard"/> 守卫下的调用。适配器只持有守卫与实例解析器，
    /// 不缓存插件实例：每次调用经解析器现取，作用域释放后解析不再成功——宿主单例与消费者
    /// 缓存适配器都不会把插件实例留成常驻引用。
    /// </remarks>
    public sealed class CapabilityContract
    {
        private readonly Func<CapabilityGuard, Func<object>, object> _createAdapter;

        /// <summary>声明一条能力契约。</summary>
        /// <param name="id">能力 id（小写单段标识，与清单 capabilities[].id 一致）。</param>
        /// <param name="abi">接口 ABI 号（从 1 起；破坏性变更 = 新 id + 新接口，不改本号）。</param>
        /// <param name="interfaceType">能力接口类型（插件可见，必须来自 SDK）。</param>
        /// <param name="createAdapter">
        /// 手写适配器工厂：入参为该插件的能力守卫与实例解析器，返回实现同一接口的守卫适配器。
        /// 适配器须在每次调用时经解析器取实例（不得把实例存进字段）。
        /// </param>
        public CapabilityContract(
            string id,
            int abi,
            Type interfaceType,
            Func<CapabilityGuard, Func<object>, object> createAdapter)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            if (abi < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(abi), abi, "能力 ABI 号从 1 起");
            }

            ArgumentNullException.ThrowIfNull(interfaceType);
            if (!interfaceType.IsInterface)
            {
                throw new ArgumentException($"能力契约类型必须是接口：{interfaceType.FullName}", nameof(interfaceType));
            }

            ArgumentNullException.ThrowIfNull(createAdapter);
            Id = id;
            Abi = abi;
            InterfaceType = interfaceType;
            _createAdapter = createAdapter;
        }

        /// <summary>能力 id。</summary>
        public string Id { get; }

        /// <summary>接口 ABI 号。</summary>
        public int Abi { get; }

        /// <summary>能力接口类型。</summary>
        public Type InterfaceType { get; }

        /// <summary>构造守卫适配器（消费者取用时调用，注册表不缓存适配器）。</summary>
        /// <param name="guard">该插件的能力守卫。</param>
        /// <param name="resolveInstance">实例解析器：每次调用现取插件实例，不可用时抛异常。</param>
        public object CreateAdapter(CapabilityGuard guard, Func<object> resolveInstance)
        {
            ArgumentNullException.ThrowIfNull(guard);
            ArgumentNullException.ThrowIfNull(resolveInstance);
            return _createAdapter(guard, resolveInstance);
        }
    }
}
