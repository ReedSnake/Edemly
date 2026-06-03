using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Edemly.Server.Api.Services;

namespace Edemly.Server.Api.Controllers.Files
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileController : ApiControllerBase
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly IUserService _userService;
        private readonly ILogger<FileController> _logger;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider;

        public FileController(
            IFileStorageService fileStorageService,
            IUserService userService,
            ILogger<FileController> logger)
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
            var userId = GetCurrentUserIdOrDefault();

            if (file == null || file.Length == 0)
                return BadRequestMessage("No file uploaded");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                return BadRequestMessage("Only image files (jpg, jpeg, png, gif) are allowed");

            try
            {
                using var stream = file.OpenReadStream();
                var result = await _fileStorageService.UploadProfilePictureAsync(userId, stream, file.FileName);

                if (!result.Success)
                    return BadRequestMessage(result.Error);

                var updateResult = await _userService.UpdateUser(userId, new UpdateUserDto
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
            var userId = GetCurrentUserIdOrDefault();

            if (file == null || file.Length == 0)
                return BadRequestMessage("No file uploaded");

            try
            {
                var contentType = file.ContentType;

                using var stream = file.OpenReadStream();
                var result = await _fileStorageService.UploadFileAsync(userId, stream, file.FileName, contentType);

                if (!result.Success)
                    return BadRequestMessage(result.Error);

                return Ok(new
                {
                    url = result.Url,
                    fileName = file.FileName,
                    fileSize = file.Length,
                    contentType,
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
                return BadRequestMessage("File URL is required");

            try
            {
                var result = await _fileStorageService.DeleteFileAsync(fileUrl);
                if (!result.Success)
                    return BadRequestMessage(result.Error);

                return OkMessage("File deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [HttpGet("download")]
        [AllowAnonymous]
        public async Task<IActionResult> DownloadFile([FromQuery] string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
                return BadRequestMessage("File URL is required");

            try
            {
                var stream = await _fileStorageService.GetFileAsync(fileUrl);
                if (stream == null)
                    return NotFoundMessage("File not found");

                var fileName = Path.GetFileName(fileUrl);

                if (!_contentTypeProvider.TryGetContentType(fileName, out var contentType))
                {
                    contentType = "application/octet-stream";
                }

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
