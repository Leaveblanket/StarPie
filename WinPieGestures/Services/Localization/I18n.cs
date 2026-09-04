using System;
using System.Collections.Generic;

namespace WinPieGestures.Services.Localization
{
    /// <summary>
    /// 静态便捷门面（ADR-0013 S3 过渡形态；#45 删除）：全部成员转发到组合根注入的
    /// <see cref="ILocalizationService"/>；未初始化时自举默认实例，便于测试与早期调用。
    /// <see cref="LanguageChanged"/> 是服务事件的桥接转发，订阅者必须成对退订。
    /// </summary>
    public static class I18n
    {
        private static ILocalizationService? _service;
        private static bool _bridged;
        private static readonly object Sync = new();

        /// <summary>语言实际变化后触发（桥接自服务事件）。</summary>
        public static event Action? LanguageChanged;

        private static ILocalizationService Service
        {
            get
            {
                if (_service == null)
                {
                    lock (Sync)
                    {
                        if (_service == null)
                        {
                            _service = new LocalizationService();
                            Bridge(_service);
                        }
                    }
                }

                return _service;
            }
        }

        /// <summary>组合根把 DI 单例注册到门面；此后全部静态调用转发该实例。</summary>
        internal static void Initialize(ILocalizationService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            lock (Sync)
            {
                if (_service == service) return;
                if (_bridged && _service != null) _service.LanguageChanged -= BridgeLanguageChanged;
                _service = service;
                Bridge(_service);
            }
        }

        private static void Bridge(ILocalizationService service)
        {
            if (_bridged) return;
            service.LanguageChanged += BridgeLanguageChanged;
            _bridged = true;
        }

        private static void BridgeLanguageChanged() => LanguageChanged?.Invoke();

        public static LanguageCode CurrentLanguage
        {
            get => Service.CurrentLanguage;
            set => Service.SetLanguage(value);
        }

        public static string CurrentLanguageCode => Service.CurrentLanguageCode;

        public static void SetLanguage(string code) => Service.SetLanguage(code);

        public static string T(string key) => Service.GetString(key);

        public static string GetString(string key) => Service.GetString(key);

        internal static IEnumerable<KeyValuePair<string, string>> EnumerateCurrentEntries() => Service.EnumerateCurrentEntries();
    }
}
