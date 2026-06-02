using Edemly.Contracts.Chats;

namespace Edemly.Server.Api.Services
{
    public interface IChatService
    {
        Task<(bool Success, string? Error, ChatDto? Chat)> CreateOrGetPrivateChat(int currentUserId, int otherUserId);
        Task<(bool Success, string? Error, List<ChatDto> Chats)> GetMyChats(int userId);
        Task<(bool Success, string? Error, ChatDto? Chat)> GetById(int chatId);
        Task<(bool Success, string? Error, ChatDto? Chat)> GetById(int chatId, int requestingUserId);
        Task<(bool Success, string? Error, ChatDto? Chat)> CreateGroupChat(int creatorId, string groupName, List<int> participantIds);
        Task<(bool Success, string? Error)> UpdateChat(int chatId, string? name, string? description, string? iconUrl);
    }
}
