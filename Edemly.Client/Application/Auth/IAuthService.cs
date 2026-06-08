using Edemly.Contracts.Auth;
namespace Edemly.Client.Application.Auth
{
    public interface IAuthService
    {
        Task<bool> SendVerificationCodeAsync(string email);

        Task<AuthResponseDto?> LoginWithCodeAsync(string email, string code);

        Task<AuthResponseDto?> RegisterWithCodeAsync(string email, string code, string username);

        Task<AuthResponseDto?> SessionLoginAsync(string sessionToken);

        Task<bool> LogoutAsync();

        void SaveAuthData(AuthResponseDto authResponse);

        AuthResponseDto? LoadAuthData();

        void ClearAuthData();
    }
}