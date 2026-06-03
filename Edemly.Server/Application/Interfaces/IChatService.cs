using Edemly.Contracts.Chats;

namespace Edemly.Server.Api.Services
{
    public interface IChatService
    {
        Task<ServiceDataResult<ChatDto>> CreateOrGetPrivateChat(int currentUserId, int otherUserId);
        Task<ServiceDataResult<List<ChatDto>>> GetMyChats(int userId);
        Task<ServiceDataResult<ChatDto>> GetById(int currentUserId, int chatId);
        Task<ServiceDataResult<ChatDto>> CreateGroupChat(int creatorId, string groupName, List<int> participantIds);
        Task<ServiceMessageResult> UpdateChat(int chatId, string? name, string? description, string? iconUrl);
    }
}
