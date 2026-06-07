using Edemly.Client.Api.Core;

namespace Edemly.Client.Api.Messages;

public sealed class MessageApiClient : ApiClientBase, IMessageApiClient
{
    public MessageApiClient(ApiClientContext context) : base(context)
    {
    }

    public Task<List<MessageDto>> GetChatMessagesAsync(
        int chatId,
        int page = 1,
        int pageSize = 50)
    {
        return GetListAsync<MessageDto>(
            $"api/message/chat/{chatId}?page={page}&pageSize={pageSize}");
    }
}
