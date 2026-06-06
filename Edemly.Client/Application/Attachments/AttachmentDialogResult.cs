#nullable enable

namespace Edemly.Client.Application.Attachments
{
    public sealed record AttachmentDialogResult(
        AttachmentDialogAction Action,
        string Caption)
    {
        public static AttachmentDialogResult Cancelled { get; } = new(AttachmentDialogAction.Cancel, string.Empty);
    }
}
