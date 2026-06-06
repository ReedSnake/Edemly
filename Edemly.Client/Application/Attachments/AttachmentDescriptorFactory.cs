#nullable enable

using System.IO;

namespace Edemly.Client.Application.Attachments
{
    public sealed class AttachmentDescriptorFactory : IAttachmentDescriptorFactory
    {
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"
        };

        private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx"
        };

        public AttachmentDescriptor Create(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            var fileInfo = new FileInfo(filePath);
            var extension = fileInfo.Extension.ToLowerInvariant();
            var kind = ResolveKind(extension);

            return new AttachmentDescriptor(
                fileInfo.FullName,
                fileInfo.Name,
                extension,
                fileInfo.Exists ? fileInfo.Length : 0,
                kind,
                ResolveMessageType(kind),
                AttachmentFileIconResolver.GetIconGlyph(fileInfo.Name),
                kind == AttachmentKind.Image);
        }

        private static AttachmentKind ResolveKind(string extension)
        {
            if (ImageExtensions.Contains(extension))
            {
                return AttachmentKind.Image;
            }

            if (DocumentExtensions.Contains(extension))
            {
                return AttachmentKind.Document;
            }

            return AttachmentKind.File;
        }

        private static int ResolveMessageType(AttachmentKind kind)
        {
            return kind switch
            {
                AttachmentKind.Image => 3,
                AttachmentKind.Document => 5,
                _ => 4
            };
        }
    }
}
