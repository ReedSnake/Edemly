using Edemly.Contracts.Remindings;

namespace Edemly.Server.Api.Services
{
    public interface IRemindingService
    {
        Task<(bool Success, string? Error)> Create(int creatorId, CreateRemindingDto model);
        Task<(bool Success, string? Error)> Update(UpdateRemindingDto model);
        Task<(bool Success, string? Error)> Delete(int id);
        Task<(bool Success, string? Error, RemindingDto Reminding)> GetById(int id);
        Task<(bool Success, string? Error, List<RemindingDto> Remindings)> GetByUser(int userId);
        Task<(bool Success, string? Error)> ConfirmReminding(int userId, int remindingId);
        Task<(bool Success, string? Error)> ToggleCompletion(int id);
    }
}