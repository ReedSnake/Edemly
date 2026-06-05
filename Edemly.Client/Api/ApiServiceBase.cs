using Edemly.Contracts.Calls;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
namespace Edemly.Client.Api
{
    public partial class ApiService : IApiService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private string _baseUrl;
        private string? _authToken;

        public ApiService(string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                throw new ArgumentException("serverUrl must be provided", nameof(serverUrl));

            _baseUrl = UrlHelper.NormalizeBaseUrl(serverUrl);

            var baseAddr = _baseUrl.EndsWith('/') ? _baseUrl : _baseUrl + "/";

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseAddr),
                Timeout = TimeSpan.FromSeconds(30)
            };

            System.Diagnostics.Debug.WriteLine($"[API SERVICE] Created with baseUrl={_httpClient.BaseAddress}");
        }

        public void SetBaseUrl(string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                throw new ArgumentException("serverUrl must be provided", nameof(serverUrl));

            _baseUrl = UrlHelper.NormalizeBaseUrl(serverUrl);

            try
            {
                var baseAddr = _baseUrl.EndsWith('/') ? _baseUrl : _baseUrl + "/";
                _httpClient.BaseAddress = new Uri(baseAddr);
                System.Diagnostics.Debug.WriteLine($"[API SERVICE] BaseAddress updated to {_httpClient.BaseAddress}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API SERVICE] Failed to set BaseAddress: {ex.Message}");
                throw;
            }
        }

        public void SetAuthToken(string token)
        {
            _authToken = string.IsNullOrWhiteSpace(token) ? null : token;
            _httpClient.DefaultRequestHeaders.Authorization =
                string.IsNullOrEmpty(_authToken)
                    ? null
                    : new AuthenticationHeaderValue("Bearer", _authToken);

            System.Diagnostics.Debug.WriteLine("[API] Auth token set on HttpClient");
        }

        public async Task<string?> GetValidTokenAsync()
        {
            await Task.CompletedTask;
            return _authToken;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        private static T? TryDeserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return default;

            try
            {
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"JSON parse error: {ex.Message}");
                return default;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unexpected parse error: {ex.Message}");
                return default;
            }
        }

        private static string BuildUrl(string relativeOrAbsolute)
        {
            return UrlHelper.BuildRelativeUrl(relativeOrAbsolute);
        }

        public async Task<List<CallDto>> GetActiveCallsAsync() //I left this here but pls make a separate file if you add more call related stuff to the api later
        {
            try
            {
                var rel = "api/call/active";
                var url = UrlHelper.BuildRelativeUrl(rel);
                System.Diagnostics.Debug.WriteLine($"[API] GET {_httpClient.BaseAddress}{url}");
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return new List<CallDto>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var calls = TryDeserialize<List<CallDto>>(json);

                return calls ?? new List<CallDto>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetActiveCallsAsync failed: {ex.Message}");
                return new List<CallDto>();
            }
        }
    }
}
