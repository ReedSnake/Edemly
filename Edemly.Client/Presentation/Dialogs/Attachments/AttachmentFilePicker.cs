#nullable enable

using Edemly.Client.Application.Attachments;
using Microsoft.Win32;

namespace Edemly.Client.Presentation.Dialogs.Attachments
{
    public sealed class AttachmentFilePicker : IAttachmentFilePicker
    {
        private const string ImageExtensions = "*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp";
        private const string DocumentExtensions = "*.pdf;*.doc;*.docx";

        public IReadOnlyList<string> PickFiles()
        {
            var dialog = new OpenFileDialog
            {
                Filter = BuildFilter(),
                Multiselect = true,
                Title = DefaultLanguage.SelectFile
            };

            return dialog.ShowDialog() == true
                ? dialog.FileNames
                : Array.Empty<string>();
        }

        private static string BuildFilter()
        {
            return
                $"{DefaultLanguage.AllFiles} (*.*)|*.*|" +
                $"{DefaultLanguage.Images} ({ImageExtensions})|{ImageExtensions}|" +
                $"{DefaultLanguage.Documents} ({DocumentExtensions})|{DocumentExtensions}";
        }
    }
}
