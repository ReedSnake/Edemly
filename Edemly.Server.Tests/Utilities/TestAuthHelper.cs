using Edemly.Contracts.Auth;
using Edemly.Server.Infrastructure.Auth;
using Edemly.Server.Tests.Infrastructure;
using Edemly.Server.Tests.TestData;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Net.Http.Json;

namespace Edemly.Server.Tests.Utilities;

public sealed record TestAuthSession(AuthTestUser User, AuthResponseDto AuthResponse)
{
    public string JwtToken => AuthResponse.Token;
}

public sealed record AuthorizedTestClient(HttpClient Client, TestAuthSession Session) : IDisposable
{
    public void Dispose()
    {
        Client.Dispose();
    }
}

public static class TestAuthHelper
{
    public static Task<TestAuthSession> CreateTestUserAsync(
        HttpClient client,
        IServiceProvider services,
        AuthTestUser? user = null,
        string? routePrefix = null,
        CancellationToken cancellationToken = default)
    {
        return RegisterAsync(client, services, user, routePrefix, cancellationToken);
    }

    public static async Task<TestAuthSession> RegisterAsync(
        HttpClient client,
        IServiceProvider services,
        AuthTestUser? user = null,
        string? routePrefix = null,
        CancellationToken cancellationToken = default)
    {
        user ??= AuthTestData.CreateUser();

        await RequestVerificationCodeAsync(client, user.Email, routePrefix, cancellationToken);
        var code = GetTestEmailService(services).GetCode(user.Email);

        using var response = await client.PostAsJsonAsync(
            BuildRoute(routePrefix, "/api/auth/register"),
            new RegistrationWithCodeDto
            {
                Email = user.Email,
                Username = user.Username,
                Code = code
            },
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        var authResponse = await ReadAuthResponseAsync(response, cancellationToken);

        return new TestAuthSession(user, authResponse);
    }

    public static async Task<TestAuthSession> LoginAsync(
        HttpClient client,
        IServiceProvider services,
        AuthTestUser user,
        string? routePrefix = null,
        CancellationToken cancellationToken = default)
    {
        await RequestVerificationCodeAsync(client, user.Email, routePrefix, cancellationToken);
        var code = GetTestEmailService(services).GetCode(user.Email);

        using var response = await client.PostAsJsonAsync(
            BuildRoute(routePrefix, "/api/auth/login"),
            new LoginWithCodeDto
            {
                Email = user.Email,
                Code = code
            },
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        var authResponse = await ReadAuthResponseAsync(response, cancellationToken);

        return new TestAuthSession(user, authResponse);
    }

    public static async Task<string> RegisterAndGetJwtTokenAsync(
        HttpClient client,
        IServiceProvider services,
        AuthTestUser? user = null,
        string? routePrefix = null,
        CancellationToken cancellationToken = default)
    {
        var session = await RegisterAsync(client, services, user, routePrefix, cancellationToken);
        return session.JwtToken;
    }

    public static async Task<string> LoginAndGetJwtTokenAsync(
        HttpClient client,
        IServiceProvider services,
        AuthTestUser user,
        string? routePrefix = null,
        CancellationToken cancellationToken = default)
    {
        var session = await LoginAsync(client, services, user, routePrefix, cancellationToken);
        return session.JwtToken;
    }

    public static async Task<AuthorizedTestClient> CreateAuthorizedClientAsync(
        CustomWebApplicationFactory factory,
        AuthTestUser? user = null,
        string? routePrefix = null,
        CancellationToken cancellationToken = default)
    {
        var client = factory.CreateClient();
        var session = await RegisterAsync(client, factory.Services, user, routePrefix, cancellationToken);
        client.AddBearerToken(session.JwtToken);

        return new AuthorizedTestClient(client, session);
    }

    private static async Task RequestVerificationCodeAsync(
        HttpClient client,
        string email,
        string? routePrefix,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            BuildRoute(routePrefix, "/api/auth/get-code"),
            new LoginRequestDto { Email = email },
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static TestEmailService GetTestEmailService(IServiceProvider services)
    {
        return services.GetRequiredService<TestEmailService>();
    }

    private static string BuildRoute(string? routePrefix, string route)
    {
        if (string.IsNullOrWhiteSpace(routePrefix))
        {
            return route;
        }

        var normalizedPrefix = routePrefix.Trim('/');
        return $"/{normalizedPrefix}{route}";
    }

    private static async Task<AuthResponseDto> ReadAuthResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>(cancellationToken);

        return authResponse ?? throw new InvalidOperationException("Auth response body was empty.");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Expected success status code but received {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}",
            inner: null,
            response.StatusCode);
    }
}

public sealed class TestEmailService : IEmailService
{
    private readonly ConcurrentDictionary<string, string> _codes = new();

    public Task<string> GenerateCodeAsync(string email)
    {
        var code = Random.Shared.Next(100000, 999999).ToString();
        _codes[NormalizeEmail(email)] = code;

        return Task.FromResult(code);
    }

    public Task<bool> VerifyCodeAsync(string email, string code)
    {
        var normalizedEmail = NormalizeEmail(email);

        if (!_codes.TryGetValue(normalizedEmail, out var storedCode) || storedCode != code)
        {
            return Task.FromResult(false);
        }

        _codes.TryRemove(normalizedEmail, out _);
        return Task.FromResult(true);
    }

    public Task SendVerificationCodeAsync(string email, string code)
    {
        return Task.CompletedTask;
    }

    public string GetCode(string email)
    {
        var normalizedEmail = NormalizeEmail(email);

        return _codes.TryGetValue(normalizedEmail, out var code)
            ? code
            : throw new InvalidOperationException($"No verification code was generated for '{normalizedEmail}'.");
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}