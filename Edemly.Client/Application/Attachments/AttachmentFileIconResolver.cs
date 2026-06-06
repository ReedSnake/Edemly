#nullable enable

using System.IO;

namespace Edemly.Client.Application.Attachments
{
    public static class AttachmentFileIconResolver
    {
        public static string GetIconGlyph(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "\U0001F4C1";
            }

            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            return extension switch
            {
                ".pdf" => "\U0001F4C4",
                ".doc" or ".docx" => "\U0001F4DD",
                ".xls" or ".xlsx" => "\U0001F4CA",
                ".ppt" or ".pptx" => "\U0001F4C8",
                ".txt" => "\U0001F4C4",
                ".zip" or ".rar" or ".7z" => "\U0001F5DC\uFE0F",
                ".mp3" or ".wav" or ".flac" => "\U0001F3B5",
                ".mp4" or ".avi" or ".mkv" => "\U0001F3AC",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "\U0001F5BC\uFE0F",
                _ => "\U0001F4C1"
            };
        }
    }
}
