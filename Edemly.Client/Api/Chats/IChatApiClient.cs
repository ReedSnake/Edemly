namespace Edemly.Client.Api.Chats;

public interface IChatApiClient
{
    Task<List<MessageDto>> GetChatMessagesAsync(int chatId, int page = 1, int pageSize = 50);

    Task<List<ChatDto>> GetMyChatsAsync();

    Task<ChatDto?> CreateOrGetPrivateChatAsync(int userId);

    Task<ChatDto?> CreateGroupChatAsync(string groupName, List<int> participantIds);

    Task<ChatDto?> GetChatByIdAsync(int chatId);

    Task<List<ChatMemberDto>> GetChatMembersAsync(int chatId);

    Task<(bool Success, string? Error)> UpdateChatAsync(
        int chatId,
        string? name = null,
        string? description = null,
        string? iconUrl = null);
}
