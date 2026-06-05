#nullable disable

using Edemly.Client.Application.Localization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace Edemly.Client.Presentation.Rendering.Chats
{
    public class ChatListItemBuilder
    {
        private const string DEFAULT_AVATAR_PATH = "pack://application:,,,/Assets/Avatars/default-avatar.png";

        private static void SetThemeBrush(FrameworkElement element, DependencyProperty property, string resourceKey)
        {
            element?.SetResourceReference(property, resourceKey);
        }

        internal Button CreateChatButton(
            ChatListItemState itemState,
            Func<Models.Contact, int, Task> onClickCallback)
        {
            return CreateChatButton(
                itemState.Contact,
                itemState.ChatId,
                onClickCallback,
                itemState.LastMessageText,
                itemState.LastMessageSender,
                itemState.HasUnread,
                itemState.IsOnline,
                itemState.IsActive,
                itemState.LastMessageTime);
        }

        public Button CreateChatButton(
            Models.Contact contact,
            int chatId,
            Func<Models.Contact, int, Task> onClickCallback,
            string lastMessageText = null,
            string lastMessageSender = null,
            bool hasUnread = false,
            bool isOnline = false,
            bool isActive = false,
            DateTime? lastMessageTime = null)
        {
            Grid chatGrid = new Grid
            {
                Height = 70,
                Margin = new Thickness(0)
            };

            Border rootBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Padding = new Thickness(6),
                Margin = new Thickness(5, 2, 5, 2),
                BorderBrush = Brushes.Transparent,
                BorderThickness = isActive ? new Thickness(2) : new Thickness(0)
            };
            if (isActive)
            {
                SetThemeBrush(rootBorder, Border.BackgroundProperty, "ThemeBorderLightBrush");
                SetThemeBrush(rootBorder, Border.BorderBrushProperty, "ThemePrimaryBrush");
            }

            chatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            chatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            chatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid avatarContainer = new Grid { Width = 45, Height = 45 };
            Grid.SetColumn(avatarContainer, 0);

            Border avatarBorder = new Border
            {
                Width = 45,
                Height = 45,
                CornerRadius = new CornerRadius(22.5),
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            SetThemeBrush(avatarBorder, Border.BorderBrushProperty, "ThemeSecondaryBrush");

            ImageBrush avatarBrush = new ImageBrush
            {
                Stretch = System.Windows.Media.Stretch.UniformToFill
            };
            avatarBorder.Background = avatarBrush;

            try
            {
                var cache = App.GlobalProfilePictureCache;
                if (cache != null && !string.IsNullOrEmpty(contact.PhotoPath))
                {
                    if (cache.TryGetFromMemory(contact.PhotoPath, out var bmp) && bmp != null)
                    {
                        avatarBrush.ImageSource = bmp;
                    }
                    else
                    {
                        LoadAvatarAsync(avatarBrush, contact.PhotoPath);
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(contact.PhotoPath) || contact.PhotoPath == DEFAULT_AVATAR_PATH)
                    {
                        avatarBrush.ImageSource = new BitmapImage(new Uri(DEFAULT_AVATAR_PATH, UriKind.RelativeOrAbsolute));
                    }
                    else
                    {
                        LoadAvatarAsync(avatarBrush, contact.PhotoPath);
                    }
                }
            }
            catch
            {
                try { avatarBrush.ImageSource = new BitmapImage(new Uri(DEFAULT_AVATAR_PATH, UriKind.RelativeOrAbsolute)); } catch { }
            }

            avatarContainer.Children.Add(avatarBorder);

            if (isOnline)
            {
                Ellipse onlineDot = new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    StrokeThickness = 2,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -4, -4, 0)
                };
                SetThemeBrush(onlineDot, Shape.FillProperty, "ThemeOnlineBrush");
                SetThemeBrush(onlineDot, Shape.StrokeProperty, "ThemeSurfaceBrush");
                avatarContainer.Children.Add(onlineDot);
            }

            chatGrid.Children.Add(avatarContainer);

            StackPanel textPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 5, 0)
            };
            Grid.SetColumn(textPanel, 1);

            TextBlock nameTextBlock = new TextBlock
            {
                Text = contact.Name,
                FontSize = 14,
                FontWeight = isActive ? FontWeights.Bold : FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 3)
            };
            SetThemeBrush(nameTextBlock, TextBlock.ForegroundProperty, isActive ? "ThemeSecondaryBrush" : "ThemeTextPrimaryBrush");
            textPanel.Children.Add(nameTextBlock);

            if (!string.IsNullOrEmpty(lastMessageText))
            {
                int maxLength = 35;
                int newlineIndex = lastMessageText.IndexOfAny(new char[] { '\r', '\n' });
                int cutIndex = (newlineIndex >= 0 && newlineIndex < maxLength) ? newlineIndex : maxLength;
                string truncatedMessage = lastMessageText.Length > cutIndex
                    ? lastMessageText.Substring(0, cutIndex) + "..."
                    : lastMessageText;

                if (truncatedMessage == "Voice Message")
                    truncatedMessage = DefaultLanguage.VoiceMessage;
                else if (truncatedMessage == "Photo")
                    truncatedMessage = DefaultLanguage.Photo;
                else if (truncatedMessage == "File")
                    truncatedMessage = DefaultLanguage.File;

                string senderDisplay = lastMessageSender;
                if (lastMessageSender == "You")
                    senderDisplay = DefaultLanguage.You;

                string displayText = string.IsNullOrEmpty(senderDisplay)
                    ? truncatedMessage
                    : $"{senderDisplay}: {truncatedMessage}";

                TextBlock lastMessageBlock = new TextBlock
                {
                    Text = displayText,
                    FontSize = 11,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontStyle = FontStyles.Italic
                };
                SetThemeBrush(lastMessageBlock, TextBlock.ForegroundProperty, isActive ? "ThemeSecondaryBrush" : "ThemeTextSecondaryBrush");
                textPanel.Children.Add(lastMessageBlock);
            }

            chatGrid.Children.Add(textPanel);

            StackPanel rightPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(5, 0, 5, 0)
            };
            Grid.SetColumn(rightPanel, 2);

            if (lastMessageTime.HasValue)
            {
                TextBlock timeTextBlock = new TextBlock
                {
                    Text = FormatMessageTime(lastMessageTime.Value),
                    FontSize = 10,
                    FontWeight = hasUnread ? FontWeights.Bold : FontWeights.Normal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                SetThemeBrush(timeTextBlock, TextBlock.ForegroundProperty, hasUnread ? "ThemePrimaryBrush" : "ThemeTextSecondaryBrush");
                rightPanel.Children.Add(timeTextBlock);
            }

            if (hasUnread)
            {
                Ellipse unreadIndicator = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    StrokeThickness = 1
                };
                SetThemeBrush(unreadIndicator, Shape.FillProperty, "ThemePrimaryBrush");
                SetThemeBrush(unreadIndicator, Shape.StrokeProperty, "ThemeSurfaceBrush");
                rightPanel.Children.Add(unreadIndicator);
            }

            chatGrid.Children.Add(rightPanel);

            rootBorder.Child = chatGrid;

            Button chatButton = new Button
            {
                Content = rootBorder,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(0),
                Tag = chatId,
                Cursor = Cursors.Hand
            };

            Style chatButtonStyle = new Style(typeof(Button));
            chatButtonStyle.Setters.Add(new Setter(Button.TemplateProperty, CreateChatButtonTemplate()));
            chatButton.Style = chatButtonStyle;

            chatButton.Click += async (s, e) =>
            {
                await onClickCallback(contact, chatId);
            };

            return chatButton;
        }

        private string FormatMessageTime(DateTime messageTime)
        {
            var now = DateTime.Now;
            var localTime = messageTime.Kind == DateTimeKind.Utc ? messageTime.ToLocalTime() : messageTime;

            if (localTime.Date == now.Date)
            {
                return localTime.ToString("HH:mm");
            }

            if (localTime.Date == now.Date.AddDays(-1))
            {
                return DefaultLanguage.Yesterday;
            }

            if ((now - localTime).TotalDays < 7)
            {
                return localTime.ToString("ddd");
            }

            if (localTime.Year == now.Year)
            {
                return localTime.ToString("dd MMM");
            }

            return localTime.ToString("dd.MM.yy");
        }

        public Button CreateUserSearchResultButton(UserDto user, Func<UserDto, Task> onClickCallback)
        {
            Grid userGrid = new Grid
            {
                Height = 65,
                Margin = new Thickness(5, 3, 5, 3)
            };

            userGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
            userGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border avatarBorder = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            SetThemeBrush(avatarBorder, Border.BorderBrushProperty, "ThemeBorderLightBrush");
            Grid.SetColumn(avatarBorder, 0);

            ImageBrush avatarBrush = new ImageBrush
            {
                Stretch = System.Windows.Media.Stretch.UniformToFill
            };
            avatarBorder.Background = avatarBrush;

            LoadAvatarAsync(avatarBrush, user.PfpUrl);
            userGrid.Children.Add(avatarBorder);

            StackPanel textPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };
            Grid.SetColumn(textPanel, 1);

            TextBlock nameTextBlock = new TextBlock
            {
                Text = user.Username,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            };
            SetThemeBrush(nameTextBlock, TextBlock.ForegroundProperty, "ThemeTextPrimaryBrush");
            textPanel.Children.Add(nameTextBlock);

            if (!string.IsNullOrEmpty(user.Email))
            {
                TextBlock emailTextBlock = new TextBlock
                {
                    Text = user.Email,
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                SetThemeBrush(emailTextBlock, TextBlock.ForegroundProperty, "ThemeTextSecondaryBrush");
                textPanel.Children.Add(emailTextBlock);
            }

            userGrid.Children.Add(textPanel);

            Button userButton = new Button
            {
                Content = userGrid,
                Tag = user,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(8, 5, 8, 5),
                Cursor = Cursors.Hand
            };

            Style buttonStyle = CreateSearchResultButtonStyle();
            userButton.Style = buttonStyle;
            userButton.MouseEnter += (s, e) => SetThemeBrush(userButton, Control.BackgroundProperty, "ThemeBorderLightBrush");
            userButton.MouseLeave += (s, e) => userButton.Background = Brushes.Transparent;

            userButton.Click += async (s, e) =>
            {
                await onClickCallback(user);
            };

            return userButton;
        }

        public Border CreateDateSeparator(DateTime date)
        {
            Border dateSeparator = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };

            dateSeparator.SetResourceReference(Border.BackgroundProperty, "ThemeSecondaryBrush");

            TextBlock dateText = new TextBlock
            {
                Text = FormatDateSeparator(date),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
            SetThemeBrush(dateText, TextBlock.ForegroundProperty, "ThemeOnSecondaryTextBrush");

            dateSeparator.Child = dateText;
            return dateSeparator;
        }

        private async void LoadAvatarAsync(ImageBrush avatarBrush, string photoPath)
        {
            try
            {
                if (string.IsNullOrEmpty(photoPath) || photoPath == DEFAULT_AVATAR_PATH)
                {
                    avatarBrush.ImageSource = new BitmapImage(new Uri(DEFAULT_AVATAR_PATH, UriKind.RelativeOrAbsolute));
                    return;
                }

                var bitmap = await App.GlobalProfilePictureCache.GetOrDownloadAsync(photoPath);

                if (bitmap != null)
                {
                    avatarBrush.ImageSource = bitmap;
                }
                else
                {
                    avatarBrush.ImageSource = new BitmapImage(new Uri(DEFAULT_AVATAR_PATH, UriKind.RelativeOrAbsolute));
                }
            }
            catch
            {
                avatarBrush.ImageSource = new BitmapImage(new Uri(DEFAULT_AVATAR_PATH, UriKind.RelativeOrAbsolute));
            }
        }

        private ControlTemplate CreateChatButtonTemplate()
        {
            FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(0));

            FrameworkElementFactory contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));
            contentPresenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentPresenterFactory.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Button.PaddingProperty));

            borderFactory.AppendChild(contentPresenterFactory);

            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = borderFactory;

            return template;
        }

        private Style CreateSearchResultButtonStyle()
        {
            Style buttonStyle = new Style(typeof(Button));

            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "border";
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));

            FrameworkElementFactory contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenterFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            contentPresenterFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentPresenterFactory);
            template.VisualTree = borderFactory;

            buttonStyle.Setters.Add(new Setter(Button.TemplateProperty, template));
            return buttonStyle;
        }

        private string FormatDateSeparator(DateTime date)
        {
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);

            if (date.Date == today)
                return DefaultLanguage.Today;
            else if (date.Date == yesterday)
                return DefaultLanguage.Yesterday;
            else
                return date.ToString("MMMM dd, yyyy");
        }
    }
}
