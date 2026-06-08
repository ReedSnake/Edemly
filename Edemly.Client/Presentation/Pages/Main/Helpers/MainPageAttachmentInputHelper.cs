#nullable enable

using System.Windows;

namespace Edemly.Client.Presentation.Pages.Main.Helpers
{
    internal static class MainPageAttachmentInputHelper
    {
        public static IReadOnlyList<string> ExtractFiles(IDataObject? dataObject)
        {
            if (dataObject?.GetDataPresent(DataFormats.FileDrop) != true)
            {
                return Array.Empty<string>();
            }

            if (dataObject.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            {
                return Array.Empty<string>();
            }

            return files;
        }

        public static string? ExtractText(IDataObject? dataObject)
        {
            if (dataObject == null)
            {
                return null;
            }

            return dataObject.GetData(DataFormats.UnicodeText) as string
                ?? dataObject.GetData(DataFormats.Text) as string;
        }

        public static bool HasFiles(IDataObject? dataObject)
        {
            return ExtractFiles(dataObject).Count > 0;
        }

        public static bool HasText(IDataObject? dataObject)
        {
            return !string.IsNullOrWhiteSpace(ExtractText(dataObject));
        }
    }
}
