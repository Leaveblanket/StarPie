using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WinPieGestures.Views.Dialogs
{
    using Brush = System.Windows.Media.Brush;
    using Color = System.Windows.Media.Color;
    using Cursors = System.Windows.Input.Cursors;
    using HorizontalAlignment = System.Windows.HorizontalAlignment;

    /// <summary>
    /// 图标选择器窗口 (T08)：选中状态、搜索过滤、导入/删除编排与确认结果全部在
    /// <see cref="IconPickerViewModel"/>；code-behind 只剩卡片渲染（主题画刷、SVG/位图元素）、
    /// 把 VM 的关闭请求落成 DialogResult。由 <see cref="DialogService"/> 创建，
    /// Owner 归设置窗口。
    /// </summary>
    public partial class IconPickerWindow : Window
    {
        private readonly IconPickerViewModel _vm;
        private Border? _selectedCard;

        public IconPickerWindow(IThemeService themeService, IconPickerViewModel viewModel, ILocalizationService localization)
        {
            InitializeComponent();
            themeService.ApplyTheme(this, themeService.CurrentEffectiveTheme);
            _vm = viewModel;
            DataContext = _vm;
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            _vm.DisplayedIcons.CollectionChanged += DisplayedIcons_Changed;
            Title = $"{localization.GetString("IconPickerTitle")} - StarPie"; // ADR-0010 例外:窗口标题品牌后缀拼接(XAML 表达不了),对话框每次 Show* 新建即时取词
        }

        /// <summary>确认结果（仅在 DialogResult == true 时非空）。</summary>
        public IconPickResult BuildResult() => _vm.BuildResult();

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IconPickerViewModel.IsCompleted))
            {
                DialogResult = true;
                Close();
            }
        }

        private void DisplayedIcons_Changed(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    IconsWrapPanel.Children.Clear();
                    _selectedCard = null;
                    break;
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                    {
                        foreach (IconEntry entry in e.NewItems)
                        {
                            IconsWrapPanel.Children.Add(BuildIconCard(entry));
                        }
                    }
                    break;
            }
        }

        /// <summary>构建单张图标卡片：SVG/位图元素与选中高亮留视图，选中状态归 VM。</summary>
        private Border BuildIconCard(IconEntry entry)
        {
            var cardBg = (Brush)FindResource("SubtleCardBrush");
            var cardBorder = (Brush)FindResource("InputBorderBrush");
            var textPrimary = (Brush)FindResource("TextPrimaryBrush");
            var textSecondary = (Brush)FindResource("TextSecondaryBrush");

            var card = new Border
            {
                Background = cardBg,
                BorderBrush = cardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(4),
                Padding = new Thickness(6),
                Cursor = Cursors.Hand,
                Tag = entry.Key
            };

            var grid = new Grid();
            var sp = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            FrameworkElement iconElem;
            if (entry.IsSvg)
            {
                iconElem = new Path
                {
                    Data = Geometry.Parse(entry.SvgData),
                    Fill = entry.IsCustom ? (Brush)FindResource("AccentPrimaryBrush") : textPrimary,
                    Width = 24,
                    Height = 24,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                };
            }
            else
            {
                var img = new System.Windows.Controls.Image
                {
                    Width = 24,
                    Height = 24,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                img.Source = IconHelper.GetCustomImageSource(entry.FilePath);
                iconElem = img;
            }

            var tb = new TextBlock
            {
                Text = entry.IsCustom ? entry.DisplayName : entry.Key,
                FontSize = 10,
                Foreground = textSecondary,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 72,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            sp.Children.Add(iconElem);
            sp.Children.Add(tb);
            grid.Children.Add(sp);

            // Delete small button for custom icons
            if (entry.IsCustom)
            {
                var delBtn = new System.Windows.Controls.Button
                {
                    Content = "✕",
                    FontSize = 9,
                    Width = 16,
                    Height = 16,
                    Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = textSecondary,
                    Cursor = Cursors.Hand,
                    ToolTip = "删除此自定义图标",
                    Command = _vm.DeleteCustomIconActionCommand,
                    CommandParameter = entry.Key
                };
                delBtn.Click += (s, e) =>
                {
                    e.Handled = true;
                };
                grid.Children.Add(delBtn);
            }

            card.Child = grid;

            if (string.Equals(_vm.SelectedIconKey, entry.Key, StringComparison.OrdinalIgnoreCase))
            {
                ApplyCardSelection(card);
            }

            // ADR-0009：事件只做纯 UI 视觉高亮；选择/双击确认均走 MouseBinding → VM 命令，
            // 完成由 IsCompleted 观察器落 DialogResult=true（View 不在事件里当业务入口）。
            card.MouseLeftButtonDown += (s, e) => ApplyCardSelection(card);
            card.InputBindings.Add(new MouseBinding
            {
                MouseAction = MouseAction.LeftClick,
                Command = _vm.SelectIconCommand,
                CommandParameter = entry
            });
            card.InputBindings.Add(new MouseBinding
            {
                MouseAction = MouseAction.LeftDoubleClick,
                Command = _vm.ConfirmCommand
            });

            return card;
        }

        /// <summary>高亮选中卡片并把原选中卡片恢复常态（纯视觉状态）。</summary>
        private void ApplyCardSelection(Border card)
        {
            if (_selectedCard != null)
            {
                _selectedCard.Background = (Brush)FindResource("SubtleCardBrush");
                _selectedCard.BorderBrush = (Brush)FindResource("InputBorderBrush");
            }

            _selectedCard = card;
            card.Background = (Brush)FindResource("NavTabActiveBgBrush");
            card.BorderBrush = (Brush)FindResource("AccentPrimaryBrush");
        }

        // ADR-0009：取消无业务语义，Click→DialogResult=false 属 code-behind 白名单。
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

    }
}
