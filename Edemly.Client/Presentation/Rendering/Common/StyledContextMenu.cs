#nullable disable

using Edemly.Client.Application.Localization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Edemly.Client.Presentation.Rendering.Common
{
    public static class StyledContextMenu
    {
        public static ContextMenu Create()
        {
            var contextMenu = new ContextMenu
            {
                Padding = new Thickness(8),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14
            };

            contextMenu.Background = ResolveBrushResource("ThemeSurfaceBrush", Color.FromRgb(0xF6, 0xFF, 0xFC));
            contextMenu.BorderBrush = ResolveBrushResource("ThemeBorderBrush", Color.FromRgb(0xE0, 0xE0, 0xE0));
            contextMenu.Foreground = ResolveBrushResource("ThemeTextPrimaryBrush", Color.FromRgb(0x03, 0x1C, 0x1C));

            var style = new Style(typeof(ContextMenu));
            style.Setters.Add(new Setter(ContextMenu.TemplateProperty, CreateContextMenuTemplate()));
            contextMenu.Style = style;

            return contextMenu;
        }

        public static MenuItem AddItem(ContextMenu menu, string icon, string text, Action onClick, bool isDanger = false)
        {
            var item = new MenuItem
            {
                Header = CreateMenuItemContent(icon, NormalizeLabel(text), isDanger),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 10, 20, 10),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var style = new Style(typeof(MenuItem));
            style.Setters.Add(new Setter(MenuItem.TemplateProperty, CreateMenuItemTemplate(isDanger)));
            item.Style = style;

            item.Click += (s, e) => onClick?.Invoke();
            menu.Items.Add(item);

            return item;
        }

        public static void AddSeparator(ContextMenu menu)
        {
            var separator = new Separator
            {
                Background = ResolveBrushResource("ThemeBorderBrush", Color.FromRgb(0xE0, 0xE0, 0xE0)),
                Height = 1,
                Margin = new Thickness(10, 5, 10, 5),
                Opacity = 0.75
            };
            menu.Items.Add(separator);
        }

        public static string NormalizeLabel(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            for (var index = 0; index < text.Length; index++)
            {
                if (char.IsLetterOrDigit(text[index]))
                {
                    return text[index..].TrimStart();
                }
            }

            return text.Trim();
        }

        private static StackPanel CreateMenuItemContent(string icon, string text, bool isDanger)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var foreground = isDanger
                ? ResolveBrushResource("ThemeDangerBrush", Color.FromRgb(0xE5, 0x39, 0x35))
                : ResolveBrushResource("ThemeTextPrimaryBrush", Color.FromRgb(0x03, 0x1C, 0x1C));

            var iconText = new TextBlock
            {
                Text = icon,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 16,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = foreground
            };

            var labelText = new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = foreground,
                VerticalAlignment = VerticalAlignment.Center
            };

            panel.Children.Add(iconText);
            panel.Children.Add(labelText);

            return panel;
        }

        private static ControlTemplate CreateContextMenuTemplate()
        {
            var template = new ControlTemplate(typeof(ContextMenu));

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, ResolveBrushResource("ThemeSurfaceBrush", Color.FromRgb(0xF6, 0xFF, 0xFC)));
            borderFactory.SetValue(Border.BorderBrushProperty, ResolveBrushResource("ThemeBorderBrush", Color.FromRgb(0xE0, 0xE0, 0xE0)));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1.5));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(4));
            borderFactory.SetValue(Border.EffectProperty, new DropShadowEffect
            {
                BlurRadius = 12,
                ShadowDepth = 4,
                Opacity = 0.16,
                Color = Colors.Black
            });

            var stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.IsItemsHostProperty, true);
            stackPanelFactory.SetValue(StackPanel.MarginProperty, new Thickness(4));

            borderFactory.AppendChild(stackPanelFactory);
            template.VisualTree = borderFactory;

            return template;
        }

        private static ControlTemplate CreateMenuItemTemplate(bool isDanger)
        {
            var template = new ControlTemplate(typeof(MenuItem));

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "Border";
            borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(12, 8, 16, 8));

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentPresenter);
            template.VisualTree = borderFactory;

            var hoverTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(
                Border.BackgroundProperty,
                isDanger
                    ? ResolveBrushResource("ThemeSurfaceAltBrush", Color.FromRgb(0xFF, 0xED, 0xEC))
                    : ResolveBrushResource("ThemeSurfaceAltBrush", Color.FromRgb(0xEC, 0xF7, 0xF3)),
                "Border"));
            template.Triggers.Add(hoverTrigger);

            return template;
        }

        public static Window CreateConfirmDialog(
            string title,
            string message,
            string confirmText = null,
            string cancelText = null,
            bool isDanger = false,
            Window owner = null)
        {
            confirmText ??= DefaultLanguage.Confirm;
            cancelText ??= DefaultLanguage.Cancel;

            var dialog = new Window
            {
                Title = title,
                Width = 420,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner ?? System.Windows.Application.Current.MainWindow,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent
            };

            var mainBorder = new Border
            {
                Background = ResolveBrushResource("ThemeSurfaceBrush", Color.FromRgb(0xF6, 0xFF, 0xFC)),
                CornerRadius = new CornerRadius(20),
                BorderBrush = isDanger
                    ? ResolveBrushResource("ThemeDangerBrush", Color.FromRgb(0xE5, 0x39, 0x35))
                    : ResolveBrushResource("ThemeBorderBrush", Color.FromRgb(0xE0, 0xE0, 0xE0)),
                BorderThickness = new Thickness(2),
                Margin = new Thickness(10),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 20,
                    ShadowDepth = 8,
                    Opacity = 0.22,
                    Color = Colors.Black
                }
            };

            var grid = new Grid { Margin = new Thickness(25) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = ResolveBrushResource("ThemeTextPrimaryBrush", Color.FromRgb(0x03, 0x1C, 0x1C)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(titleBlock, 0);
            grid.Children.Add(titleBlock);

            var messageBlock = new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = ResolveBrushResource("ThemeTextSecondaryBrush", Color.FromRgb(0x66, 0x66, 0x66)),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10)
            };
            Grid.SetRow(messageBlock, 1);
            grid.Children.Add(messageBlock);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 15, 0, 0)
            };
            Grid.SetRow(buttonPanel, 2);

            var cancelButton = CreateStyledButton(cancelText, false, false);
            cancelButton.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };
            cancelButton.Margin = new Thickness(0, 0, 10, 0);
            buttonPanel.Children.Add(cancelButton);

            var confirmButton = CreateStyledButton(confirmText, true, isDanger);
            confirmButton.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };
            buttonPanel.Children.Add(confirmButton);

            grid.Children.Add(buttonPanel);
            mainBorder.Child = grid;
            dialog.Content = mainBorder;

            mainBorder.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                {
                    dialog.DragMove();
                }
            };

            return dialog;
        }

        public static Button CreateStyledButton(string text, bool isPrimary, bool isDanger = false)
        {
            var button = new Button
            {
                Content = text,
                Width = 120,
                Height = 40,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            if (isDanger)
            {
                button.Background = ResolveBrushResource("ThemeDangerBrush", Color.FromRgb(0xE5, 0x39, 0x35));
                button.Foreground = ResolveBrushResource("ThemeOnPrimaryTextBrush", Colors.White);
            }
            else if (isPrimary)
            {
                button.Background = ResolveBrushResource("ThemeSecondaryBrush", Color.FromRgb(0x0B, 0x45, 0x39));
                button.Foreground = ResolveBrushResource("ThemeOnSecondaryTextBrush", Colors.White);
            }
            else
            {
                button.Background = ResolveBrushResource("ThemeSurfaceAltBrush", Color.FromRgb(0xEC, 0xF7, 0xF3));
                button.Foreground = ResolveBrushResource("ThemeTextPrimaryBrush", Color.FromRgb(0x03, 0x1C, 0x1C));
            }

            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Button.TemplateProperty, CreateButtonTemplate(isPrimary, isDanger)));
            button.Style = style;

            return button;
        }

        private static ControlTemplate CreateButtonTemplate(bool isPrimary, bool isDanger)
        {
            var template = new ControlTemplate(typeof(Button));

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "Border";
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentPresenter);
            template.VisualTree = borderFactory;

            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(
                Border.BackgroundProperty,
                isDanger
                    ? ResolveBrushResource("ThemeDangerBrush", Color.FromRgb(0xE5, 0x39, 0x35))
                    : isPrimary
                        ? ResolveBrushResource("ThemePrimaryBrush", Color.FromRgb(0x05, 0x72, 0x72))
                        : ResolveBrushResource("ThemeSurfaceBrush", Color.FromRgb(0xF6, 0xFF, 0xFC)),
                "Border"));
            template.Triggers.Add(hoverTrigger);

            return template;
        }

        private static Brush ResolveBrushResource(string resourceKey, Color fallbackColor)
        {
            if (System.Windows.Application.Current?.Resources[resourceKey] is Brush brush)
            {
                return brush;
            }

            return new SolidColorBrush(fallbackColor);
        }
    }
}
