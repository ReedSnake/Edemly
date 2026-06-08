using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Edemly.Server.Infrastructure.Tenancy;

namespace Edemly.Server.Application.Common
{
    public abstract class TenantAwareServiceBase
    {
        private readonly ServerDbContext _serverDbContext;
        private readonly ITenantProvider _tenantProvider;
        private readonly ITenantDbContextFactory _tenantDbContextFactory;

        protected TenantAwareServiceBase(
            ServerDbContext serverDbContext,
            ITenantProvider tenantProvider,
            ITenantDbContextFactory tenantDbContextFactory)
        {
            _serverDbContext = serverDbContext;
            _tenantProvider = tenantProvider;
            _tenantDbContextFactory = tenantDbContextFactory;
        }

        protected DbContextLease ResolveDbContext()
        {
            return DbContextResolver.ResolveLease(_serverDbContext, _tenantProvider, _tenantDbContextFactory);
        }

        protected bool IsTenantRequest => _tenantProvider.IsTenant && _tenantProvider.CurrentCompany != null;
    }
}