using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Edemly.Server.Api.Services;

namespace Edemly.Server.Api.Controllers
{
    [ApiController]
    public abstract class ApiControllerBase : ControllerBase
    {
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

        protected IActionResult MessageResult(int statusCode, string message)
        {
            return statusCode switch
            {
                StatusCodes.Status200OK => OkMessage(message),
                StatusCodes.Status400BadRequest => BadRequestMessage(message),
                StatusCodes.Status401Unauthorized => UnauthorizedMessage(message),
                StatusCodes.Status403Forbidden => Forbid(),
                StatusCodes.Status404NotFound => NotFoundMessage(message),
                _ => StatusCode(statusCode, new { message })
            };
        }

        protected IActionResult ToServiceMessageResult(ServiceMessageResult result)
        {
            return MessageResult(result.StatusCode, result.Message);
        }

        protected IActionResult ToServiceDataResult<T>(ServiceDataResult<T> result)
        {
            if (result.Success)
            {
                return Ok(result.Data);
            }

            return MessageResult(result.StatusCode, result.Message ?? "Request failed");
        }

        protected IActionResult ToServiceDataResult<TInput, TOutput>(ServiceDataResult<TInput> result, Func<TInput?, TOutput> successMapper)
        {
            if (result.Success)
            {
                return Ok(successMapper(result.Data));
            }

            return MessageResult(result.StatusCode, result.Message ?? "Request failed");
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
