using uchat_server.Data.Entities;
using uchat_server.Data;
using Microsoft.EntityFrameworkCore;

namespace uchat_server.Services
{
    public interface ITenantDbContextFactory
    {
        CompanyDbContext CreateCompanyDbContext(Company company);
    }
}
