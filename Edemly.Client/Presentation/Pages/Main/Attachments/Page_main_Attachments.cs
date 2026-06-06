#nullable enable

using System.Windows;

namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class Page_main
    {
        private async void AttachFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureAttachmentChatSelected())
            {
                return;
            }

            var filePaths = _attachmentFilePicker.PickFiles();
            if (filePaths.Count == 0)
            {
                return;
            }

            await RunAttachmentSelectionAsync(filePaths);
        }

        private bool EnsureAttachmentChatSelected()
        {
            if (_chatController?.CurrentChatId >= 0)
            {
                return true;
            }

            MessageBox.ShowWarning(DefaultLanguage.SelectChat, DefaultLanguage.ErrorTitle);
            return false;
        }

        private async Task RunAttachmentSelectionAsync(IEnumerable<string> filePaths, string? initialCaption = null)
        {
            if (!EnsureAttachmentChatSelected())
            {
                return;
            }

            try
            {
                SetAttachmentSendingState(isSending: true);

                var workflowResult = await _attachmentWorkflowCoordinator.ProcessSelectionAsync(
                    _chatController?.CurrentChatId ?? -1,
                    filePaths,
                    initialCaption);

                if (!workflowResult.Success)
                {
                    HandleAttachmentFailure(workflowResult.ErrorMessage);
                }
            }
            finally
            {
                SetAttachmentSendingState(isSending: false);
            }
        }

        private async Task SendFileAsync(string filePath, string caption = "")
        {
            if (!EnsureAttachmentChatSelected())
            {
                return;
            }

            try
            {
                SetAttachmentSendingState(isSending: true);

                var sendResult = await _attachmentWorkflowCoordinator.SendFileAsync(
                    _chatController?.CurrentChatId ?? -1,
                    filePath,
                    caption);

                if (!sendResult.Success)
                {
                    HandleAttachmentFailure(sendResult.ErrorMessage);
                }
            }
            finally
            {
                SetAttachmentSendingState(isSending: false);
            }
        }

        private void HandleAttachmentFailure(string? errorMessage)
        {
            MessageBox.ShowError(errorMessage ?? DefaultLanguage.FailedSendMessage, DefaultLanguage.ErrorTitle);
        }

        private void SetAttachmentSendingState(bool isSending)
        {
            if (AttachFileButton != null)
            {
                AttachFileButton.IsEnabled = !isSending;
            }

            if (StickerButton != null)
            {
                StickerButton.IsEnabled = !isSending;
            }
        }
    }
}
