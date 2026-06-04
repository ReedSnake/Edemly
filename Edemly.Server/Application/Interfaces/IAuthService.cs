using Edemly.Contracts.Auth;

namespace Edemly.Server.Api.Services
{
    public interface IAuthService
    {
        Task<ServiceResult> GetLoginCodeAsync(LoginRequestDto? request);

        Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginWithCodeDto request);

        Task<ServiceResult<AuthResponseDto>> RegisterAsync(RegistrationWithCodeDto request);

        Task<ServiceResult<AuthResponseDto>> SessionLoginAsync(SessionLoginDto request);

        Task<ServiceResult> LogoutAsync(int userId);
    }
}