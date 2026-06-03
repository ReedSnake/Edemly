using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Edemly.Server.Api.Services;
using Edemly.Contracts.ChatMembers;

namespace Edemly.Server.Api.Controllers.Chats
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatMemberController : ApiControllerBase
    {
        private readonly IChatMemberService _chatMemberService;

        public ChatMemberController(IChatMemberService chatMemberService)
        {
            _chatMemberService = chatMemberService;
        }

        [HttpGet("id/{chatMemberId}")]
        public async Task<IActionResult> GetMemberAsync(int chatMemberId)
        {
            return ToServiceResult(await _chatMemberService.GetMemberAsync(chatMemberId));
        }

        [Authorize]
        [HttpGet("list/{chatId}")]
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
        [HttpGet("my-memberships")]
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
        [HttpPost("add")]
        public async Task<IActionResult> CreateAsync([FromBody] CreateChatMemberDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var requesterId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _chatMemberService.AddMemberAsync(requesterId, request));
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateChatMemberDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var requesterId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _chatMemberService.UpdateAsync(requesterId, request));
        }

        [Authorize]
        [HttpDelete("delete/{chatMemberId}")]
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
