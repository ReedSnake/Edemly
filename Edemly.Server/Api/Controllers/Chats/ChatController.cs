using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Edemly.Contracts.Chats;
using Edemly.Server.Api.Middleware;
using Edemly.Server.Api.Services;
using Edemly.Server.Data;
using Edemly.Server.Hubs;
using Edemly.Server.Services;

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
            IHubContext<MainHub> hubContext,
            ServerDbContext serverDb,
            ITenantProvider tenantProvider,
            ITenantDbContextFactory tenantDbFactory,
            IConfiguration configuration)
            : base(serverDb, tenantProvider, tenantDbFactory, configuration)
        {
            _chatService = chatService;
            _permissionService = permissionService;
            _hubContext = hubContext;
        }

        [Authorize]
        [HttpPost("create-private")]
        public async Task<IActionResult> CreatePrivateChat([FromBody] CreatePrivateChatDto request)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (userIdClaim == 0)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var result = await _chatService.CreateOrGetPrivateChat(userIdClaim, request.UserId);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(new { Chat = result.Chat });
        }

        [Authorize]
        [HttpPost("create-group")]
        public async Task<IActionResult> CreateGroupChat([FromBody] CreateGroupChatDto request)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (userIdClaim == 0)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            if (string.IsNullOrWhiteSpace(request.GroupName))
            {
                return BadRequest(new { message = "Group name is required" });
            }

            if (request.ParticipantIds == null || request.ParticipantIds.Count == 0)
            {
                return BadRequest(new { message = "At least one participant is required" });
            }

            var result = await _chatService.CreateGroupChat(userIdClaim, request.GroupName, request.ParticipantIds);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Error });
            }

            if (result.Chat != null)
            {
                var allMemberIds = new List<int> { userIdClaim };
                allMemberIds.AddRange(request.ParticipantIds);

                var memberIdStrings = allMemberIds.Distinct().Select(id => id.ToString()).ToList();

                await _hubContext.Clients.Users(memberIdStrings).SendAsync("GroupCreated", new
                {
                    ChatId = result.Chat.Id,
                    ChatName = result.Chat.Name,
                    ChatType = result.Chat.Type,
                    CreatorId = userIdClaim
                });
            }

            return Ok(new { Chat = result.Chat });
        }

        [Authorize]
        [HttpGet("my-chats")]
        public async Task<IActionResult> GetMyChats()
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (userIdClaim == 0)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var result = await _chatService.GetMyChats(userIdClaim);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(result.Chats);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (userIdClaim == 0)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var result = await _chatService.GetById(id, userIdClaim);

            if (!result.Success)
            {
                return NotFound(new { message = result.Error });
            }

            if (!await _permissionService.IsInChat(userIdClaim, id))
            {
                return Forbid();
            }

            return Ok(result.Chat);
        }

        [Authorize]
        [HttpPut("update")]
        public async Task<IActionResult> UpdateChat([FromBody] UpdateChatDto request)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (userIdClaim == 0)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var result = await _chatService.UpdateChat(request.Id, request.Name, request.Description, request.IconUrl);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Error });
            }

            return Ok(new { message = "Chat updated successfully" });
        }

        [Authorize]
        [HttpPost("upload-icon")]
        public async Task<IActionResult> UploadGroupIcon([FromServices] IFileStorageService fileStorageService)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (userIdClaim == 0)
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var chatIdStr = Request.Form["chatId"].FirstOrDefault();
            if (string.IsNullOrEmpty(chatIdStr) || !int.TryParse(chatIdStr, out var chatId))
            {
                return BadRequest(new { message = "Invalid chat ID" });
            }

            var file = Request.Form.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded" });
            }

            try
            {
                using var stream = file.OpenReadStream();
                var result = await fileStorageService.UploadFileAsync(
                    userIdClaim,
                    stream,
                    $"group_{chatId}_{DateTime.UtcNow.Ticks}{Path.GetExtension(file.FileName)}",
                    file.ContentType ?? "image/jpeg");

                if (!result.Success)
                {
                    return BadRequest(new { message = result.Error ?? "Failed to upload file" });
                }

                var updateResult = await _chatService.UpdateChat(chatId, name: null, description: null, iconUrl: result.Url);
                if (!updateResult.Success)
                {
                    return BadRequest(new { message = updateResult.Error ?? "Failed to update chat icon" });
                }

                return Ok(new { url = result.Url });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error uploading file: {ex.Message}" });
            }
        }
    }
}
