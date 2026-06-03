using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Edemly.Server.Api.Services;
using Edemly.Contracts.ChatMembers;
using Edemly.Server.Data;
using Edemly.Server.Services;
using Edemly.Server.Api.Middleware;
using Microsoft.Extensions.Configuration;

namespace Edemly.Server.Api.Controllers.Chats
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
            return OkOrNotFound(result.Success, result.Error, result.Member);
        }

        [Authorize]
        [HttpGet("list/{chatId}")]
        public async Task<IActionResult> GetMembers(int chatId)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (!await _permissionService.IsInChat(userId, chatId))
            {
                return Forbid();
            }

            var result = await _service.GetMembers(chatId);
            return OkOrNotFound(result.Success, result.Error, result.Members);
        }

        [Authorize]
        [HttpGet("my-memberships")]
        public async Task<IActionResult> GetMemberships()
        {
            var userId = GetCurrentUserIdOrDefault();

            var result = await _service.GetMemberships(userId);
            return OkOrNotFound(result.Success, result.Error, result.Memberships);
        }

        [Authorize]
        [HttpPost("add")]
        public async Task<IActionResult> AddMember([FromBody] CreateChatMemberDto model)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (!await _permissionService.CanAddChatMember(userId, model.ChatId))
            {
                return Forbid();
            }

            var result = await _service.AddMember(model);
            return OkMessageOrBadRequest(result.Success, result.Error, "Chat member added");
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateMember([FromBody] UpdateChatMemberDto model)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (!await _permissionService.CanUpdateChatMember(userId, model.Id))
            {
                return Forbid();
            }

            var result = await _service.UpdateMember(model);
            return OkMessageOrBadRequest(result.Success, result.Error, "Chat member updated");
        }

        [Authorize]
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteMember(int id)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (!await _permissionService.CanDeleteChatMember(userId, id))
            {
                return Forbid();
            }

            var result = await _service.DeleteMember(id);
            return OkMessageOrBadRequest(result.Success, result.Error, "Chat member removed");
        }

    }
}
