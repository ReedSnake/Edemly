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

        public UserController(IUserService userService)
        {
            _service = userService;
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetSelf()
        {
            return ToServiceDataResult(await _service.GetFullInfo(GetCurrentUserIdOrDefault()), user => new { User = user });
        }

        [HttpGet("id/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            return ToServiceDataResult(await _service.GetById(id), user => new { User = user });
        }

        [Authorize]
        [HttpGet("search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string query)
        {
            return ToServiceDataResult(
                await _service.SearchUsers(query),
                users => new { users = users ?? new List<UserDto>(), count = users?.Count ?? 0 });
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateUser([FromBody] UpdateUserDto model)
        {
            return ToServiceMessageResult(await _service.UpdateUser(GetCurrentUserIdOrDefault(), model));
        }

        [Authorize]
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            return ToServiceMessageResult(await _service.DeleteUser(GetCurrentUserIdOrDefault(), id));
        }

        [Authorize]
        [HttpPost("batch")]
        public async Task<IActionResult> GetUsersBatch([FromBody] List<int> userIds)
        {
            return ToServiceDataResult(
                await _service.GetUsersBatch(userIds),
                users => new { Users = users ?? new List<UserDto>(), Count = users?.Count ?? 0 });
        }
    }
}
