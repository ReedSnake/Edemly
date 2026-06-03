using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Edemly.Contracts.Chats;
using Edemly.Server.Api.Services;
using Edemly.Server.Hubs;

namespace Edemly.Server.Api.Controllers.Chats
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ApiControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IHubContext<MainHub> _hubContext;

        public ChatController(
            IChatService chatService,
            IHubContext<MainHub> hubContext)
        {
            _chatService = chatService;
            _hubContext = hubContext;
        }

        [Authorize]
        [HttpPost("create-private")]
        public async Task<IActionResult> CreatePrivateChat([FromBody] CreatePrivateChatDto request)
        {
            var userId = GetCurrentUserIdOrDefault();

            return ToServiceDataResult(
                await _chatService.CreateOrGetPrivateChat(userId, request.UserId),
                chat => new { Chat = chat });
        }

        [Authorize]
        [HttpPost("create-group")]
        public async Task<IActionResult> CreateGroupChat([FromBody] CreateGroupChatDto request)
        {
            var userId = GetCurrentUserIdOrDefault();

            var result = await _chatService.CreateGroupChat(userId, request.GroupName, request.ParticipantIds);
            if (!result.Success)
            {
                return MessageResult(result.StatusCode, result.Message ?? "Failed to create group chat");
            }

            if (result.Data != null)
            {
                var allMemberIds = new List<int> { userId };
                allMemberIds.AddRange(request.ParticipantIds);

                var memberIdStrings = allMemberIds.Distinct().Select(id => id.ToString()).ToList();

                await _hubContext.Clients.Users(memberIdStrings).SendAsync("GroupCreated", new
                {
                    ChatId = result.Data.Id,
                    ChatName = result.Data.Name,
                    ChatType = result.Data.Type,
                    CreatorId = userId
                });
            }

            return Ok(new { Chat = result.Data });
        }

        [Authorize]
        [HttpGet("my-chats")]
        public async Task<IActionResult> GetMyChats()
        {
            var userId = GetCurrentUserIdOrDefault();

            return ToServiceDataResult(await _chatService.GetMyChats(userId));
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = GetCurrentUserIdOrDefault();

            return ToServiceDataResult(await _chatService.GetById(userId, id));
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateChat([FromBody] UpdateChatDto request)
        {
            var userId = GetCurrentUserIdOrDefault();

            return ToServiceMessageResult(await _chatService.UpdateChat(request.Id, request.Name, request.Description, request.IconUrl));
        }

        [Authorize]
        [HttpPost("upload-icon")]
        public async Task<IActionResult> UploadGroupIcon([FromServices] IFileStorageService fileStorageService)
        {
            var userId = GetCurrentUserIdOrDefault();

            var chatIdStr = Request.Form["chatId"].FirstOrDefault();
            if (string.IsNullOrEmpty(chatIdStr) || !int.TryParse(chatIdStr, out var chatId))
            {
                return BadRequestMessage("Invalid chat ID");
            }

            var file = Request.Form.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
            {
                return BadRequestMessage("No file uploaded");
            }

            try
            {
                using var stream = file.OpenReadStream();
                var result = await fileStorageService.UploadFileAsync(
                    userId,
                    stream,
                    $"group_{chatId}_{DateTime.UtcNow.Ticks}{Path.GetExtension(file.FileName)}",
                    file.ContentType ?? "image/jpeg");

                if (!result.Success)
                {
                    return BadRequestMessage(result.Error ?? "Failed to upload file");
                }

                var updateResult = await _chatService.UpdateChat(chatId, name: null, description: null, iconUrl: result.Url);
                if (!updateResult.Success)
                {
                    return MessageResult(updateResult.StatusCode, updateResult.Message);
                }

                return Ok(new { url = result.Url });
            }
            catch (Exception)
            {
                return BadRequestMessage("Error uploading file");
            }
        }
    }
}
