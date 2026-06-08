using Edemly.Contracts.Auth;
using Edemly.Server.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Edemly.Server.Api.Controllers.Auth
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ApiControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("get-code")]
        public async Task<IActionResult> GetLoginCodeAsync([FromBody] LoginRequestDto request)
        {
            return ToServiceResult(await _authService.GetLoginCodeAsync(request));
        }

        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync([FromBody] LoginWithCodeDto request)
        {
            return ToServiceResult(await _authService.LoginAsync(request));
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterAsync([FromBody] RegistrationWithCodeDto request)
        {
            return ToServiceResult(await _authService.RegisterAsync(request));
        }

        [HttpPost("session-login")]
        public async Task<IActionResult> SessionLoginAsync([FromBody] SessionLoginDto request)
        {
            return ToServiceResult(await _authService.SessionLoginAsync(request));
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> LogoutAsync()
        {
            var unauthorizedResult = RequireCurrentUserId(out var currentUserId);
            if (unauthorizedResult != null)
            {
                return unauthorizedResult;
            }

            return ToServiceResult(await _authService.LogoutAsync(currentUserId));
        }
    }
}