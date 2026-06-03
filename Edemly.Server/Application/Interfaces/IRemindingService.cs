using Edemly.Contracts.Remindings;

namespace Edemly.Server.Api.Services
{
    public interface IRemindingService
    {
        Task<ServiceMessageResult> Create(int currentUserId, CreateRemindingDto model);
        Task<ServiceMessageResult> Update(int currentUserId, UpdateRemindingDto model);
        Task<ServiceMessageResult> Delete(int currentUserId, int id);
        Task<ServiceDataResult<RemindingDto>> GetById(int currentUserId, int id);
        Task<ServiceDataResult<List<RemindingDto>>> GetByUser(int currentUserId);
        Task<ServiceMessageResult> ConfirmReminding(int userId, int remindingId);
        Task<ServiceMessageResult> ToggleCompletion(int currentUserId, int id);
    }
}
