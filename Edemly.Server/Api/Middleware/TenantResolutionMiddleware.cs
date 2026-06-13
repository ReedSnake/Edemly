using Edemly.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Edemly.Server.Api.Middleware
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
            TenantRequestContext.Clear(context, tenantProvider);

            var tenantCandidate = GetTenantCandidate(context);
            if (tenantCandidate == null)
            {
                await _next(context);
                return;
            }

            try
            {
                var serverDb = context.RequestServices.GetService<ServerDbContext>();

                if (serverDb == null)
                {
                    logger.LogWarning("TenantResolution: ServerDbContext not available in request services - continuing as master (no tenant). Path='{Path}'", context.Request.Path);
                    await _next(context);
                    return;
                }

                var firstNormalized = tenantCandidate.TenantName.ToLowerInvariant();
                var company = await serverDb.Companies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Name != null && c.Name.ToLower() == firstNormalized);

                logger.LogInformation(
                    "TenantResolution: incoming path='{PathRaw}' tenantCandidate='{TenantCandidate}' source='{Source}'",
                    context.Request.Path,
                    tenantCandidate.TenantName,
                    tenantCandidate.Source);

                if (company != null)
                {
                    TenantRequestContext.SetCurrentCompany(context, tenantProvider, company);

                    logger.LogInformation(
                        "TenantResolution: matched company '{CompanyName}' from {Source}",
                        company.Name,
                        tenantCandidate.Source);

                    if (tenantCandidate.ShouldRewritePath)
                    {
                        var newPath = context.Request.Path.Value!.Substring(tenantCandidate.TenantName.Length + 1);
                        if (string.IsNullOrEmpty(newPath)) newPath = "/";
                        logger.LogInformation("TenantResolution: rewriting path from '{Old}' to '{New}'", context.Request.Path, newPath);
                        context.Request.Path = newPath;
                    }
                }
                else
                {
                    logger.LogInformation(
                        "TenantResolution: no company matched for candidate '{TenantCandidate}' from {Source}",
                        tenantCandidate.TenantName,
                        tenantCandidate.Source);
                }
            }
            catch (System.Exception ex)
            {
                logger.LogWarning(ex, "Tenant resolution failed - continuing as master (no tenant). Path='{Path}'", context.Request.Path);
                TenantRequestContext.Clear(context, tenantProvider);
            }

            await _next(context);
        }

        private static TenantCandidate? GetTenantCandidate(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.StartsWith('/'))
            {
                path = path.Substring(1);
            }

            var segments = path.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 1 &&
                string.Equals(segments[0], "uploads", System.StringComparison.OrdinalIgnoreCase) &&
                !IsReservedUploadsFolder(segments[1]))
            {
                return new TenantCandidate(segments[1], "uploads-path", ShouldRewritePath: false);
            }

            if (segments.Length > 0)
            {
                var first = segments[0]?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(first) && !IsReservedRootSegment(first))
                {
                    return new TenantCandidate(first, "path", ShouldRewritePath: !IsTenantUploadsRequest(segments));
                }
            }

            var tenantQuery = context.Request.Query["tenant"].FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(tenantQuery))
            {
                return new TenantCandidate(tenantQuery, "query", ShouldRewritePath: false);
            }

            return null;
        }

        private static bool IsReservedRootSegment(string segment)
        {
            return string.Equals(segment, "uploads", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "api", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "swagger", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "hubs", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "main", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "call", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTenantUploadsRequest(string[] segments)
        {
            return segments.Length >= 2
                && string.Equals(segments[1], "uploads", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReservedUploadsFolder(string segment)
        {
            return string.Equals(segment, "profile-pictures", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "files", System.StringComparison.OrdinalIgnoreCase);
        }

        private sealed record TenantCandidate(string TenantName, string Source, bool ShouldRewritePath);
    }
}
