namespace StarPie.Ports
{
    /// <summary>
    /// 主题应用端口（宿主内核 → Ui）：把有效主题应用为界面呈现所需的调色板。
    /// </summary>
    /// <remarks>
    /// 内核零 WPF，主题效果只能回抛接口由 Ui 侧适配器实现；实现方（承载 XAML 主题字典与
    /// Application 合并字典）在 UI 线程上调用，内核不感知其实现细节。
    /// </remarks>
    public interface IThemeApplier
    {
        /// <summary>把有效主题整项换入（"Light"/"Dark"/"MidnightNavy"/"RoyalViolet"/"TitaniumGray"，未知名回落 Light）。</summary>
        void ApplyTheme(string effectiveTheme);
    }
}
