namespace Edemly.Client.Api.ChatMember;

public interface IChatMemberApiClient
{
    Task<List<ChatMemberDto>> GetChatMembersAsync(int chatId);
}
