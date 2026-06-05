using System.Net.Http;
using System.Text;
using System.Text.Json;
namespace Edemly.Client.Api
{
    public partial class ApiService : IApiService, IDisposable
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

        public async Task<bool> UpdateUserInfoAsync(string? phoneNumber, string? description, string? pfpUrl, string? name)
        {
            try
            {
                var updateData = new UpdateUserDto();
                bool hasChanges = false;

                if (name != null)
                {
                    var trimmedName = name.Trim();

                    if (string.IsNullOrEmpty(trimmedName))
                    {
                        updateData.FirstName = string.Empty;
                    }
                    else
                    {
                        var nameParts = trimmedName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        updateData.FirstName = nameParts.Length > 0 ? nameParts[0] : trimmedName;
                    }

                    updateData.LastName = null;
                    hasChanges = true;
                }

                if (!string.IsNullOrWhiteSpace(phoneNumber))
                {
                    updateData.PhoneNumber = phoneNumber;
                    hasChanges = true;
                }

                if (!string.IsNullOrWhiteSpace(pfpUrl))
                {
                    updateData.PfpUrl = pfpUrl;
                    hasChanges = true;
                }

                if (!hasChanges)
                {
                    return false;
                }

                var json = JsonSerializer.Serialize(updateData, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var rel = "api/user/update";
                var url = BuildUrl(rel);
                var response = await _httpClient.PutAsync(url, content);

                return response.IsSuccessStatusCode;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}