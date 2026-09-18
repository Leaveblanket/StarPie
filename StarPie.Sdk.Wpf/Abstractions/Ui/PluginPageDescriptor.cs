using System;

namespace StarPie.Sdk.Wpf.Abstractions.Ui
{
    /// <summary>
    /// 导航页描述符：纯数据 + VM 工厂，宿主在 UI 线程调用工厂创建实例并挂进导航容器。
    /// </summary>
    /// <param name="NavigationKey">插件页稳定键（宿主据此生成导航标识，与插件 id 一起保证唯一）。</param>
    /// <param name="TitleKey">页面标题的宿主文案键（<see cref="DisplayName"/> 为空时使用）。</param>
    /// <param name="IconData">侧边栏图标数据（几何路径串）。</param>
    /// <param name="ViewModelFactory">页面 VM 工厂；宿主在 UI 线程调用，禁止返回已构造实例的缓存。</param>
    public sealed record PluginPageDescriptor(
        string NavigationKey,
        string TitleKey,
        string IconData,
        Func<object> ViewModelFactory)
    {
        /// <summary>页面显示名（字面量、语言无关）；非空时优先于 <see cref="TitleKey"/>。</summary>
        public string? DisplayName { get; init; }
    }
}
