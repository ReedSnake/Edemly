using Edemly.Server.Data.Entities;
using Edemly.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Services
{
    public interface ITenantDbContextFactory
    {
        CompanyDbContext CreateCompanyDbContext(Company company);
    }
}
