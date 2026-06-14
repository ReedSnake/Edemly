using Edemly.Contracts.ChatMembers;
using Edemly.Server.Application.ChatMembers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edemly.Server.Api.Controllers.Chats
{
    [ApiController]
    [Route("api")]
    public class ChatMemberController : ApiControllerBase
    {
        private readonly IChatMemberService _chatMemberService;

        public ChatMemberController(IChatMemberService chatMemberService)
        {
            _chatMemberService = chatMemberService;
        }

        [Authorize]
        [HttpGet("chat-members/{chatMemberId}")]
        public async Task<IActionResult> GetMemberAsync(int chatMemberId)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _chatMemberService.GetMemberAsync(currentUserId, chatMemberId));
        }

        [Authorize]
        [HttpGet("chats/{chatId}/members")]
        public async Task<IActionResult> GetMembersAsync(int chatId)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _chatMemberService.GetMembersAsync(currentUserId, chatId));
        }

        [Authorize]
        [HttpGet("chat-members/me")]
        public async Task<IActionResult> GetMembershipsAsync()
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _chatMemberService.GetMembershipsAsync(currentUserId));
        }

        [Authorize]
        [HttpPost("chats/{chatId}/members")]
        public async Task<IActionResult> CreateAsync(int chatId, [FromBody] CreateChatMemberDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var requesterId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            request.ChatId = chatId;
            return ToServiceResult(await _chatMemberService.AddMemberAsync(requesterId, request));
        }

        [Authorize]
        [HttpPut("chat-members/{chatMemberId}")]
        public async Task<IActionResult> UpdateAsync(int chatMemberId, [FromBody] UpdateChatMemberDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var requesterId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(
                await _chatMemberService.UpdateAsync(requesterId, chatMemberId, request));
        }

        [Authorize]
        [HttpDelete("chat-members/{chatMemberId}")]
        public async Task<IActionResult> DeleteAsync(int chatMemberId)
        {
            var unauthorizedResult = RequireCurrentUserId(out var requesterId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _chatMemberService.DeleteAsync(requesterId, chatMemberId));
        }
    }
}
