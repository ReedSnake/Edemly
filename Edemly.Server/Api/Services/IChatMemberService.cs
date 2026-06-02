using static uchat_server.Api.DTOs.ChatMemberDtos;
using uchat_server.Data.Entities;

namespace uchat_server.Api.Services
{
    public interface IChatMemberService
    {
        Task<(bool Success, string? Error)> AddMember(ChatMemberCreateDto model);
        Task<(bool Success, string? Error)> AddMember(int chatId, int userId, ChatMemberRole role);
        Task<(bool Success, string? Error)> UpdateMember(ChatMemberUpdateDto model);
        Task<(bool Success, string? Error)> DeleteMember(int id);
        Task<(bool Success, string? Error, ChatMemberGetDto Member)> GetMember(int id);
        Task<(bool Success, string? Error, List<ChatMemberGetDto> Members)> GetMembers(int chatId);
        Task<(bool Success, string? Error, List<ChatMemberGetDto> Memberships)> GetMemberships(int userId);
    }
}
