#nullable enable

namespace Edemly.Client.Application.Attachments
{
    public interface IAttachmentDescriptorFactory
    {
        AttachmentDescriptor Create(string filePath);
    }
}
