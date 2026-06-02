using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using uchat_server.Api.DTOs;
using uchat_server.Data;
using uchat_server.Data.Entities;
using uchat_server.Api.Middleware;
using uchat_server.Services;

namespace uchat_server.Utils
{
    public class DbContextResolver
    {
        public static DbContext Resolve(out bool isTenant, ServerDbContext serverDb, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
        {
            isTenant = tenantProvider != null && tenantProvider.IsTenant && tenantProvider.CurrentCompany != null;
            if (isTenant)
            {
                var company = tenantProvider.CurrentCompany!;
                return tenantDbFactory.CreateCompanyDbContext(company);
            }

            return serverDb;
        }
    }
}
