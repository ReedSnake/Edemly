using Edemly.Contracts.Remindings;

namespace Edemly.Server.Api.Services
{
    public interface IRemindingService
    {
        Task<ServiceResult> CreateAsync(int currentUserId, CreateRemindingDto request);

        Task<ServiceResult> UpdateAsync(int currentUserId, UpdateRemindingDto request);

        Task<ServiceResult> DeleteAsync(int currentUserId, int remindingId);

        Task<ServiceResult<RemindingDto>> GetByIdAsync(int currentUserId, int remindingId);

        Task<ServiceResult<List<RemindingDto>>> GetByUserAsync(int currentUserId);

        Task<ServiceResult> ConfirmRemindingAsync(int currentUserId, int remindingId);

        Task<ServiceResult> ToggleCompletionAsync(int currentUserId, int remindingId);
    }
}