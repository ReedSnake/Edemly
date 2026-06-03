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

        public MessageController(IMessageService service)
        {
            _service = service;
        }

        [HttpGet("id/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            return ToServiceDataResult(await _service.GetById(id));
        }

        [Authorize]
        [HttpGet("chat/last/{chatId}")]
        public async Task<IActionResult> GetLastByChat(int chatId)
        {
            return ToServiceDataResult(await _service.GetLastByChat(GetCurrentUserIdOrDefault(), chatId));
        }

        [Authorize]
        [HttpGet("chat/{chatId}")]
        public async Task<IActionResult> GetByChat(int chatId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            return ToServiceDataResult(await _service.GetByChat(GetCurrentUserIdOrDefault(), chatId, page, pageSize));
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateMessageDto model)
        {
            return ToServiceMessageResult(await _service.Create(GetCurrentUserIdOrDefault(), model));
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] UpdateMessageDto model)
        {
            return ToServiceMessageResult(await _service.Update(GetCurrentUserIdOrDefault(), model));
        }

        [Authorize]
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            return ToServiceMessageResult(await _service.Delete(GetCurrentUserIdOrDefault(), id));
        }
    }
}
