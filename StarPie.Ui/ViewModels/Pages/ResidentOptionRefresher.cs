using System;
using StarPie.Host.Localization;

namespace StarPie.Ui.ViewModels.Pages
{
    /// <summary>
    /// 驻留文案的固定选项目录：语言切换时重建目录并补发选中通知，释放时成对退订。
    /// </summary>
    /// <remarks>
    /// 目录内容归持有者（各设置页的选项来源与标签来源都不同），本件只承担这套配方。配方里有一条
    /// 不显然的正确性要求：**重建之后必须补发选中通知**——绑定在目录被整体替换后拿不到原选中项，
    /// 不补发会表现为下拉框丢选中（或被绑定回推成空值）。
    /// </remarks>
    public sealed class ResidentOptionRefresher : IDisposable
    {
        private readonly ILocalizationService _localization;
        private readonly Action _rebuild;
        private readonly Action _notifySelection;
        private bool _disposed;

        /// <summary>构造并订阅语言切换。</summary>
        /// <param name="localization">文案服务（非 null）。</param>
        /// <param name="rebuild">重建目录（构建并赋值给承载属性；非 null）。</param>
        /// <param name="notifySelection">重建后补发的选中通知（非 null）。</param>
        public ResidentOptionRefresher(
            ILocalizationService localization,
            Action rebuild,
            Action notifySelection)
        {
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _rebuild = rebuild ?? throw new ArgumentNullException(nameof(rebuild));
            _notifySelection = notifySelection ?? throw new ArgumentNullException(nameof(notifySelection));

            _localization.LanguageChanged += OnLanguageChanged;
        }

        /// <summary>退订语言切换；幂等，重复释放不重复退订。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _localization.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            _rebuild();
            _notifySelection();
        }
    }
}
