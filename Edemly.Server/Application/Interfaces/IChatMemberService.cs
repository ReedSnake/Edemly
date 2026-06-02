using Edemly.Contracts.ChatMembers;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Api.Services
{
    public interface IChatMemberService
    {
        Task<(bool Success, string? Error)> AddMember(CreateChatMemberDto model);
        Task<(bool Success, string? Error)> AddMember(int chatId, int userId, ChatMemberRole role);
        Task<(bool Success, string? Error)> UpdateMember(UpdateChatMemberDto model);
        Task<(bool Success, string? Error)> DeleteMember(int id);
        Task<(bool Success, string? Error, ChatMemberDto Member)> GetMember(int id);
        Task<(bool Success, string? Error, List<ChatMemberDto> Members)> GetMembers(int chatId);
        Task<(bool Success, string? Error, List<ChatMemberDto> Memberships)> GetMemberships(int userId);
    }
}
