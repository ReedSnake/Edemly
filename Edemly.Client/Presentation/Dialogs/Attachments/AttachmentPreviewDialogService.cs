#nullable enable

using Edemly.Client.Application.Attachments;

namespace Edemly.Client.Presentation.Dialogs.Attachments
{
    public sealed class AttachmentPreviewDialogService : IAttachmentPreviewDialogService
    {
        public AttachmentDialogResult Show(AttachmentDescriptor descriptor, string? initialCaption = null)
        {
            var dialog = new AttachmentPreviewDialog(descriptor, initialCaption)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            dialog.ShowDialog();
            return dialog.Result;
        }
    }
}
