#nullable disable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using uchat.Lang;

namespace uchat.Helpers
{
    /// <summary>
    /// Створює красиві стилізовані контекстні меню та діалоги у стилі додатку
    /// </summary>
    public static class StyledContextMenu
    {
        /// <summary>
        /// Створює стилізоване контекстне меню
        /// </summary>
        public static ContextMenu Create()
        {
            var contextMenu = new ContextMenu
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F6FFFC")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#82C8C3")),
                BorderThickness = new Thickness(2),
                Padding = new Thickness(8),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
            };

            var style = new Style(typeof(ContextMenu));
            style.Setters.Add(new Setter(ContextMenu.TemplateProperty, CreateContextMenuTemplate()));
            contextMenu.Style = style;

            return contextMenu;
        }

        /// <summary>
        /// Додає стилізований елемент меню
        /// </summary>
        public static MenuItem AddItem(ContextMenu menu, string icon, string text, Action onClick, bool isDanger = false)
        {
            var item = new MenuItem
            {
                Header = CreateMenuItemContent(icon, text, isDanger),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 10, 20, 10),
                Cursor = System.Windows.Input.Cursors.Hand,
            };

            var style = new Style(typeof(MenuItem));
            style.Setters.Add(new Setter(MenuItem.TemplateProperty, CreateMenuItemTemplate(isDanger)));
            item.Style = style;

            item.Click += (s, e) => onClick?.Invoke();
            menu.Items.Add(item);

            return item;
        }

        /// <summary>
        /// Додає роздільник
        /// </summary>
        public static void AddSeparator(ContextMenu menu)
        {
            var separator = new Separator
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0F0EC")),
                Height = 1,
                Margin = new Thickness(10, 5, 10, 5)
            };
            menu.Items.Add(separator);
        }

        private static StackPanel CreateMenuItemContent(string icon, string text, bool isDanger)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 16,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var labelText = new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = isDanger 
                    ? new SolidColorBrush(Color.FromRgb(220, 53, 69))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#031C1C")),
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
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F6FFFC")));
            borderFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#82C8C3")));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(2));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(4));
            borderFactory.SetValue(Border.EffectProperty, new DropShadowEffect
            {
                BlurRadius = 15,
                ShadowDepth = 5,
                Opacity = 0.25,
                Color = (Color)ColorConverter.ConvertFromString("#004040")
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

            // Hover trigger
            var hoverTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, 
                isDanger 
                    ? new SolidColorBrush(Color.FromArgb(30, 220, 53, 69))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E6FFFD")), 
                "Border"));
            template.Triggers.Add(hoverTrigger);

            return template;
        }

        /// <summary>
        /// Створює красивий діалог підтвердження
        /// </summary>
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
                Owner = owner ?? Application.Current.MainWindow,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent
            };

            var mainBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F6FFFC")),
                CornerRadius = new CornerRadius(20),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#82C8C3")),
                BorderThickness = new Thickness(2),
                Margin = new Thickness(10),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 20,
                    ShadowDepth = 8,
                    Opacity = 0.35,
                    Color = (Color)ColorConverter.ConvertFromString("#004040")
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
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#031C1C")),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(titleBlock, 0);
            grid.Children.Add(titleBlock);

            var messageBlock = new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333")),
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
                    dialog.DragMove();
            };

            return dialog;
        }

        /// <summary>
        /// Створює стилізовану кнопку
        /// </summary>
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
                button.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69));
                button.Foreground = Brushes.White;
            }
            else if (isPrimary)
            {
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#057272"));
                button.Foreground = Brushes.White;
            }
            else
            {
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6c757d"));
                button.Foreground = Brushes.White;
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

            // Hover trigger
            var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            if (isDanger)
            {
                hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(200, 35, 51)), "Border"));
            }
            else if (isPrimary)
            {
                hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0B4539")), "Border"));
            }
            else
            {
                hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5a6268")), "Border"));
            }
            template.Triggers.Add(hoverTrigger);

            return template;
        }
    }
}
