using Edemly.Contracts.Users;
using Edemly.Server.Api.Controllers.Users;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Application.Common;
using Edemly.Server.Application.Users;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

namespace Edemly.Server.Tests.Unit.Users;

public sealed class UserRefactorTests
{
    [Test]
    public async Task Controller_DeleteUser_Should_Return_Forbid_When_Service_Returns_ForbiddenAsync()
    {
        var service = new Mock<IUserService>();
        service.Setup(x => x.DeleteAsync(11, 7))
            .ReturnsAsync(ServiceResult.Forbidden());

        var controller = new UserController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = TestPrincipal(11)
                }
            }
        };

        var result = await controller.DeleteAsync(7);

        Assert.That(result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task Controller_GetUsersBatch_Should_Return_Users_And_Count_From_ServiceAsync()
    {
        var service = new Mock<IUserService>();
        service.Setup(x => x.GetUsersBatchAsync(It.IsAny<List<int>>()))
            .ReturnsAsync(ServiceResult<List<UserDto>>.Ok(new List<UserDto>
            {
                new() { Id = 4, Username = "alice" }
            }));

        var controller = new UserController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = TestPrincipal(11)
                }
            }
        };

        var result = await controller.GetUsersBatchAsync(new List<int> { 4 });

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);

        var json = JsonSerializer.Serialize(ok!.Value);
        Assert.That(json, Does.Contain("\"Count\":1"));
        Assert.That(json, Does.Contain("\"Users\""));
    }

    [Test]
    public async Task Service_DeleteUser_Should_Return_Forbidden_When_Deleting_Other_UserAsync()
    {
        using var connection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(connection);

        var requester = await CreateUserAsync(serverDb, "requester@example.test");
        var target = await CreateUserAsync(serverDb, "target@example.test");

        var service = CreateService(serverDb);
        var result = await service.DeleteAsync(requester.Id, target.Id);

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        Assert.That(serverDb.Users.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task Service_GetSelf_Should_Return_NotFound_When_Current_User_Does_Not_ExistAsync()
    {
        using var connection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(connection);

        var service = CreateService(serverDb);
        var result = await service.GetFullInfoAsync(999);

        Assert.That(result.Success, Is.False);
        Assert.That(result.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
    }

    private static UserService CreateService(ServerDbContext serverDb)
    {
        return new UserService(
            serverDb,
            NullLogger<UserService>.Instance,
            new TenantProvider(),
            new ThrowingTenantDbContextFactory());
    }

    private static async Task<User> CreateUserAsync(ServerDbContext serverDb, string email)
    {
        var loginInfo = new LoginInfo { Email = email, IsEmailVerified = true };
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