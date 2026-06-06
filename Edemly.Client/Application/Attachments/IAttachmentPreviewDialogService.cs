#nullable enable

namespace Edemly.Client.Application.Attachments
{
    public interface IAttachmentPreviewDialogService
    {
        AttachmentDialogResult Show(AttachmentDescriptor descriptor, string? initialCaption = null);
    }
}
