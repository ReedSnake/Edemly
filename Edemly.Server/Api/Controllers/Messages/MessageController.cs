using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Edemly.Server.Api.Services;
using Edemly.Server.Services;
using Edemly.Server.Data;
using Edemly.Server.Api.Middleware;
using Microsoft.Extensions.Configuration;

namespace Edemly.Server.Api.Controllers.Messages
{
    [ApiController]
    [Route("api/[controller]")]
    public class MessageController : ApiControllerBase
    {
        private readonly IMessageService _service;
        private readonly IPermissionService _permissionService;

        public MessageController(IMessageService service, IPermissionService permissionService, ServerDbContext serverDb, ITenantProvider tenantProvider, ITenantDbContextFactory tenantDbFactory, IConfiguration configuration)
            : base(serverDb, tenantProvider, tenantDbFactory, configuration)
        {
            _service = service;
            _permissionService = permissionService;
        }

        [HttpGet("id/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetById(id);
            if (!result.Success) return NotFound(new { message = result.Error });
            return Ok(result.Message);
        }

        [Authorize]
        [HttpGet("chat/last/{chatId}")]
        public async Task<IActionResult> GetLastByChat(int chatId)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!await _permissionService.IsInChat(userIdClaim, chatId))
            {
                return Forbid();
            }

            var result = await _service.GetLastByChat(chatId);
            if (!result.Success) return NotFound(new { message = result.Error });
            return Ok(result.Message);
        }

        [Authorize]
        [HttpGet("chat/{chatId}")]
        public async Task<IActionResult> GetByChat(int chatId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!await _permissionService.IsInChat(userIdClaim, chatId))
            {
                return Forbid();
            }

            var result = await _service.GetByChat(chatId, page, pageSize);
            if (!result.Success) return NotFound(new { message = result.Error });
            return Ok(result.Messages);
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateMessageDto model)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!await _permissionService.IsInChat(userIdClaim, model.ChatId))
            {
                return Forbid();
            }

            var result = await _service.Create(userIdClaim, model);
            if (!result.Success) return BadRequest(new { message = result.Error });
            return Ok(new { message = "Message created" });
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] UpdateMessageDto model)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!await _permissionService.CanUpdateMessage(userIdClaim, model.Id))
            {
                return Forbid();
            }

            var result = await _service.Update(model);
            if (!result.Success) return BadRequest(new { message = result.Error });
            return Ok(new { message = "Message updated" });
        }

        [Authorize]
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (!await _permissionService.CanDeleteMessage(userIdClaim, id))
            {
                return Forbid();
            }

            var result = await _service.Delete(id);
            if (!result.Success) return BadRequest(new { message = result.Error });
            return Ok(new { message = "Message deleted" });
        }
    }
}
