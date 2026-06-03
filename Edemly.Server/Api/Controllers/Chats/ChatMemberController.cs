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
        private readonly IChatMemberService _service;

        public ChatMemberController(IChatMemberService service)
        {
            _service = service;
        }

        [HttpGet("id/{id}")]
        public async Task<IActionResult> GetMember(int id)
        {
            return ToServiceDataResult(await _service.GetMember(id));
        }

        [Authorize]
        [HttpGet("list/{chatId}")]
        public async Task<IActionResult> GetMembers(int chatId)
        {
            return ToServiceDataResult(await _service.GetMembers(GetCurrentUserIdOrDefault(), chatId));
        }

        [Authorize]
        [HttpGet("my-memberships")]
        public async Task<IActionResult> GetMemberships()
        {
            return ToServiceDataResult(await _service.GetMemberships(GetCurrentUserIdOrDefault()));
        }

        [Authorize]
        [HttpPost("add")]
        public async Task<IActionResult> AddMember([FromBody] CreateChatMemberDto model)
        {
            return ToServiceMessageResult(await _service.AddMember(GetCurrentUserIdOrDefault(), model));
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateMember([FromBody] UpdateChatMemberDto model)
        {
            return ToServiceMessageResult(await _service.UpdateMember(GetCurrentUserIdOrDefault(), model));
        }

        [Authorize]
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteMember(int id)
        {
            return ToServiceMessageResult(await _service.DeleteMember(GetCurrentUserIdOrDefault(), id));
        }
    }
}
