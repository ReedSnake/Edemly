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
        return PostAsync<CreateRemindingDto, RemindingDto>(
            "api/reminding/create",
            model);
    }

    public Task<List<RemindingDto>> GetMyRemindingsAsync()
    {
        return GetListAsync<RemindingDto>("api/reminding/my-remindings");
    }

    public async Task<bool> UpdateRemindingAsync(UpdateRemindingDto model)
    {
        var result = await PutAsync("api/reminding/update", model);
        return result.Success;
    }

    public async Task<bool> ToggleRemindingAsync(int id)
    {
        var result = await PutAsync<object?>($"api/reminding/toggle-completion/{id}", null);
        return result.Success;
    }

    public Task<bool> DeleteRemindingAsync(int id)
    {
        return DeleteAsync($"api/reminding/delete/{id}");
    }
}