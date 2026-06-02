using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using uchat_server.Data;
using uchat_server.Api.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace uchat_server.Api.Middleware
{
    public class TenantResolutionMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantResolutionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider, ILogger<TenantResolutionMiddleware> logger)
        {
            // Extract path segments
            var path = context.Request.Path.Value ?? string.Empty;

            // Normalize: remove leading '/'
            if (path.StartsWith('/')) path = path.Substring(1);

            var segments = path.Split('/', System.StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
            {
                tenantProvider.CurrentCompany = null;
                await _next(context);
                return;
            }

            var first = segments[0]?.Trim() ?? string.Empty;

            // Skip reserved root folder 'uploads'
            if (string.Equals(first, "uploads", System.StringComparison.OrdinalIgnoreCase))
            {
                tenantProvider.CurrentCompany = null;
                await _next(context);
                return;
            }

            try
            {
                // Resolve DbContext from the current request services to avoid it being created before tenant is resolved.
                var serverDb = context.RequestServices.GetService<ServerDbContext>();

                if (serverDb == null)
                {
                    logger.LogWarning("TenantResolution: ServerDbContext not available in request services - continuing as master (no tenant). Path='{Path}'", context.Request.Path);
                    tenantProvider.CurrentCompany = null;
                    await _next(context);
                    return;
                }

                // Try to find company by name (case-insensitive)
                var firstNormalized = first.ToLowerInvariant();
                var company = await serverDb.Companies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Name != null && c.Name.ToLower() == firstNormalized);

                // log at information level so it's visible by default
                logger.LogInformation("TenantResolution: incoming path='{PathRaw}' firstSegment='{Segment}'", context.Request.Path, first);

                if (company != null)
                {
                    tenantProvider.CurrentCompany = company;

                    // Store company in HttpContext.Items so non-HTTP DI scopes (SignalR) can access it
                    try
                    {
                        context.Items["TenantCompany"] = company;
                    }
                    catch (Exception ex)
                    {
                        try { logger.LogDebug(ex, "TenantResolution: failed to set HttpContext.Items[\"TenantCompany\"]"); } catch { System.Diagnostics.Debug.WriteLine(ex.ToString()); }
                    }

                    logger.LogInformation("TenantResolution: matched company '{CompanyName}' for path segment '{Segment}'", company.Name, first);

                    // If request is for tenant uploads (e.g. /{tenant}/uploads/...) keep path as-is so static files map to wwwroot/{tenant}/uploads
                    if (segments.Length >= 2 && string.Equals(segments[1], "uploads", System.StringComparison.OrdinalIgnoreCase))
                    {
                        // do not rewrite path
                    }
                    else
                    {
                        // Rewrite path to remove tenant prefix so controllers keep same routes
                        var newPath = context.Request.Path.Value!.Substring(first.Length + 1);
                        if (string.IsNullOrEmpty(newPath)) newPath = "/";
                        logger.LogInformation("TenantResolution: rewriting path from '{Old}' to '{New}'", context.Request.Path, newPath);
                        context.Request.Path = newPath;
                    }
                }
                else
                {
                    tenantProvider.CurrentCompany = null;
                    logger.LogInformation("TenantResolution: no company matched for segment '{Segment}'", first);
                }
            }
            catch (System.Exception ex)
            {
                // If Companies table doesn't exist or any DB error occurs, treat as no tenant and continue.
                logger.LogWarning(ex, "Tenant resolution failed - continuing as master (no tenant). Path='{Path}'", context.Request.Path);
                tenantProvider.CurrentCompany = null;
            }

            await _next(context);
        }
    }
}
