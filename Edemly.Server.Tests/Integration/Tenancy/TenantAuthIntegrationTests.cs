using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Edemly.Contracts.Auth;
using Edemly.Server.Data;
using Edemly.Server.Services;
using Edemly.Server.Tests.Infrastructure;
using Edemly.Server.Tests.TestData;
using Edemly.Server.Tests.Utilities;

namespace Edemly.Server.Tests.Integration.Tenancy;

public sealed class TenantAuthIntegrationTests
{
    [Test]
    public async Task TenantRegister_Should_Create_User_In_Tenant_Database_When_Email_Is_Allowed()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var company = await TestTenantHelper.CreateCompanyAsync(factory, "acme-register");
        var tenantUser = AuthTestData.CreateUser("tenantregister");
        await TestTenantHelper.AllowEmailAsync(factory.Services, company, tenantUser.Email);

        var session = await TestAuthHelper.RegisterAsync(client, factory.Services, tenantUser, company.Name);

        using var scope = factory.Services.CreateScope();
        var masterDb = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var tenantFactory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
        await using var tenantDb = tenantFactory.CreateCompanyDbContext(company);

        var masterLoginExists = await masterDb.LoginInfos.AnyAsync(item => item.Email == tenantUser.Email);
        var tenantLogin = await tenantDb.LoginInfos
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Email == tenantUser.Email);
        var tenantSession = await tenantDb.Sessions.SingleOrDefaultAsync(item => item.UserId == session.AuthResponse.UserId);

        Assert.Multiple(() =>
        {
            Assert.That(masterLoginExists, Is.False);
            Assert.That(tenantLogin, Is.Not.Null);
            Assert.That(tenantLogin!.User, Is.Not.Null);
            Assert.That(tenantLogin.User!.Id, Is.EqualTo(session.AuthResponse.UserId));
            Assert.That(tenantLogin.User.Username, Is.EqualTo(tenantUser.Username));
            Assert.That(tenantLogin.User.FirstName, Is.Null);
            Assert.That(tenantLogin.User.LastName, Is.Null);
            Assert.That(session.AuthResponse.Email, Is.EqualTo(tenantUser.Email));
            Assert.That(session.AuthResponse.SessionToken, Is.Not.Empty);
            Assert.That(tenantSession, Is.Not.Null);
        });
    }

    [Test]
    public async Task TenantRegister_Should_Allow_Empty_Username_When_Email_Is_Allowed()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var company = await TestTenantHelper.CreateCompanyAsync(factory, "acme-empty-username");
        var tenantUser = AuthTestData.CreateUser("tenant-empty-username");
        await TestTenantHelper.AllowEmailAsync(factory.Services, company, tenantUser.Email);

        using (var codeResponse = await client.PostAsJsonAsync(
                   $"/{company.Name}/api/auth/get-code",
                   new LoginRequestDto { Email = tenantUser.Email }))
        {
            Assert.That(codeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        var code = factory.Services.GetRequiredService<TestEmailService>().GetCode(tenantUser.Email);

        using var response = await client.PostAsJsonAsync(
            $"/{company.Name}/api/auth/register",
            new RegistrationWithCodeDto
            {
                Email = tenantUser.Email,
                Username = null,
                Code = code
            });
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        using var scope = factory.Services.CreateScope();
        var tenantFactory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
        await using var tenantDb = tenantFactory.CreateCompanyDbContext(company);
        var tenantLogin = await tenantDb.LoginInfos
            .Include(item => item.User)
            .SingleAsync(item => item.Email == tenantUser.Email);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(authResponse, Is.Not.Null);
            Assert.That(authResponse!.Username, Is.Empty);
            Assert.That(tenantLogin.User, Is.Not.Null);
            Assert.That(tenantLogin.User!.Username, Is.Null);
            Assert.That(tenantLogin.User.FirstName, Is.Null);
            Assert.That(tenantLogin.User.LastName, Is.Null);
        });
    }

    [Test]
    public async Task TenantRegister_Should_Return_BadRequest_When_Email_Is_Not_Allowed()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var company = await TestTenantHelper.CreateCompanyAsync(factory, "acme-register-denied");
        var disallowedUser = AuthTestData.CreateUser("tenantdenied");

        using (var codeResponse = await client.PostAsJsonAsync(
            "/api/auth/get-code",
            new LoginRequestDto { Email = disallowedUser.Email }))
        {
            Assert.That(codeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        var code = factory.Services.GetRequiredService<TestEmailService>().GetCode(disallowedUser.Email);

        using var response = await client.PostAsJsonAsync(
            $"/{company.Name}/api/auth/register",
            new RegistrationWithCodeDto
            {
                Email = disallowedUser.Email,
                Username = disallowedUser.Username,
                Code = code
            });
        var body = await response.Content.ReadAsStringAsync();

        using var scope = factory.Services.CreateScope();
        var masterDb = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var tenantFactory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
        await using var tenantDb = tenantFactory.CreateCompanyDbContext(company);

        var masterLoginExists = await masterDb.LoginInfos.AnyAsync(item => item.Email == disallowedUser.Email);
        var tenantLoginExists = await tenantDb.LoginInfos.AnyAsync(item => item.Email == disallowedUser.Email);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(GetMessage(body), Is.EqualTo("Email is not allowed for registration in this company"));
            Assert.That(masterLoginExists, Is.False);
            Assert.That(tenantLoginExists, Is.False);
        });
    }

    [Test]
    public async Task TenantRegister_Should_Create_Welcome_Chat_And_Membership()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var company = await TestTenantHelper.CreateCompanyAsync(factory, "acme-welcome");
        var tenantUser = AuthTestData.CreateUser("tenantwelcome");
        await TestTenantHelper.AllowEmailAsync(factory.Services, company, tenantUser.Email);

        var session = await TestAuthHelper.RegisterAsync(client, factory.Services, tenantUser, company.Name);

        using var scope = factory.Services.CreateScope();
        var tenantFactory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
        await using var tenantDb = tenantFactory.CreateCompanyDbContext(company);
        var welcomeChat = await tenantDb.Chats
            .Include(chat => chat.ChatMembers)
            .SingleOrDefaultAsync(chat => chat.Name == "Edemly" && chat.Type == Edemly.Server.Data.Entities.ChatType.Group);

        Assert.Multiple(() =>
        {
            Assert.That(welcomeChat, Is.Not.Null);
            Assert.That(welcomeChat!.ChatMembers.Select(member => member.UserId), Does.Contain(session.AuthResponse.UserId));
        });
    }

    [Test]
    public async Task TenantLogin_Should_Return_Token_When_User_Exists_In_Tenant_Database()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var company = await TestTenantHelper.CreateCompanyAsync(factory, "acme-login");
        var tenantUser = AuthTestData.CreateUser("tenantlogin");
        await TestTenantHelper.AllowEmailAsync(factory.Services, company, tenantUser.Email);
        var registeredSession = await TestAuthHelper.RegisterAsync(client, factory.Services, tenantUser, company.Name);

        var loginSession = await TestAuthHelper.LoginAsync(client, factory.Services, tenantUser, company.Name);

        Assert.Multiple(() =>
        {
            Assert.That(loginSession.AuthResponse.UserId, Is.EqualTo(registeredSession.AuthResponse.UserId));
            Assert.That(loginSession.AuthResponse.Email, Is.EqualTo(tenantUser.Email));
            Assert.That(loginSession.JwtToken, Is.Not.Empty);
            Assert.That(loginSession.AuthResponse.SessionToken, Is.Not.Empty);
        });
    }

    [Test]
    public async Task TenantLogin_Should_Return_Unauthorized_When_User_Does_Not_Exist_In_Tenant_Database()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var company = await TestTenantHelper.CreateCompanyAsync(factory, "acme-login-missing");
        var tenantUser = AuthTestData.CreateUser("tenantmissing");
        await TestTenantHelper.AllowEmailAsync(factory.Services, company, tenantUser.Email);

        using (var codeResponse = await client.PostAsJsonAsync(
                   $"/{company.Name}/api/auth/get-code",
                   new LoginRequestDto { Email = tenantUser.Email }))
        {
            Assert.That(codeResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        var code = factory.Services.GetRequiredService<TestEmailService>().GetCode(tenantUser.Email);

        using var response = await client.PostAsJsonAsync(
            $"/{company.Name}/api/auth/login",
            new LoginWithCodeDto
            {
                Email = tenantUser.Email,
                Code = code
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task TenantSessionLogin_Should_Return_Token_When_SessionToken_Is_Valid_For_Tenant()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var company = await TestTenantHelper.CreateCompanyAsync(factory, "acme-session");
        var tenantUser = AuthTestData.CreateUser("tenantsession");
        await TestTenantHelper.AllowEmailAsync(factory.Services, company, tenantUser.Email);
        var registeredSession = await TestAuthHelper.RegisterAsync(client, factory.Services, tenantUser, company.Name);

        using var response = await client.PostAsJsonAsync(
            $"/{company.Name}/api/auth/session-login",
            new SessionLoginDto
            {
                SessionToken = registeredSession.AuthResponse.SessionToken
            });
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(authResponse, Is.Not.Null);
            Assert.That(authResponse!.UserId, Is.EqualTo(registeredSession.AuthResponse.UserId));
            Assert.That(authResponse.Email, Is.EqualTo(tenantUser.Email));
            Assert.That(authResponse.Token, Is.Not.Empty);
            Assert.That(authResponse.SessionToken, Is.Not.Empty);
        });
    }

    [Test]
    public async Task TenantSessionLogin_Should_Return_Unauthorized_When_SessionToken_Is_Invalid_For_Tenant()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var company = await TestTenantHelper.CreateCompanyAsync(factory, "acme-session-invalid");

        using var response = await client.PostAsJsonAsync(
            $"/{company.Name}/api/auth/session-login",
            new SessionLoginDto
            {
                SessionToken = Guid.NewGuid().ToString("N")
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task TenantLogout_Should_Remove_Session_When_User_Is_Authenticated()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var company = await TestTenantHelper.CreateCompanyAsync(factory, "acme-logout");
        var tenantUser = AuthTestData.CreateUser("tenantlogout");
        await TestTenantHelper.AllowEmailAsync(factory.Services, company, tenantUser.Email);
        var session = await TestAuthHelper.RegisterAsync(client, factory.Services, tenantUser, company.Name);
        client.AddBearerToken(session.JwtToken);

        using var response = await client.PostAsync($"/{company.Name}/api/auth/logout", content: null);

        using var scope = factory.Services.CreateScope();
        var tenantFactory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
        await using var tenantDb = tenantFactory.CreateCompanyDbContext(company);
        var sessionExists = await tenantDb.Sessions.AnyAsync(item => item.UserId == session.AuthResponse.UserId);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(sessionExists, Is.False);
        });
    }

    [Test]
    public async Task TenantGetCode_Should_Resolve_Company_From_QueryParameter_When_Tenant_Is_Provided()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var company = await TestTenantHelper.CreateCompanyAsync(factory, "acme-query");

        using var response = await client.PostAsJsonAsync(
            $"/api/auth/get-code?tenant={company.Name}",
            new LoginRequestDto
            {
                Email = "blocked@example.test"
            });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(GetMessage(body), Is.EqualTo("Email is not allowed for this company"));
        });
    }

    private static string? GetMessage(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("message", out var messageElement)
            ? messageElement.GetString()
            : null;
    }
}
