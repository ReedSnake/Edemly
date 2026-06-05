#nullable disable

using Edemly.Client.Application.Localization;
using Edemly.Client.Presentation.Rendering.Common;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Edemly.Client.Presentation.Rendering.Messages
{
    public sealed class MessageActions
    {
        private readonly MessageUiUpdater _uiUpdater;

        public MessageActions(MessageUiUpdater uiUpdater)
        {
            _uiUpdater = uiUpdater;
        }

        public async Task OpenDownloadedContentAsync(string contentUrl, string fileName)
        {
            try
            {
                var filePath = await App.GlobalFileCache.GetOrDownloadAsync(contentUrl, fileName);
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
        }

        public async Task EditMessageAsync(MessageDto message, int currentUserId)
        {
            try
            {
                var inputDialog = new Window
                {
                    Title = DefaultLanguage.EditMessageTitle,
                    Width = 450,
                    Height = 250,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ResizeMode = ResizeMode.NoResize,
                    Owner = System.Windows.Application.Current.MainWindow
                };

                var grid = new Grid { Margin = new Thickness(15) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var label = new TextBlock
                {
                    Text = DefaultLanguage.EditMessageLabel,
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                Grid.SetRow(label, 0);
                grid.Children.Add(label);

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

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                Grid.SetRow(buttonPanel, 2);

                var cancelButton = new Button
                {
                    Content = DefaultLanguage.Cancel,
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                cancelButton.Click += (s, e) => inputDialog.DialogResult = false;

                var saveButton = new Button
                {
                    Content = DefaultLanguage.Save,
                    Width = 80,
                    Height = 30,
                    Background = new SolidColorBrush(Color.FromRgb(5, 114, 114)),
                    Foreground = Brushes.White
                };
                saveButton.Click += (s, e) => inputDialog.DialogResult = true;

                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(saveButton);
                grid.Children.Add(buttonPanel);

                inputDialog.Content = grid;

                if (inputDialog.ShowDialog() != true)
                {
                    return;
                }

                var newText = textBox.Text.Trim();

                if (string.IsNullOrEmpty(newText))
                {
                    MessageBox.ShowWarning(DefaultLanguage.MessageCannotBeEmpty, DefaultLanguage.Validation);
                    return;
                }

                if (newText == message.Text)
                {
                    return;
                }

                var updatedMessage = new UpdateMessageDto
                {
                    Id = message.Id,
                    ChatId = message.ChatId,
                    Text = newText
                };

                bool success = await App.HubService.UpdateMessageAsync(updatedMessage);

                if (!success)
                {
                    MessageBox.ShowError(DefaultLanguage.FailedUpdateMessage, DefaultLanguage.ErrorTitle);
                    return;
                }

                message.Text = newText;
                _uiUpdater.UpdateMessageInUI(message, currentUserId);

                try
                {
                    App.GlobalChatController?.UpdateMessageLocally(message);
                }
                catch
                {
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error editing message: {ex.Message}");
                MessageBox.ShowError($"{DefaultLanguage.Error}: {ex.Message}", DefaultLanguage.ErrorTitle);
            }
        }

        public async Task DeleteMessageAsync(MessageDto message)
        {
            try
            {
                var result = MessageBox.ShowQuestion(
                    DefaultLanguage.ConfirmDeleteMessage,
                    DefaultLanguage.ContactDeleteConfirmTitle);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                bool success = await App.HubService.DeleteMessageAsync(message.Id, message.ChatId);

                if (!success)
                {
                    MessageBox.ShowError(DefaultLanguage.FailedDeleteMessage, DefaultLanguage.ErrorTitle);
                    return;
                }

                _uiUpdater.RemoveMessageFromUI(message.Id);

                try
                {
                    App.GlobalChatController?.RemoveMessageLocally(message.ChatId, message.Id);
                }
                catch
                {
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting message: {ex.Message}");
                MessageBox.ShowError($"{DefaultLanguage.Error}: {ex.Message}", DefaultLanguage.ErrorTitle);
            }
        }
    }
}
