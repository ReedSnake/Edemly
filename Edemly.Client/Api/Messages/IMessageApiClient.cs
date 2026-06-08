using Edemly.Contracts.Messages;

namespace Edemly.Client.Api.Messages;

public interface IMessageApiClient
{
    Task<List<MessageDto>> GetChatMessagesAsync(int chatId, int page = 1, int pageSize = 50);
}
