using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using StarPie.Sdk.Services;
using StarPie.Sdk.Services.Themes;

namespace StarPie.Ui.ViewModels.Pages
{
    /// <summary>
    /// 界面主题（AppTheme）设置子 ViewModel：独占软件界面主题的透传——读直取运行态配置、
    /// 写直穿（经防抖消息请求落盘）——并提供驻留的主题选项目录（固定六项、标签即时取词，
    /// 随语言切换重建并补发选中通知恢复选中）。
    /// </summary>
    /// <remarks>
    /// 主题应用到窗口属壳层 View 效果：写穿后发布 <see cref="AppThemeChangedMessage"/>，由
    /// 壳层主窗口（MainView）订阅执行窗口主题应用；配置导入后的重挂路径同样经本消息由壳层
    /// 执行。本 VM 由 <c>ThemeContributor</c> 以会话作用域注册（设置台会话内驻留，随设置台关闭
    /// 释放），外观页的界面主题卡 DataContext 指向它；切语重建选项目录，
    /// 由 <see cref="ResidentOptionRefresher"/> 管订阅与退订。
    /// </remarks>
    public partial class InterfaceThemeSettingsViewModel : ObservableObject, IDisposable
    {
        private readonly IConfigService _config;
        private readonly IMessenger _messenger;
        private readonly ILocalizationService _localization;
        private readonly ResidentOptionRefresher _residentOptions;
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

            RebuildAppThemeOptions();

            // 主题选项目录标签属驻留文案：切语重建目录并补发选中通知（退订随本 VM 释放）。
            _residentOptions = new ResidentOptionRefresher(
                _localization,
                RebuildAppThemeOptions,
                () => OnPropertyChanged(nameof(AppTheme)));
        }

        private AppConfig Config => _config.Current;

        /// <summary>
        /// 软件界面主题（System/Light/Dark/MidnightNavy/RoyalViolet/TitaniumGray）。
        /// 透传属性：读直取运行态配置，取值经主题目录归一到常量原形——空值、遗留别名与未知名
        /// 回落 System，否则旧的遗留值会让下拉空白且界面静默停在别的主题上；归一只读不写盘。
        /// 写直穿配置后经防抖消息请求落盘，并发布 <see cref="AppThemeChangedMessage"/>
        /// 交壳层主窗口应用窗口主题。
        /// 下拉项重建期间绑定回推的瞬态 null/空值被忽略，避免切语重建目录时误把选中清成 System。
        /// </summary>
        public string AppTheme
        {
            get => AppThemeNames.CanonicalOrNull(Config.AppTheme) ?? AppThemeNames.System;
            set
            {
                // 重建选项目录期间绑定回推 null/空值：忽略以免误清当前主题。
                if (string.IsNullOrEmpty(value)) return;
                if (string.Equals(Config.AppTheme, value, StringComparison.Ordinal)) return;

                Config.AppTheme = value;
                OnPropertyChanged();
                _messenger.Send(DebouncedSaveRequestedMessage.Instance);
                _messenger.Send(new AppThemeChangedMessage(value));
            }
        }

        private IReadOnlyList<AppThemeOptionItem> _appThemeOptions = Array.Empty<AppThemeOptionItem>();

        /// <summary>界面主题下拉的固定选项：Tag 供 SelectedValue 匹配，标签即时取词。</summary>
        public IReadOnlyList<AppThemeOptionItem> AppThemeOptions
        {
            get => _appThemeOptions;
            private set => SetProperty(ref _appThemeOptions, value);
        }

        /// <summary>重建 <see cref="AppThemeOptions"/>（切语/构造时调用；目录固定六项、标签随语言变化）。</summary>
        private void RebuildAppThemeOptions()
        {
            AppThemeOptions = new[]
            {
                new AppThemeOptionItem(AppThemeNames.System, _localization.GetString("ThemeSystem")),
                new AppThemeOptionItem(AppThemeNames.Light, _localization.GetString("ThemeLight")),
                new AppThemeOptionItem(AppThemeNames.Dark, _localization.GetString("ThemeDark")),
                new AppThemeOptionItem(AppThemeNames.MidnightNavy, _localization.GetString("ThemeNavy")),
                new AppThemeOptionItem(AppThemeNames.RoyalViolet, _localization.GetString("ThemeViolet")),
                new AppThemeOptionItem(AppThemeNames.TitaniumGray, _localization.GetString("ThemeGray"))
            };
        }

        /// <summary>
        /// 导入配置后从当前配置重挂：透传属性读穿新配置实例，无需状态迁移——补发选中通知
        /// 让绑定拉取新值恢复 ComboBox 选中，并发布 <see cref="AppThemeChangedMessage"/> 由壳层
        /// 主窗口执行窗口主题应用。
        /// </summary>
        public void ReloadFromConfig()
        {
            OnPropertyChanged(nameof(AppTheme));
            _messenger.Send(new AppThemeChangedMessage(AppTheme));
        }

        /// <summary>退订本地化事件与导入广播（页面 VM 随设置台会话释放）。</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _residentOptions.Dispose();
            _messenger.UnregisterAll(this);
        }
    }

    /// <summary>界面主题下拉选项条目：Tag 供 SelectedValue 匹配，Label 为展示文案。</summary>
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
