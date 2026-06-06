#nullable enable

using System.Windows.Media.Imaging;

namespace Edemly.Client.Application.Attachments
{
    public interface IClipboardImageTempFileStore
    {
        string? SaveToTemporaryPng(BitmapSource source);

        void Delete(string? filePath);
    }
}
