namespace Edemly.Client.Api.ChatMembers;

public interface IChatMembersApiClient
{
    Task<List<ChatMemberDto>> GetChatMembersAsync(int chatId);
}
