using Edemly.Contracts.Auth;
using Edemly.Server.Api.Controllers.Auth;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Application.Auth;
using Edemly.Server.Application.Welcome;
using Edemly.Server.Configuration;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Infrastructure.Auth;
using Edemly.Server.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using System.Text.Json;

namespace Edemly.Server.Tests.Unit.Auth;

public sealed class AuthControllerUnitTests
{
    [Test]
    public async Task GetLoginCode_Should_Return_BadRequest_When_Request_Model_Is_MissingAsync()
    {
        using var serverConnection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(serverConnection);
        var controller = CreateController(serverDb);

        var result = await controller.GetLoginCodeAsync(null!);

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That(GetMessage(result), Is.EqualTo("Email must be provided"));
    }

    [Test]
    public async Task GetLoginCode_Should_Return_BadRequest_When_Email_Format_Is_InvalidAsync()
    {
        using var serverConnection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(serverConnection);
        var controller = CreateController(serverDb);

        var result = await controller.GetLoginCodeAsync(new LoginRequestDto { Email = "invalid-email" });

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That(GetMessage(result), Is.EqualTo("Invalid email format"));
    }

    [Test]
    public async Task GetLoginCode_Should_Use_CurrentCompany_From_TenantProviderAsync()
    {
        using var serverConnection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(serverConnection);
        using var tenantFactory = new SqliteTenantDbContextFactory();
        var company = new Company { Name = "acme", DbName = "tenant_acme" };
        var tenantProvider = new TenantProvider { CurrentCompany = company };
        var controller = CreateController(serverDb, tenantProvider: tenantProvider, tenantDbFactory: tenantFactory);

        var result = await controller.GetLoginCodeAsync(new LoginRequestDto { Email = "blocked@example.test" });

        Assert.That(result, Is.TypeOf<BadRequestObjectResult>());
        Assert.That(GetMessage(result), Is.EqualTo("Email is not allowed for this company"));
    }

    [Test]
    public async Task GetLoginCode_Should_Return_ServerError_When_TenantAllowlistLookup_FailsAsync()
    {
        using var serverConnection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(serverConnection);
        var company = new Company { Name = "acme", DbName = "tenant_acme" };
        var tenantProvider = new TenantProvider { CurrentCompany = company };
        var controller = CreateController(
            serverDb,
            tenantProvider: tenantProvider,
            tenantDbFactory: new ThrowingTenantDbContextFactory());

        var result = await controller.GetLoginCodeAsync(new LoginRequestDto { Email = "user@example.test" });

        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That(GetStatusCode(result), Is.EqualTo(StatusCodes.Status500InternalServerError));
        Assert.That(GetMessage(result), Is.EqualTo("Server error while validating email for company"));
    }

    [Test]
    public async Task GetLoginCode_Should_Return_ServerError_When_EmailService_ThrowsAsync()
    {
        using var serverConnection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(serverConnection);
        var emailService = new Mock<IEmailService>();
        emailService.Setup(service => service.GenerateCodeAsync(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));

        var controller = CreateController(serverDb, emailService: emailService);

        var result = await controller.GetLoginCodeAsync(new LoginRequestDto { Email = "user@example.test" });

        Assert.That(result, Is.TypeOf<ObjectResult>());
        Assert.That(GetStatusCode(result), Is.EqualTo(StatusCodes.Status500InternalServerError));
        Assert.That(GetMessage(result), Does.Contain("Failed to send verification email"));
    }

