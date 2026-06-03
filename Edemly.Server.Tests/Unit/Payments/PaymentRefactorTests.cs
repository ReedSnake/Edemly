using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Edemly.Server.Api.Controllers.Payments;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Api.Services;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;

namespace Edemly.Server.Tests.Unit.Payments;

public sealed class PaymentRefactorTests
{
    [Test]
    public async Task Controller_GetPaymentHistory_Should_Return_Unauthorized_When_NameIdentifier_Claim_Is_Missing()
    {
        var configuration = CreateConfiguration();
        var wayForPayService = new WayForPayService(
            configuration,
            new HttpClient(),
            NullLogger<WayForPayService>.Instance,
            Mock.Of<IPaymentService>(),
            Mock.Of<IPublicUrlProvider>(),
            new HttpContextAccessor());

        var controller = new PaymentController(
            wayForPayService,
            Mock.Of<IPaymentService>(),
            NullLogger<PaymentController>.Instance,
            configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.GetPaymentHistory();

        Assert.That(result, Is.TypeOf<UnauthorizedObjectResult>());
    }

    [Test]
    public async Task Service_UpgradeUserToPremium_Should_Update_Subscription_Status_And_Expiration()
    {
        using var connection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(connection);

        var user = await CreateUserAsync(serverDb, "premium@example.test");
        var service = CreateService(serverDb);

        var before = DateTime.UtcNow;
        var result = await service.UpgradeUserToPremium(user.Id, 30);
        var refreshed = await serverDb.Users.FindAsync(user.Id);

        Assert.That(result.Success, Is.True);
        Assert.That(refreshed, Is.Not.Null);
        Assert.That(refreshed!.SubscriptionStatus, Is.EqualTo(SubscriptionStatus.Premium));
        Assert.That(refreshed.SubscriptionExpiration, Is.Not.Null);
        Assert.That(refreshed.SubscriptionExpiration!.Value, Is.GreaterThan(before.AddDays(29)));
    }

    private static PaymentService CreateService(ServerDbContext serverDb)
    {
        return new PaymentService(
            serverDb,
            NullLogger<PaymentService>.Instance,
            new TenantProvider(),
            new ThrowingTenantDbContextFactory());
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WayForPay:MerchantAccount"] = "merchant",
                ["WayForPay:SecretKey"] = "secret"
            })
            .Build();
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
}
