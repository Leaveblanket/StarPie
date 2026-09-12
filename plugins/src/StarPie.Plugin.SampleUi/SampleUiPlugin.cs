using StarPie.Abstractions;

namespace StarPie.Plugin.SampleUi
{
    /// <summary>
    /// 首个 UI 示例插件的 headless 入口（清单 entryType）：不注册能力，只示范
    /// <see cref="IPlugin"/> 生命周期打点；UI 资产全部经
    /// <see cref="SampleUiUiModule"/>（清单 ui.entryType）在宿主 UI 线程注册。
    /// </summary>
    public sealed class SampleUiPlugin : IPlugin
    {
        /// <inheritdoc/>
        public Task StartAsync(IPluginContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            context.Log.Write(PluginLogLevel.Information, "UI 示例插件已启动");
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
