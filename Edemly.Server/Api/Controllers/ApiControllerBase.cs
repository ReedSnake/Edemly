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

        protected int GetCurrentUserIdOrDefault()
        {
            return GetCurrentUserIdOrDefault("userId");
        }

        protected int GetCurrentUserIdOrDefault(params string[] claimTypes)
        {
            return TryGetCurrentUserId(out var userId, claimTypes) ? userId : 0;
        }

        protected bool TryGetCurrentUserId(out int userId, params string[] claimTypes)
        {
            var typesToCheck = claimTypes.Length == 0 ? new[] { "userId" } : claimTypes;

            foreach (var claimType in typesToCheck)
            {
                var claimValue = User.FindFirst(claimType)?.Value;
                if (!string.IsNullOrWhiteSpace(claimValue) && int.TryParse(claimValue, out userId))
                {
                    return true;
                }
            }

            userId = 0;
            return false;
        }

        protected OkObjectResult OkMessage(string message)
        {
            return Ok(new { message });
        }

        protected BadRequestObjectResult BadRequestMessage(string? message)
        {
            return BadRequest(new { message });
        }

        protected NotFoundObjectResult NotFoundMessage(string? message)
        {
            return NotFound(new { message });
        }

        protected UnauthorizedObjectResult UnauthorizedMessage(string? message)
        {
            return Unauthorized(new { message });
        }

        protected IActionResult OkOrNotFound<T>(bool success, string? error, T payload)
        {
            return success ? Ok(payload) : NotFoundMessage(error);
        }

        protected IActionResult OkOrBadRequest<T>(bool success, string? error, T payload)
        {
            return success ? Ok(payload) : BadRequestMessage(error);
        }

        protected IActionResult OkMessageOrBadRequest(bool success, string? error, string successMessage)
        {
            return success ? OkMessage(successMessage) : BadRequestMessage(error);
        }
    }
}
