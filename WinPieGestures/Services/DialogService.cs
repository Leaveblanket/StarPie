using System;
using System.Windows;

namespace WinPieGestures
{
    /// <summary>
    /// 对话框服务实现 (T06/T07, ADR-0004)。Owner 采用惰性回填：组合根先建服务、后建设置窗口，
    /// 窗口创建完成后调 <see cref="SetOwner"/> 回填引用，化解"服务需要 Owner ↔ 窗口依赖服务"
    /// 的循环。Owner 的用法是实现内部自由，不泄露进接口。
    /// 迁移期混装：程序选择器（T06）、输入框（T07）与图标/颜色选择器、屏上取色（T08）
    /// 已走 VM 化链路；接口保持不变。
    /// </summary>
    public sealed class DialogService : IDialogService
    {
        private readonly IThemeService _themeService;
        private Window? _owner;

        public DialogService(IThemeService themeService)
        {
            _themeService = themeService;
        }

        /// <summary>组合根在设置窗口创建完成后回填 Owner；此前调用任何 Show* 都不带 Owner。</summary>
        public void SetOwner(Window owner) => _owner = owner;

        public ProgramPickResult? ShowProgramPicker()
        {
            var window = new ProgramPickerWindow(_themeService, this) { Owner = _owner };
            if (window.ShowDialog() != true) return null;
            return window.BuildResult();
        }

        public InputDialogResult? ShowInputDialog(
            string title,
            string prompt,
            string defaultText = "",
            Func<string, (bool IsValid, string ErrorMessage)>? validator = null)
        {
            // T07：确认与验证逻辑已迁 InputViewModel，窗口只剩布局接线（ADR-0004）。
            var viewModel = new InputViewModel(title, prompt, defaultText, validator);
            var dialog = new InputDialog(_themeService, viewModel) { Owner = _owner };
            return dialog.ShowDialog() == true ? viewModel.BuildResult() : null;
        }

        public IconPickResult? ShowIconPicker(string? currentIconKey)
        {
            var picker = new IconPickerWindow(_themeService, this, currentIconKey) { Owner = _owner };
            return picker.ShowDialog() == true ? picker.BuildResult() : null;
        }

        public ColorPickResult? ShowColorPicker(string initialHex)
        {
            var dialog = new ColorPickerWindow(_themeService, this, initialHex) { Owner = _owner };
            return dialog.ShowDialog() == true ? dialog.BuildResult() : null;
        }

        public EyedropResult? ShowEyedropper()
        {
            // 全屏置顶工具，刻意不用 Owner（ADR-0004）。
            var eyedropper = new ScreenEyedropperOverlay();
            return eyedropper.ShowDialog() == true && !string.IsNullOrEmpty(eyedropper.CapturedHexColor)
                ? new EyedropResult(eyedropper.CapturedHexColor!)
                : null;
        }

        public FilePickResult? ShowOpenFileDialog(string filter, string? title = null)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                Title = title ?? "",
                CheckFileExists = true
            };

            return openFileDialog.ShowDialog(_owner) == true ? new FilePickResult(openFileDialog.FileName) : null;
        }

        public FilePickResult? ShowSaveFileDialog(string filter, string? fileName = null, string? title = null)
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = filter,
                FileName = fileName ?? "",
                Title = title ?? ""
            };

            return saveFileDialog.ShowDialog(_owner) == true ? new FilePickResult(saveFileDialog.FileName) : null;
        }
    }
}
