using StarPie.Abstractions;

namespace StarPie.Plugin.MinimalUi
{
    /// <summary>最小 UI 插件的 headless 入口（清单 entryType）：只做生命周期打点。</summary>
    public sealed class MinimalUiPlugin : IPlugin
    {
        /// <inheritdoc/>
        public Task StartAsync(IPluginContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            context.Log.Write(PluginLogLevel.Information, "最小 UI 示例插件已启动");
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
