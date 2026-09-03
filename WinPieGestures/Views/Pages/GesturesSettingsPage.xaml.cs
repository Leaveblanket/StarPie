namespace WinPieGestures.Views.Pages
{
    /// <summary>
    /// 手势与动作页面 (T19/T21)：方案增删改与扇区数切换编排在 <see cref="ProfileListViewModel"/>，
    /// 列表选中经 ProfilesListBox.SelectedItem 双向绑定 SelectedProfile（默认选中与导入回落
    /// 均在 VM 内维护），code-behind 只保留本地化回填（ADR-0009）。
    /// </summary>
    public partial class GesturesSettingsPage : SettingsPageBase
    {
        public GesturesSettingsPage()
        {
            InitializeComponent();
        }

        protected override void ApplyLocalization()
        {
            GesturesPageHeader.Text = I18n.T("GesturesHeader");
            ProfileCardTitleText.Text = I18n.T("ProfileCardTitle");
            ProfileCardDescText.Text = I18n.T("ProfileCardDesc");
            AddProfileButton.Content = I18n.T("BtnAddAppProfile");
            AddCustomProfileButton.Content = I18n.T("BtnAddCustomProfile");
            RenameProfileButton.Content = I18n.T("BtnRenameProfile");
            DeleteProfileButton.Content = I18n.T("BtnDeleteProfile");
            SectorCountTitleText.Text = I18n.T("SectorCountOptionTitle");
            SectorCountDescText.Text = I18n.T("SectorCountOptionDesc");
            SectorCount4Radio.Content = I18n.T("SectorCount4");
            SectorCount8Radio.Content = I18n.T("SectorCount8");
            SectorCount12Radio.Content = I18n.T("SectorCount12");
            SectorActionListTitleText.Text = I18n.T("SectorActionListTitle");
            SectorActionListDescText.Text = I18n.T("SectorActionListDesc");
        }
    }
}
