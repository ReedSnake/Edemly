#nullable enable

namespace Edemly.Client.Application.Attachments
{
    public sealed record AttachmentDescriptor(
        string FilePath,
        string FileName,
        string Extension,
        long SizeBytes,
        AttachmentKind Kind,
        int MessageType,
        string IconGlyph,
        bool CanPreviewImage);
}
