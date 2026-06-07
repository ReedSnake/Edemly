using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Edemly.Client.Api.Core;

public abstract class ApiClientBase
{
    protected readonly HttpClient HttpClient;
    protected readonly ApiClientContext Context;

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected ApiClientBase(ApiClientContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        HttpClient = context.HttpClient;
    }

    protected static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        try
        {
            return await ReadJsonAsync<T>(response);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] JSON parse failed: {ex.Message}");
            return default;
        }
    }
    protected async Task<T?> GetAsync<T>(string endpoint)
    {
        try
        {
            var url = UrlHelper.BuildRelativeUrl(endpoint);
            System.Diagnostics.Debug.WriteLine($"[API] GET {HttpClient.BaseAddress}{url}");

            var response = await HttpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return default;

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] GET {endpoint} failed: {ex.Message}");
            return default;
        }
    }

    protected async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request)
    {
        try
        {
            var url = UrlHelper.BuildRelativeUrl(endpoint);
            System.Diagnostics.Debug.WriteLine($"[API] POST {HttpClient.BaseAddress}{url}");

            var response = await HttpClient.PostAsJsonAsync(url, request, JsonOptions);

            if (!response.IsSuccessStatusCode)
                return default;

            return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] POST {endpoint} failed: {ex.Message}");
            return default;
        }
    }

    protected async Task<(bool Success, string? Error)> PutAsync<TRequest>(
        string endpoint,
        TRequest request)
    {
        try
        {
            var url = UrlHelper.BuildRelativeUrl(endpoint);
            System.Diagnostics.Debug.WriteLine($"[API] PUT {HttpClient.BaseAddress}{url}");

            var response = await HttpClient.PutAsJsonAsync(url, request, JsonOptions);

            if (response.IsSuccessStatusCode)
                return (true, null);

            var error = await response.Content.ReadAsStringAsync();
            return (false, error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    protected async Task<List<T>> GetListAsync<T>(string endpoint)
    {
        return await GetAsync<List<T>>(endpoint) ?? new List<T>();
    }
    protected async Task<bool> DeleteAsync(string endpoint)
    {
        try
        {
            var url = UrlHelper.BuildRelativeUrl(endpoint);
            System.Diagnostics.Debug.WriteLine($"[API] DELETE {HttpClient.BaseAddress}{url}");

            var response = await HttpClient.DeleteAsync(url);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] DELETE {endpoint} failed: {ex.Message}");
            return false;
        }
    }
    protected static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] JSON parse failed: {ex.Message}");
            return default;
        }
    }
}