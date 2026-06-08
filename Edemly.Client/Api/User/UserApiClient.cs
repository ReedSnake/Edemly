using Edemly.Client.Api.Core;
using Edemly.Client.Application.Users.Profile;
using Edemly.Contracts.Users;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Edemly.Client.Api.Users;

public sealed class UserApiClient : ApiClientBase, IUserApiClient
{
    public UserApiClient(ApiClientContext context) : base(context)
    {
    }

    public async Task<UserInfoDto> GetUserInfoAsync()
    {
        try
        {
            var url = UrlHelper.BuildRelativeUrl("api/users/me");

            System.Diagnostics.Debug.WriteLine($"[API] GET {HttpClient.BaseAddress}{url}");

            var response = await HttpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return new UserInfoDto();

            var wrapper = await ReadJsonAsync<GetUserInfoResponseDto>(response);

            return wrapper?.User ?? new UserInfoDto();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] GetUserInfoAsync failed: {ex.Message}");
            return new UserInfoDto();
        }
    }

    public async Task<List<UserDto>> SearchUsersAsync(string query)
    {
        try
        {
            var url = UrlHelper.BuildRelativeUrl($"api/users/search?query={Uri.EscapeDataString(query)}");

            System.Diagnostics.Debug.WriteLine($"[API] GET {HttpClient.BaseAddress}{url}");

            var response = await HttpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return new List<UserDto>();

            var result = await ReadJsonAsync<SearchUsersResponseDto>(response);

            return result?.Users ?? new List<UserDto>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] SearchUsersAsync failed: {ex.Message}");
            return new List<UserDto>();
        }
    }

    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        try
        {
            var url = UrlHelper.BuildRelativeUrl($"api/users/{userId}");

            System.Diagnostics.Debug.WriteLine($"[API] GET {HttpClient.BaseAddress}{url}");

            var response = await HttpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var result = await ReadJsonAsync<GetUserResponseDto>(response);

            return result?.User;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] GetUserByIdAsync failed: {ex.Message}");
            return null;
        }
    }

    public async Task<(bool Success, string? Error)> UpdateUserInfoAsync(UpdateUserDto request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        try
        {
            var json = JsonSerializer.Serialize(
                request,
                new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = UrlHelper.BuildRelativeUrl("api/users/me");
            var response = await HttpClient.PutAsync(url, content);

            if (response.IsSuccessStatusCode)
                return (true, null);

            var responseText = await response.Content.ReadAsStringAsync();
            var errorMessage = Deserialize<ApiMessageResponse>(responseText)?.Message;

            return (
                false,
                string.IsNullOrWhiteSpace(errorMessage)
                    ? response.ReasonPhrase
                    : errorMessage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] UpdateUserInfoAsync failed: {ex.Message}");
            return (false, ex.Message);
        }
    }

    private sealed class ApiMessageResponse
    {
        public string? Message { get; set; }
    }
}