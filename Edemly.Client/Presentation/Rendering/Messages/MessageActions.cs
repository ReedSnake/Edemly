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
        private readonly IMessageEditDialogService _editDialogService;

        public MessageActions(MessageUiUpdater uiUpdater, IMessageEditDialogService editDialogService)
        {
            _uiUpdater = uiUpdater;
            _editDialogService = editDialogService;
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
                var newText = _editDialogService.Show(message.Text);
                if (newText == null)
                {
                    return;
                }

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
