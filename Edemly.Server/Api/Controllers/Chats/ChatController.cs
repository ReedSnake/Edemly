using Edemly.Server.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edemly.Server.Api.Controllers.Chats
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ApiControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IChatRealtimeNotifier _chatRealtimeNotifier;

        public ChatController(
            IChatService chatService,
            IChatRealtimeNotifier chatRealtimeNotifier)
        {
            _chatService = chatService;
            _chatRealtimeNotifier = chatRealtimeNotifier;
        }

        [Authorize]
        [HttpPost("create-private")]
        public async Task<IActionResult> CreatePrivateChatAsync([FromBody] CreatePrivateChatDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(
                await _chatService.CreateOrGetPrivateChatAsync(currentUserId, request.UserId),
                chat => new { Chat = chat });
        }

        [Authorize]
        [HttpPost("create-group")]
        public async Task<IActionResult> CreateGroupChatAsync([FromBody] CreateGroupChatDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var requesterId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            var result = await _chatService.CreateGroupChatAsync(requesterId, request.GroupName, request.ParticipantIds);
            if (result.Data != null)
            {
                await _chatRealtimeNotifier.NotifyGroupCreatedAsync(result.Data, requesterId, request.ParticipantIds);
            }

            return ToServiceResult(result, chat => new { Chat = chat });
        }

        [Authorize]
        [HttpGet("my-chats")]
        public async Task<IActionResult> GetMyChatsAsync()
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _chatService.GetMyChatsAsync(currentUserId));
        }

        [Authorize]
        [HttpGet("{chatId}")]
        public async Task<IActionResult> GetByIdAsync(int chatId)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _chatService.GetByIdAsync(currentUserId, chatId));
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateChatAsync([FromBody] UpdateChatDto request)
        {
            return ToServiceResult(await _chatService.UpdateAsync(request.Id, request.Name, request.Description, request.IconUrl));
        }
    }
}