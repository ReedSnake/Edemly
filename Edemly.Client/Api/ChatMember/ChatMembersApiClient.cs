using Edemly.Client.Api.ChatMembers;
using Edemly.Client.Api.Core;
using Edemly.Contracts.ChatMembers;

namespace Edemly.Client.Api.ChatMembers;

public sealed class ChatMembersApiClient : ApiClientBase, IChatMembersApiClient
{
    public ChatMembersApiClient(ApiClientContext context) : base(context)
    {
    }

    public Task<List<ChatMemberDto>> GetChatMembersAsync(int chatId)
    {
        return GetListAsync<ChatMemberDto>($"api/chats/{chatId}/members");
    }
}