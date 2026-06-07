using Edemly.Contracts.Auth;
using Edemly.Client.Infrastructure.Storage;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace Edemly.Client.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigService _configService;
        private readonly string _baseUrl;

        public AuthService(string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                throw new ArgumentException("serverUrl must be provided", nameof(serverUrl));

            _baseUrl = serverUrl.TrimEnd('/');
            var baseAddress = new Uri(_baseUrl.EndsWith("/") ? _baseUrl : _baseUrl + "/");
            _httpClient = new HttpClient
            {
                BaseAddress = baseAddress,
                Timeout = TimeSpan.FromSeconds(30)
            };
            _configService = ConfigService.Instance;

            Debug.WriteLine($"[AUTH SERVICE] Created with BaseAddress={_httpClient.BaseAddress}");
        }

        public async Task<bool> SendVerificationCodeAsync(string email)
        {
            try
            {
                var request = new LoginRequestDto { Email = email };
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var requestPath = "api/auth/get-code";
                Debug.WriteLine($"[AUTH SERVICE] POST {_httpClient.BaseAddress}{requestPath}");

                var response = await _httpClient.PostAsync(requestPath, content);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AUTH SERVICE] SendVerificationCodeAsync error: {ex.Message}");
                return false;
            }
        }

        public async Task<AuthResponseDto?> LoginWithCodeAsync(string email, string code)
        {
            try
            {
                var request = new LoginWithCodeDto { Email = email, Code = code };
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var requestPath = "api/auth/login";
                Debug.WriteLine($"[AUTH SERVICE] POST {_httpClient.BaseAddress}{requestPath}");

                var response = await _httpClient.PostAsync(requestPath, content);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var authResponse = JsonSerializer.Deserialize<AuthResponseDto>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (authResponse != null)
                {
                    SaveAuthData(authResponse);
                }

                return authResponse;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AUTH SERVICE] LoginWithCodeAsync error: {ex.Message}");
                return null;
            }
        }

        public async Task<AuthResponseDto?> RegisterWithCodeAsync(string email, string code, string username)
        {
            try
            {
                var request = new RegistrationWithCodeDto { Email = email, Code = code, Username = username };
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var requestPath = "api/auth/register";
                Debug.WriteLine($"[AUTH SERVICE] POST {_httpClient.BaseAddress}{requestPath}");

                var response = await _httpClient.PostAsync(requestPath, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[AUTH SERVICE] Register failed: {errorContent}");
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var authResponse = JsonSerializer.Deserialize<AuthResponseDto>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (authResponse != null)
                {
                    SaveAuthData(authResponse);
                }

                return authResponse;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AUTH SERVICE] RegisterWithCodeAsync error: {ex.Message}");
                return null;
            }
        }

        public async Task<AuthResponseDto?> SessionLoginAsync(string sessionToken)
        {
            try
            {
                var request = new SessionLoginDto { SessionToken = sessionToken };
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var requestPath = "api/auth/session-login";
                Debug.WriteLine($"[AUTH SERVICE] POST {_httpClient.BaseAddress}{requestPath}");

                var response = await _httpClient.PostAsync(requestPath, content);

                if (!response.IsSuccessStatusCode)
                {
                    ClearAuthData();
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var authResponse = JsonSerializer.Deserialize<AuthResponseDto>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (authResponse != null)
                {
                    SaveAuthData(authResponse);
                }

                return authResponse;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AUTH SERVICE] SessionLoginAsync error: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> LogoutAsync()
        {
            try
            {
                var authData = LoadAuthData();
                if (authData != null)
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", authData.Token);

                    var requestPath = "api/auth/logout";
                    Debug.WriteLine($"[AUTH SERVICE] POST {_httpClient.BaseAddress}{requestPath}");

                    await _httpClient.PostAsync(requestPath, null);
                }

                ClearAuthData();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AUTH SERVICE] LogoutAsync error: {ex.Message}");
                ClearAuthData(); // best-effort cleanup
                return false;
            }
        }

        public void SaveAuthData(AuthResponseDto authResponse)
        {
            try
            {
                _configService.SetValue("AuthToken", authResponse.Token);
                _configService.SetValue("SessionToken", authResponse.SessionToken);
                _configService.SetValue("UserId", authResponse.UserId);
                _configService.SetValue("Username", authResponse.Username);
                _configService.SetValue("Email", authResponse.Email);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AUTH SERVICE] SaveAuthData error: {ex.Message}");
            }
        }

        public AuthResponseDto? LoadAuthData()
        {
            try
            {
                var token = _configService.GetValue<string>("AuthToken", string.Empty);
                var sessionToken = _configService.GetValue<string>("SessionToken", string.Empty);
                var userId = _configService.GetValue<int>("UserId", 0);
                var username = _configService.GetValue<string>("Username", string.Empty);
                var email = _configService.GetValue<string>("Email", string.Empty);

                if (string.IsNullOrEmpty(token) || userId == 0)
                {
                    return null;
                }

                return new AuthResponseDto
                {
                    Token = token,
                    SessionToken = sessionToken,
                    UserId = userId,
                    Username = username,
                    Email = email
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AUTH SERVICE] LoadAuthData error: {ex.Message}");
                return null;
            }
        }

        public void ClearAuthData()
        {
            try
            {
                _configService.SetValue<string?>("AuthToken", null);
                _configService.SetValue<string?>("SessionToken", null);
                _configService.SetValue<int?>("UserId", null);
                _configService.SetValue<string?>("Username", null);
                _configService.SetValue<string?>("Email", null);

                try { App.GlobalProfilePictureCache?.SetAuthToken(null); } catch (Exception ex) { Debug.WriteLine($"[AUTH SERVICE] Failed to clear ProfilePictureCache token: {ex.Message}"); }
                try { App.GlobalFileCache?.SetAuthToken(null); } catch (Exception ex) { Debug.WriteLine($"[AUTH SERVICE] Failed to clear FileCache token: {ex.Message}"); }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AUTH SERVICE] ClearAuthData error: {ex.Message}");
            }
        }
    }
}
