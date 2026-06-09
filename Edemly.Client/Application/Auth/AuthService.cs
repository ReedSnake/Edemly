using Edemly.Client.Api;
using Edemly.Client.Infrastructure.Storage;
using Edemly.Contracts.Auth;
using System.Diagnostics;

namespace Edemly.Client.Application.Auth
{
    public class AuthService : IAuthService
    {
        private readonly IConfigService _configService;
        private readonly IApiClients _apiClients;

        public AuthService(string serverUrl)
        {
            if (string.IsNullOrWhiteSpace(serverUrl))
                throw new ArgumentException("serverUrl must be provided", nameof(serverUrl));

            _configService = ConfigService.Instance;
            _apiClients = App.ApiClients;

            Debug.WriteLine($"[AUTH SERVICE] Created for serverUrl={serverUrl}");
        }

        public Task<bool> SendVerificationCodeAsync(string email)
        {
            return _apiClients.Auth.GetVerificationCodeAsync(email);
        }

        public async Task<AuthResponseDto?> LoginWithCodeAsync(string email, string code)
        {
            try
            {
                var authResponse = await _apiClients.Auth.LoginAsync(email, code);

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
                var authResponse = await _apiClients.Auth.RegisterAsync(email, code, username);

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
                var authResponse = await _apiClients.Auth.SessionLoginAsync(sessionToken);

                if (authResponse == null)
                {
                    ClearAuthData();
                    return null;
                }

                SaveAuthData(authResponse);
                return authResponse;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AUTH SERVICE] SessionLoginAsync error: {ex.Message}");
                ClearAuthData();
                return null;
            }
        }

        public async Task<bool> LogoutAsync()
        {
            try
            {
                await _apiClients.Auth.LogoutAsync();

                ClearAuthData();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AUTH SERVICE] LogoutAsync error: {ex.Message}");
                ClearAuthData();
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

                try { App.GlobalProfilePictureCache?.SetAuthToken(null); }
                catch (Exception ex) { Debug.WriteLine($"[AUTH SERVICE] Failed to clear ProfilePictureCache token: {ex.Message}"); }

                try { App.GlobalFileCache?.SetAuthToken(null); }
                catch (Exception ex) { Debug.WriteLine($"[AUTH SERVICE] Failed to clear FileCache token: {ex.Message}"); }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AUTH SERVICE] ClearAuthData error: {ex.Message}");
            }
        }
    }
}