using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace uchat_server.Api.Middleware
{
    public class EnsureUploadsAuthMiddleware
    {
        private readonly RequestDelegate _next;

        public EnsureUploadsAuthMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? string.Empty;

            // normalize
            if (path.StartsWith('/')) path = path.Substring(1);

            var segments = path.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
            bool isUploadPath = false;

            if (segments.Length > 0)
            {
                // /uploads/... or /{tenant}/uploads/...
                if (string.Equals(segments[0], "uploads", System.StringComparison.OrdinalIgnoreCase))
                    isUploadPath = true;
                else if (segments.Length > 1 && string.Equals(segments[1], "uploads", System.StringComparison.OrdinalIgnoreCase))
                    isUploadPath = true;
            }

            if (isUploadPath)
            {
                if (!context.User?.Identity?.IsAuthenticated ?? true)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Authentication required to access uploads");
                    return;
                }
            }

            await _next(context);
        }
    }
}
