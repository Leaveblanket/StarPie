using System;

namespace StarPie.Abstractions.Ui
{
    /// <summary>
    /// 设置页区块描述符：宿主把区块排在宿主设置区之内，工厂在 UI 线程创建区块 VM。
    /// </summary>
    /// <param name="SectionKey">区块稳定键（插件内唯一）。</param>
    /// <param name="TitleKey">区块标题的文案键。</param>
    /// <param name="Order">区块在宿主设置区内的排序权重（小的在前）。</param>
    /// <param name="ViewModelFactory">区块 VM 工厂；宿主在 UI 线程调用。</param>
    public sealed record PluginSettingsSectionDescriptor(
        string SectionKey,
        string TitleKey,
        int Order,
        Func<object> ViewModelFactory);
}
