using Edemly.Contracts.Auth;

namespace Edemly.Client.Api.Auth;

public interface IAuthApiClient
{
    Task<bool> GetVerificationCodeAsync(string email);

    Task<AuthResponseDto?> LoginAsync(string email, string code);

    Task<AuthResponseDto?> RegisterAsync(string email, string code, string username);

    Task<AuthResponseDto?> SessionLoginAsync(string sessionToken);

    Task<bool> LogoutAsync();

}