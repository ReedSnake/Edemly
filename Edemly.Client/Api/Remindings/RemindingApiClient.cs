using System.Net.Http;
using System.Net.Http.Json;
using Edemly.Client.Api.Core;

namespace Edemly.Client.Api.Remindings;

public sealed class RemindingApiClient : ApiClientBase, IRemindingApiClient
{
    public RemindingApiClient(ApiClientContext context) : base(context)
    {
    }

    public async Task<RemindingDto?> CreateRemindingAsync(CreateRemindingDto model)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[OUTGOING JSON] " + System.Text.Json.JsonSerializer.Serialize(model));

            var result = await PostAsync<CreateRemindingDto, RemindingDto>(
                "api/reminding/create",
                model);

            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] CreateRemindingAsync exception: {ex.Message}");
            return null;
        }
    }

    public Task<List<RemindingDto>> GetMyRemindingsAsync()
    {
        return GetListAsync<RemindingDto>("api/reminding/my-remindings");
    }

    public async Task<bool> UpdateRemindingAsync(UpdateRemindingDto model)
    {
        try
        {
            var url = UrlHelper.BuildRelativeUrl("api/reminding/update");
            var response = await HttpClient.PutAsJsonAsync(url, model);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] UpdateRemindingAsync exception: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ToggleRemindingAsync(int id)
    {
        try
        {
            var url = UrlHelper.BuildRelativeUrl($"api/reminding/toggle-completion/{id}");
            var response = await HttpClient.PutAsync(url, null);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] ToggleRemindingAsync exception: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteRemindingAsync(int id)
    {
        try
        {
            var url = UrlHelper.BuildRelativeUrl($"api/reminding/delete/{id}");
            var response = await HttpClient.DeleteAsync(url);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] DeleteRemindingAsync exception: {ex.Message}");
            return false;
        }
    }
}