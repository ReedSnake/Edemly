using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Edemly.Contracts.Auth;
using Edemly.Server.Api.Services;

namespace Edemly.Server.Api.Controllers.Auth
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("get-code")]
        public async Task<IActionResult> GetLoginCode([FromBody] LoginRequestDto model)
        {
            var result = await _authService.GetLoginCodeAsync(model);
            return ToMessageResult(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginWithCodeDto model)
        {
            var result = await _authService.LoginAsync(model);
            return ToAuthResponseResult(result);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegistrationWithCodeDto model)
        {
            var result = await _authService.RegisterAsync(model);
            return ToAuthResponseResult(result);
        }

        [HttpPost("session-login")]
        public async Task<IActionResult> SessionLogin([FromBody] SessionLoginDto model)
        {
            var result = await _authService.SessionLoginAsync(model);
            return ToAuthResponseResult(result);
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst("userId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized();
            }

            var result = await _authService.LogoutAsync(userId);
            return ToMessageResult(result);
        }

        private IActionResult ToMessageResult(AuthMessageResult result)
        {
            if (result.Success)
            {
                return Ok(new { message = result.Message });
            }

            return CreateErrorResult(result.StatusCode, result.Message);
        }

        private IActionResult ToAuthResponseResult(AuthResponseResult result)
        {
            if (result.Success && result.AuthResponse != null)
            {
                return Ok(result.AuthResponse);
            }

            return CreateErrorResult(result.StatusCode, result.Message ?? "Authentication failed");
        }

        private IActionResult CreateErrorResult(int statusCode, string message)
        {
            return statusCode switch
            {
                StatusCodes.Status400BadRequest => BadRequest(new { message }),
                StatusCodes.Status401Unauthorized => Unauthorized(new { message }),
                _ => StatusCode(statusCode, new { message })
            };
        }
    }
}
