using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StarPie.ViewModels.Dialogs
{
    /// <summary>程序选择器列表条目：扫描候选（纯数据 <see cref="ProgramEntry"/>）与按路径装配的图标。</summary>
    public sealed record ProgramPickerItem(ProgramEntry Program, ImageSource? IconSource)
    {
        public string Name => Program.Name;
        public string Path => Program.Path;
        public string FriendlyPath => Program.FriendlyPath;
    }

    /// <summary>
    /// 程序选择器 ViewModel：完整接管扫描编排（注入 <see cref="IProgramScanner"/>，
    /// 测试可换假实现）、图标装配（注入 <see cref="IIconAssetService"/>）、
    /// 搜索过滤与选择结果。窗口 code-behind 只剩布局、本地化文案与把
    /// <see cref="IsCompleted"/> 落成 DialogResult。
    /// </summary>
    /// <remarks>确认结果遵循可空结果对象约定：未选中时 <see cref="BuildResult"/> 为 null，
    /// VM 经对话框服务弹提示并保持窗口打开；取消与无效不产生结果。</remarks>
    public partial class ProgramPickerViewModel : ObservableObject
    {
        /// <summary>手动浏览的系统文件对话框过滤器（即时取词：文件对话框瞬态呈现）。</summary>
        public string ManualBrowseFilter => _localization.GetString("ProgramPickerExeFilter");

        private readonly IProgramScanner _programScanner;
        private readonly IDialogService _dialogs;
        private readonly ILocalizationService _localization;
        private readonly IShortcutTargetResolver _shortcutResolver;
        private readonly IIconAssetService _iconAssets;
        private readonly List<ProgramPickerItem> _allPrograms = new();

        /// <summary>当前过滤条件下的展示列表。</summary>
        public ObservableCollection<ProgramPickerItem> DisplayedPrograms { get; } = new();

        [ObservableProperty]
        private string _searchText = "";

        [ObservableProperty]
        private ProgramPickerItem? _selectedProgram;

        [ObservableProperty]
        private string _statusText;

        [ObservableProperty]
        private bool _hasError;

        /// <summary>状态条可见性：打开时提示扫描中，扫描成功后隐藏，出错后常驻显示错误。</summary>
        [ObservableProperty]
        private bool _isStatusVisible = true;

        /// <summary>确认或手动浏览后请求窗口关闭；null 表示无效选择。</summary>
        [ObservableProperty]
        private ProgramPickResult? _result;

        /// <summary>确认或手动浏览成功后变为 true，视图据此关闭窗口。</summary>
        [ObservableProperty]
        private bool _isCompleted;

        public ProgramPickerViewModel(
            IProgramScanner programScanner,
            IDialogService dialogs,
            ILocalizationService localization,
            IShortcutTargetResolver shortcutResolver,
            IIconAssetService iconAssets)
        {
            _programScanner = programScanner ?? throw new ArgumentNullException(nameof(programScanner));
            _dialogs = dialogs;
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _shortcutResolver = shortcutResolver ?? throw new ArgumentNullException(nameof(shortcutResolver));
            _iconAssets = iconAssets ?? throw new ArgumentNullException(nameof(iconAssets));
            _statusText = _localization.GetString("ProgramPickerScanning");
            _ = LoadAsync();
        }

        partial void OnSearchTextChanged(string value) => ApplySearch(value);

        private Task? _loadTask;

        /// <summary>扫描编排：构造自动启动一次；重复调用返回同一任务（single-flight，避免并发双扫）。</summary>
        public Task LoadAsync() => _loadTask ??= LoadCoreAsync();

        private async Task LoadCoreAsync()
        {
            HasError = false;
                StatusText = _localization.GetString("ProgramPickerScanning");

            try
            {
                // 扫描与图标装配同处后台线程（图标提取为 Win32/图像构造，不阻塞 UI）。
                var items = await Task.Run(() => _programScanner.ScanInstalledPrograms()
                    .Select(entry => new ProgramPickerItem(entry, _iconAssets.GetIcon(entry.Path)))
                    .ToList());

                _allPrograms.Clear();
                _allPrograms.AddRange(items);
                ApplySearch(SearchText);
                IsStatusVisible = false;
            }
            catch (Exception ex)
            {
                HasError = true;
                StatusText = $"{_localization.GetString("Error")}: {ex.Message}";
            }
        }

        /// <summary>搜索过滤：委托 <see cref="ProgramCatalog.MatchesFilter"/> 纯函数重建展示列表
        /// （保留同一 <see cref="ProgramPickerItem"/> 实例，选中项引用不随过滤抖动）。</summary>
        public void ApplySearch(string? filter)
        {
            DisplayedPrograms.Clear();
            foreach (var item in _allPrograms)
            {
                if (ProgramCatalog.MatchesFilter(item.Program, filter))
                {
                    DisplayedPrograms.Add(item);
                }
            }
        }

        /// <summary>选择结果：未选中（或选中项为空）返回 null，调用方只判一次 null。</summary>
        public ProgramPickResult? BuildResult()
            => SelectedProgram is { } selected ? new ProgramPickResult(selected.Name, selected.Path) : null;

        /// <summary>确认：未选中时提示并保持窗口打开；选中时完成。</summary>
        [RelayCommand]
        private void Confirm()
        {
            Result = BuildResult();
            if (Result == null)
            {
                _dialogs.ShowInfo(
                    _localization.GetString("ProgramPickerNone"),
                    string.Format(_localization.GetString("ProgramPickerNoneHint"), _localization.GetString("BtnManualBrowse")));
                return;
            }

            IsCompleted = true;
        }

        /// <summary>手动浏览：经对话框服务开系统文件对话框；.lnk 解析为真实目标，
        /// 取消则停留在本窗口。</summary>
        [RelayCommand]
        private void BrowseManually()
        {
            var picked = _dialogs.ShowOpenFileDialog(ManualBrowseFilter, _localization.GetString("BtnBrowseApp"));
            if (picked == null) return;

            string chosenPath = picked.Path;
            if (chosenPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) &&
                _shortcutResolver.ResolveShortcutTarget(chosenPath, out string targetPath, out _, out _) &&
                !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
            {
                chosenPath = targetPath;
            }

            Result = new ProgramPickResult(Path.GetFileNameWithoutExtension(picked.Path), chosenPath);
            IsCompleted = true;
        }
    }
}
