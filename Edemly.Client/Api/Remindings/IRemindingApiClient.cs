using Edemly.Contracts.Remindings;

namespace Edemly.Client.Api.Remindings;

public interface IRemindingApiClient
{
    Task<RemindingDto?> CreateRemindingAsync(CreateRemindingDto model);

    Task<List<RemindingDto>> GetMyRemindingsAsync();

    Task<bool> UpdateRemindingAsync(UpdateRemindingDto model, int requestId);

    Task<bool> ToggleRemindingAsync(int id);

    Task<bool> DeleteRemindingAsync(int id);
}