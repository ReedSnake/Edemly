using Edemly.Server.Application.Chats;
using Edemly.Server.Application.Common;
using Edemly.Server.Infrastructure.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edemly.Server.Api.Controllers.Chats
{
    [ApiController]
    [Route("api/chats")]
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
        [HttpPost("{chatId}/icon")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> UploadIconAsync(int chatId, IFormFile file)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            if (file == null || file.Length == 0)
            {
                return ToServiceResult(ServiceResult.BadRequest("No file uploaded"));
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                return ToServiceResult(
                    ServiceResult.BadRequest("Only image files (jpg, jpeg, png, gif) are allowed"));
            }

            try
            {
                using var stream = file.OpenReadStream();

                var fileName =
                    $"group_{chatId}_{DateTime.UtcNow.Ticks}{extension}";

                var uploadResult = await _fileStorageService.UploadFileAsync(
                    currentUserId,
                    stream,
                    fileName,
                    file.ContentType ?? "image/jpeg");

                if (!uploadResult.Success)
                {
                    return ToServiceResult(
                        ServiceResult.BadRequest(uploadResult.Error ?? "Failed to upload file"));
                }

                var updateResult = await _chatService.UpdateAsync(
                    chatId,
                    name: null,
                    description: null,
                    iconUrl: uploadResult.Url);

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