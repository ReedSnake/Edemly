using uchat_server.Api.DTOs;

namespace uchat_server.Api.Services
{
    public interface IChatService
    {
        Task<(bool Success, string? Error, ChatDtos.ChatGetDto? Chat)> CreateOrGetPrivateChat(int currentUserId, int otherUserId);
        Task<(bool Success, string? Error, List<ChatDtos.ChatGetDto> Chats)> GetMyChats(int userId);
        Task<(bool Success, string? Error, ChatDtos.ChatGetDto? Chat)> GetById(int chatId);
        Task<(bool Success, string? Error, ChatDtos.ChatGetDto? Chat)> GetById(int chatId, int requestingUserId);
        Task<(bool Success, string? Error, ChatDtos.ChatGetDto? Chat)> CreateGroupChat(int creatorId, string groupName, List<int> participantIds);
        Task<(bool Success, string? Error)> UpdateChat(int chatId, string? name, string? description, string? iconUrl);
    }
}
