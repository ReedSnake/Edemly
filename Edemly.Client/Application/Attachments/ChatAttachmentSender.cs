#nullable enable

using System.IO;
using Edemly.Client.Api;
using Edemly.Client.Infrastructure.Realtime;
using Edemly.Contracts.Messages;

namespace Edemly.Client.Application.Attachments
{
    public sealed class ChatAttachmentSender : IChatAttachmentSender
    {
        private const long MaxFileSizeBytes = 50 * 1024 * 1024;

        private readonly IApiClients _apiClient;
        private readonly IHubService _hubService;

        public ChatAttachmentSender(IApiClients apiClient, IHubService hubService)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _hubService = hubService ?? throw new ArgumentNullException(nameof(hubService));
        }

        public async Task<AttachmentSendResult> SendAsync(int chatId, AttachmentDescriptor descriptor, string caption)
        {
            try
            {
                if (chatId < 0)
                {
                    return AttachmentSendResult.Fail(DefaultLanguage.SelectChat);
                }

                if (descriptor == null || !File.Exists(descriptor.FilePath))
                {
                    return AttachmentSendResult.Fail(DefaultLanguage.AttachmentFileMissing);
                }

                if (descriptor.SizeBytes > MaxFileSizeBytes)
                {
                    return AttachmentSendResult.Fail(DefaultLanguage.AttachmentTooLarge);
                }

                var uploadResult = await _apiClient.Files.UploadFileAsync(descriptor.FilePath);
                if (!uploadResult.Success || string.IsNullOrWhiteSpace(uploadResult.Url))
                {
                    return AttachmentSendResult.Fail(string.Format(DefaultLanguage.UploadFailed, uploadResult.Error));
                }

                var message = new CreateMessageDto
                {
                    ChatId = chatId,
                    Text = string.IsNullOrWhiteSpace(caption) ? string.Empty : caption.Trim(),
                    Type = descriptor.MessageType,
                    ContentUrl = uploadResult.Url,
                    FileName = uploadResult.FileName
                };

                var success = await _hubService.SendMessageAsync(message);
                return success
                    ? AttachmentSendResult.Ok()
                    : AttachmentSendResult.Fail(DefaultLanguage.FailedSendMessage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ATTACHMENTS] SendAsync failed: {ex.Message}");
                return AttachmentSendResult.Fail($"{DefaultLanguage.Error}: {ex.Message}");
            }
        }
    }
}
