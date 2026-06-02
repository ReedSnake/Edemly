using Microsoft.Extensions.DependencyInjection;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Edemly.Server.Tests.Infrastructure;

namespace Edemly.Server.Tests.Utilities;

public static class TestTenantHelper
{
    public static Task<Company> CreateCompanyAsync(CustomWebApplicationFactory factory, string name, string? dbName = null)
    {
        return factory.CreateCompanyAsync(name, dbName);
    }

    public static async Task AllowEmailAsync(IServiceProvider services, Company company, string email)
    {
        using var scope = services.CreateScope();
        var tenantFactory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();

        await using var tenantContext = tenantFactory.CreateCompanyDbContext(company);
        tenantContext.Emails.Add(new Email { EmailAddress = email });
        await tenantContext.SaveChangesAsync();
    }
}
