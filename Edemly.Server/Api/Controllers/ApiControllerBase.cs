using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Edemly.Server.Data;
using Edemly.Server.Services;
using Edemly.Server.Api.Middleware;

namespace Edemly.Server.Api.Controllers
{
    [ApiController]
    public abstract class ApiControllerBase : ControllerBase
    {
        protected readonly ServerDbContext ServerDb;
        protected readonly ITenantProvider TenantProvider;
        protected readonly ITenantDbContextFactory TenantDbFactory;
        protected readonly IConfiguration Configuration;

        protected ApiControllerBase(ServerDbContext serverDb, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory, IConfiguration configuration)
        {
            ServerDb = serverDb;
            TenantProvider = tenantProvider;
            TenantDbFactory = tenantDbFactory;
            Configuration = configuration;
        }
    }
}
