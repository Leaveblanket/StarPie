using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace StarPie.Ui.Services.Navigation
{
    /// <summary>
    /// 设置台会话作用域的页面 VM 缓存：同一会话内保留实例（来回切页不丢页内状态），
    /// 会话结束整批释放；页面 VM 只经导航执行缝向本缓存取实例。
    /// </summary>
    /// <remarks>
    /// 会话的实例边界是 DI 作用域：贡献者把页面 VM 与设置台会话级 VM 注册为 **scoped**，
    /// 作用域由组合根交付的工厂创建（解析面仍收在组合根），本类只负责开/结束会话并按类型取实例。
    /// 常驻型页面（插件管理页）注册为 singleton——从本作用域解析得到宿主根实例、不随会话释放，
    /// 于是"哪个页面随设置台销毁"由注册生命周期表达，本类不含类型清单。
    /// 缓存字典是 scoped 之上的显式一层：同一会话内重复取同一类型返回同一实例（幂等），
    /// 会话结束时随作用域一起丢弃。
    /// </remarks>
    public sealed class ConsolePageSession : IDisposable
    {
        private readonly Func<IServiceScope> _createScope;
        private readonly Dictionary<Type, object> _instances = new();
        private IServiceScope? _scope;

        /// <summary>构造会话缓存；<paramref name="createScope"/> 由组合根交付（唯一作用域来源）。</summary>
        public ConsolePageSession(Func<IServiceScope> createScope)
        {
            _createScope = createScope ?? throw new ArgumentNullException(nameof(createScope));
        }

        /// <summary>当前是否有会话开着。</summary>
        public bool IsOpen => _scope is not null;

        /// <summary>会话作用域的解析面（会话未开时抛）。</summary>
        public IServiceProvider Services => _scope is { } scope
            ? scope.ServiceProvider
            : throw new InvalidOperationException("设置台未开：会话作用域不存在");

        /// <summary>开启新会话（幂等：已开着时先结束旧会话——旧会话实例整批释放）。</summary>
        public void Begin()
        {
            End();
            _scope = _createScope();
        }

        /// <summary>
        /// 按类型取页面 VM：同一会话内命中即返回同一实例；未命中从会话作用域解析并记账
        /// （scoped 实例由作用域持有，会话结束时随作用域释放）。
        /// </summary>
        public object Resolve(Type viewModelType)
        {
            ArgumentNullException.ThrowIfNull(viewModelType);
            if (_instances.TryGetValue(viewModelType, out object? cached))
            {
                return cached;
            }

            object instance = Services.GetRequiredService(viewModelType);
            _instances[viewModelType] = instance;
            return instance;
        }

        /// <summary>按类型取会话级 VM（导航区/壳区/设置台子 VM 的取用面，与页面 VM 共用同一作用域）。</summary>
        public T Resolve<T>() where T : notnull => (T)Resolve(typeof(T));

        /// <summary>结束会话：实例记账清空 + 作用域释放（scoped 实例的成对退订在此执行）；幂等。</summary>
        public void End()
        {
            _instances.Clear();
            _scope?.Dispose();
            _scope = null;
        }

        public void Dispose() => End();
    }
}
