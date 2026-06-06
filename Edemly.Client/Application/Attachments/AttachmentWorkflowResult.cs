#nullable enable

namespace Edemly.Client.Application.Attachments
{
    public sealed record AttachmentWorkflowResult(bool Cancelled, bool Success, int SentCount, string? ErrorMessage = null)
    {
        public static AttachmentWorkflowResult Completed(int sentCount) => new(false, true, sentCount);

        public static AttachmentWorkflowResult CancelledByUser(int sentCount) => new(true, true, sentCount);

        public static AttachmentWorkflowResult Failed(string? errorMessage, int sentCount) => new(false, false, sentCount, errorMessage);
    }
}
