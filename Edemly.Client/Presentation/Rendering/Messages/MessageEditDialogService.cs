#nullable disable

using Edemly.Client.Application.Localization;
using Edemly.Client.Presentation.Rendering.Common;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public sealed class MessageEditDialogService : IMessageEditDialogService
    {
        public string Show(string currentText)
        {
            var inputDialog = new Window
            {
                Title = DefaultLanguage.EditMessageTitle,
                Width = 460,
                Height = 270,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Owner = System.Windows.Application.Current.MainWindow
            };

            var mainBorder = new Border
            {
                Background = ResolveBrush("ThemeSurfaceBrush", Color.FromRgb(0xF6, 0xFF, 0xFC)),
                BorderBrush = ResolveBrush("ThemeBorderBrush", Color.FromRgb(0xE0, 0xE0, 0xE0)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(18),
                Margin = new Thickness(10),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 18,
                    ShadowDepth = 6,
                    Opacity = 0.18,
                    Color = Colors.Black
                }
            };

            var grid = new Grid { Margin = new Thickness(18) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = DefaultLanguage.EditMessageLabel,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12),
                Foreground = ResolveBrush("ThemeTextPrimaryBrush", Color.FromRgb(0x03, 0x1C, 0x1C))
            };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var textBox = CreateEditorTextBox(currentText);
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };
            Grid.SetRow(buttonPanel, 2);

            var cancelButton = StyledContextMenu.CreateStyledButton(DefaultLanguage.Cancel, false);
            cancelButton.Width = 90;
            cancelButton.Height = 36;
            cancelButton.Margin = new Thickness(0, 0, 10, 0);
            cancelButton.Click += (s, e) => inputDialog.DialogResult = false;

            var saveButton = StyledContextMenu.CreateStyledButton(DefaultLanguage.Save, true);
            saveButton.Width = 90;
            saveButton.Height = 36;
            saveButton.Click += (s, e) => inputDialog.DialogResult = true;

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(saveButton);
            grid.Children.Add(buttonPanel);

            mainBorder.Child = grid;
            inputDialog.Content = mainBorder;

            mainBorder.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                {
                    inputDialog.DragMove();
                }
            };

            inputDialog.Loaded += (s, e) =>
            {
                textBox.Focus();
                textBox.SelectAll();
            };

            return inputDialog.ShowDialog() == true
                ? textBox.Text?.Trim()
                : null;
        }

        private static TextBox CreateEditorTextBox(string currentText)
        {
            var textBox = new TextBox
            {
                Text = currentText ?? string.Empty,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontSize = 13,
                Padding = new Thickness(10),
                MinHeight = 120
            };

            textBox.SetResourceReference(Control.BackgroundProperty, "ThemeInputBackgroundBrush");
            textBox.SetResourceReference(Control.ForegroundProperty, "ThemeTextPrimaryBrush");
            textBox.SetResourceReference(Control.BorderBrushProperty, "ThemeBorderBrush");

            return textBox;
        }

        private static Brush ResolveBrush(string resourceKey, Color fallbackColor)
        {
            if (System.Windows.Application.Current?.Resources[resourceKey] is Brush brush)
            {
                return brush;
            }

            return new SolidColorBrush(fallbackColor);
        }
    }
}
