using Edemly.Server.Data;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Infrastructure.Tenancy
{
    public interface ITenantDbContextFactory
    {
        CompanyDbContext CreateCompanyDbContext(Company company);
    }
}