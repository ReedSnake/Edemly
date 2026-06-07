namespace Edemly.Client.Api.ChatMember;

public interface IChatMembersApiClient
{
    Task<List<ChatMemberDto>> GetChatMembersAsync(int chatId);
}
