using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using StarPie.Kernel.Localization;

namespace StarPie.Services.Dialogs
{
    /// <summary>
    /// 对话框服务实现。Owner 采用惰性回填：组合根先建服务、后建设置窗口，
    /// 窗口创建完成后调 <see cref="SetOwner"/> 回填引用，化解“服务需要 Owner ↔ 窗口依赖
    /// 服务”的循环；Owner 的用法是实现内部自由，不泄露进接口。
    /// </summary>
    /// <remarks>
    /// 程序选择器、输入框、图标/颜色选择器与屏上取色均已走 VM 化链路。
    /// 程序扫描候选来源经构造注入的 <see cref="IProgramScanner"/> 契约提供（契约驻共享
    /// 内核、实现与注册由 M3 下放，ADR-0020/#88），图标资产经注入的
    /// <see cref="IIconAssetService"/> 实例服务与 <see cref="IconCatalog"/> 纯目录
    /// （ADR-0019/#87：S1 双形，对话框服务不直连业务模块静态内部）。
    /// </remarks>
    public sealed class DialogService : IDialogService
    {
        private readonly IThemeService _themeService;
        private readonly ILocalizationService _localization;
        private readonly IIconAssetService _iconAssets;
        private readonly IShortcutTargetResolver _shortcutResolver;
        private readonly IProgramScanner _programScanner;
        private Window? _owner;
        // 后台模式（--background，e2e 静默跑用）：提示类对话框不呈现、确认类取"是"——
        // 无人在场时不能把系统 MessageBox 弹到用户屏幕上（它不跟随离屏 owner，按显示器居中）。
        // 对话框↔VM 的接线由 xUnit 的 TestDialogService 覆盖，e2e 不断言弹框本身。
        private bool _backgroundMode;

        public DialogService(
            IThemeService themeService,
            ILocalizationService localization,
            IIconAssetService iconAssets,
            IShortcutTargetResolver shortcutResolver,
            IProgramScanner programScanner)
        {
            _themeService = themeService;
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _iconAssets = iconAssets ?? throw new ArgumentNullException(nameof(iconAssets));
            _shortcutResolver = shortcutResolver ?? throw new ArgumentNullException(nameof(shortcutResolver));
            _programScanner = programScanner ?? throw new ArgumentNullException(nameof(programScanner));
        }

        /// <summary>组合根在设置窗口创建完成后回填 Owner；此前调用任何 Show* 都不带 Owner。</summary>
        public void SetOwner(Window owner) => _owner = owner;

        /// <summary>宿主启动时按 <c>--background</c> 回填（后台/静默运行语义）。</summary>
        public void SetBackgroundMode(bool value) => _backgroundMode = value;

        public ProgramPickResult? ShowProgramPicker()
        {
            var viewModel = new ProgramPickerViewModel(_programScanner, this, _localization, _shortcutResolver);
            var window = PrepareBackgroundDialog(new ProgramPickerWindow(_themeService, viewModel, _localization) { Owner = _owner });
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
            var dialog = PrepareBackgroundDialog(new InputDialog(_themeService, viewModel) { Owner = _owner });
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
            var picker = PrepareBackgroundDialog(new IconPickerWindow(_themeService, viewModel, _localization, _iconAssets) { Owner = _owner });
            return picker.ShowDialog() == true ? picker.BuildResult() : null;
        }

        public ColorPickResult? ShowColorPicker(string initialHex)
        {
            var viewModel = new ColorPickerViewModel(this, initialHex);
            var dialog = PrepareBackgroundDialog(new ColorPickerWindow(_themeService, viewModel, _localization) { Owner = _owner });
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
            // 后台模式无人在场应答：按"是"继续，不呈现窗口。
            if (_backgroundMode)
            {
                return true;
            }

            return MessageBox.Show(_owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public void ShowInfo(string title, string message)
        {
            // 后台模式不呈现提示框（无人阅读，且会弹到用户屏幕中央并抢前台）。
            if (_backgroundMode)
            {
                return;
            }

            MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ==== 后台模式对话框形态（#135：v135 程序选择器交互用例的真实打开路径）====

        private const int BackgroundCoordinate = -32000;
        private const int GwlExStyle = -20;
        private const int WsExNoActivate = 0x08000000;

        /// <summary>
        /// 后台模式下把 WPF 对话框切成离屏 + 不可激活（与 AppHost 设置控制台同配方）：
        /// e2e 会真实打开对话框（如程序选择器），不得让它出现在用户屏幕上或抢前台。
        /// 仅改变窗口呈现/激活；对话框内容与交互语义不变。
        /// </summary>
        private T PrepareBackgroundDialog<T>(T window) where T : Window
        {
            if (!_backgroundMode) return window;

            window.ShowActivated = false;
            window.ShowInTaskbar = false;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = BackgroundCoordinate;
            window.Top = BackgroundCoordinate;
            window.SourceInitialized += (_, _) =>
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                {
                    return;
                }

                int exStyle = GetWindowLong(hwnd, GwlExStyle);
                SetWindowLong(hwnd, GwlExStyle, exStyle | WsExNoActivate);
            };
            return window;
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
