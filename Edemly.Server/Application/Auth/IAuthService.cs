using Edemly.Contracts.Auth;
using Edemly.Server.Application.Common;

namespace Edemly.Server.Application.Auth
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