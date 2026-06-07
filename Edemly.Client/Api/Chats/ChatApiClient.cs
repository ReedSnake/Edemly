using Edemly.Client.Api.Core;

namespace Edemly.Client.Api.Chats;

public sealed class ChatApiClient : ApiClientBase, IChatApiClient
{
    public ChatApiClient(ApiClientContext context) : base(context)
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

    public Task<List<ChatDto>> GetMyChatsAsync()
    {
        return GetListAsync<ChatDto>("api/chat/my-chats");
    }

    public async Task<ChatDto?> CreateOrGetPrivateChatAsync(int userId)
    {
        var request = new CreatePrivateChatDto
        {
            UserId = userId
        };

        var result = await PostAsync<CreatePrivateChatDto, CreateChatResponseDto>(
            "api/chat/create-private",
            request);

        return result?.Chat;
    }

    public async Task<ChatDto?> CreateGroupChatAsync(
        string groupName,
        List<int> participantIds)
    {
        var request = new CreateGroupChatDto
        {
            GroupName = groupName,
            ParticipantIds = participantIds
        };

        var result = await PostAsync<CreateGroupChatDto, CreateGroupChatResponseDto>(
            "api/chat/create-group",
            request);

        return result?.Chat;
    }

    public Task<ChatDto?> GetChatByIdAsync(int chatId)
    {
        return GetAsync<ChatDto>($"api/chat/{chatId}");
    }

    public Task<List<ChatMemberDto>> GetChatMembersAsync(int chatId)
    {
        return GetListAsync<ChatMemberDto>($"api/chatmember/list/{chatId}");
    }

    public Task<(bool Success, string? Error)> UpdateChatAsync(
        int chatId,
        string? name = null,
        string? description = null,
        string? iconUrl = null)
    {
        var request = new UpdateChatDto
        {
            Id = chatId,
            Name = name,
            Description = description,
            IconUrl = iconUrl
        };

        return PutAsync("api/chat/update", request);
    }

    public void Dispose()
    {
        // HttpClient тут не dispose-имо, якщо він передається ззовні
        // і використовується іншими ApiClient.
    }
}