#nullable disable
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Edemly.Client.DTOs;
using Edemly.Client.Lang;
using Edemly.Client.Services;

namespace Edemly.Client.Helpers
{
    public class MessageRenderer
    {
        private readonly StackPanel _messagesPanel;
        private readonly int _currentUserId;
        private bool _isGroupChat = false;
        private string _senderName = "";

        public MessageRenderer(StackPanel messagesPanel, int currentUserId)
        {
            _messagesPanel = messagesPanel;
            _currentUserId = currentUserId;
        }

        public void SetGroupChatMode(bool isGroupChat)
        {
            _isGroupChat = isGroupChat;
        }

        public void RenderMessage(MessageDto message, bool isHistorical = false, string senderName = null)
        {
            bool isMyMessage = message.SenderId == _currentUserId;
            _senderName = senderName ?? "";

            // 0 - Text, 1 - Voice, 2 - Video, 3 - Photo, 4 - File, 5 - Document
            if (message.Type == 0) // текст
            {
                if (isMyMessage)
                    AddMyMessage(message, isHistorical);
                else
                    AddFriendMessage(message, isHistorical);
            }
            else if (message.Type == 1) // голосове повідомлення
            {
                if (isMyMessage)
                    VoiceMessageHelper.AddMyVoiceMessage(message, _messagesPanel, _currentUserId, isHistorical);
                else
                    VoiceMessageHelper.AddFriendVoiceMessage(message, _messagesPanel, _currentUserId, isHistorical, _senderName, _isGroupChat);
            }
            else if (message.Type == 3) // фото
            {
                if (isMyMessage)
                    AddMyPhotoMessage(message, isHistorical);
                else
                    AddFriendPhotoMessage(message, isHistorical);
            }
            else if (message.Type == 4 || message.Type == 5) // файл або документ
            {
                if (isMyMessage)
                    AddMyFileMessage(message, isHistorical);
                else
                    AddFriendFileMessage(message, isHistorical);
            }
        }

        private void AddMyMessage(MessageDto message, bool isHistorical)
        {
            Border messageBorder = new Border();
            StackPanel messageContainer = new StackPanel();

            messageBorder.Tag = message.Id;

            // використовуємо RichTextHelper для відображення форматованого тексту
            var messageText = RichTextHelper.CreateRichTextBlock(message.Text, GetMyMessageTextBrush(), allowSelection: true);
            messageText.Margin = new Thickness(0, 0, 0, 5);

            string timeString = message.SentAt.ToLocalTime().ToString("HH:mm");
            if (isHistorical)
            {
                timeString = message.SentAt.ToLocalTime().ToString("dd.MM HH:mm");
            }

            TextBlock timeText = new TextBlock
            {
                Text = timeString,
                FontSize = 10,
                Foreground = GetMyMessageTextBrush(),
                Opacity = 0,
                VerticalAlignment = VerticalAlignment.Center
            };

            StackPanel bottomPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            bottomPanel.Children.Add(timeText);

            messageContainer.Children.Add(messageText);
            messageContainer.Children.Add(bottomPanel);
            messageContainer.Margin = new Thickness(12, 8, 12, 8);

            messageBorder.Background = new SolidColorBrush(GetMyMessageColor());
            messageBorder.CornerRadius = new CornerRadius(15, 15, 0, 15);
            messageBorder.Margin = new Thickness(150, 8, 15, 8);
            messageBorder.HorizontalAlignment = HorizontalAlignment.Right;
            messageBorder.MaxWidth = 500;
            messageBorder.Padding = new Thickness(0);
            messageBorder.Child = messageContainer;
            messageBorder.Opacity = isHistorical ? 0.8 : 1;

            messageBorder.MouseEnter += (s, e) => { timeText.Opacity = 0.7; };
            messageBorder.MouseLeave += (s, e) => { timeText.Opacity = 0; };

            // додаємо контекстне menu
            AddMessageContextMenu(messageBorder, message);

            _messagesPanel.Children.Add(messageBorder);

            if (!isHistorical)
            {
                DoubleAnimation fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.3)
                };
                messageBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }

