using Edemly.Server.Application.Common;
using Edemly.Server.Infrastructure.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Edemly.Server.Api.Controllers.Files
{
    [ApiController]
    [Route("api/files")]
    public class FilesController : ApiControllerBase
    {
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<FilesController> _logger;
        private readonly FileExtensionContentTypeProvider _fileContentTypeProvider;

        public FilesController(
            IFileStorageService fileStorageService,
            ILogger<FilesController> logger)
        {
            _fileStorageService = fileStorageService;
            _logger = logger;
            _fileContentTypeProvider = new FileExtensionContentTypeProvider();
        }

        [Authorize]
        [HttpPost]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> UploadFileAsync(IFormFile file)
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

            try
            {
                var contentType = file.ContentType;

                using var stream = file.OpenReadStream();

                var result = await _fileStorageService.UploadFileAsync(
                    currentUserId,
                    stream,
                    file.FileName,
                    contentType);

                if (!result.Success)
                {
                    return ToServiceResult(
                        ServiceResult.BadRequest(result.Error ?? "Failed to upload file"));
                }

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
        [HttpDelete]
        public async Task<IActionResult> DeleteAsync([FromQuery] string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                return ToServiceResult(ServiceResult.BadRequest("File URL is required"));
            }

            try
            {
                var result = await _fileStorageService.DeleteFileAsync(fileUrl);

                if (!result.Success)
                {
                    return ToServiceResult(
                        ServiceResult.BadRequest(result.Error ?? "Failed to delete file"));
                }

                return ToServiceResult(ServiceResult.Ok("File deleted successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting file");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [Authorize]
        [HttpGet("download")]
        public async Task<IActionResult> DownloadFileAsync([FromQuery] string fileUrl)
        {
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                return ToServiceResult(ServiceResult.BadRequest("File URL is required"));
            }

            try
            {
                var stream = await _fileStorageService.GetFileAsync(fileUrl);

                if (stream == null)
                {
                    return ToServiceResult(ServiceResult.NotFound("File not found"));
                }

                var fileName = Path.GetFileName(fileUrl);

                if (!_fileContentTypeProvider.TryGetContentType(fileName, out var contentType))
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

        [Authorize]
        [HttpGet("/uploads/{**filePath}")]
        [HttpGet("/{company}/uploads/{**filePath}")]
        public async Task<IActionResult> GetUploadedFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return ToServiceResult(ServiceResult.BadRequest("File path is required"));
            }

            try
            {
                var requestPath = HttpContext.Request.Path.Value ?? filePath;
                var stream = await _fileStorageService.GetFileAsync(requestPath);

                if (stream == null)
                {
                    return ToServiceResult(ServiceResult.NotFound("File not found"));
                }

                var fileName = Path.GetFileName(filePath);
                if (!_fileContentTypeProvider.TryGetContentType(fileName, out var contentType))
                {
                    contentType = "application/octet-stream";
                }

                return File(stream, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading uploaded file {FilePath}", filePath);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }
    }
}
