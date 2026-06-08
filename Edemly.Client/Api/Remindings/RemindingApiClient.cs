using Edemly.Client.Api.Core;
using Edemly.Contracts.Remindings;

namespace Edemly.Client.Api.Remindings;

public sealed class RemindingApiClient : ApiClientBase, IRemindingApiClient
{
    public RemindingApiClient(ApiClientContext context) : base(context)
    {
    }

    public Task<RemindingDto?> CreateRemindingAsync(CreateRemindingDto model)
    {
        return PostAsync<CreateRemindingDto, RemindingDto>("api/remindings", model);
    }

    public Task<List<RemindingDto>> GetMyRemindingsAsync()
    {
        return GetListAsync<RemindingDto>("api/remindings");
    }

    public async Task<bool> UpdateRemindingAsync(UpdateRemindingDto model, int remindingId)
    {
        var result = await PutAsync($"api/remindings/{remindingId}", model);
        return result.Success;
    }

    public async Task<bool> ToggleRemindingAsync(int id)
    {
        var result = await PatchAsync<object?>($"api/remindings/{id}/completion", null);
        return result.Success;
    }

    public Task<bool> DeleteRemindingAsync(int id)
    {
        return DeleteAsync($"api/remindings/{id}");
    }
}