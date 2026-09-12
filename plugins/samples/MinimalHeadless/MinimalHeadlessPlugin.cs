using StarPie.Abstractions;

namespace StarPie.Plugin.MinimalHeadless
{
    /// <summary>
    /// 最小 headless 插件入口（清单 entryType）：只示范 <see cref="IPlugin"/> 生命周期
    /// 与宿主日志面。headless 插件的全部宿主可达面都在 <see cref="IPluginContext"/> 上。
    /// </summary>
    public sealed class MinimalHeadlessPlugin : IPlugin
    {
        /// <inheritdoc/>
        public Task StartAsync(IPluginContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            context.Log.Write(PluginLogLevel.Information, "最小 headless 示例插件已启动");
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
