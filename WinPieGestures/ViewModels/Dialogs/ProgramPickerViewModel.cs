using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WinPieGestures.ViewModels.Dialogs
{
    /// <summary>
    /// 程序选择器 ViewModel (T06, ADR-0001/0004)：完整接管扫描编排（注入扫描委托，测试可换假实现）、
    /// 搜索过滤与选择结果。窗口 code-behind 只剩布局、本地化文案与把 <see cref="CloseRequested"/>
    /// 落成 DialogResult。确认结果遵循可空结果对象约定：未选中时 <see cref="BuildResult"/> 为 null，
    /// 由视图层弹"请选择"提示；取消与无效不产生结果。
    /// </summary>
    public partial class ProgramPickerViewModel : ObservableObject
    {
        /// <summary>手动浏览的系统文件对话框过滤器（与迁移前一致）。</summary>
        public const string ManualBrowseFilter = "可执行程序 (*.exe)|*.exe|快捷方式 (*.lnk)|*.lnk|所有文件 (*.*)|*.*";

        private readonly Func<IReadOnlyList<ProgramEntry>> _scanPrograms;
        private readonly IDialogService _dialogs;
        private readonly ILocalizationService _localization;
        private readonly List<ProgramEntry> _allPrograms = new();

        /// <summary>当前过滤条件下的展示列表。</summary>
        public ObservableCollection<ProgramEntry> DisplayedPrograms { get; } = new();

        [ObservableProperty]
        private string _searchText = "";

        [ObservableProperty]
        private ProgramEntry? _selectedProgram;

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
            Func<IReadOnlyList<ProgramEntry>> scanPrograms,
            IDialogService dialogs,
            ILocalizationService localization)
        {
            _scanPrograms = scanPrograms;
            _dialogs = dialogs;
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
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
                var programs = await Task.Run(() => _scanPrograms());
                _allPrograms.Clear();
                _allPrograms.AddRange(programs);
                ApplySearch(SearchText);
                IsStatusVisible = false;
            }
            catch (Exception ex)
            {
                HasError = true;
                StatusText = $"{_localization.GetString("Error")}: {ex.Message}";
            }
        }

        /// <summary>搜索过滤：委托 <see cref="ProgramCatalog.FilterPrograms"/> 纯函数重建展示列表。</summary>
        public void ApplySearch(string? filter)
        {
            DisplayedPrograms.Clear();
            foreach (var entry in ProgramCatalog.FilterPrograms(_allPrograms, filter))
            {
                DisplayedPrograms.Add(entry);
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
                _dialogs.ShowInfo("未选择", "请选择一个程序，或者点击“手动浏览文件...”");
                return;
            }

            IsCompleted = true;
        }

        /// <summary>手动浏览：经对话框服务开系统文件对话框；.lnk 解析为真实目标（沿用旧行为），
        /// 取消则停留在本窗口。</summary>
        [RelayCommand]
        private void BrowseManually()
        {
            var picked = _dialogs.ShowOpenFileDialog(ManualBrowseFilter, _localization.GetString("BtnBrowseApp"));
            if (picked == null) return;

            string chosenPath = picked.Path;
            if (chosenPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) &&
                IconHelper.ResolveShortcutTarget(chosenPath, out string targetPath, out _, out _) &&
                !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
            {
                chosenPath = targetPath;
            }

            Result = new ProgramPickResult(Path.GetFileNameWithoutExtension(picked.Path), chosenPath);
            IsCompleted = true;
        }
    }
}
