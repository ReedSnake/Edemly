using Edemly.Client.Api.Core;
using Edemly.Contracts.Auth;
using Edemly.Contracts.Users;

namespace Edemly.Client.Api.Auth;

public sealed class AuthApiClient : ApiClientBase, IAuthApiClient
{
    public AuthApiClient(ApiClientContext context) : base(context)
    {
    }

    public Task<bool> GetVerificationCodeAsync(string email)
    {
        var request = new LoginRequestDto { Email = email };
        return PostAsync("api/auth/get-code", request);
    }
    public Task<AuthResponseDto?> LoginAsync(string email, string code)
    {
        var request = new LoginWithCodeDto
        {
            Email = email,
            Code = code
        };

        return PostAsync<LoginWithCodeDto, AuthResponseDto>("api/auth/login", request);
    }

    public Task<AuthResponseDto?> RegisterAsync(string email, string code, string username)
    {
        var request = new RegistrationWithCodeDto
        {
            Email = email,
            Code = code,
            Username = username
        };

        return PostAsync<RegistrationWithCodeDto, AuthResponseDto>("api/auth/register", request);
    }

    public Task<AuthResponseDto?> SessionLoginAsync(string sessionToken)
    {
        var request = new SessionLoginDto { SessionToken = sessionToken };
        return PostAsync<SessionLoginDto, AuthResponseDto>("api/auth/session-login", request);
    }

    public Task<bool> LogoutAsync()
    {
        return PostAsync("api/auth/logout");
    }
}