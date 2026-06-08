using Edemly.Contracts.ChatMembers;
using Edemly.Server.Application.Common;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Application.ChatMembers
{
    public interface IChatMemberService
    {
        Task<ServiceResult> AddMemberAsync(int requesterId, CreateChatMemberDto request);

        Task<ServiceResult> AddMemberAsync(int chatId, int targetUserId, ChatMemberRole role);

        Task<ServiceResult> UpdateAsync(int requesterId, int chatMemberId, UpdateChatMemberDto request);

        Task<ServiceResult> DeleteAsync(int requesterId, int chatMemberId);

        Task<ServiceResult<ChatMemberDto>> GetMemberAsync(int chatMemberId);

        Task<ServiceResult<List<ChatMemberDto>>> GetMembersAsync(int currentUserId, int chatId);

        Task<ServiceResult<List<ChatMemberDto>>> GetMembershipsAsync(int currentUserId);
    }
}