using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Edemly.Contracts.Remindings;
using Edemly.Server.Api.Controllers.Remindings;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Api.Services;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;

namespace Edemly.Server.Tests.Unit.Remindings;

public sealed class RemindingRefactorTests
{
    [Test]
    public async Task Controller_Toggle_Should_Return_Forbid_When_Service_Returns_Forbidden()
    {
        var service = new Mock<IRemindingService>();
        service.Setup(x => x.ToggleCompletion(0, 5))
            .ReturnsAsync(ServiceMessageResult.Forbidden());

        var controller = new RemindingController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.Toggle(5);

        Assert.That(result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task Controller_GetByUser_Should_Return_Ok_When_Service_Returns_Data()
    {
        var service = new Mock<IRemindingService>();
        service.Setup(x => x.GetByUser(0))
            .ReturnsAsync(ServiceDataResult<List<RemindingDto>>.Ok(new List<RemindingDto>
            {
                new()
                {
                    Id = 1,
                    UserId = 0,
                    Content = "Check",
                    CreatedAt = DateTime.UtcNow,
                    Name = "Check",
                    Type = (int)RemindingType.Work
                }
            }));

        var controller = new RemindingController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.GetByUser();

        Assert.That(result, Is.TypeOf<OkObjectResult>());
        Assert.That(GetJsonArrayLength(result), Is.EqualTo(1));
    }

    [Test]
    public async Task Service_ToggleCompletion_Should_Update_Owned_Reminding()
    {
        using var connection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(connection);

        var owner = await CreateUserAsync(serverDb, "owner@example.test");

        var reminding = new Reminding
        {
            UserId = owner.Id,
            Content = "Call",
            CreatedAt = DateTime.UtcNow,
            LastTime = DateTime.UtcNow,
            ShouldNotify = true,
            Name = "Call",
            Type = (int)RemindingType.Work,
            ShowTime = true,
            IsCompleted = false
        };

        serverDb.Remindings.Add(reminding);
        await serverDb.SaveChangesAsync();

        var service = new RemindingService(
            serverDb,
            NullLogger<RemindingService>.Instance,
            new TenantProvider(),
            new ThrowingTenantDbContextFactory());

        var result = await service.ToggleCompletion(1, reminding.Id);

        Assert.That(result.Success, Is.True);
        Assert.That(serverDb.Remindings.Single().IsCompleted, Is.True);
    }

    private static int GetJsonArrayLength(IActionResult result)
    {
        if (result is not ObjectResult objectResult || objectResult.Value == null)
        {
            return 0;
        }

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(objectResult.Value));
        return document.RootElement.GetArrayLength();
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
}
