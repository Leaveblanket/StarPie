using System;
using System.Collections.Generic;
using System.Windows;
using StarPie.Services.Localization;

namespace StarPie.Services.Dialogs
{
    /// <summary>
    /// 对话框服务实现。Owner 采用惰性回填：组合根先建服务、后建设置窗口，
    /// 窗口创建完成后调 <see cref="SetOwner"/> 回填引用，化解“服务需要 Owner ↔ 窗口依赖
    /// 服务”的循环；Owner 的用法是实现内部自由，不泄露进接口。
    /// </summary>
    /// <remarks>
    /// 程序选择器、输入框、图标/颜色选择器与屏上取色均已走 VM 化链路。
    /// 程序扫描候选来源经构造注入的扫描委托提供（组合根以
    /// <see cref="ProgramScanner.ScanInstalledPrograms"/> 登记），图标资产经注入的
    /// <see cref="IIconAssetService"/> 实例服务与 <see cref="IconCatalog"/> 纯目录
    /// （ADR-0019/#87：S1 双形，对话框服务不直连业务模块静态内部）。
    /// </remarks>
    public sealed class DialogService : IDialogService
    {
        private readonly IThemeService _themeService;
        private readonly ILocalizationService _localization;
        private readonly IIconAssetService _iconAssets;
        private readonly IShortcutTargetResolver _shortcutResolver;
        private readonly Func<IReadOnlyList<ProgramEntry>> _scanPrograms;
        private Window? _owner;

        public DialogService(
            IThemeService themeService,
            ILocalizationService localization,
            IIconAssetService iconAssets,
            IShortcutTargetResolver shortcutResolver,
            Func<IReadOnlyList<ProgramEntry>> scanPrograms)
        {
            _themeService = themeService;
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _iconAssets = iconAssets ?? throw new ArgumentNullException(nameof(iconAssets));
            _shortcutResolver = shortcutResolver ?? throw new ArgumentNullException(nameof(shortcutResolver));
            _scanPrograms = scanPrograms ?? throw new ArgumentNullException(nameof(scanPrograms));
        }

        /// <summary>组合根在设置窗口创建完成后回填 Owner；此前调用任何 Show* 都不带 Owner。</summary>
        public void SetOwner(Window owner) => _owner = owner;

        public ProgramPickResult? ShowProgramPicker()
        {
            var viewModel = new ProgramPickerViewModel(_scanPrograms, this, _localization, _shortcutResolver);
            var window = new ProgramPickerWindow(_themeService, viewModel, _localization) { Owner = _owner };
            if (window.ShowDialog() != true) return null;
            return window.BuildResult();
        }

        public InputDialogResult? ShowInputDialog(
            string title,
            string prompt,
            string defaultText = "",
            Func<string, (bool IsValid, string ErrorMessage)>? validator = null)
        {
            // 确认与验证逻辑在 InputViewModel，窗口只剩布局接线。
            var viewModel = new InputViewModel(title, prompt, this, _localization, defaultText, validator);
            var dialog = new InputDialog(_themeService, viewModel) { Owner = _owner };
            return dialog.ShowDialog() == true ? viewModel.BuildResult() : null;
        }

        public IconPickResult? ShowIconPicker(string? currentIconKey)
        {
            var viewModel = new IconPickerViewModel(
                _iconAssets.GetCustomIcons,
                () => IconCatalog.VectorIconList,
                this,
                _localization,
                currentIconKey,
                _iconAssets.DeleteCustomIcon,
                path => _iconAssets.ImportCustomIcon(path));
            var picker = new IconPickerWindow(_themeService, viewModel, _localization, _iconAssets) { Owner = _owner };
            return picker.ShowDialog() == true ? picker.BuildResult() : null;
        }

        public ColorPickResult? ShowColorPicker(string initialHex)
        {
            var viewModel = new ColorPickerViewModel(this, initialHex);
            var dialog = new ColorPickerWindow(_themeService, viewModel, _localization) { Owner = _owner };
            return dialog.ShowDialog() == true ? dialog.BuildResult() : null;
        }

        public EyedropResult? ShowEyedropper()
        {
            // 全屏置顶工具，刻意不设 Owner。
            var eyedropper = new ScreenEyedropperWindow(new ScreenEyedropperViewModel());
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

        public FilePickResult? ShowFolderDialog(string? initialDirectory = null, string? title = null)
        {
            var openFolderDialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = title ?? "",
                Multiselect = false
            };
            if (!string.IsNullOrWhiteSpace(initialDirectory) && System.IO.Directory.Exists(initialDirectory))
            {
                openFolderDialog.InitialDirectory = initialDirectory;
            }

            return openFolderDialog.ShowDialog(_owner) == true ? new FilePickResult(openFolderDialog.FolderName) : null;
        }

        public bool Confirm(string title, string message)
        {
            return MessageBox.Show(_owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public void ShowInfo(string title, string message)
        {
            MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
