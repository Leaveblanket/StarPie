namespace WinPieGestures.Views.Pages
{
    /// <summary>
    /// 手势与动作页面 (T19/T21/T24)：方案增删改与扇区数切换编排在 <see cref="ProfileListViewModel"/>，
    /// 列表选中经 ProfilesListBox.SelectedItem 双向绑定 SelectedProfile（默认选中与导入回落
    /// 均在 VM 内维护），code-behind 已无业务（本地化文本经 T24 语言字典声明式化；
    /// 页面副标题等未本地化硬编码文案维持现状，非本票范围）。
    /// </summary>
    public partial class GesturesSettingsPage : SettingsPageBase
    {
        public GesturesSettingsPage()
        {
            InitializeComponent();
        }
    }
}
