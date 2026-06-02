using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Edemly.Client.Api;

using Edemly.Contracts.Users;

namespace Edemly.Client.Services.Api
{
    public partial class ApiService : IApiService, IDisposable
    {
        public async Task<UserInfoDto> GetUserInfo()
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
                System.Diagnostics.Debug.WriteLine($"[API] GetUserInfo failed: {ex.Message}");
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

        public async Task<bool> UpdateUserInfo(string? phoneNumber, string? description, string? pfpUrl, string? name)
        {
            try
            {
                var updateData = new UpdateUserDto();
                bool hasChanges = false;

                if (name != null)
                {
                    var trimmedName = name.Trim();

                    // Accept single-name updates (first name only). Do not require last name.
                    // If an empty string is passed, we'll send an empty first name (clearing it on server)
                    // If a multi-word name is passed, use the first token as FirstName (ignore surname).
                    if (string.IsNullOrEmpty(trimmedName))
                    {
                        updateData.FirstName = string.Empty;
                    }
                    else
                    {
                        var nameParts = trimmedName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        updateData.FirstName = nameParts.Length > 0 ? nameParts[0] : trimmedName;
                    }

                    // Do not send LastName (server won't be updated). Use null so serializer will omit it.
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
