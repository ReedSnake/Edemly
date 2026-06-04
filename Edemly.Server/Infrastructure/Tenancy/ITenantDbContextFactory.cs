using Edemly.Server.Data;
using Edemly.Server.Data.Entities;

namespace Edemly.Server.Services
{
    public interface ITenantDbContextFactory
    {
        CompanyDbContext CreateCompanyDbContext(Company company);
    }
}