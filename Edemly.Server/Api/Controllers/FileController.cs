using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using uchat_server.Api.Services;
using uchat_server.Data;
using uchat_server.Services;
using uchat_server.Api.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Linq;
using System;
using System.Threading.Tasks;
using uchat_server.Api.DTOs;
using Microsoft.AspNetCore.StaticFiles; // ПОТРІБНО ДОДАТИ ЦЕЙ USING

namespace uchat_server.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileController : ApiControllerBase
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IUserService _userService;
        private readonly ILogger<FileController> _logger;
        // Провайдер для визначення типу файлу
        private readonly FileExtensionContentTypeProvider _contentTypeProvider;

        public FileController(
            IFileStorageService fileStorageService,
            IUserService userService,
            ILogger<FileController> logger,
            ServerDbContext serverDb,
            ITenantProvider tenantProvider,
            ITenantDbContextFactory tenantDbFactory,
            IConfiguration configuration)
            : base(serverDb, tenantProvider, tenantDbFactory, configuration)
        {
            _fileStorageService = fileStorageService;
            _userService = userService;
            _logger = logger;
            _contentTypeProvider = new FileExtensionContentTypeProvider();
        }

        [Authorize]
        [HttpPost("upload-profile-picture")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> UploadProfilePicture(IFormFile file)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { message = "Only image files (jpg, jpeg, png, gif) are allowed" });

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _fileStorageService.UploadProfilePictureAsync(
                    userIdClaim, stream, file.FileName);

                if (!result.Success)
                    return BadRequest(new { message = result.Error });

                var updateResult = await _userService.UpdateUser(userIdClaim, new UserUpdateDto
                {
                    PfpUrl = result.Url
                });

                if (!updateResult.Success)
                    _logger.LogWarning("Failed to update user profile picture URL: {Error}", updateResult.Error);

                return Ok(new { url = result.Url, message = "Profile picture uploaded successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading profile picture");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [Authorize]
        [HttpPost("upload")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> UploadFile(IFormFile file)
        {
            var userIdClaim = int.Parse(User.Claims.FirstOrDefault(c => c.Type == "userId")?.Value ?? "0");

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file uploaded" });

            try
            {
                var contentType = file.ContentType;

                using var stream = file.OpenReadStream();
                var result = await _fileStorageService.UploadFileAsync(
                    userIdClaim, stream, file.FileName, contentType);

                if (!result.Success)
                    return BadRequest(new { message = result.Error });

                return Ok(new
                {
                    url = result.Url,
                    fileName = file.FileName,
                    fileSize = file.Length,
                    contentType = contentType,
                    message = "File uploaded successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [Authorize]
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteFile([FromQuery] string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
                return BadRequest(new { message = "File URL is required" });

            try
            {
                var result = await _fileStorageService.DeleteFileAsync(fileUrl);
                if (!result.Success)
                    return BadRequest(new { message = result.Error });

                return Ok(new { message = "File deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        // --- ВИПРАВЛЕНИЙ МЕТОД ---
        [HttpGet("download")]
        [AllowAnonymous] // Додайте це, якщо картинки мають бути публічними
        public async Task<IActionResult> DownloadFile([FromQuery] string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
                return BadRequest(new { message = "File URL is required" });

            try
            {
                var stream = await _fileStorageService.GetFileAsync(fileUrl);
                if (stream == null)
                    return NotFound(new { message = "File not found" });

                var fileName = Path.GetFileName(fileUrl);

                // Спроба визначити правильний Content-Type (наприклад, image/jpeg)
                if (!_contentTypeProvider.TryGetContentType(fileName, out var contentType))
                {
                    contentType = "application/octet-stream";
                }

                // Повертаємо файл без вказування імені файлу другим параметром, 
                // щоб браузер відобразив його (inline), а не змушував скачувати (attachment)
                return File(stream, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}
