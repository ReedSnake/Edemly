using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Infrastructure.Auth;
using Edemly.Server.Infrastructure.BackgroundServices;
using Edemly.Server.Infrastructure.Tenancy;
using Edemly.Server.Tests.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Edemly.Server.Tests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Startup:UseDatabaseMigrations"] = "false",
                ["Startup:SeedDatabase"] = "false",
                ["Brevo:ApiKey"] = "MOCK_MODE",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
                ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ServerDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ServerDbContext>>();
            services.RemoveAll<IEmailService>();
            services.RemoveAll<ITenantDbContextFactory>();

            var maintenanceWorker = services.FirstOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(ServerMaintenanceWorker));

            if (maintenanceWorker != null)
            {
                services.Remove(maintenanceWorker);
            }

            if (_connection.State != System.Data.ConnectionState.Open)
            {
                _connection.Open();
            }

            services.AddDbContext<ServerDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            services.AddSingleton<TestEmailService>();
            services.AddSingleton<IEmailService>(provider => provider.GetRequiredService<TestEmailService>());
            services.AddSingleton<ITenantDbContextFactory, TestTenantDbContextFactory>();

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            dbContext.Database.EnsureCreated();
        });
    }

    public async Task<Company> CreateCompanyAsync(string name, string? dbName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var company = new Company
        {
            Name = name,
            DbName = dbName ?? $"tenant_{name}"
        };

        dbContext.Companies.Add(company);
        await dbContext.SaveChangesAsync();

        var tenantFactory = (TestTenantDbContextFactory)scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
        await using var tenantContext = tenantFactory.CreateCompanyDbContext(company);
        await tenantContext.Database.EnsureCreatedAsync();

        return company;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}