using Edemly.Server.Tests.Infrastructure;
using Edemly.Server.Tests.Utilities;

namespace Edemly.Server.Tests.Integration.Auth;

public sealed class AuthHelperTests
{
    [Test]
    public async Task TestAuthHelper_Should_Register_Login_And_Add_Bearer_TokenAsync()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var registeredSession = await TestAuthHelper.RegisterAsync(client, factory.Services);
        var loginSession = await TestAuthHelper.LoginAsync(client, factory.Services, registeredSession.User);

        client.AddBearerToken(loginSession.JwtToken);

        Assert.Multiple(() =>
        {
            Assert.That(registeredSession.AuthResponse.UserId, Is.GreaterThan(0));
            Assert.That(registeredSession.JwtToken, Is.Not.Empty);
            Assert.That(loginSession.JwtToken, Is.Not.Empty);
            Assert.That(client.DefaultRequestHeaders.Authorization?.Scheme, Is.EqualTo("Bearer"));
            Assert.That(client.DefaultRequestHeaders.Authorization?.Parameter, Is.EqualTo(loginSession.JwtToken));
        });
    }
}
