using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Edemly.Contracts.Users;
using Edemly.Server.Api.Services;
using Edemly.Server.Data;
using Edemly.Server.Services;
using Edemly.Server.Api.Middleware;
using Microsoft.Extensions.Configuration;

namespace Edemly.Server.Api.Controllers.Users
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ApiControllerBase
    {
        private readonly IUserService _service;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, IPermissionService permissionService, ILogger<UserController> logger, ServerDbContext serverDb, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory, IConfiguration configuration)
            : base(serverDb, tenantProvider, tenantDbFactory, configuration)
        {
            _permissionService = permissionService;
            _service = userService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetSelf()
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0"); //get the authenticated users id 

            var result = await _service.GetFullInfo(userIdClaim);
            if (!result.Success)
            {
                _logger.LogWarning($"Failed to fetch user: {result.Error}");
                return BadRequest(new { message = result.Error });
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
                return NotFound(new { message = result.Error ?? "User not found" });
            }

            return Ok(new { result.User });
        }

        [Authorize]
        [HttpGet("search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { message = "Search query is required" });
            }

            var result = await _service.SearchUsers(query);

            if (!result.Success)
            {
                _logger.LogWarning($"Failed to search users with query '{query}': {result.Error}");
                return BadRequest(new { message = result.Error });
            }

            return Ok(new { users = result.Users, count = result.Users.Count });
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateUser([FromBody] UpdateUserDto model)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            var result = await _service.UpdateUser(userIdClaim, model);
            if (!result.Success)
            {
                _logger.LogWarning($"Failed to update user {userIdClaim}: {result.Error}");
                return BadRequest(new { message = result.Error });
            }

            return Ok(new { message = "User updated successfully" });
        }

        [Authorize]
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!_permissionService.CanDeleteUser(userIdClaim, id))
            {
                return Forbid();
            }

            var result = await _service.DeleteUser(id);
            if (!result.Success)
            {
                _logger.LogWarning($"Failed to delete user {id}: {result.Error}");
                return BadRequest(new { message = result.Error });
            }

            return Ok(new { message = "User deleted successfully" });
        }

        [Authorize]
        [HttpPost("batch")]
        public async Task<IActionResult> GetUsersBatch([FromBody] List<int> userIds)
        {
            if (userIds == null || userIds.Count == 0)
            {
                return BadRequest(new { message = "User IDs list is required" });
            }

            try
            {
                // Використовуємо сервіс замість прямого доступу до _context
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
                return BadRequest(new { message = ex.Message });
            }
        }

    }
}
