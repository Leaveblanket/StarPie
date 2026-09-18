using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace StarPie.Ui.Views.Controls
{
    /// <summary>
    /// 热键录制框的自动化对等体：自定义 Control 不提供对等体时对 UIA 完全不可见，
    /// 无障碍客户端与 e2e 既定位不到、也读不到已录制内容。
    /// </summary>
    /// <remarks>
    /// 对等体把控件发布为可聚焦的编辑项，并把已录制热键作为值暴露（读值/写值与
    /// <see cref="HotkeyRecorderBox.HotkeyText"/> 同源）；键盘事件的录制语义仍在控件内，
    /// 对等体不重复实现。
    /// </remarks>
    public sealed class HotkeyRecorderBoxAutomationPeer : FrameworkElementAutomationPeer, IValueProvider
    {
        public HotkeyRecorderBoxAutomationPeer(HotkeyRecorderBox owner) : base(owner)
        {
        }

        protected override string GetClassNameCore() => "HotkeyRecorderBox";

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;

        /// <summary>名称取已录制热键；尚未录制时回落到占位文案，与用户所见一致。</summary>
        protected override string GetNameCore()
        {
            HotkeyRecorderBox owner = (HotkeyRecorderBox)Owner;
            return string.IsNullOrEmpty(owner.HotkeyText) ? owner.Placeholder : owner.HotkeyText;
        }

        public bool IsReadOnly => false;

        public string Value => ((HotkeyRecorderBox)Owner).HotkeyText;

        public void SetValue(string value) => ((HotkeyRecorderBox)Owner).HotkeyText = value;
    }
}
