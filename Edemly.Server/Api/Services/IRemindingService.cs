using static Edemly.Server.Api.DTOs.RemindingDtos;

namespace Edemly.Server.Api.Services
{
    public interface IRemindingService
    {
        Task<(bool Success, string? Error)> Create(int creatorId, RemindingCreateDto model);
        Task<(bool Success, string? Error)> Update(RemindingUpdateDto model);
        Task<(bool Success, string? Error)> Delete(int id);
        Task<(bool Success, string? Error, RemindingGetDto Reminding)> GetById(int id);
        Task<(bool Success, string? Error, List<RemindingGetDto> Remindings)> GetByUser(int userId);
        Task<(bool Success, string? Error)> ConfirmReminding(int userId, int remindingId);
        Task<(bool Success, string? Error)> ToggleCompletion(int id);
    }
}