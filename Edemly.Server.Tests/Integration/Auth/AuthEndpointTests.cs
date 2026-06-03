using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Edemly.Contracts.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Edemly.Server.Data;
using Edemly.Server.Tests.Infrastructure;
using Edemly.Server.Tests.TestData;
using Edemly.Server.Tests.Utilities;

namespace Edemly.Server.Tests.Integration.Auth;

public sealed class AuthEndpointTests
{
    [Test]
    public async Task Register_Should_Create_User_When_Request_Is_Valid()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var testUser = AuthTestData.CreateUser();

        var session = await TestAuthHelper.RegisterAsync(client, factory.Services, testUser);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var loginInfo = await dbContext.LoginInfos
            .Include(login => login.User)
            .SingleOrDefaultAsync(login => login.Email == testUser.Email);

        Assert.Multiple(() =>
        {
            Assert.That(loginInfo, Is.Not.Null);
            Assert.That(loginInfo!.IsEmailVerified, Is.True);
            Assert.That(loginInfo.User, Is.Not.Null);
            Assert.That(loginInfo.User!.Id, Is.EqualTo(session.AuthResponse.UserId));
            Assert.That(loginInfo.User.Username, Is.EqualTo(testUser.Username));
            Assert.That(session.AuthResponse.Username, Is.EqualTo(testUser.Username));
            Assert.That(loginInfo.User.FirstName, Is.Null);
            Assert.That(loginInfo.User.LastName, Is.Null);
            Assert.That(session.AuthResponse.Email, Is.EqualTo(testUser.Email));
            Assert.That(session.JwtToken, Is.Not.Empty);
        });
    }

    [Test]
    public async Task Register_Should_Return_BadRequest_When_Email_Already_Exists()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var testUser = AuthTestData.CreateUser();
        await TestAuthHelper.RegisterAsync(client, factory.Services, testUser);

        await RequestVerificationCodeAsync(client, testUser.Email);
        var code = GetVerificationCode(factory.Services, testUser.Email);

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegistrationWithCodeDto
            {
                Email = testUser.Email,
                Username = testUser.Username,
                Code = code
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Register_Should_Return_BadRequest_When_Request_Is_Invalid()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegistrationWithCodeDto
            {
                Email = "not-an-email",
                Username = "ab",
                Code = string.Empty
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Register_Should_Return_Unauthorized_When_Verification_Code_Is_Invalid()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var testUser = AuthTestData.CreateUser();

        await RequestVerificationCodeAsync(client, testUser.Email);

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegistrationWithCodeDto
            {
                Email = testUser.Email,
                Username = testUser.Username,
                Code = "000000"
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Register_Should_Create_Welcome_Chat_And_Membership()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var session = await TestAuthHelper.RegisterAsync(client, factory.Services);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var welcomeChat = await dbContext.Chats
            .Include(chat => chat.ChatMembers)
            .SingleOrDefaultAsync(chat => chat.Name == "Edemly" && chat.Type == Edemly.Server.Data.Entities.ChatType.Group);

        Assert.Multiple(() =>
        {
            Assert.That(welcomeChat, Is.Not.Null);
            Assert.That(welcomeChat!.ChatMembers.Select(member => member.UserId), Does.Contain(session.AuthResponse.UserId));
        });
    }

    [Test]
    public async Task Register_Should_Allow_Empty_Username()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var testUser = AuthTestData.CreateUser("empty-username");

        await RequestVerificationCodeAsync(client, testUser.Email);
        var code = GetVerificationCode(factory.Services, testUser.Email);

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegistrationWithCodeDto
            {
                Email = testUser.Email,
                Username = null,
                Code = code
            });
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var createdUser = await dbContext.LoginInfos
            .Include(login => login.User)
            .SingleAsync(login => login.Email == testUser.Email);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(authResponse, Is.Not.Null);
            Assert.That(createdUser.User, Is.Not.Null);
            Assert.That(createdUser.User!.Username, Is.Null);
            Assert.That(authResponse!.Username, Is.Empty);
            Assert.That(createdUser.User.FirstName, Is.Null);
            Assert.That(createdUser.User.LastName, Is.Null);
        });
    }

    [Test]
    public async Task Register_Should_Return_BadRequest_When_Username_Already_Exists()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var firstUser = AuthTestData.CreateUser("duplicate-username-one");
        var secondUser = AuthTestData.CreateUser("duplicate-username-two");

        await RequestVerificationCodeAsync(client, firstUser.Email);
        var firstCode = GetVerificationCode(factory.Services, firstUser.Email);
        using (var firstResponse = await client.PostAsJsonAsync(
                   "/api/auth/register",
                   new RegistrationWithCodeDto
                   {
                       Email = firstUser.Email,
                       Username = "shareduser",
                       Code = firstCode
                   }))
        {
            Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        await RequestVerificationCodeAsync(client, secondUser.Email);
        var secondCode = GetVerificationCode(factory.Services, secondUser.Email);

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegistrationWithCodeDto
            {
                Email = secondUser.Email,
                Username = "shareduser",
                Code = secondCode
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Register_Should_Not_Derive_ProfileNames_From_Username()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var testUser = AuthTestData.CreateUser("explicit-username");

        await RequestVerificationCodeAsync(client, testUser.Email);
        var code = GetVerificationCode(factory.Services, testUser.Email);

        using var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegistrationWithCodeDto
            {
                Email = testUser.Email,
                Username = "John Smith",
                Code = code
            });

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var createdUser = await dbContext.LoginInfos
            .Include(login => login.User)
            .SingleAsync(login => login.Email == testUser.Email);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(createdUser.User, Is.Not.Null);
            Assert.That(createdUser.User!.Username, Is.EqualTo("John Smith"));
            Assert.That(createdUser.User.FirstName, Is.Null);
            Assert.That(createdUser.User.LastName, Is.Null);
        });
    }

    [Test]
    public async Task Login_Should_Return_Token_When_Credentials_Are_Valid()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var registeredSession = await TestAuthHelper.RegisterAsync(client, factory.Services);

        var loginSession = await TestAuthHelper.LoginAsync(client, factory.Services, registeredSession.User);

        Assert.Multiple(() =>
        {
            Assert.That(loginSession.AuthResponse.UserId, Is.EqualTo(registeredSession.AuthResponse.UserId));
            Assert.That(loginSession.AuthResponse.Email, Is.EqualTo(registeredSession.User.Email));
            Assert.That(loginSession.JwtToken, Is.Not.Empty);
            Assert.That(loginSession.JwtToken.Split('.'), Has.Length.EqualTo(3));
        });
    }

    [Test]
    public async Task Login_Should_Return_Unauthorized_When_Password_Is_Wrong()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var registeredSession = await TestAuthHelper.RegisterAsync(client, factory.Services);

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginWithCodeDto
            {
                Email = registeredSession.User.Email,
                Code = "000000"
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Login_Should_Return_Unauthorized_When_Email_Does_Not_Exist()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var testUser = AuthTestData.CreateUser();

        await RequestVerificationCodeAsync(client, testUser.Email);
        var code = GetVerificationCode(factory.Services, testUser.Email);

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginWithCodeDto
            {
                Email = testUser.Email,
                Code = code
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Login_Should_Not_Return_Password_Or_PasswordHash()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var registeredSession = await TestAuthHelper.RegisterAsync(client, factory.Services);

        await RequestVerificationCodeAsync(client, registeredSession.User.Email);
        var code = GetVerificationCode(factory.Services, registeredSession.User.Email);

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginWithCodeDto
            {
                Email = registeredSession.User.Email,
                Code = code
            });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(JsonContainsProperty(body, "password"), Is.False);
            Assert.That(JsonContainsProperty(body, "passwordHash"), Is.False);
        });
    }

    [Test]
    public async Task Login_Should_Return_BadRequest_When_Request_Is_Invalid()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginWithCodeDto
            {
                Email = "not-an-email",
                Code = string.Empty
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task SessionLogin_Should_Return_Token_When_SessionToken_Is_Valid()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var session = await TestAuthHelper.RegisterAsync(client, factory.Services);

        using var response = await client.PostAsJsonAsync(
            "/api/auth/session-login",
            new SessionLoginDto
            {
                SessionToken = session.AuthResponse.SessionToken
            });
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(authResponse, Is.Not.Null);
            Assert.That(authResponse!.UserId, Is.EqualTo(session.AuthResponse.UserId));
            Assert.That(authResponse.Email, Is.EqualTo(session.User.Email));
            Assert.That(authResponse.Token, Is.Not.Empty);
            Assert.That(authResponse.SessionToken, Is.EqualTo(session.AuthResponse.SessionToken));
        });
    }

    [Test]
    public async Task SessionLogin_Should_Return_Unauthorized_When_SessionToken_Is_Invalid()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/session-login",
            new SessionLoginDto
            {
                SessionToken = Guid.NewGuid().ToString("N")
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task SessionLogin_Should_Return_Unauthorized_When_SessionToken_Is_Expired()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var session = await TestAuthHelper.RegisterAsync(client, factory.Services);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            var storedSession = await dbContext.Sessions.SingleAsync(item => item.UserId == session.AuthResponse.UserId);
            storedSession.ExpirationTime = DateTime.UtcNow.AddMinutes(-5);
            await dbContext.SaveChangesAsync();
        }

        using var response = await client.PostAsJsonAsync(
            "/api/auth/session-login",
            new SessionLoginDto
            {
                SessionToken = session.AuthResponse.SessionToken
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Logout_Should_Remove_Session_When_User_Is_Authenticated()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var session = await TestAuthHelper.RegisterAsync(client, factory.Services);
        client.AddBearerToken(session.JwtToken);

        using var response = await client.PostAsync("/api/auth/logout", content: null);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var sessionExists = await dbContext.Sessions.AnyAsync(item => item.UserId == session.AuthResponse.UserId);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(sessionExists, Is.False);
        });
    }

    [Test]
    public async Task Protected_Endpoint_Should_Return_Unauthorized_Without_Token()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/user/me");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Protected_Endpoint_Should_Return_Success_With_Valid_Token()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var session = await TestAuthHelper.RegisterAsync(client, factory.Services);
        client.AddBearerToken(session.JwtToken);

        using var response = await client.GetAsync("/api/user/me");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static async Task RequestVerificationCodeAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/auth/get-code",
            new LoginRequestDto { Email = email });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    private static string GetVerificationCode(IServiceProvider services, string email)
    {
        return services.GetRequiredService<TestEmailService>().GetCode(email);
    }

    private static bool JsonContainsProperty(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        return JsonContainsProperty(document.RootElement, propertyName);
    }

    private static bool JsonContainsProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                    || JsonContainsProperty(property.Value, propertyName))
                {
                    return true;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (JsonContainsProperty(item, propertyName))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
