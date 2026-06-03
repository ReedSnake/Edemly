using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Edemly.Server.Api.Services;

namespace Edemly.Server.Api.Controllers.Messages
{
    [ApiController]
    [Route("api/[controller]")]
    public class MessageController : ApiControllerBase
    {
        private readonly IMessageService _messageService;

        public MessageController(IMessageService messageService)
        {
            _messageService = messageService;
        }

        [HttpGet("id/{messageId}")]
        public async Task<IActionResult> GetByIdAsync(int messageId)
        {
            return ToServiceResult(await _messageService.GetByIdAsync(messageId));
        }

        [Authorize]
        [HttpGet("chat/last/{chatId}")]
        public async Task<IActionResult> GetLastByChatAsync(int chatId)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _messageService.GetLastByChatAsync(currentUserId, chatId));
        }

        [Authorize]
        [HttpGet("chat/{chatId}")]
        public async Task<IActionResult> GetByChatAsync(int chatId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _messageService.GetByChatAsync(currentUserId, chatId, page, pageSize));
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> CreateAsync([FromBody] CreateMessageDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _messageService.CreateAsync(currentUserId, request));
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateMessageDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _messageService.UpdateAsync(currentUserId, request));
        }

        [Authorize]
        [HttpDelete("delete/{messageId}")]
        public async Task<IActionResult> DeleteAsync(int messageId)
        {
            var unauthorizedResult = RequireCurrentUserId(out var requesterId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _messageService.DeleteAsync(requesterId, messageId));
        }
    }
}
