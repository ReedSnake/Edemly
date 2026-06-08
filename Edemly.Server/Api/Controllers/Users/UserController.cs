using Edemly.Contracts.Users;
using Edemly.Server.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edemly.Server.Api.Controllers.Users
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ApiControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetSelfAsync()
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _userService.GetFullInfoAsync(currentUserId), user => new { User = user });
        }

        [HttpGet("id/{targetUserId}")]
        public async Task<IActionResult> GetByIdAsync(int targetUserId)
        {
            return ToServiceResult(await _userService.GetByIdAsync(targetUserId), user => new { User = user });
        }

        [Authorize]
        [HttpGet("search")]
        public async Task<IActionResult> SearchUsersAsync([FromQuery] string query)
        {
            return ToServiceResult(
                await _userService.SearchUsersAsync(query),
                users => new { users = users ?? new List<UserDto>(), count = users?.Count ?? 0 });
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateUserDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _userService.UpdateAsync(currentUserId, request));
        }

        [Authorize]
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteAsync(int targetUserId)
        {
            var unauthorizedResult = RequireCurrentUserId(out var requesterId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _userService.DeleteAsync(requesterId, targetUserId));
        }

        [Authorize]
        [HttpPost("batch")]
        public async Task<IActionResult> GetUsersBatchAsync([FromBody] List<int> targetUserIds)
        {
            return ToServiceResult(
                await _userService.GetUsersBatchAsync(targetUserIds),
                users => new { Users = users ?? new List<UserDto>(), Count = users?.Count ?? 0 });
        }
    }
}