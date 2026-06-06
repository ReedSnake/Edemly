#nullable enable

namespace Edemly.Client.Application.Attachments
{
    public interface IAttachmentWorkflowCoordinator
    {
        Task<AttachmentWorkflowResult> ProcessSelectionAsync(int chatId, IEnumerable<string> filePaths, string? initialCaption = null);

        Task<AttachmentSendResult> SendFileAsync(int chatId, string filePath, string caption);
    }
}
