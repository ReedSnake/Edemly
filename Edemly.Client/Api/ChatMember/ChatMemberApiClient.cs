using Edemly.Client.Api.Core;


namespace Edemly.Client.Api.ChatMember;

public sealed class ChatMemberApiClient : ApiClientBase, IChatMemberApiClient
{
    public ChatMemberApiClient(ApiClientContext context) : base(context)
    {
    }

    public Task<List<ChatMemberDto>> GetChatMembersAsync(int chatId)
    {
        return GetListAsync<ChatMemberDto>($"api/chatmember/list/{chatId}");
    }

}

