using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Edemly.Contracts.Users;
using Edemly.Server.Api.Services;

namespace Edemly.Server.Api.Controllers.Users
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ApiControllerBase
    {
        private readonly IUserService _service;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, IPermissionService permissionService, ILogger<UserController> logger)
        {
            _permissionService = permissionService;
            _service = userService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetSelf()
        {
            var userId = GetCurrentUserIdOrDefault();

            var result = await _service.GetFullInfo(userId);
            if (!result.Success)
            {
                _logger.LogWarning($"Failed to fetch user: {result.Error}");
                return BadRequestMessage(result.Error);
            }

            return Ok(new { result.User });
        }

        [HttpGet("id/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetById(id);

            if (!result.Success || result.User == null)
            {
                _logger.LogWarning($"Failed to fetch user by ID {id}: {result.Error}");
                return NotFoundMessage(result.Error ?? "User not found");
            }

            return Ok(new { result.User });
        }

        [Authorize]
        [HttpGet("search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequestMessage("Search query is required");
            }

            var result = await _service.SearchUsers(query);

            if (!result.Success)
            {
                _logger.LogWarning($"Failed to search users with query '{query}': {result.Error}");
                return BadRequestMessage(result.Error);
            }

            return Ok(new { users = result.Users, count = result.Users.Count });
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateUser([FromBody] UpdateUserDto model)
        {
            var userId = GetCurrentUserIdOrDefault();

            var result = await _service.UpdateUser(userId, model);
            if (!result.Success)
            {
                _logger.LogWarning($"Failed to update user {userId}: {result.Error}");
                return BadRequestMessage(result.Error);
            }

            return OkMessage("User updated successfully");
        }

        [Authorize]
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (!_permissionService.CanDeleteUser(userId, id))
            {
                return Forbid();
            }

            var result = await _service.DeleteUser(id);
            if (!result.Success)
            {
                _logger.LogWarning($"Failed to delete user {id}: {result.Error}");
                return BadRequestMessage(result.Error);
            }

            return OkMessage("User deleted successfully");
        }

        [Authorize]
        [HttpPost("batch")]
        public async Task<IActionResult> GetUsersBatch([FromBody] List<int> userIds)
        {
            if (userIds == null || userIds.Count == 0)
            {
                return BadRequestMessage("User IDs list is required");
            }

            try
            {
                var tasks = userIds.Select(id => _service.GetById(id));
                var results = await Task.WhenAll(tasks);

                var users = results
                    .Where(r => r.Success && r.User != null)
                    .Select(r => r.User)
                    .ToList();

                return Ok(new { Users = users, Count = users.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get users batch");
                return BadRequestMessage(ex.Message);
            }
        }
    }
}
