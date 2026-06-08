using Edemly.Contracts.Chats;
using Edemly.Server.Application.Common;

namespace Edemly.Server.Application.Chats
{
    public interface IChatService
    {
        Task<ServiceResult<ChatDto>> CreateOrGetPrivateChatAsync(int currentUserId, int targetUserId);

        Task<ServiceResult<List<ChatDto>>> GetMyChatsAsync(int currentUserId);

        Task<ServiceResult<ChatDto>> GetByIdAsync(int currentUserId, int chatId);

        Task<ServiceResult<ChatDto>> CreateGroupChatAsync(int requesterId, string groupName, List<int> participantIds);

        Task<ServiceResult> UpdateAsync(int chatId, string? name, string? description, string? iconUrl);
    }
}