    [Test]
    public async Task Login_Should_Request_Admin_Token_When_Admin_Email_Matches_ConfigurationAsync()
    {
        using var serverConnection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(serverConnection);
        var email = "admin@example.test";
        var loginInfo = new LoginInfo
        {
            Email = email,
            IsEmailVerified = true
        };
        serverDb.LoginInfos.Add(loginInfo);
        await serverDb.SaveChangesAsync();

        var user = new User
        {
            Username = "adminuser",
            LoginInfoId = loginInfo.Id,
            CreatedAt = DateTime.UtcNow,
            SubscriptionStatus = SubscriptionStatus.Free
        };
        serverDb.Users.Add(user);
        await serverDb.SaveChangesAsync();

        var emailService = new Mock<IEmailService>();
        emailService.Setup(service => service.VerifyCodeAsync(email, "123456"))
            .ReturnsAsync(true);

        var jwtService = new Mock<IJwtService>();
        jwtService.Setup(service => service.GenerateToken(user.Id, user.Username, email, true))
            .Returns("admin-token");

        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["AdminEmail"] = email
        });

        var controller = CreateController(
            serverDb,
            jwtService: jwtService,
            emailService: emailService,
            configuration: configuration);

        var result = await controller.LoginAsync(new LoginWithCodeDto
        {
            Email = email,
            Code = "123456"
        });

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        var okResult = (OkObjectResult)result;
        var authResponse = okResult.Value as AuthResponseDto;
        Assert.That(authResponse, Is.Not.Null);
        Assert.That(authResponse!.Token, Is.EqualTo("admin-token"));
        jwtService.Verify(service => service.GenerateToken(user.Id, user.Username, email, true), Times.Once);
    }

    [Test]
    public async Task Logout_Should_Return_Unauthorized_When_UserIdClaim_Is_InvalidAsync()
    {
        using var serverConnection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(serverConnection);
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim("userId", "not-a-number")
                },
                authenticationType: "Test"))
        };
        var controller = CreateController(serverDb, httpContext: httpContext);

        var result = await controller.LogoutAsync();

        Assert.That(result, Is.TypeOf<UnauthorizedResult>());
    }

    private static AuthController CreateController(
        ServerDbContext serverDb,
        Mock<IJwtService>? jwtService = null,
        Mock<IEmailService>? emailService = null,
        ITenantProvider? tenantProvider = null,
        IConfiguration? configuration = null,
        ITenantDbContextFactory? tenantDbFactory = null,
        DefaultHttpContext? httpContext = null)
    {
        var ownsJwtMock = jwtService == null;
        jwtService ??= new Mock<IJwtService>();
        if (ownsJwtMock)
        {
            jwtService.Setup(service => service.GenerateToken(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns("jwt-token");
        }

        var ownsEmailMock = emailService == null;
        emailService ??= new Mock<IEmailService>();
        if (ownsEmailMock)
        {
            emailService.Setup(service => service.GenerateCodeAsync(It.IsAny<string>()))
                .ReturnsAsync("123456");
            emailService.Setup(service => service.SendVerificationCodeAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            emailService.Setup(service => service.VerifyCodeAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
        }

        var resolvedConfiguration = configuration ?? CreateConfiguration();
        var authResponseFactory = new AuthResponseFactory(
            CreateJwtSettings(),
            jwtService.Object,
            resolvedConfiguration);
        var welcomeChatService = new WelcomeChatService(NullLogger<WelcomeChatService>.Instance);
        var authService = new AuthService(
            serverDb,
            NullLogger<AuthService>.Instance,
            emailService.Object,
            tenantProvider ?? new TenantProvider(),
            tenantDbFactory ?? new SqliteTenantDbContextFactory(),
            authResponseFactory,
            welcomeChatService);
        var controller = new AuthController(authService);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext ?? new DefaultHttpContext()
        };

        return controller;
    }

    private static JwtSettings CreateJwtSettings()
    {
        return new JwtSettings
        {
            Key = "test-key-123456789012345678901234567890",
            Issuer = "Edemly.Tests",
            Audience = "Edemly.Tests",
            ExpiresInMinutes = 60,
            RefreshTokenExpiresInDays = 14
        };
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?>? values = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();
    }

    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static ServerDbContext CreateServerDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ServerDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new ServerDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static string? GetMessage(IActionResult result)
    {
        if (result is not ObjectResult objectResult || objectResult.Value == null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(objectResult.Value));
        return document.RootElement.TryGetProperty("message", out var messageElement)
            ? messageElement.GetString()
            : null;
    }

    private static int? GetStatusCode(IActionResult result)
    {
        return result is ObjectResult objectResult
            ? objectResult.StatusCode
            : null;
    }

    private sealed class ThrowingTenantDbContextFactory : ITenantDbContextFactory
    {
        public CompanyDbContext CreateCompanyDbContext(Company company)
        {
            throw new InvalidOperationException("Tenant DB unavailable");
        }
    }

    private sealed class SqliteTenantDbContextFactory : ITenantDbContextFactory, IDisposable
    {
        private readonly SqliteConnection _connection = CreateOpenConnection();

        public SqliteTenantDbContextFactory()
        {
            using var bootstrapContext = CreateDbContext();
            bootstrapContext.Database.EnsureCreated();
        }

        public CompanyDbContext CreateCompanyDbContext(Company company)
        {
            return CreateDbContext();
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private CompanyDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<CompanyDbContext>()
                .UseSqlite(_connection)
                .Options;

            return new CompanyDbContext(options);
        }
    }
}