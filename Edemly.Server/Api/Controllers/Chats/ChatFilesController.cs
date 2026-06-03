using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Edemly.Server.Api.Services;

namespace Edemly.Server.Api.Controllers.Chats
{
    [ApiController]
    [Route("api/chat")]
    public class ChatFilesController : ApiControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<ChatFilesController> _logger;

        public ChatFilesController(
            IChatService chatService,
            IFileStorageService fileStorageService,
            ILogger<ChatFilesController> logger)
        {
            _chatService = chatService;
            _fileStorageService = fileStorageService;
            _logger = logger;
        }

        [Authorize]
        [HttpPost("upload-icon")]
        public async Task<IActionResult> UploadIconAsync()
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            var chatIdStr = Request.Form["chatId"].FirstOrDefault();
            if (string.IsNullOrEmpty(chatIdStr) || !int.TryParse(chatIdStr, out var chatId))
            {
                return ToServiceResult(ServiceResult.BadRequest("Invalid chat ID"));
            }

            var file = Request.Form.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
            {
                return ToServiceResult(ServiceResult.BadRequest("No file uploaded"));
            }

            try
            {
                using var stream = file.OpenReadStream();
                var uploadResult = await _fileStorageService.UploadFileAsync(
                    currentUserId,
                    stream,
                    $"group_{chatId}_{DateTime.UtcNow.Ticks}{Path.GetExtension(file.FileName)}",
                    file.ContentType ?? "image/jpeg");

                if (!uploadResult.Success)
                {
                    return ToServiceResult(ServiceResult.BadRequest(uploadResult.Error ?? "Failed to upload file"));
                }

                var updateResult = await _chatService.UpdateAsync(chatId, name: null, description: null, iconUrl: uploadResult.Url);
                if (!updateResult.Success)
                {
                    return ToServiceResult(updateResult);
                }

                return Ok(new { url = uploadResult.Url });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading chat icon for chat {ChatId}", chatId);
                return ToServiceResult(ServiceResult.BadRequest("Error uploading file"));
            }
        }
    }
}
