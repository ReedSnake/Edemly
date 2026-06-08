using Edemly.Server.Api.Middleware;
using Edemly.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Infrastructure.Tenancy
{
    public class DbContextResolver
    {
        public static DbContext Resolve(out bool isTenant, ServerDbContext serverDb, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
        {
            var company = tenantProvider.CurrentCompany;
            isTenant = tenantProvider.IsTenant && company != null;
            if (isTenant)
            {
                return tenantDbFactory.CreateCompanyDbContext(company!);
            }

            return serverDb;
        }

        public static DbContextLease ResolveLease(ServerDbContext serverDb, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory)
        {
            var company = tenantProvider.CurrentCompany;
            if (tenantProvider.IsTenant && company != null)
            {
                return DbContextLease.Owned(tenantDbFactory.CreateCompanyDbContext(company));
            }

            return DbContextLease.Shared(serverDb);
        }
    }

    public sealed class DbContextLease : IAsyncDisposable
    {
        private readonly bool _ownsContext;

        private DbContextLease(DbContext context, bool ownsContext)
        {
            Context = context;
            _ownsContext = ownsContext;
        }

        public DbContext Context { get; }

        public static DbContextLease Owned(DbContext context) => new(context, ownsContext: true);

        public static DbContextLease Shared(DbContext context) => new(context, ownsContext: false);

        public ValueTask DisposeAsync()
        {
            return _ownsContext ? Context.DisposeAsync() : ValueTask.CompletedTask;
        }
    }
}