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
        private readonly IPermissionService _permissionService;
        private readonly IHubContext<MainHub> _hubContext;

        public ChatController(
            IChatService chatService,
            IPermissionService permissionService,
            IHubContext<MainHub> hubContext)
        {
            _chatService = chatService;
            _permissionService = permissionService;
            _hubContext = hubContext;
        }

        [Authorize]
        [HttpPost("create-private")]
        public async Task<IActionResult> CreatePrivateChat([FromBody] CreatePrivateChatDto request)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (userId == 0)
            {
                return UnauthorizedMessage("User not authenticated");
            }

            var result = await _chatService.CreateOrGetPrivateChat(userId, request.UserId);
            return OkOrBadRequest(result.Success, result.Error, new { Chat = result.Chat });
        }

        [Authorize]
        [HttpPost("create-group")]
        public async Task<IActionResult> CreateGroupChat([FromBody] CreateGroupChatDto request)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (userId == 0)
            {
                return UnauthorizedMessage("User not authenticated");
            }

            if (string.IsNullOrWhiteSpace(request.GroupName))
            {
                return BadRequestMessage("Group name is required");
            }

            if (request.ParticipantIds == null || request.ParticipantIds.Count == 0)
            {
                return BadRequestMessage("At least one participant is required");
            }

            var result = await _chatService.CreateGroupChat(userId, request.GroupName, request.ParticipantIds);

            if (!result.Success)
            {
                return BadRequestMessage(result.Error);
            }

            if (result.Chat != null)
            {
                var allMemberIds = new List<int> { userId };
                allMemberIds.AddRange(request.ParticipantIds);

                var memberIdStrings = allMemberIds.Distinct().Select(id => id.ToString()).ToList();

                await _hubContext.Clients.Users(memberIdStrings).SendAsync("GroupCreated", new
                {
                    ChatId = result.Chat.Id,
                    ChatName = result.Chat.Name,
                    ChatType = result.Chat.Type,
                    CreatorId = userId
                });
            }

            return Ok(new { Chat = result.Chat });
        }

        [Authorize]
        [HttpGet("my-chats")]
        public async Task<IActionResult> GetMyChats()
        {
            var userId = GetCurrentUserIdOrDefault();

            if (userId == 0)
            {
                return UnauthorizedMessage("User not authenticated");
            }

            var result = await _chatService.GetMyChats(userId);
            return OkOrBadRequest(result.Success, result.Error, result.Chats);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (userId == 0)
            {
                return UnauthorizedMessage("User not authenticated");
            }

            var result = await _chatService.GetById(id, userId);

            if (!result.Success)
            {
                return NotFoundMessage(result.Error);
            }

            if (!await _permissionService.IsInChat(userId, id))
            {
                return Forbid();
            }

            return Ok(result.Chat);
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateChat([FromBody] UpdateChatDto request)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (userId == 0)
            {
                return UnauthorizedMessage("User not authenticated");
            }

            var result = await _chatService.UpdateChat(request.Id, request.Name, request.Description, request.IconUrl);
            return OkMessageOrBadRequest(result.Success, result.Error, "Chat updated successfully");
        }

        [Authorize]
        [HttpPost("upload-icon")]
        public async Task<IActionResult> UploadGroupIcon([FromServices] IFileStorageService fileStorageService)
        {
            var userId = GetCurrentUserIdOrDefault();

            if (userId == 0)
            {
                return UnauthorizedMessage("User not authenticated");
            }

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
                    return BadRequestMessage(updateResult.Error ?? "Failed to update chat icon");
                }

                return Ok(new { url = result.Url });
            }
            catch (Exception ex)
            {
                return BadRequestMessage($"Error uploading file: {ex.Message}");
            }
        }
    }
}
