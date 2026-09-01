using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WinPieGestures.ViewModels.Dialogs
{
    /// <summary>图标卡片条目：自定义图标与内置矢量图标统一成视图可渲染的数据。</summary>
    public sealed record IconEntry(
        string Key,
        string DisplayName,
        bool IsCustom,
        bool IsSvg,
        string SvgData,
        string FilePath);

    /// <summary>
    /// 图标选择器 ViewModel (T08, ADR-0001/0004)：选中状态、搜索过滤、导入/删除编排与确认结果
    /// 全部在此；窗口 code-behind 只剩卡片渲染（主题画刷、SVG/位图元素）与把
    /// <see cref="CloseRequested"/> 落成 DialogResult。图标来源注入委托，测试可换假实现。
    /// </summary>
    public partial class IconPickerViewModel : ObservableObject
    {
        /// <summary>导入自定义图标的系统文件对话框过滤器（与迁移前一致）。</summary>
        public const string ImportIconFilter =
            "所有支持的图标 (*.svg;*.png;*.ico;*.jpg;*.jpeg;*.bmp;*.webp)|*.svg;*.png;*.ico;*.jpg;*.jpeg;*.bmp;*.webp|SVG 矢量图 (*.svg)|*.svg|图片文件 (*.png;*.ico;*.jpg;*.jpeg;*.bmp)|*.png;*.ico;*.jpg;*.jpeg;*.bmp|所有文件 (*.*)|*.*";

        /// <summary>导入自定义图标的系统文件对话框标题（与迁移前一致）。</summary>
        public const string ImportIconDialogTitle = "导入自定义图标 (SVG / PNG / ICO / JPG)";

        private const string CustomSuffix = " (自定义)";

        private readonly Func<IReadOnlyList<IconHelper.CustomIconItem>> _getCustomIcons;
        private readonly Func<IReadOnlyList<VectorIconItem>> _getVectorIcons;
        private readonly Func<string, bool> _deleteCustomIcon;
        private readonly Func<string, IconHelper.CustomIconItem?> _importCustomIcon;
        private readonly IDialogService _dialogs;

        /// <summary>当前过滤条件下的展示列表（自定义图标在前、内置矢量在后，与迁移前一致）。</summary>
        public ObservableCollection<IconEntry> DisplayedIcons { get; } = new();

        [ObservableProperty]
        private string _searchText = "";

        /// <summary>当前选中的图标键；null 表示沿用"未选择"初始态（迁移前行为）。</summary>
        public string? SelectedIconKey { get; private set; }

        /// <summary>"已选图标"文案；选中自定义图标带 "(自定义)" 后缀，清空为 "(无图标)"。</summary>
        [ObservableProperty]
        private string _selectedIconDisplayName;

        /// <summary>确认后请求窗口关闭；总是携带结果（IconKey 可为 null/空，沿用迁移前语义）。</summary>
        public event Action<IconPickResult?>? CloseRequested;

        /// <summary>导入失败时通知视图弹提示（迁移前为 code-behind 直接 MessageBox）。</summary>
        public event Action<string>? ImportFailed;

        public IconPickerViewModel(
            Func<IReadOnlyList<IconHelper.CustomIconItem>> getCustomIcons,
            Func<IReadOnlyList<VectorIconItem>> getVectorIcons,
            IDialogService dialogs,
            string? initialKey = null,
            Func<string, bool>? deleteCustomIcon = null,
            Func<string, IconHelper.CustomIconItem?>? importCustomIcon = null)
        {
            _getCustomIcons = getCustomIcons;
            _getVectorIcons = getVectorIcons;
            _dialogs = dialogs;
            _deleteCustomIcon = deleteCustomIcon ?? IconHelper.DeleteCustomIcon;
            // ImportCustomIcon 带可选第二参，方法组不能直接转 Func<string, T>，用 lambda 适配。
            _importCustomIcon = importCustomIcon ?? (path => IconHelper.ImportCustomIcon(path));

            SelectedIconKey = initialKey;
            // 迁移前：初始键为空时本地化为 "(未选择)"，非空但未匹配到卡片时停留在 XAML 默认文案。
            _selectedIconDisplayName = string.IsNullOrEmpty(initialKey) ? I18n.T("IconPickerNone") : "(未选择)";
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter(value);

        /// <summary>
        /// 重建展示列表：自定义图标在前、内置矢量在后；自定义按显示名/键过滤，
        /// 内置按显示名/分类/键过滤（均忽略大小写）。重建时若卡片键与选中键一致则恢复选中
        /// 文案——与迁移前 PopulateIcons 的选择恢复行为一致。
        /// </summary>
        public void ApplyFilter(string? filter)
        {
            var f = filter?.Trim() ?? "";
            DisplayedIcons.Clear();

            var customs = _getCustomIcons();
            if (f.Length > 0)
            {
                customs = customs
                    .Where(i => i.DisplayName.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                                i.Key.Contains(f, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            foreach (var custom in customs)
            {
                DisplayedIcons.Add(new IconEntry(custom.Key, custom.DisplayName, IsCustom: true, custom.IsSvg, custom.SvgData, custom.FilePath));
            }

            var vectors = _getVectorIcons();
            if (f.Length > 0)
            {
                vectors = vectors
                    .Where(i => i.DisplayName.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                                i.Category.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                                i.Key.Contains(f, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            foreach (var item in vectors)
            {
                DisplayedIcons.Add(new IconEntry(item.Key, item.DisplayName, IsCustom: false, IsSvg: true, item.SvgData, FilePath: ""));
            }

            foreach (var entry in DisplayedIcons)
            {
                if (string.Equals(SelectedIconKey, entry.Key, StringComparison.OrdinalIgnoreCase))
                {
                    Select(entry);
                }
            }
        }

        /// <summary>选中卡片：更新选中键与"已选"文案（自定义图标带后缀）。</summary>
        public void Select(IconEntry entry)
        {
            SelectedIconKey = entry.Key;
            SelectedIconDisplayName = entry.IsCustom ? entry.DisplayName + CustomSuffix : entry.DisplayName;
        }

        /// <summary>清空选择：键置空串、文案落"(无图标)"（与迁移前清空按钮一致）。</summary>
        [RelayCommand]
        private void ClearIcon()
        {
            SelectedIconKey = "";
            SelectedIconDisplayName = "(无图标)";
        }

        /// <summary>
        /// 导入自定义图标：经对话框服务选文件，导入成功后选中新图标并按当前过滤重建列表
        /// （旧按钮行为）；取消则停留原状，导入异常经 <see cref="ImportFailed"/> 交视图提示。
        /// </summary>
        [RelayCommand]
        private void ImportIcon()
        {
            try
            {
                var picked = _dialogs.ShowOpenFileDialog(ImportIconFilter, ImportIconDialogTitle);
                if (picked == null) return;

                var imported = _importCustomIcon(picked.Path);
                if (imported != null)
                {
                    SelectedIconKey = imported.Key;
                    ApplyFilter(SearchText);
                }
            }
            catch (Exception ex)
            {
                ImportFailed?.Invoke($"导入图标失败:\n{ex.Message}");
            }
        }

        /// <summary>删除自定义图标：成功后按当前过滤重建列表；删除失败（含键不存在）不动列表。</summary>
        public void DeleteCustomIcon(string key)
        {
            if (_deleteCustomIcon(key))
            {
                ApplyFilter(SearchText);
            }
        }

        /// <summary>确认结果：携带当前选中键（可能为 null/空，语义见 <see cref="IconPickResult"/>）。</summary>
        public IconPickResult BuildResult() => new(SelectedIconKey);

        /// <summary>确认：经 CloseRequested 请求关窗；取消不经过此命令，由视图直接关窗。</summary>
        [RelayCommand]
        private void Confirm() => CloseRequested?.Invoke(BuildResult());
    }
}
