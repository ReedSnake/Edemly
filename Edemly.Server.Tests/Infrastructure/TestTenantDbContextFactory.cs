using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;

namespace Edemly.Server.Tests.Infrastructure;

public sealed class TestTenantDbContextFactory : ITenantDbContextFactory, IDisposable
{
    private readonly ConcurrentDictionary<string, SqliteConnection> _connections = new(StringComparer.OrdinalIgnoreCase);

    public CompanyDbContext CreateCompanyDbContext(Company company)
    {
        ArgumentNullException.ThrowIfNull(company);
        ArgumentException.ThrowIfNullOrWhiteSpace(company.Name);

        var connection = _connections.GetOrAdd(company.Name, _ =>
        {
            var tenantConnection = new SqliteConnection("Data Source=:memory:");
            tenantConnection.Open();

            using var bootstrapContext = CreateContext(tenantConnection);
            bootstrapContext.Database.EnsureCreated();

            return tenantConnection;
        });

        return CreateContext(connection);
    }

    public void Dispose()
    {
        foreach (var connection in _connections.Values)
        {
            connection.Dispose();
        }

        _connections.Clear();
    }

    private static CompanyDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<CompanyDbContext>()
            .UseSqlite(connection)
            .Options;

        return new CompanyDbContext(options);
    }
}
