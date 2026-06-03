using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Edemly.Server.Api.Services;
using Edemly.Contracts.Remindings;

namespace Edemly.Server.Api.Controllers.Remindings
{
    [ApiController]
    [Route("api/[controller]")]
    public class RemindingController : ApiControllerBase
    {
        private readonly IRemindingService _remindingService;

        public RemindingController(IRemindingService remindingService)
        {
            _remindingService = remindingService;
        }

        [Authorize]
        [HttpGet("id/{remindingId}")]
        public async Task<IActionResult> GetByIdAsync(int remindingId)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _remindingService.GetByIdAsync(currentUserId, remindingId));
        }

        [Authorize]
        [HttpGet("my-remindings")]
        public async Task<IActionResult> GetByUserAsync()
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _remindingService.GetByUserAsync(currentUserId));
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> CreateAsync([FromBody] CreateRemindingDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _remindingService.CreateAsync(currentUserId, request));
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateRemindingDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _remindingService.UpdateAsync(currentUserId, request));
        }

        [Authorize]
        [HttpPut("toggle-completion/{remindingId}")]
        public async Task<IActionResult> ToggleAsync(int remindingId)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _remindingService.ToggleCompletionAsync(currentUserId, remindingId));
        }

        [Authorize]
        [HttpDelete("delete/{remindingId}")]
        public async Task<IActionResult> DeleteAsync(int remindingId)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _remindingService.DeleteAsync(currentUserId, remindingId));
        }
    }
}
