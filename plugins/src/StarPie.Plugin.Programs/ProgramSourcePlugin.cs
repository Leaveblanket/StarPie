using StarPie.Abstractions;
using StarPie.Services.Programs;
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
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
