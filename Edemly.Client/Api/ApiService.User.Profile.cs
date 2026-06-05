using Edemly.Client.Application.Profiles;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Edemly.Client.Api
{
    public partial class ApiService
    {
        public async Task<(bool Success, string? Error)> UpdateUserInfoAsync(UserProfileUpdateRequest request)
        {
            try
            {
                if (request == null)
                {
                    throw new ArgumentNullException(nameof(request));
                }

                var updateData = new UpdateUserDto
                {
                    Username = request.Username?.Trim(),
                    FirstName = request.FirstName?.Trim(),
                    LastName = request.LastName?.Trim(),
                    PhoneNumber = request.PhoneNumber?.Trim(),
                    Description = request.Description?.Trim(),
                    PfpUrl = request.PfpUrl?.Trim()
                };

                var json = JsonSerializer.Serialize(updateData, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var rel = "api/user/update";
                var url = BuildUrl(rel);
                var response = await _httpClient.PutAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    return (true, null);
                }

                var responseText = await response.Content.ReadAsStringAsync();
                var errorMessage = TryDeserialize<ApiMessageResponse>(responseText)?.Message;

                return (false, string.IsNullOrWhiteSpace(errorMessage) ? response.ReasonPhrase : errorMessage);
            }
            catch (ArgumentNullException)
            {
                throw;
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
}
