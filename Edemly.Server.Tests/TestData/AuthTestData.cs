namespace Edemly.Server.Tests.TestData;

public sealed record AuthTestUser(string Email, string Username);

public static class AuthTestData
{
    public static AuthTestUser CreateUser(string? suffix = null)
    {
        suffix ??= Guid.NewGuid().ToString("N")[..12];

        return new AuthTestUser(
            Email: $"test-user-{suffix}@example.test",
            Username: $"User{suffix}");
    }
}