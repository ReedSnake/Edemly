using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Tests.Unit.Tenancy;

public sealed class TenantResolutionMiddlewareTests
{
    [Test]
    public async Task InvokeAsync_Should_Set_Tenant_From_Query_When_Path_Uses_Reserved_Root()
    {
        using var connection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(connection);
        serverDb.Companies.Add(new Company { Name = "acme", DbName = "tenant_acme" });
        await serverDb.SaveChangesAsync();

        using var serviceProvider = new ServiceCollection()
            .AddSingleton(serverDb)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };
        httpContext.Request.Path = "/api/auth/get-code";
        httpContext.Request.QueryString = new QueryString("?tenant=acme");

        var tenantProvider = new TenantProvider();
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, tenantProvider, NullLogger<TenantResolutionMiddleware>.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(tenantProvider.CurrentCompany?.Name, Is.EqualTo("acme"));
            Assert.That(httpContext.Request.Path.Value, Is.EqualTo("/api/auth/get-code"));
            Assert.That(httpContext.Items[TenantRequestContext.CompanyItemKey], Is.InstanceOf<Company>());
        });
    }

    [Test]
    public async Task InvokeAsync_Should_Rewrite_Path_When_Tenant_Is_First_PathSegment()
    {
        using var connection = CreateOpenConnection();
        await using var serverDb = CreateServerDbContext(connection);
        serverDb.Companies.Add(new Company { Name = "acme", DbName = "tenant_acme" });
        await serverDb.SaveChangesAsync();

        using var serviceProvider = new ServiceCollection()
            .AddSingleton(serverDb)
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };
        httpContext.Request.Path = "/acme/api/auth/login";

        var tenantProvider = new TenantProvider();
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(httpContext, tenantProvider, NullLogger<TenantResolutionMiddleware>.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(tenantProvider.CurrentCompany?.Name, Is.EqualTo("acme"));
            Assert.That(httpContext.Request.Path.Value, Is.EqualTo("/api/auth/login"));
            Assert.That(httpContext.Items[TenantRequestContext.CompanyItemKey], Is.InstanceOf<Company>());
        });
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
}
