using System;
using System.Collections.Generic;
using System.Linq;
using StarPie.HostServices;

namespace StarPie.PluginRuntime.Registry
{
    /// <summary>
    /// 能力表：宿主声明的契约 + 内置条目 + 各插件经 <see cref="PluginServiceScope"/> 注册的条目。
    /// </summary>
    /// <remarks>
    /// 顺序语义：内置条目永远最前（内置不可被插件覆盖——插件条目只会追加，不替换内置条目），
    /// 其后按清单 priority、plugin id 稳定序排列，避免列表顺序随装载顺序抖动。
    /// 只有活动态插件的能力对消费者可见；表内不缓存插件实例：插件实例活在该插件的服务作用域里，
    /// 消费者每次取用都拿一个新的守卫适配器（短租用），适配器同样不缓存实例、每次调用现取。
    /// </remarks>
    public sealed class CapabilityRegistry
    {
        private readonly object _sync = new();
        private readonly List<CapabilityContract> _contracts = new();
        private readonly List<BuiltinEntry> _builtins = new();
        private readonly List<PluginEntry> _plugins = new();

        /// <summary>宿主声明一条能力契约；契约未声明前插件不能注册该能力。</summary>
        /// <param name="contract">能力契约。</param>
        public void DeclareContract(CapabilityContract contract)
        {
            ArgumentNullException.ThrowIfNull(contract);
            lock (_sync)
            {
                EnsureContractDeclared(contract);
            }
        }

        /// <summary>声明能力契约并登记一条内置条目（宿主自有实现，永远排在插件条目之前）。</summary>
        /// <param name="contract">能力契约。</param>
        /// <param name="instance">宿主自有实现实例。</param>
        public void DeclareBuiltin(CapabilityContract contract, object instance)
        {
            ArgumentNullException.ThrowIfNull(contract);
            ArgumentNullException.ThrowIfNull(instance);
            if (!contract.InterfaceType.IsInstanceOfType(instance))
            {
                throw new ArgumentException(
                    $"内置实例未实现契约接口 {contract.InterfaceType.FullName}",
                    nameof(instance));
            }

            lock (_sync)
            {
                EnsureContractDeclared(contract);
                _builtins.Add(new BuiltinEntry(contract, instance));
            }
        }

        /// <summary>按接口类型找已声明的契约；未声明时返回 null。</summary>
        /// <param name="interfaceType">能力接口类型。</param>
        public CapabilityContract? FindContract(Type interfaceType)
        {
            ArgumentNullException.ThrowIfNull(interfaceType);
            lock (_sync)
            {
                return _contracts.FirstOrDefault(
                    contract => contract.InterfaceType == interfaceType);
            }
        }

        /// <summary>
        /// 取某个能力接口的全部可用条目：内置优先 → 清单 priority → plugin id 稳定序。
        /// </summary>
        /// <typeparam name="T">能力接口类型。</typeparam>
        /// <exception cref="InvalidOperationException">该接口未被宿主声明为能力契约。</exception>
        public IReadOnlyList<T> GetAll<T>() where T : class
        {
            Type interfaceType = typeof(T);
            lock (_sync)
            {
                if (!_contracts.Any(contract => contract.InterfaceType == interfaceType))
                {
                    throw new InvalidOperationException($"未声明的能力契约：{interfaceType.FullName}");
                }

                var items = new List<T>();
                foreach (BuiltinEntry builtin in _builtins
                    .Where(entry => entry.Contract.InterfaceType == interfaceType))
                {
                    items.Add((T)builtin.Instance);
                }

                foreach (PluginEntry entry in _plugins
                    .Where(entry => entry.Contract.InterfaceType == interfaceType
                        && entry.Scope.IsActive)
                    .OrderBy(entry => entry.Scope.ManifestPriority)
                    .ThenBy(entry => entry.Scope.PluginId, StringComparer.Ordinal))
                {
                    object? adapter = entry.Scope.CreateAdapter(entry.Contract);
                    if (adapter is not null)
                    {
                        items.Add((T)adapter);
                    }
                }

                return items;
            }
        }

        /// <summary>登记一条插件能力条目（实例留在该插件的作用域里，本表只记归属与契约）。</summary>
        /// <param name="scope">插件服务作用域。</param>
        /// <param name="contract">已声明的能力契约。</param>
        internal void RegisterPluginCapability(PluginServiceScope scope, CapabilityContract contract)
        {
            lock (_sync)
            {
                // 契约必须先前经 DeclareContract/DeclareBuiltin 声明（引用同一实例）。
                if (!_contracts.Contains(contract))
                {
                    throw new InvalidOperationException($"能力契约未声明：{contract.Id} ABI {contract.Abi}");
                }

                _plugins.Add(new PluginEntry(scope, contract));
            }
        }

        /// <summary>摘除某个插件的全部条目（作用域释放时调用）。</summary>
        /// <param name="scope">插件服务作用域。</param>
        internal void RemovePlugin(PluginServiceScope scope)
        {
            lock (_sync)
            {
                _plugins.RemoveAll(entry => entry.Scope == scope);
            }
        }

        /// <summary>契约按 id + ABI 唯一：同名不同 ABI 视为两条契约（破坏性变更 = 新接口 + 新 id）。</summary>
        private void EnsureContractDeclared(CapabilityContract contract)
        {
            bool duplicate = _contracts.Any(existing =>
                string.Equals(existing.Id, contract.Id, StringComparison.Ordinal)
                && existing.Abi == contract.Abi);
            if (duplicate)
            {
                throw new InvalidOperationException($"能力契约重复声明：{contract.Id} ABI {contract.Abi}");
            }

            bool interfaceTaken = _contracts.Any(existing =>
                existing.InterfaceType == contract.InterfaceType);
            if (interfaceTaken)
            {
                throw new InvalidOperationException(
                    $"能力接口重复绑定契约：{contract.InterfaceType.FullName}");
            }

            _contracts.Add(contract);
        }

        private readonly record struct BuiltinEntry(CapabilityContract Contract, object Instance);

        private readonly record struct PluginEntry(PluginServiceScope Scope, CapabilityContract Contract);
    }
}
