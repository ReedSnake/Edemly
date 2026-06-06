#nullable enable

using System.Windows;

namespace Edemly.Client.Pages.Main
{
    public partial class Page_main
    {
        private void MessageTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            try
            {
                e.Effects = Page_main_AttachmentInputHelper.HasFiles(e.Data) || Page_main_AttachmentInputHelper.HasText(e.Data)
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
                var droppedFiles = Page_main_AttachmentInputHelper.ExtractFiles(e.Data);
                if (droppedFiles.Count > 0)
                {
                    e.Handled = true;
                    _ = ProcessDroppedFilesAsync(droppedFiles);
                    return;
                }

                var droppedText = Page_main_AttachmentInputHelper.ExtractText(e.Data);
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
