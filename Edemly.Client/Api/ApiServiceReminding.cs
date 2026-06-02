using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Edemly.Client.Api;

using Edemly.Contracts.Remindings;

namespace Edemly.Client.Api
{
    public partial class ApiService : IApiService, IDisposable
    {
        public async Task<RemindingDto?> CreateRemindingAsync(CreateRemindingDto model)
        {
            try
            {
                var json = JsonSerializer.Serialize(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var rel = "api/reminding/create";
                var url = BuildUrl(rel);
                System.Diagnostics.Debug.WriteLine("[OUTGOING JSON] " + json);
                System.Diagnostics.Debug.WriteLine($"[API] POST {_httpClient.BaseAddress}{url}");

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<RemindingDto>(responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return result;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[API] CreateRemindingAsync failed: {responseContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] CreateRemindingAsync exception: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> UpdateRemindingAsync(UpdateRemindingDto model)
        {
            try
            {
                var json = JsonSerializer.Serialize(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var rel = "api/reminding/update";
                var url = BuildUrl(rel);

                await _httpClient.PutAsync(url, content);

                return true;
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
                var rel = $"api/reminding/toggle-completion/{id}";
                var url = BuildUrl(rel);

                await _httpClient.PutAsync(url, null);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] ToggleRemindingAsync exception: {ex.Message}");
                return false;
            }
        }

        public async Task<List<RemindingDto>> GetMyRemindingsAsync()
        {
            try
            {
                var url = BuildUrl("api/reminding/my-remindings");
                System.Diagnostics.Debug.WriteLine($"[API] GET {_httpClient.BaseAddress}{url}");

                var response = await _httpClient.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var remindings = JsonSerializer.Deserialize<List<RemindingDto>>(responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return remindings ?? new List<RemindingDto>();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[API] GetMyRemindingsAsync failed: {responseContent}");
                    return new List<RemindingDto>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetMyRemindingsAsync exception: {ex.Message}");
                return new List<RemindingDto>();
            }
        }

        public async Task<bool> DeleteRemindingAsync(int id)
        {
            try
            {
                var rel = $"api/reminding/delete/{id}";
                var url = BuildUrl(rel);

                await _httpClient.DeleteAsync(url);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
