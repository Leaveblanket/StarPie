using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using WinPieGestures.Services;

namespace WinPieGestures.ViewModels.Pages
{
    /// <summary>
    /// 界面主题模块设置子 ViewModel（#54，ADR-0014 决策 1/6/7）：独占软件界面主题（AppTheme）的
    /// 透传——读直取运行态配置、写直穿（落盘语义不变：防抖消息上报）——与驻留主题选项目录
    /// （ItemsSource 化：静态六项标签即时取词，随语言切换重建并补发选中通知恢复选中）。
    /// 主题应用到窗口属壳层 View 效果：写穿后发布 <see cref="AppThemeChangedMessage"/>，由
    /// MainView（壳层 code-behind 白名单）订阅执行 ApplyAppTheme；配置导入后的重挂路径同样
    /// 经本消息由壳层执行窗口主题应用（页面不再持有主题 SelectionChanged 处理器）。
    /// DI 单例注入 <see cref="AppearanceSettingsViewModel"/>（外观页界面主题卡 DataContext
    /// 指向本 VM）；ADR-0010 驻留文案机制：切语重建选项目录，Dispose 成对退订
    /// （组合根随 Composition.Dispose 释放）。
    /// </summary>
    public partial class InterfaceThemeSettingsViewModel : ObservableObject, IDisposable
    {
        private readonly IConfigService _config;
        private readonly IMessenger _messenger;
        private readonly ILocalizationService _localization;
        private bool _disposed;

        public InterfaceThemeSettingsViewModel(
            IConfigService config,
            IMessenger messenger,
            ILocalizationService localization)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));

            // 配置导入会替换运行态配置实例（JsonConfigService.Import）：重挂后补发选中通知并发布
            // 主题应用消息，由壳层主窗口订阅执行（含配置导入后重挂路径）。
            messenger.Register<ConfigImportedMessage>(this, (_, _) => ReloadFromConfig());

            RebuildThemeOptions();

            // ADR-0010 驻留文案机制：界面主题选项目录标签随语言切换重建（单例 VM 成对退订）。
            _localization.LanguageChanged += OnLanguageChanged;
        }

        private AppConfig Config => _config.Current;

        /// <summary>
        /// 软件界面主题（System/Light/Dark/MidnightNavy/RoyalViolet/TitaniumGray）。
        /// 透传属性：读直取运行态配置（空值回落 System）；写直穿配置后经防抖消息请求落盘，
        /// 并发布 <see cref="AppThemeChangedMessage"/> 交壳层主窗口应用窗口主题。
        /// 下拉项重建期间绑定回推的瞬态 null/空值被忽略（同轮盘 SelectedTheme 先例），
        /// 避免切语重建目录时误把选中清成 System。
        /// </summary>
        public string AppTheme
        {
            get => Config.AppTheme ?? "System";
            set
            {
                // ItemsSource 化后重建期间绑定回推 null/空值：忽略以免误清当前主题
                //（同轮盘配色下拉 SelectedTheme 的先例）。
                if (string.IsNullOrEmpty(value)) return;
                if (string.Equals(Config.AppTheme, value, StringComparison.Ordinal)) return;

                Config.AppTheme = value;
                OnPropertyChanged();
                _messenger.Send(DebouncedSaveRequestedMessage.Instance);
                _messenger.Send(new AppThemeChangedMessage(value));
            }
        }

        private IReadOnlyList<AppThemeOptionItem> _appThemeOptions = Array.Empty<AppThemeOptionItem>();

        /// <summary>界面主题下拉的固定选项（Tag 与迁移前 XAML 静态 ComboBoxItem 一致；标签即时取词）。</summary>
        public IReadOnlyList<AppThemeOptionItem> AppThemeOptions
        {
            get => _appThemeOptions;
            private set => SetProperty(ref _appThemeOptions, value);
        }

        /// <summary>重建 <see cref="AppThemeOptions"/>（切语/构造时调用；目录固定六项、标签随语言变化）。</summary>
        private void RebuildThemeOptions()
        {
            AppThemeOptions = new[]
            {
                new AppThemeOptionItem("System", _localization.GetString("ThemeSystem")),
                new AppThemeOptionItem("Light", _localization.GetString("ThemeLight")),
                new AppThemeOptionItem("Dark", _localization.GetString("ThemeDark")),
                new AppThemeOptionItem("MidnightNavy", _localization.GetString("ThemeNavy")),
                new AppThemeOptionItem("RoyalViolet", _localization.GetString("ThemeViolet")),
                new AppThemeOptionItem("TitaniumGray", _localization.GetString("ThemeGray"))
            };
        }

        /// <summary>语言切换后重建界面主题选项目录并补发选中通知，让 ComboBox 从新目录恢复选中。</summary>
        private void OnLanguageChanged()
        {
            RebuildThemeOptions();
            OnPropertyChanged(nameof(AppTheme));
        }

        /// <summary>
        /// 导入配置后从当前配置重挂（#54）：透传属性读穿新配置实例，无需状态迁移——补发选中通知
        /// 让绑定拉取新值恢复 ComboBox 选中，并发布 <see cref="AppThemeChangedMessage"/> 由壳层
        /// 主窗口执行窗口主题应用。
        /// </summary>
        public void ReloadFromConfig()
        {
            OnPropertyChanged(nameof(AppTheme));
            _messenger.Send(new AppThemeChangedMessage(AppTheme));
        }

        /// <summary>退订本地化事件（ADR-0010 第 3 条：单例 VM 配 IDisposable，组合根随 Composition.Dispose 调用）。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _localization.LanguageChanged -= OnLanguageChanged;
        }
    }

    /// <summary>界面主题下拉选项条目（#54 ItemsSource 化）：Tag 供 SelectedValue 匹配，Label 为展示文案。</summary>
    public sealed class AppThemeOptionItem
    {
        public string Tag { get; }

        public string Label { get; }

        public AppThemeOptionItem(string tag, string label)
        {
            Tag = tag;
            Label = label;
        }

        // 兜底展示/UIA 可访问文本（DisplayMemberPath 之外的安全网）。
        public override string ToString() => Label;
    }
}
