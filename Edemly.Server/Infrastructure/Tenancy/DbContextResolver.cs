using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Services;

namespace Edemly.Server.Utils
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
