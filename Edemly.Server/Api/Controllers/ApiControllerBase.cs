using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Edemly.Server.Data;
using Edemly.Server.Data.Entities;
using Edemly.Server.Services;
using Edemly.Server.Api.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

        /// <summary>
        /// Returns a DbContext to use for the current request. If tenant is active, returns a new CompanyDbContext
        /// (caller is responsible for disposing it). Otherwise returns the shared ServerDbContext.
        /// </summary>
        protected DbContext ResolveDbContextForRequest(out bool isTenant)
        {
            // Get request-scoped logger if available
            var logger = HttpContext?.RequestServices.GetService<ILogger<ApiControllerBase>>();

            // Primary: check tenant provider
            isTenant = TenantProvider != null && TenantProvider.IsTenant && TenantProvider.CurrentCompany != null;
            if (isTenant)
            {
                logger?.LogDebug("ResolveDbContextForRequest: TenantProvider indicates tenant '{Company}'", TenantProvider.CurrentCompany?.Name);
                return TenantDbFactory.CreateCompanyDbContext(TenantProvider.CurrentCompany!);
            }

            // Secondary: check HttpContext.Items (set by middleware) in case tenant provider wasn't populated for some reason
            try
            {
                var http = this.HttpContext;
                if (http != null)
                {
                    if (http.Items.TryGetValue("TenantCompany", out var item) && item is Company companyFromItems)
                    {
                        // populate tenant provider so downstream code sees tenant
                        try { TenantProvider.CurrentCompany = companyFromItems; } catch (Exception ex) { logger?.LogWarning(ex, "ResolveDbContextForRequest: failed to set TenantProvider.CurrentCompany from HttpContext.Items"); }

                        logger?.LogDebug("ResolveDbContextForRequest: Found tenant in HttpContext.Items '{Company}'", companyFromItems.Name);

                        isTenant = true;
                        return TenantDbFactory.CreateCompanyDbContext(companyFromItems);
                    }

                    // Tertiary: try to resolve tenant from first path segment
                    var path = http.Request?.Path.Value ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        var segments = path.Split(new[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
                        if (segments.Length > 0)
                        {
                            var first = segments[0];
                            if (!string.Equals(first, "api", System.StringComparison.OrdinalIgnoreCase))
                            {
                                var found = ServerDb.Companies.AsNoTracking().FirstOrDefault(c => c.Name == first);
                                if (found != null)
                                {
                                    try { TenantProvider.CurrentCompany = found; } catch (Exception ex) { logger?.LogWarning(ex, "ResolveDbContextForRequest: failed to set TenantProvider.CurrentCompany from path segment"); }

                                    logger?.LogDebug("ResolveDbContextForRequest: Resolved tenant from path segment '{Segment}' -> '{Company}'", first, found.Name);

                                    isTenant = true;
                                    return TenantDbFactory.CreateCompanyDbContext(found);
                                }
                                else
                                {
                                    logger?.LogDebug("ResolveDbContextForRequest: No company found for path segment '{Segment}'", first);
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                logger?.LogDebug(ex, "ResolveDbContextForRequest: error while resolving tenant");
                // ignore and fall back to master DB
            }

            logger?.LogDebug("ResolveDbContextForRequest: falling back to master DB");
            return ServerDb;
        }
    }
}
