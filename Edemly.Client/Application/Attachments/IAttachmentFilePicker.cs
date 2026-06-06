#nullable enable

namespace Edemly.Client.Application.Attachments
{
    public interface IAttachmentFilePicker
    {
        IReadOnlyList<string> PickFiles();
    }
}
