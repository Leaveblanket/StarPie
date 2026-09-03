using System.Windows;
using System.Windows.Controls;
using WinPieGestures.Services;

namespace WinPieGestures.Views.Pages
{
    /// <summary>
    /// 设置页面基类 (T19)：页面 View 经 DataTemplate 无参构造、按导航重建，本基类承载共有约定——
    /// <para>①I18n 语言广播的 Loaded/Unloaded 成对订阅退订（语言切换后挂载中的页面文本即时刷新，
    /// 页面卸载不泄漏静态事件订阅）；</para>
    /// <para>②<see cref="OnPageLoaded"/>/<see cref="OnPageUnloaded"/>
    /// 钩子供各页做控件回填与 VM 视图事件的成对订阅退订（页面 VM 是容器单例，页面过期引用靠退订释放）；</para>
    /// <para>③迁移前的视图辅助（SetComboBoxSelectedValue 含旧版 Tag 映射表）原样收编。</para>
    /// </summary>
    public abstract class SettingsPageBase : UserControl
    {
        private bool _languageHookAttached;

        protected SettingsPageBase()
        {
            Loaded += (_, _) =>
            {
                if (!_languageHookAttached)
                {
                    _languageHookAttached = true;
                    I18n.LanguageChanged += ApplyLocalization;
                }

                ApplyLocalization();
                OnPageLoaded();
            };

            Unloaded += (_, _) =>
            {
                if (_languageHookAttached)
                {
                    _languageHookAttached = false;
                    I18n.LanguageChanged -= ApplyLocalization;
                }

                OnPageUnloaded();
            };
        }

        /// <summary>页面挂载（首次与每次导航回到本页）：控件初值回填、订阅 VM 视图事件的时机。</summary>
        protected virtual void OnPageLoaded()
        {
        }

        /// <summary>页面卸载：退订 VM 视图事件与广播，防单例 VM 持有过期页面引用。</summary>
        protected virtual void OnPageUnloaded()
        {
        }

        /// <summary>刷新本页界面文本（I18n 语言广播与页面挂载时调用）。</summary>
        protected abstract void ApplyLocalization();

        /// <summary>把 ComboBox 选中项按值同步（迁移前 SetComboBoxSelectedValue 原样收编，含旧版 Tag 映射）。</summary>
        protected static void SetComboBoxSelectedValue(System.Windows.Controls.ComboBox comboBox, string value)
        {
            if (comboBox == null || string.IsNullOrEmpty(value)) return;
            string mappedValue = value;
            if (value == "RoundedRect" || value == "FloatingCapsules" || value == "Capsule") mappedValue = "RoundedCapsule";
            if (value == "OrganicPetals" || value == "ArcTracker" || value == "LiquidDroplets" || value == "MinimalArc") mappedValue = "Original";

            foreach (System.Windows.Controls.ComboBoxItem item in comboBox.Items)
            {
                string tag = item.Tag?.ToString() ?? "";
                if (string.Equals(tag, value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tag, mappedValue, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }
    }
}
