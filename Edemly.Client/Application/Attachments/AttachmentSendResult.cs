#nullable enable

namespace Edemly.Client.Application.Attachments
{
    public sealed record AttachmentSendResult(
        bool Success,
        string? ErrorMessage = null)
    {
        public static AttachmentSendResult Ok() => new(true);

        public static AttachmentSendResult Fail(string errorMessage) => new(false, errorMessage);
    }
}
