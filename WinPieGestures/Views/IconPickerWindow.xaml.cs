using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace WinPieGestures
{
    using Brush = System.Windows.Media.Brush;
    using Color = System.Windows.Media.Color;
    using Cursors = System.Windows.Input.Cursors;
    using HorizontalAlignment = System.Windows.HorizontalAlignment;

    public partial class IconPickerWindow : Window
    {
        public string? SelectedIconKey { get; private set; }
        private Border? _selectedCard;

        public IconPickerWindow(IThemeService themeService, string? initialKey = null)
        {
            InitializeComponent();
            themeService.ApplyTheme(this, themeService.CurrentEffectiveTheme);
            SelectedIconKey = initialKey;
            PopulateIcons();
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            this.Title = $"{I18n.T("IconPickerTitle")} - StarPie";
            if (HeaderTitleText != null) HeaderTitleText.Text = I18n.T("IconPickerHeader");
            if (HeaderSubtitleText != null) HeaderSubtitleText.Text = I18n.T("IconPickerSubtitle");
            if (SearchTextBox != null) SearchTextBox.ToolTip = I18n.T("IconPickerSearchTooltip");
            if (ImportIconButton != null) ImportIconButton.Content = I18n.T("IconPickerImport");
            if (ClearIconButton != null) ClearIconButton.Content = I18n.T("IconPickerClear");
            if (SelectedIconPrefixText != null) SelectedIconPrefixText.Text = I18n.T("IconPickerSelected") + " ";
            if (ConfirmButton != null) ConfirmButton.Content = I18n.T("BtnConfirm");
            if (CancelButton != null) CancelButton.Content = I18n.T("BtnCancel");
            if (string.IsNullOrEmpty(SelectedIconKey) && SelectedIconNameLabel != null)
            {
                SelectedIconNameLabel.Text = I18n.T("IconPickerNone");
            }
        }

        private void PopulateIcons(string filter = "")
        {
            IconsWrapPanel.Children.Clear();
            _selectedCard = null;

            var cardBg = (Brush)FindResource("SubtleCardBrush");
            var cardBorder = (Brush)FindResource("InputBorderBrush");
            var textPrimary = (Brush)FindResource("TextPrimaryBrush");
            var textSecondary = (Brush)FindResource("TextSecondaryBrush");

            // 1. Render Custom User Icons (if any)
            var customIcons = IconHelper.GetCustomIcons();
            if (!string.IsNullOrEmpty(filter))
            {
                customIcons = customIcons.Where(i => 
                    i.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) || 
                    i.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            foreach (var custom in customIcons)
            {
                var card = new Border
                {
                    Background = cardBg,
                    BorderBrush = cardBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(4),
                    Padding = new Thickness(6),
                    Cursor = Cursors.Hand,
                    Tag = custom.Key
                };

                var grid = new Grid();
                var sp = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                FrameworkElement iconElem;
                if (custom.IsSvg)
                {
                    iconElem = new Path
                    {
                        Data = Geometry.Parse(custom.SvgData),
                        Fill = (Brush)FindResource("AccentPrimaryBrush"),
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
                    img.Source = IconHelper.GetCustomImageSource(custom.FilePath);
                    iconElem = img;
                }

                var tb = new TextBlock
                {
                    Text = custom.DisplayName,
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
                    Foreground = (Brush)FindResource("TextSecondaryBrush"),
                    Cursor = Cursors.Hand,
                    ToolTip = "删除此自定义图标"
                };
                delBtn.Click += (s, e) =>
                {
                    e.Handled = true;
                    if (IconHelper.DeleteCustomIcon(custom.Key))
                    {
                        PopulateIcons(SearchTextBox.Text.Trim());
                    }
                };
                grid.Children.Add(delBtn);

                card.Child = grid;

                if (string.Equals(SelectedIconKey, custom.Key, StringComparison.OrdinalIgnoreCase))
                {
                    SelectCustomCard(card, custom);
                }

                card.MouseLeftButtonDown += (s, e) =>
                {
                    SelectCustomCard(card, custom);
                    if (e.ClickCount == 2)
                    {
                        Confirm_Click(this, new RoutedEventArgs());
                    }
                };

                IconsWrapPanel.Children.Add(card);
            }

            // 2. Render Built-in Vector Icons
            var items = IconHelper.VectorIconList;
            if (!string.IsNullOrEmpty(filter))
            {
                items = items.Where(i => 
                    i.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) || 
                    i.Category.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    i.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            foreach (var item in items)
            {
                var card = new Border
                {
                    Background = cardBg,
                    BorderBrush = cardBorder,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(4),
                    Padding = new Thickness(6),
                    Cursor = Cursors.Hand,
                    Tag = item
                };

                var sp = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var path = new Path
                {
                    Data = Geometry.Parse(item.SvgData),
                    Fill = textPrimary,
                    Width = 24,
                    Height = 24,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                };

                var tb = new TextBlock
                {
                    Text = item.Key,
                    FontSize = 10,
                    Foreground = textSecondary,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 72,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };

                sp.Children.Add(path);
                sp.Children.Add(tb);
                card.Child = sp;

                // Selection check
                if (string.Equals(SelectedIconKey, item.Key, StringComparison.OrdinalIgnoreCase))
                {
                    SelectCard(card, item);
                }

                card.MouseLeftButtonDown += (s, e) =>
                {
                    SelectCard(card, item);
                    if (e.ClickCount == 2)
                    {
                        Confirm_Click(this, new RoutedEventArgs());
                    }
                };

                IconsWrapPanel.Children.Add(card);
            }
        }

        private void SelectCustomCard(Border card, IconHelper.CustomIconItem custom)
        {
            if (_selectedCard != null)
            {
                _selectedCard.Background = (Brush)FindResource("SubtleCardBrush");
                _selectedCard.BorderBrush = (Brush)FindResource("InputBorderBrush");
            }

            _selectedCard = card;
            SelectedIconKey = custom.Key;
            SelectedIconNameLabel.Text = custom.DisplayName + " (自定义)";

            card.Background = (Brush)FindResource("NavTabActiveBgBrush");
            card.BorderBrush = (Brush)FindResource("AccentPrimaryBrush");
        }

        private void ImportIcon_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "导入自定义图标 (SVG / PNG / ICO / JPG)",
                    Filter = "所有支持的图标 (*.svg;*.png;*.ico;*.jpg;*.jpeg;*.bmp;*.webp)|*.svg;*.png;*.ico;*.jpg;*.jpeg;*.bmp;*.webp|SVG 矢量图 (*.svg)|*.svg|图片文件 (*.png;*.ico;*.jpg;*.jpeg;*.bmp)|*.png;*.ico;*.jpg;*.jpeg;*.bmp|所有文件 (*.*)|*.*",
                    Multiselect = false
                };

                if (dialog.ShowDialog(this) == true && !string.IsNullOrEmpty(dialog.FileName))
                {
                    var imported = IconHelper.ImportCustomIcon(dialog.FileName);
                    if (imported != null)
                    {
                        SelectedIconKey = imported.Key;
                        PopulateIcons(SearchTextBox.Text.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"导入图标失败:\n{ex.Message}", "StarPie", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SelectCard(Border card, VectorIconItem item)
        {
            if (_selectedCard != null)
            {
                _selectedCard.Background = (Brush)FindResource("SubtleCardBrush");
                _selectedCard.BorderBrush = (Brush)FindResource("InputBorderBrush");
            }

            _selectedCard = card;
            SelectedIconKey = item.Key;
            SelectedIconNameLabel.Text = item.DisplayName;

            card.Background = (Brush)FindResource("NavTabActiveBgBrush");
            card.BorderBrush = (Brush)FindResource("AccentPrimaryBrush");
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PopulateIcons(SearchTextBox.Text.Trim());
        }

        private void ClearIcon_Click(object sender, RoutedEventArgs e)
        {
            SelectedIconKey = "";
            SelectedIconNameLabel.Text = "(无图标)";
            if (_selectedCard != null)
            {
                _selectedCard.Background = (Brush)FindResource("SubtleCardBrush");
                _selectedCard.BorderBrush = (Brush)FindResource("InputBorderBrush");
                _selectedCard = null;
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