        private void AddFriendMessage(MessageDto message, bool isHistorical)
        {
            Border messageBorder = new Border();
            StackPanel messageContainer = new StackPanel();

            // Якщо це груповий чат - додаємо ім'я відправника
            if (_isGroupChat && !string.IsNullOrEmpty(_senderName))
            {
                TextBlock senderNameText = new TextBlock
                {
                    Text = _senderName,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 220, 220)),
                    Margin = new Thickness(0, 0, 0, 3)
                };
                messageContainer.Children.Add(senderNameText);
            }

            // використовуємо RichTextHelper
            var messageText = RichTextHelper.CreateRichTextBlock(message.Text, GetFriendMessageTextBrush(), allowSelection: true);
            messageText.Margin = new Thickness(0, 0, 0, 5);

            string timeString = message.SentAt.ToLocalTime().ToString("HH:mm");
            if (isHistorical)
            {
                timeString = message.SentAt.ToLocalTime().ToString("dd.MM HH:mm");
            }

            TextBlock timeText = new TextBlock
            {
                Text = timeString,
                FontSize = 10,
                Foreground = Brushes.White,
                Opacity = 0,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            messageContainer.Children.Add(messageText);
            messageContainer.Children.Add(timeText);
            messageContainer.Margin = new Thickness(12, 8, 12, 8);

            messageBorder.Background = new SolidColorBrush(GetFriendMessageColor());
            messageBorder.CornerRadius = new CornerRadius(15, 15, 15, 0);
            messageBorder.Margin = new Thickness(15, 8, 150, 8);
            messageBorder.HorizontalAlignment = HorizontalAlignment.Left;
            messageBorder.MaxWidth = 500;
            messageBorder.Padding = new Thickness(0);
            messageBorder.Child = messageContainer;
            messageBorder.Opacity = isHistorical ? 0.8 : 1;

            // ВАЖЛИВО: встановлюємо Tag, щоб можна було знаходити елемент по Id для оновлення/видалення
            messageBorder.Tag = message.Id;

            messageBorder.MouseEnter += (s, e) => { timeText.Opacity = 0.7; };
            messageBorder.MouseLeave += (s, e) => { timeText.Opacity = 0; };

            // додаємо контекстне menu
            AddMessageContextMenu(messageBorder, message);

            _messagesPanel.Children.Add(messageBorder);

            if (!isHistorical)
            {
                DoubleAnimation fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.3)
                };
                messageBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }

        #region Photo Messages

        private void AddMyPhotoMessage(MessageDto message, bool isHistorical)
        {
            Border messageBorder = new Border
            {
                Tag = message.Id,
                Background = new SolidColorBrush(GetMyMessageColor()),
                CornerRadius = new CornerRadius(15, 15, 0, 15),
                Margin = new Thickness(150, 8, 15, 8),
                HorizontalAlignment = HorizontalAlignment.Right,
                MaxWidth = 400,
                Padding = new Thickness(8),
                Cursor = Cursors.Hand,
                Opacity = isHistorical ? 0.8 : 1
            };

            StackPanel stackPanel = new StackPanel();

            // зображення
            Image image = new Image
            {
                MaxWidth = 350,
                MaxHeight = 350,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 0, 5)
            };

            // асинхронне завантаження зображення
            LoadPhotoAsync(message.ContentUrl, image);

            // текст (може бути під фото)
            if (!string.IsNullOrWhiteSpace(message.Text))
            {
                TextBlock messageText = new TextBlock
                {
                    Text = message.Text,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14,
                    Foreground = GetMyMessageTextBrush(),
                    Margin = new Thickness(0, 5, 0, 5)
                };
                stackPanel.Children.Add(messageText);
            }

            // час
            TextBlock timeText = new TextBlock
            {
                Text = isHistorical
                    ? message.SentAt.ToLocalTime().ToString("dd.MM HH:mm")
                    : message.SentAt.ToLocalTime().ToString("HH:mm"),
                FontSize = 10,
                Foreground = GetMyMessageTextBrush(),
                Opacity = 0,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            stackPanel.Children.Insert(0, image);
            stackPanel.Children.Add(timeText);
            messageBorder.Child = stackPanel;

            messageBorder.MouseEnter += (s, e) => { timeText.Opacity = 0.7; };
            messageBorder.MouseLeave += (s, e) => { timeText.Opacity = 0; };

            // відкриття файлу при кліку
            messageBorder.MouseLeftButtonDown += async (s, e) =>
            {
                try
                {
                    var filePath = await App.GlobalFileCache.GetOrDownloadAsync(message.ContentUrl, message.FileName ?? "image.jpg");
                    if (filePath != null && File.Exists(filePath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot open image: {ex.Message}");
                }
            };

            // додаємо контекстне menu
            AddMessageContextMenu(messageBorder, message);

            _messagesPanel.Children.Add(messageBorder);

            if (!isHistorical)
            {
                DoubleAnimation fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.3)
                };
                messageBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }

        private void AddFriendPhotoMessage(MessageDto message, bool isHistorical)
        {
            Border messageBorder = new Border
            {
                Tag = message.Id,
                Background = new SolidColorBrush(GetFriendMessageColor()),
                CornerRadius = new CornerRadius(15, 15, 15, 0),
                Margin = new Thickness(15, 8, 150, 8),
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 400,
                Padding = new Thickness(8),
                Cursor = Cursors.Hand,
                Opacity = isHistorical ? 0.8 : 1
            };

            StackPanel stackPanel = new StackPanel();

            // якщо груповий - ім'я
            if (_isGroupChat && !string.IsNullOrEmpty(_senderName))
            {
                TextBlock senderNameText = new TextBlock
                {
                    Text = _senderName,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 220, 220)),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                stackPanel.Children.Add(senderNameText);
            }

            Image image = new Image
            {
                MaxWidth = 350,
                MaxHeight = 350,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 0, 5)
            };

            LoadPhotoAsync(message.ContentUrl, image);

            stackPanel.Children.Add(image);

            if (!string.IsNullOrWhiteSpace(message.Text))
            {
                TextBlock messageText = new TextBlock
                {
                    Text = message.Text,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                stackPanel.Children.Add(messageText);
            }

            TextBlock timeText = new TextBlock
            {
                Text = isHistorical
                    ? message.SentAt.ToLocalTime().ToString("dd.MM HH:mm")
                    : message.SentAt.ToLocalTime().ToString("HH:mm"),
                FontSize = 10,
                Foreground = Brushes.White,
                Opacity = 0,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            stackPanel.Children.Add(timeText);
            messageBorder.Child = stackPanel;

            messageBorder.MouseEnter += (s, e) => { timeText.Opacity = 0.7; };
            messageBorder.MouseLeave += (s, e) => { timeText.Opacity = 0; };

            messageBorder.MouseLeftButtonDown += async (s, e) =>
            {
                try
                {
                    var filePath = await App.GlobalFileCache.GetOrDownloadAsync(message.ContentUrl, message.FileName ?? "image.jpg");
                    if (filePath != null && File.Exists(filePath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot open image: {ex.Message}");
                }
            };

            // додаємо контекстне menu
            AddMessageContextMenu(messageBorder, message);

            _messagesPanel.Children.Add(messageBorder);

            if (!isHistorical)
            {
                DoubleAnimation fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.3)
                };
                messageBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }

        #endregion

        #region File Messages

        private void AddMyFileMessage(MessageDto message, bool isHistorical)
        {
            Border messageBorder = new Border
            {
                Tag = message.Id,
                Background = new SolidColorBrush(GetFileMessageColor()),
                CornerRadius = new CornerRadius(15, 15, 0, 15),
                Margin = new Thickness(150, 8, 15, 8),
                Padding = new Thickness(12, 10, 12, 10),
                HorizontalAlignment = HorizontalAlignment.Right,
                MaxWidth = 400,
                Cursor = Cursors.Hand,
                Opacity = isHistorical ? 0.8 : 1
            };

            StackPanel stackPanel = new StackPanel();

            // іконка + назва файлу
            StackPanel fileInfoPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 5)
            };

            TextBlock fileIcon = new TextBlock
            {
                Text = GetFileIcon(message.FileName),
                FontSize = 20,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock fileName = new TextBlock
            {
                Text = message.FileName ?? "File",
                FontSize = 14,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };

            fileInfoPanel.Children.Add(fileIcon);
            fileInfoPanel.Children.Add(fileName);

            // підказка
            TextBlock hintText = new TextBlock
            {
                Text = DefaultLanguage.ClickToOpen, // ✅ ЛОКАЛИЗОВАНО
                FontSize = 11,
                Foreground = Brushes.White,
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 5)
            };

            // час
            TextBlock timeText = new TextBlock
            {
                Text = isHistorical
                    ? message.SentAt.ToLocalTime().ToString("dd.MM HH:mm")
                    : message.SentAt.ToLocalTime().ToString("HH:mm"),
                FontSize = 10,
                Foreground = Brushes.White,
                Opacity = 0,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            stackPanel.Children.Add(fileInfoPanel);
            stackPanel.Children.Add(hintText);
            stackPanel.Children.Add(timeText);
            messageBorder.Child = stackPanel;

            messageBorder.MouseEnter += (s, e) =>
            {
                messageBorder.Background = new SolidColorBrush(Color.FromArgb(255, 8, 138, 138));
                timeText.Opacity = 0.7;
            };
            messageBorder.MouseLeave += (s, e) =>
            {
                messageBorder.Background = new SolidColorBrush(Color.FromArgb(255, 11, 69, 57));
                timeText.Opacity = 0;
            };

            messageBorder.MouseLeftButtonDown += async (s, e) =>
            {
                try
                {
                    var filePath = await App.GlobalFileCache.GetOrDownloadAsync(message.ContentUrl, message.FileName ?? "file");
                    if (filePath != null && File.Exists(filePath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot open file: {ex.Message}");
                }
            };

            // додаємо контекстне menu
            AddMessageContextMenu(messageBorder, message);

            _messagesPanel.Children.Add(messageBorder);

            if (!isHistorical)
            {
                DoubleAnimation fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.3)
                };
                messageBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }

        private void AddFriendFileMessage(MessageDto message, bool isHistorical)
        {
            Border messageBorder = new Border
            {
                Tag = message.Id,
                Background = new SolidColorBrush(GetFriendMessageColor()),
                CornerRadius = new CornerRadius(15, 15, 15, 0),
                Margin = new Thickness(15, 8, 150, 8),
                Padding = new Thickness(12, 10, 12, 10),
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 400,
                Cursor = Cursors.Hand,
                Opacity = isHistorical ? 0.8 : 1
            };

            StackPanel stackPanel = new StackPanel();

            // якщо груповий - ім'я
            if (_isGroupChat && !string.IsNullOrEmpty(_senderName))
            {
                TextBlock senderNameText = new TextBlock
                {
                    Text = _senderName,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 220, 220)),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                stackPanel.Children.Add(senderNameText);
            }

            StackPanel fileInfoPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 5)
            };

            TextBlock fileIcon = new TextBlock
            {
                Text = GetFileIcon(message.FileName),
                FontSize = 20,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock fileName = new TextBlock
            {
                Text = message.FileName ?? "File",
                FontSize = 14,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };

            fileInfoPanel.Children.Add(fileIcon);
            fileInfoPanel.Children.Add(fileName);

            TextBlock hintText = new TextBlock
            {
                Text = DefaultLanguage.ClickToOpen, // ✅ ЛОКАЛІЗОВАНО
                FontSize = 11,
                Foreground = Brushes.White,
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 5)
            };

            TextBlock timeText = new TextBlock
            {
                Text = isHistorical
                    ? message.SentAt.ToLocalTime().ToString("dd.MM HH:mm")
                    : message.SentAt.ToLocalTime().ToString("HH:mm"),
                FontSize = 10,
                Foreground = Brushes.White,
                Opacity = 0,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            stackPanel.Children.Add(fileInfoPanel);
            stackPanel.Children.Add(hintText);
            stackPanel.Children.Add(timeText);
            messageBorder.Child = stackPanel;

            messageBorder.MouseEnter += (s, e) =>
            {
                messageBorder.Background = new SolidColorBrush(Color.FromArgb(255, 7, 150, 150));
                timeText.Opacity = 0.7;
            };
            messageBorder.MouseLeave += (s, e) =>
            {
                messageBorder.Background = new SolidColorBrush(Color.FromArgb(255, 5, 114, 114));
                timeText.Opacity = 0;
            };

            messageBorder.MouseLeftButtonDown += async (s, e) =>
            {
                try
                {
                    var filePath = await App.GlobalFileCache.GetOrDownloadAsync(message.ContentUrl, message.FileName ?? "file");
                    if (filePath != null && File.Exists(filePath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot open file: {ex.Message}");
                }
            };

            // додаємо контекстне menu
            AddMessageContextMenu(messageBorder, message);

            _messagesPanel.Children.Add(messageBorder);

            if (!isHistorical)
            {
                DoubleAnimation fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(0.3)
                };
                messageBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }

        #endregion

        #region Helper Methods

        private async void LoadPhotoAsync(string url, Image imageControl)
        {
            try
            {
                if (string.IsNullOrEmpty(url)) return;

                var bitmap = await App.GlobalProfilePictureCache.GetOrDownloadAsync(url);
                if (bitmap != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        imageControl.Source = bitmap;
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load photo: {ex.Message}");
            }
        }

        private string GetFileIcon(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "📁";

            var extension = Path.GetExtension(fileName).ToLower();

            return extension switch
            {
                ".pdf" => "📄",      // document / PDF
                ".doc" or ".docx" => "📝", // word document
                ".xls" or ".xlsx" => "📊", // spreadsheet
                ".ppt" or ".pptx" => "📈", // presentation
                ".txt" => "📄",      // text file
                ".zip" or ".rar" or ".7z" => "🗜️", // archive
                ".mp3" or ".wav" or ".flac" => "🎵", // audio
                ".mp4" or ".avi" or ".mkv" => "🎬", // video
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "🖼️", // image
                _ => "📁"
            };
        }

        /// <summary>
        /// додає контекстне меню (Copy, Edit, Delete)
        /// </summary>
        private void AddMessageContextMenu(Border messageBorder, MessageDto message)
        {
            var contextMenu = new ContextMenu();

            // копіювання: тільки для текстових повідомлень
            if (message.Type == 0 && !string.IsNullOrEmpty(message.Text))
            {
                var copyItem = new MenuItem
                {
                    Header = DefaultLanguage.CopyMessage, // ✅ ЛОКАЛІЗОВАНО
                    FontSize = 13
                };
                copyItem.Click += (s, e) =>
                {
                    Clipboard.SetText(message.Text);
                };
                contextMenu.Items.Add(copyItem);
            }

            // якщо повідомлення моє - додаємо Edit/Delete
            if (message.SenderId == _currentUserId)
            {
                // якщо є попередній пункт - додаємо роздільник
                if (contextMenu.Items.Count > 0)
                {
                    contextMenu.Items.Add(new Separator());
                }

                // редагувати (тільки для тексту)
                if (message.Type == 0)
                {
                    var editItem = new MenuItem
                    {
                        Header = DefaultLanguage.EditMessage, // ✅ ЛОКАЛІЗОВАНО
                        FontSize = 13
                    };
                    editItem.Click += async (s, e) =>
                    {
                        await EditMessageAsync(message);
                    };
                    contextMenu.Items.Add(editItem);
                }

                // видалити
                var deleteItem = new MenuItem
                {
                    Header = DefaultLanguage.DeleteMessage, // ✅ ЛОКАЛІЗОВАНО
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69))
                };
                deleteItem.Click += async (s, e) =>
                {
                    await DeleteMessageAsync(message);
                };
                contextMenu.Items.Add(deleteItem);
            }

            // якщо контекстне меню не пусте - прив'язуємо до Border
            if (contextMenu.Items.Count > 0)
            {
                messageBorder.ContextMenu = contextMenu;
            }
        }

        /// <summary>
        /// редактор повідомлення
        /// </summary>
        private async Task EditMessageAsync(MessageDto message)
        {
            try
            {
                var inputDialog = new Window
                {
                    Title = DefaultLanguage.EditMessageTitle, // ✅ ЛОКАЛІЗОВАНО
                    Width = 450,
                    Height = 250,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ResizeMode = ResizeMode.NoResize,
                    Owner = Application.Current.MainWindow
                };

                var grid = new Grid { Margin = new Thickness(15) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // Label
                var label = new TextBlock
                {
                    Text = DefaultLanguage.EditMessageLabel, // ✅ ЛОКАЛІЗОВАНО
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                Grid.SetRow(label, 0);
                grid.Children.Add(label);

                // TextBox
                var textBox = new TextBox
                {
                    Text = message.Text,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    FontSize = 13,
                    Padding = new Thickness(8)
                };
                Grid.SetRow(textBox, 1);
                grid.Children.Add(textBox);

                // Buttons
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                Grid.SetRow(buttonPanel, 2);

                var cancelButton = new Button
                {
                    Content = DefaultLanguage.Cancel, // ✅ ЛОКАЛІЗОВАНО
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                cancelButton.Click += (s, e) => inputDialog.DialogResult = false;

                var saveButton = new Button
                {
                    Content = DefaultLanguage.Save, // ✅ ЛОКАЛІЗОВАНО
                    Width = 80,
                    Height = 30,
                    Background = new SolidColorBrush(Color.FromRgb(5, 114, 114)),
                    Foreground = Brushes.White
                };
                saveButton.Click += async (s, e) => inputDialog.DialogResult = true;

                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(saveButton);
                grid.Children.Add(buttonPanel);

                inputDialog.Content = grid;

                if (inputDialog.ShowDialog() == true)
                {
                    var newText = textBox.Text.Trim();

                    if (string.IsNullOrEmpty(newText))
                    {
                        Edemly.Client.Pages.MessageBox.ShowWarning(DefaultLanguage.MessageCannotBeEmpty, DefaultLanguage.Validation); // ✅ ЛОКАЛІЗОВАНО
                        return;
                    }

                    if (newText == message.Text)
                    {
                        return; // нічого не змінилося
                    }

                    var updatedMessage = new UpdateMessageDto
                    {
                        Id = message.Id,
                        ChatId = message.ChatId,
                        Text = newText
                    };

                    bool success = await App.HubService.UpdateMessageAsync(updatedMessage);

                    if (success)
                    {
                        message.Text = newText; // Оновлюємо локально

                        // Оновлюємо UI
                        UpdateMessageInUI(message);

                        // Оновлюємо локальну історію у ChatManager щоб повідомлення не з'явилось знову
                        try
                        {
                            App.GlobalChatManager?.UpdateMessageLocally(message);
                        }
                        catch { }
                    }
                    else
                    {
                        Edemly.Client.Pages.MessageBox.ShowError(DefaultLanguage.FailedUpdateMessage, DefaultLanguage.ErrorTitle); // ✅ ЛОКАЛІЗОВАНО
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error editing message: {ex.Message}");
                Edemly.Client.Pages.MessageBox.ShowError($"{DefaultLanguage.Error}: {ex.Message}", DefaultLanguage.ErrorTitle); // ✅ ЛОКАЛІЗОВАНО
            }
        }

        /// <summary>
        /// видалення повідомлення
        /// </summary>
        private async Task DeleteMessageAsync(MessageDto message)
        {
            try
            {
                var result = Edemly.Client.Pages.MessageBox.ShowQuestion(
                    DefaultLanguage.ConfirmDeleteMessage, // ✅ ЛОКАЛІЗОВАНО
                    DefaultLanguage.ContactDeleteConfirmTitle); // ✅ ЛОКАЛІЗОВАНО

                if (result == MessageBoxResult.Yes)
                {
                    bool success = await App.HubService.DeleteMessageAsync(message.Id, message.ChatId);

                    if (success)
                    {
                        RemoveMessageFromUI(message.Id); // Видаляємо з UI

                        // Повідомляємо ChatManager щоб видалити з локальної історії
                        try
                        {
                            App.GlobalChatManager?.RemoveMessageLocally(message.ChatId, message.Id);
                        }
                        catch { }
                    }
                    else
                    {
                        Edemly.Client.Pages.MessageBox.ShowError(DefaultLanguage.FailedDeleteMessage, DefaultLanguage.ErrorTitle); // ✅ ЛОКАЛІЗОВАНО
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting message: {ex.Message}");
                Edemly.Client.Pages.MessageBox.ShowError($"{DefaultLanguage.Error}: {ex.Message}", DefaultLanguage.ErrorTitle); // ✅ ЛОКАЛІЗОВАНО
            }
        }

        #endregion

        #region Update/Delete UI Methods

        /// <summary>
        /// Оновлює повідомлення в UI.
        /// </summary>
        public void UpdateMessageInUI(MessageDto updatedMessage)
        {
            // Безпечне знаходження Border по Tag
            var messageBorder = _messagesPanel.Children
                .OfType<Border>()
                .FirstOrDefault(b =>
                {
                    if (b.Tag == null) return false;
                    if (b.Tag is int i) return i == updatedMessage.Id;
                    if (int.TryParse(b.Tag.ToString(), out var parsed)) return parsed == updatedMessage.Id;
                    return false;
                });

            if (messageBorder != null)
            {
                // Оновлюємо текст повідомлення
                var messageContainer = messageBorder.Child as Panel;
                if (messageContainer != null)
                {
                    // шукаємо TextBlock, створений RichTextHelper (має Cursor = IBeam, TextWrapping або FontSize 14)
                    TextBlock messageText = null;

                    foreach (var tb in messageContainer.Children.OfType<TextBlock>())
                    {
                        if (tb.Cursor == Cursors.IBeam || tb.TextWrapping == TextWrapping.Wrap || tb.FontSize == 14)
                        {
                            messageText = tb;
                            break;
                        }
                    }

                    if (messageText != null)
                    {
                        var isMy = updatedMessage.SenderId == _currentUserId;
                        var newBlock = RichTextHelper.CreateRichTextBlock(updatedMessage.Text, isMy ? Brushes.Black : Brushes.White, allowSelection: true);
                        newBlock.Margin = messageText.Margin;

                        var parent = messageText.Parent as Panel;
                        if (parent != null)
                        {
                            int idx = parent.Children.IndexOf(messageText);
                            if (idx >= 0)
                            {
                                parent.Children.RemoveAt(idx);
                                parent.Children.Insert(idx, newBlock);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Видаляє повідомлення з UI.
        /// </summary>
        public void RemoveMessageFromUI(int messageId)
        {
            var messageBorder = _messagesPanel.Children
                .OfType<Border>()
                .FirstOrDefault(b =>
                {
                    if (b.Tag == null) return false;
                    if (b.Tag is int i) return i == messageId;
                    if (int.TryParse(b.Tag.ToString(), out var parsed)) return parsed == messageId;
                    return false;
                });

            if (messageBorder != null)
            {
                _messagesPanel.Children.Remove(messageBorder);
            }
        }

        #endregion // Закриваємо регіон Update/Delete UI Methods

        public void UpdateMessagesPanel(StackPanel messagesPanel)
        {
            typeof(MessageRenderer).GetField("_messagesPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(this, messagesPanel);
        }

        /// <summary>
        /// Отримати колір для моїх повідомлень (світлий варіант теми)
        /// </summary>
        private Color GetMyMessageColor()
        {
            var palette = ThemeService.Instance.GetCurrentPalette();
            // Використовуємо BorderLight як базовий колір для моїх повідомлень
            return palette.BorderLight;
        }

        /// <summary>
        /// Отримати колір для повідомлень друга (основний колір теми)
        /// </summary>
        private Color GetFriendMessageColor()
        {
            var palette = ThemeService.Instance.GetCurrentPalette();
            return palette.Primary;
        }

        /// <summary>
        /// Отримати колір для файлових повідомлень (темний варіант)
        /// </summary>
        private Color GetFileMessageColor()
        {
            var palette = ThemeService.Instance.GetCurrentPalette();
            return palette.Secondary;
        }

        /// <summary>
        /// Отримати колір тексту для моїх повідомлень
        /// </summary>
        private Brush GetMyMessageTextBrush()
        {
            var palette = ThemeService.Instance.GetCurrentPalette();
            return new SolidColorBrush(palette.TextPrimary);
        }

        /// <summary>
        /// Отримати колір тексту для повідомлень друга
        /// </summary>
        private Brush GetFriendMessageTextBrush()
        {
            return Brushes.White;
        }
    }
}
