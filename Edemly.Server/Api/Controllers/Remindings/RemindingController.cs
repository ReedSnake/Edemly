using Edemly.Contracts.Remindings;
using Edemly.Server.Application.Remindings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edemly.Server.Api.Controllers.Remindings
{
    [ApiController]
    [Authorize]
    [Route("api/remindings")]
    public class RemindingsController : ApiControllerBase
    {
        private readonly IRemindingService _remindingService;

        public RemindingsController(IRemindingService remindingService)
        {
            _remindingService = remindingService;
        }

        [HttpGet("{remindingId}")]
        public async Task<IActionResult> GetByIdAsync(int remindingId)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _remindingService.GetByIdAsync(currentUserId, remindingId));
        }

        [HttpGet]
        public async Task<IActionResult> GetByUserAsync()
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _remindingService.GetByUserAsync(currentUserId));
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateRemindingDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _remindingService.CreateAsync(currentUserId, request));
        }

        [HttpPut("{remindingId}")]
        public async Task<IActionResult> UpdateAsync(
            int remindingId,
            [FromBody] UpdateRemindingDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(
                await _remindingService.UpdateAsync(currentUserId, remindingId, request));
        }

        [HttpPatch("{remindingId}/completion")]
        public async Task<IActionResult> ToggleCompletionAsync(int remindingId)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(
                await _remindingService.ToggleCompletionAsync(currentUserId, remindingId));
        }

        [HttpDelete("{remindingId}")]
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