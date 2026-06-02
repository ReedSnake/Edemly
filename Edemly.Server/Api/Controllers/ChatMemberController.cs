using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using uchat_server.Api.Services;
using static uchat_server.Api.DTOs.ChatMemberDtos;
using uchat_server.Data;
using uchat_server.Services;
using uchat_server.Api.Middleware;
using Microsoft.Extensions.Configuration;

namespace uchat_server.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatMemberController : ApiControllerBase
    {
        private readonly IChatMemberService _service;
        private readonly IPermissionService _permissionService;

        public ChatMemberController(IChatMemberService service, IPermissionService permissionService, ServerDbContext serverDb, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory, IConfiguration configuration)
            : base(serverDb, tenantProvider, tenantDbFactory, configuration)
        {
            _service = service;
            _permissionService = permissionService;
        }

        [HttpGet("id/{id}")]
        public async Task<IActionResult> GetMember(int id)
        {
            var result = await _service.GetMember(id);
            if (!result.Success) return NotFound(new { message = result.Error });
            return Ok(result.Member);
        }

        [Authorize]
        [HttpGet("list/{chatId}")]
        public async Task<IActionResult> GetMembers(int chatId)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!await _permissionService.IsInChat(userIdClaim, chatId))
            {
                return Forbid();
            }

            var result = await _service.GetMembers(chatId);
            if (!result.Success) return NotFound(new { message = result.Error });
            return Ok(result.Members);
        }

        [Authorize]
        [HttpGet("my-memberships")]
        public async Task<IActionResult> GetMemberships()
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            var result = await _service.GetMemberships(userIdClaim);
            if (!result.Success) return NotFound(new { message = result.Error });
            return Ok(result.Memberships);
        }

        [Authorize]
        [HttpPost("add")]
        public async Task<IActionResult> AddMember([FromBody] ChatMemberCreateDto model)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!await _permissionService.CanAddChatMember(userIdClaim, model.ChatId))
            {
                return Forbid();
            }

            var result = await _service.AddMember(model);
            if (!result.Success) return BadRequest(new { message = result.Error });
            return Ok(new { message = "Chat member added" });
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateMember([FromBody] ChatMemberUpdateDto model)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!await _permissionService.CanUpdateChatMember(userIdClaim, model.Id))
            {
                return Forbid();
            }

            var result = await _service.UpdateMember(model);
            if (!result.Success) return BadRequest(new { message = result.Error });
            return Ok(new { message = "Chat member updated" });
        }

        [Authorize]
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteMember(int id)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!await _permissionService.CanDeleteChatMember(userIdClaim, id))
            {
                return Forbid();
            }

            var result = await _service.DeleteMember(id);
            if (!result.Success) return BadRequest(new { message = result.Error });
            return Ok(new { message = "Chat member removed" });
        }

    }
}
