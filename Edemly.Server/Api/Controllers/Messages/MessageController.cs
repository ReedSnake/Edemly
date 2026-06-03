using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Edemly.Server.Api.Services;

namespace Edemly.Server.Api.Controllers.Messages
{
    [ApiController]
    [Route("api/[controller]")]
    public class MessageController : ApiControllerBase
    {
        private readonly IMessageService _service;
        private readonly IPermissionService _permissionService;

        public MessageController(IMessageService service, IPermissionService permissionService)
        {
            _service = service;
            _permissionService = permissionService;
        }

        [HttpGet("id/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _service.GetById(id);
            return OkOrNotFound(result.Success, result.Error, result.Message);
        }

        [Authorize]
        [HttpGet("chat/last/{chatId}")]
        public async Task<IActionResult> GetLastByChat(int chatId)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (!await _permissionService.IsInChat(userId, chatId))
            {
                return Forbid();
            }

            var result = await _service.GetLastByChat(chatId);
            return OkOrNotFound(result.Success, result.Error, result.Message);
        }

        [Authorize]
        [HttpGet("chat/{chatId}")]
        public async Task<IActionResult> GetByChat(int chatId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (!await _permissionService.IsInChat(userId, chatId))
            {
                return Forbid();
            }

            var result = await _service.GetByChat(chatId, page, pageSize);
            return OkOrNotFound(result.Success, result.Error, result.Messages);
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateMessageDto model)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (!await _permissionService.IsInChat(userId, model.ChatId))
            {
                return Forbid();
            }

            var result = await _service.Create(userId, model);
            return OkMessageOrBadRequest(result.Success, result.Error, "Message created");
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] UpdateMessageDto model)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (!await _permissionService.CanUpdateMessage(userId, model.Id))
            {
                return Forbid();
            }

            var result = await _service.Update(model);
            return OkMessageOrBadRequest(result.Success, result.Error, "Message updated");
        }

        [Authorize]
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (!await _permissionService.CanDeleteMessage(userId, id))
            {
                return Forbid();
            }

            var result = await _service.Delete(id);
            return OkMessageOrBadRequest(result.Success, result.Error, "Message deleted");
        }
    }
}
