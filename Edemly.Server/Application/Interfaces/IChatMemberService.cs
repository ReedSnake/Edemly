using Edemly.Contracts.ChatMembers;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Api.Services
{
    public interface IChatMemberService
    {
        Task<ServiceMessageResult> AddMember(int currentUserId, CreateChatMemberDto model);
        Task<(bool Success, string? Error)> AddMember(int chatId, int userId, ChatMemberRole role);
        Task<ServiceMessageResult> UpdateMember(int currentUserId, UpdateChatMemberDto model);
        Task<ServiceMessageResult> DeleteMember(int currentUserId, int id);
        Task<ServiceDataResult<ChatMemberDto>> GetMember(int id);
        Task<ServiceDataResult<List<ChatMemberDto>>> GetMembers(int currentUserId, int chatId);
        Task<ServiceDataResult<List<ChatMemberDto>>> GetMemberships(int currentUserId);
    }
}
