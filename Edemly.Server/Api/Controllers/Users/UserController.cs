using Edemly.Contracts.Users;
using Edemly.Server.Application.Common;
using Edemly.Server.Application.Users;
using Edemly.Server.Infrastructure.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edemly.Server.Api.Controllers.Users
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ApiControllerBase
    {
        private readonly IUserService _userService;
        private readonly IFileStorageService _fileStorageService;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            IUserService userService,
            IFileStorageService fileStorageService,
            ILogger<UsersController> logger)
        {
            _userService = userService;
            _fileStorageService = fileStorageService;
            _logger = logger;
        }
        [Authorize]
        [HttpPost("me/profile-picture")]
        [RequestSizeLimit(52428800)]
        public async Task<IActionResult> UploadProfilePictureAsync(IFormFile file)
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

                var result = await _fileStorageService.UploadProfilePictureAsync(
                    currentUserId,
                    stream,
                    file.FileName);

                if (!result.Success)
                {
                    return ToServiceResult(
                        ServiceResult.BadRequest(result.Error ?? "Failed to upload profile picture"));
                }

                var updateResult = await _userService.UpdateAsync(currentUserId, new UpdateUserDto
                {
                    PfpUrl = result.Url
                });

                if (!updateResult.Success)
                {
                    _logger.LogWarning(
                        "Failed to update user profile picture URL: {Error}",
                        updateResult.Message);

                    return ToServiceResult(
                        ServiceResult.BadRequest(updateResult.Message ?? "Failed to update user profile picture"));
                }

                return Ok(new
                {
                    url = result.Url,
                    message = "Profile picture uploaded successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading profile picture");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetSelfAsync()
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(
                await _userService.GetFullInfoAsync(currentUserId),
                user => new { User = user });
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetByIdAsync(int userId)
        {
            return ToServiceResult(
                await _userService.GetByIdAsync(userId),
                user => new { User = user });
        }

        [Authorize]
        [HttpGet("search")]
        public async Task<IActionResult> SearchUsersAsync([FromQuery] string query)
        {
            return ToServiceResult(
                await _userService.SearchUsersAsync(query),
                users => new
                {
                    Users = users ?? new List<UserDto>(),
                    Count = users?.Count ?? 0
                });
        }

        [Authorize]
        [HttpPut("me")]
        public async Task<IActionResult> UpdateSelfAsync([FromBody] UpdateUserDto request)
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _userService.UpdateAsync(currentUserId, request));
        }

        [Authorize]
        [HttpDelete("me")]
        public async Task<IActionResult> DeleteSelfAsync()
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _userService.DeleteAsync(currentUserId, currentUserId));
        }

        [Authorize]
        [HttpPost("batch")]
        public async Task<IActionResult> GetUsersBatchAsync([FromBody] List<int> userIds)
        {
            return ToServiceResult(
                await _userService.GetUsersBatchAsync(userIds),
                users => new
                {
                    Users = users ?? new List<UserDto>(),
                    Count = users?.Count ?? 0
                });
        }
    }
}