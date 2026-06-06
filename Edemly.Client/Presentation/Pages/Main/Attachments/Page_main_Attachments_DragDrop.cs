#nullable enable

using Edemly.Client.Presentation.Pages.Main.Helpers;
using System.Windows;

namespace Edemly.Client.Presentation.Pages.Main
{
    public partial class Page_main
    {
        private void MessageTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            try
            {
                e.Effects = PageMainAttachmentInputHelper.HasFiles(e.Data) || PageMainAttachmentInputHelper.HasText(e.Data)
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ATTACHMENTS] MessageTextBox_PreviewDragOver error: {ex.Message}");
            }
        }

        private void MessageTextBox_PreviewDrop(object sender, DragEventArgs e)
        {
            try
            {
                var droppedFiles = PageMainAttachmentInputHelper.ExtractFiles(e.Data);
                if (droppedFiles.Count > 0)
                {
                    e.Handled = true;
                    _ = ProcessDroppedFilesAsync(droppedFiles);
                    return;
                }

                var droppedText = PageMainAttachmentInputHelper.ExtractText(e.Data);
                if (!string.IsNullOrEmpty(droppedText))
                {
                    InsertTextIntoMessageBox(droppedText);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ATTACHMENTS] MessageTextBox_PreviewDrop error: {ex.Message}");
            }
        }

        private Task ProcessDroppedFilesAsync(IEnumerable<string> files)
        {
            return RunAttachmentSelectionAsync(files);
        }
    }
}
