#nullable enable

using Edemly.Client.Application.Attachments;
using System.IO;
using System.Windows.Media.Imaging;

namespace Edemly.Client.Infrastructure.Attachments
{
    public sealed class ClipboardImageTempFileStore : IClipboardImageTempFileStore
    {
        public string? SaveToTemporaryPng(BitmapSource source)
        {
            try
            {
                var tempFile = Path.Combine(Path.GetTempPath(), $"edemly_clip_{Guid.NewGuid():N}.png");
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(source));

                using var stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None);
                encoder.Save(stream);

                System.Diagnostics.Debug.WriteLine($"[ATTACHMENTS] Saved clipboard image to: {tempFile}");
                return tempFile;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ATTACHMENTS] Failed to save clipboard image: {ex.Message}");
                return null;
            }
        }

        public void Delete(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }

            try
            {
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ATTACHMENTS] Failed to delete temp file '{filePath}': {ex.Message}");
            }
        }
    }
}
