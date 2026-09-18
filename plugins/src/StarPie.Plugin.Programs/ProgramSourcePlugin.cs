using StarPie.Sdk.Abstractions;
using StarPie.Sdk.Services.Messages;
using StarPie.Sdk.Services.Programs;
using System.Runtime.Versioning;

namespace StarPie.Plugin.Programs
{
    /// <summary>
    /// 首个随包 headless 插件：以「程序来源」能力提供宿主内置来源之外的已安装程序扫描。
    /// </summary>
    /// <remarks>
    /// 入口同时是能力实现：<see cref="StartAsync"/> 里经 <see cref="IPluginContext.RegisterCapability{T}"/>
    /// 把自身登记为 <see cref="IProgramScanner"/>；停用即整插件卸载，宿主侧能力条目随作用域释放摘除。
    /// 插件只引 <c>StarPie.Sdk</c>，不引宿主实现，也不随包分发 SDK。
    /// 内存自治示范：订阅宿主托盘信号（<see cref="MinimizedToTrayMessage"/>，进托盘方向），
    /// 随宿主分层常驻策略释放自身深扫缓存——插件自治出账，宿主不感知插件内部缓存。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public sealed class ProgramSourcePlugin : IPlugin, IProgramScanner
    {
        private readonly InstalledProgramScanner _scanner = new();

        /// <inheritdoc/>
        public IReadOnlyList<ProgramEntry> ScanInstalledPrograms() => _scanner.ScanInstalledPrograms();

        /// <inheritdoc/>
        public Task StartAsync(IPluginContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);

            context.RegisterCapability<IProgramScanner>(this);

            // 托盘自治出账：进托盘即释放深扫缓存，恢复由下次消费按需重建。
            // 处理委托带捕获(this)——无捕获 lambda 会被编译器缓存进 ALC 静态字段（见 SampleUi 同款注释）。
            context.Events.Subscribe<MinimizedToTrayMessage>(_ => _scanner.InvalidateCache());
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
