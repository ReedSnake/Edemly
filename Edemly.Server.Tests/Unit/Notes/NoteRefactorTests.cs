using Edemly.Contracts.Notes;
using Edemly.Server.Api.Controllers.Notes;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Api.Services;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

namespace Edemly.Server.Tests.Unit.Notes;

public sealed class NoteRefactorTests
{
    [Test]
    public async Task Controller_GetById_Should_Return_Forbid_When_Service_Returns_ForbiddenAsync()
    {
        var service = new Mock<INoteService>();
        service.Setup(x => x.GetByIdAsync(8, 42))
            .ReturnsAsync(ServiceResult<NoteDto>.Forbidden());

        var controller = new NoteController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = TestPrincipal(8)
                }
            }
        };

        var result = await controller.GetByIdAsync(42);

        Assert.That(result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task Controller_GetCount_Should_Map_Service_Data_To_Count_ObjectAsync()
    {
        var service = new Mock<INoteService>();
        service.Setup(x => x.GetCountAsync(8))
            .ReturnsAsync(ServiceResult<int>.Ok(7));

        var controller = new NoteController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = TestPrincipal(8)
                }
            }
        };

        var result = await controller.GetCountAsync();

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        Assert.That(GetJsonProperty(result, "count"), Is.EqualTo("7"));
    }

    [Test]
    public async Task Service_GetById_Should_Return_Forbidden_When_Note_Belongs_To_Other_UserAsync()
    {
        using var connection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(connection);

        var owner = await CreateUserAsync(serverDb, "owner@example.test");
        var subjectUser = await CreateUserAsync(serverDb, "subject@example.test");
        var requester = await CreateUserAsync(serverDb, "requester@example.test");

        serverDb.Notes.Add(new Note
        {
            CreatorId = owner.Id,
            UserId = subjectUser.Id,
            Content = "secret"
        });
        await serverDb.SaveChangesAsync();

        var service = new NoteService(
            serverDb,
            NullLogger<NoteService>.Instance,
            new TenantProvider(),
            new ThrowingTenantDbContextFactory());

        var result = await service.GetByIdAsync(requester.Id, serverDb.Notes.Single().Id);

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
    }

    [Test]
    public async Task Service_GetById_Should_Return_NotFound_When_Note_Does_Not_ExistAsync()
    {
        using var connection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(connection);

        var requester = await CreateUserAsync(serverDb, "requester@example.test");
        var service = new NoteService(
            serverDb,
            NullLogger<NoteService>.Instance,
            new TenantProvider(),
            new ThrowingTenantDbContextFactory());

        var result = await service.GetByIdAsync(requester.Id, 999);

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    private static string? GetJsonProperty(IActionResult result, string propertyName)
    {
        if (result is not ObjectResult objectResult || objectResult.Value == null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(objectResult.Value));
        return document.RootElement.TryGetProperty(propertyName, out var element)
            ? element.ToString()
            : null;
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

    private static async Task<User> CreateUserAsync(ServerDbContext serverDb, string email)
    {
        var loginInfo = new LoginInfo
        {
            Email = email,
            IsEmailVerified = true
        };

        serverDb.LoginInfos.Add(loginInfo);
        await serverDb.SaveChangesAsync();

        var user = new User
        {
            LoginInfoId = loginInfo.Id,
            Username = email.Split('@')[0],
            CreatedAt = DateTime.UtcNow,
            SubscriptionStatus = SubscriptionStatus.Free
        };

        serverDb.Users.Add(user);
        await serverDb.SaveChangesAsync();
        return user;
    }

    private sealed class ThrowingTenantDbContextFactory : ITenantDbContextFactory
    {
        public CompanyDbContext CreateCompanyDbContext(Company company)
        {
            throw new InvalidOperationException("Tenant DB should not be used in this test.");
        }
    }

    private static System.Security.Claims.ClaimsPrincipal TestPrincipal(int userId)
    {
        return new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[]
                {
                    new System.Security.Claims.Claim("userId", userId.ToString())
                },
                "test"));
    }
}