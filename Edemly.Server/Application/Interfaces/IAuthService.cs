using Edemly.Contracts.Auth;

namespace Edemly.Server.Api.Services
{
    public interface IAuthService
    {
        Task<AuthMessageResult> GetLoginCodeAsync(LoginRequestDto? model);
        Task<AuthResponseResult> LoginAsync(LoginWithCodeDto model);
        Task<AuthResponseResult> RegisterAsync(RegistrationWithCodeDto model);
        Task<AuthResponseResult> SessionLoginAsync(SessionLoginDto model);
        Task<AuthMessageResult> LogoutAsync(int userId);
    }

    public sealed record AuthMessageResult(bool Success, int StatusCode, string Message);

    public sealed record AuthResponseResult(bool Success, int StatusCode, AuthResponseDto? AuthResponse, string? Message);
}
