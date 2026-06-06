#nullable enable

namespace Edemly.Client.Application.Attachments
{
    public interface IChatAttachmentSender
    {
        Task<AttachmentSendResult> SendAsync(int chatId, AttachmentDescriptor descriptor, string caption);
    }
}
