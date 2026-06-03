using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Services;
using Edemly.Server.Utils;

namespace Edemly.Server.Api.Services
{
    public abstract class TenantAwareServiceBase
    {
        private readonly ServerDbContext _serverDb;
        private readonly ITenantProvider _tenantProvider;
        private readonly ITenantDbContextFactory _tenantDbFactory;

        protected TenantAwareServiceBase(
            ServerDbContext serverDb,
            ITenantProvider tenantProvider,
            ITenantDbContextFactory tenantDbFactory)
        {
            _serverDb = serverDb;
            _tenantProvider = tenantProvider;
            _tenantDbFactory = tenantDbFactory;
        }

        protected DbContextLease ResolveDbContext()
        {
            return DbContextResolver.ResolveLease(_serverDb, _tenantProvider, _tenantDbFactory);
        }

        protected bool IsTenantRequest => _tenantProvider.IsTenant && _tenantProvider.CurrentCompany != null;
    }
}
