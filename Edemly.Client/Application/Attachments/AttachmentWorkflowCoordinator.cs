#nullable enable

using System.IO;

namespace Edemly.Client.Application.Attachments
{
    public sealed class AttachmentWorkflowCoordinator : IAttachmentWorkflowCoordinator
    {
        private readonly IAttachmentDescriptorFactory _descriptorFactory;
        private readonly IAttachmentPreviewDialogService _previewDialogService;
        private readonly IChatAttachmentSender _attachmentSender;

        public AttachmentWorkflowCoordinator(
            IAttachmentDescriptorFactory descriptorFactory,
            IAttachmentPreviewDialogService previewDialogService,
            IChatAttachmentSender attachmentSender)
        {
            _descriptorFactory = descriptorFactory ?? throw new ArgumentNullException(nameof(descriptorFactory));
            _previewDialogService = previewDialogService ?? throw new ArgumentNullException(nameof(previewDialogService));
            _attachmentSender = attachmentSender ?? throw new ArgumentNullException(nameof(attachmentSender));
        }

        public async Task<AttachmentWorkflowResult> ProcessSelectionAsync(int chatId, IEnumerable<string> filePaths, string? initialCaption = null)
        {
            var sentCount = 0;

            foreach (var filePath in Normalize(filePaths))
            {
                if (!TryCreateDescriptor(filePath, out var descriptor))
                {
                    continue;
                }

                var dialogResult = _previewDialogService.Show(descriptor, initialCaption);
                if (dialogResult.Action == AttachmentDialogAction.Cancel)
                {
                    return AttachmentWorkflowResult.CancelledByUser(sentCount);
                }

                if (dialogResult.Action == AttachmentDialogAction.Remove)
                {
                    continue;
                }

                var sendResult = await _attachmentSender.SendAsync(chatId, descriptor, dialogResult.Caption);
                if (!sendResult.Success)
                {
                    return AttachmentWorkflowResult.Failed(sendResult.ErrorMessage, sentCount);
                }

                sentCount++;
            }

            return AttachmentWorkflowResult.Completed(sentCount);
        }

        public async Task<AttachmentSendResult> SendFileAsync(int chatId, string filePath, string caption)
        {
            return !TryCreateDescriptor(filePath, out var descriptor)
                ? AttachmentSendResult.Fail(DefaultLanguage.AttachmentFileMissing)
                : await _attachmentSender.SendAsync(chatId, descriptor, caption);
        }

        private IEnumerable<string> Normalize(IEnumerable<string> filePaths)
        {
            return filePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private bool TryCreateDescriptor(string filePath, out AttachmentDescriptor descriptor)
        {
            descriptor = default!;

            try
            {
                if (!File.Exists(filePath))
                {
                    return false;
                }

                descriptor = _descriptorFactory.Create(filePath);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ATTACHMENTS] Failed to create descriptor for '{filePath}': {ex.Message}");
                return false;
            }
        }
    }
}
