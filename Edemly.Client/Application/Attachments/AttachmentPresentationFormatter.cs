#nullable enable

namespace Edemly.Client.Application.Attachments
{
    public static class AttachmentPresentationFormatter
    {
        public static string GetKindLabel(AttachmentKind kind)
        {
            return kind switch
            {
                AttachmentKind.Image => DefaultLanguage.Photo,
                AttachmentKind.Document => DefaultLanguage.Documents,
                _ => DefaultLanguage.File
            };
        }

        public static string FormatSize(long sizeBytes)
        {
            string[] suffixes = ["B", "KB", "MB", "GB"];
            double size = Math.Max(0, sizeBytes);
            var index = 0;

            while (size >= 1024 && index < suffixes.Length - 1)
            {
                size /= 1024;
                index++;
            }

            var precision = index == 0 ? 0 : 1;
            return $"{Math.Round(size, precision):0.#} {suffixes[index]}";
        }
    }
}
