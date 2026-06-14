using Edemly.Contracts.Chats;
using Edemly.Server.Application.Chats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edemly.Server.Api.Controllers.Chats
{
    [ApiController]
    [Route("api/chats")]
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
        [HttpPost("private")]
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
        [HttpPost("group")]
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
        [HttpGet]
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
        [HttpPut("{chatId}")]
        public async Task<IActionResult> UpdateChatAsync(int chatId, [FromBody] UpdateChatDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(
                await _chatService.UpdateAsync(currentUserId, chatId, request.Name, request.Description, request.IconUrl));
        }
    }
}
