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

        /// <summary>确认或手动浏览后请求窗口关闭；携带 null 表示无效选择（视图层负责提示）。</summary>
        public event Action<ProgramPickResult?>? CloseRequested;

        public ProgramPickerViewModel(Func<IReadOnlyList<ProgramEntry>> scanPrograms, IDialogService dialogs)
        {
            _scanPrograms = scanPrograms;
            _dialogs = dialogs;
            _statusText = I18n.T("ProgramPickerScanning");
        }

        partial void OnSearchTextChanged(string value) => ApplySearch(value);

        /// <summary>扫描编排：后台线程跑来源扫描，回来后重建展示列表（保持"扫描中"提示的旧行为）。</summary>
        public async Task LoadAsync()
        {
            HasError = false;
            StatusText = I18n.T("ProgramPickerScanning");

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
                StatusText = $"{I18n.T("Error")}: {ex.Message}";
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

        /// <summary>确认：未选中时携带 null（视图层弹"请选择"提示并保持窗口打开）；
        /// 取消不经过此事件，由视图直接关窗。</summary>
        [RelayCommand]
        private void Confirm() => CloseRequested?.Invoke(BuildResult());

        /// <summary>手动浏览：经对话框服务开系统文件对话框；.lnk 解析为真实目标（沿用旧行为），
        /// 取消则停留在本窗口。</summary>
        [RelayCommand]
        private void BrowseManually()
        {
            var picked = _dialogs.ShowOpenFileDialog(ManualBrowseFilter, I18n.T("BtnBrowseApp"));
            if (picked == null) return;

            string chosenPath = picked.Path;
            if (chosenPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) &&
                IconHelper.ResolveShortcutTarget(chosenPath, out string targetPath, out _, out _) &&
                !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
            {
                chosenPath = targetPath;
            }

            CloseRequested?.Invoke(new ProgramPickResult(Path.GetFileNameWithoutExtension(picked.Path), chosenPath));
        }
    }
}
