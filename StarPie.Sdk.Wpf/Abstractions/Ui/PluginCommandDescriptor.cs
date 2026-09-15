using System;

namespace StarPie.Abstractions.Ui
{
    /// <summary>
    /// 命令描述符：宿主记入资产登记表，卸载时注销；命令体由插件提供，在 UI 线程执行。
    /// </summary>
    /// <param name="CommandId">命令稳定 id（插件内唯一）。</param>
    /// <param name="TitleKey">命令标题的宿主文案键（<see cref="DisplayName"/> 为空时使用）。</param>
    /// <param name="Execute">执行体（非 null）。</param>
    /// <param name="CanExecute">可选可用性判定；为 null 时视为恒可用。</param>
    public sealed record PluginCommandDescriptor(
        string CommandId,
        string TitleKey,
        Action Execute,
        Func<bool>? CanExecute = null)
    {
        /// <summary>命令显示名（字面量、语言无关）；非空时优先于 <see cref="TitleKey"/>。</summary>
        /// <remarks>宿主只按命令 id 路由，命令标题不参与宿主渲染。</remarks>
        public string? DisplayName { get; init; }
    }
}
