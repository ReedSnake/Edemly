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

        protected IActionResult? RequireCurrentUserId(out int userId, params string[] claimTypes)
        {
            if (TryGetCurrentUserId(out userId, claimTypes))
            {
                return null;
            }

            return Unauthorized();
        }

        protected IActionResult ToServiceResult(ServiceResult result)
        {
            return result.StatusCode switch
            {
                StatusCodes.Status200OK => Ok(new { message = result.Message }),
                StatusCodes.Status201Created => StatusCode(result.StatusCode, new { message = result.Message }),
                StatusCodes.Status204NoContent => NoContent(),
                StatusCodes.Status400BadRequest => BadRequest(new { message = result.Message }),
                StatusCodes.Status401Unauthorized => Unauthorized(new { message = result.Message }),
                StatusCodes.Status403Forbidden => Forbid(),
                StatusCodes.Status404NotFound => NotFound(new { message = result.Message }),
                StatusCodes.Status409Conflict => Conflict(new { message = result.Message }),
                _ => StatusCode(result.StatusCode, new { message = result.Message })
            };
        }

        protected IActionResult ToServiceResult<T>(ServiceResult<T> result)
        {
            if (result.Success)
            {
                return result.StatusCode switch
                {
                    StatusCodes.Status200OK => Ok(result.Data),
                    StatusCodes.Status201Created => StatusCode(result.StatusCode, result.Data),
                    StatusCodes.Status204NoContent => NoContent(),
                    _ => StatusCode(result.StatusCode, result.Data)
                };
            }

            return result.StatusCode switch
            {
                StatusCodes.Status400BadRequest => BadRequest(new { message = result.Message }),
                StatusCodes.Status401Unauthorized => Unauthorized(new { message = result.Message }),
                StatusCodes.Status403Forbidden => Forbid(),
                StatusCodes.Status404NotFound => NotFound(new { message = result.Message }),
                StatusCodes.Status409Conflict => Conflict(new { message = result.Message }),
                _ => StatusCode(result.StatusCode, new { message = result.Message })
            };
        }

        protected IActionResult ToServiceResult<TInput, TOutput>(
            ServiceResult<TInput> result,
            Func<TInput?, TOutput> successMapper)
        {
            if (result.Success)
            {
                return result.StatusCode switch
                {
                    StatusCodes.Status200OK => Ok(successMapper(result.Data)),
                    StatusCodes.Status201Created => StatusCode(result.StatusCode, successMapper(result.Data)),
                    StatusCodes.Status204NoContent => NoContent(),
                    _ => StatusCode(result.StatusCode, successMapper(result.Data))
                };
            }

            return result.StatusCode switch
            {
                StatusCodes.Status400BadRequest => BadRequest(new { message = result.Message }),
                StatusCodes.Status401Unauthorized => Unauthorized(new { message = result.Message }),
                StatusCodes.Status403Forbidden => Forbid(),
                StatusCodes.Status404NotFound => NotFound(new { message = result.Message }),
                StatusCodes.Status409Conflict => Conflict(new { message = result.Message }),
                _ => StatusCode(result.StatusCode, new { message = result.Message })
            };
        }
    }
}
