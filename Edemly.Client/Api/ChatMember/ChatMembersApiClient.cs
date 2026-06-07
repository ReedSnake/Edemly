using Edemly.Client.Api.Core;


namespace Edemly.Client.Api.ChatMember;

public sealed class ChatMembersApiClient : ApiClientBase, IChatMembersApiClient
{
    public ChatMembersApiClient(ApiClientContext context) : base(context)
    {
    }

    public Task<List<ChatMemberDto>> GetChatMembersAsync(int chatId)
    {
        return GetListAsync<ChatMemberDto>($"api/chatmember/list/{chatId}");
    }

}

