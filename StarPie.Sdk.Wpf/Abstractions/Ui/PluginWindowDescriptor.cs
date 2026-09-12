using System;
using System.Windows;

namespace StarPie.Abstractions.Ui
{
    /// <summary>
    /// 窗口描述符：只注册工厂，宿主创建、显示、跟踪窗口实例并在卸载时关闭清理。
    /// </summary>
    /// <param name="WindowKey">窗口稳定键（插件内唯一）。</param>
    /// <param name="TitleKey">窗口标题的文案键。</param>
    /// <param name="WindowFactory">窗口工厂；宿主在 UI 线程调用，禁止自行 Show 或缓存实例。</param>
    public sealed record PluginWindowDescriptor(
        string WindowKey,
        string TitleKey,
        Func<Window> WindowFactory);
}
