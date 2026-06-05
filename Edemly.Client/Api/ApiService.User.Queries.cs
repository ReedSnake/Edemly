using System.Net.Http;

namespace Edemly.Client.Api
{
    public partial class ApiService
    {
        public async Task<UserInfoDto> GetUserInfoAsync()
        {
            try
            {
                var rel = "api/user/me";
                var url = BuildUrl(rel);
                System.Diagnostics.Debug.WriteLine($"[API] GET {_httpClient.BaseAddress}{url}");
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return new UserInfoDto();
                }

                var json = await response.Content.ReadAsStringAsync();
                var responseWrapper = TryDeserialize<GetUserInfoResponseDto>(json);

                return responseWrapper?.User ?? new UserInfoDto();
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
                var rel = $"api/user/search?query={Uri.EscapeDataString(query)}";
                var url = BuildUrl(rel);
                System.Diagnostics.Debug.WriteLine($"[API] GET {_httpClient.BaseAddress}{url}");
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return new List<UserDto>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = TryDeserialize<SearchUsersResponseDto>(json);

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
                var rel = $"api/user/id/{userId}";
                var url = BuildUrl(rel);
                System.Diagnostics.Debug.WriteLine($"[API] GET {_httpClient.BaseAddress}{url}");
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = TryDeserialize<GetUserResponseDto>(json);

                return result?.User;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetUserByIdAsync failed: {ex.Message}");
                return null;
            }
        }
    }
}
